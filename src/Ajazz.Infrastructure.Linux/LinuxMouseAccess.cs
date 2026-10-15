using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Ajazz.Application;
using Ajazz.Core;
using Microsoft.Win32.SafeHandles;

namespace Ajazz.Infrastructure.Linux;

public sealed class LinuxMouseAccess
{
    public Task<MouseIdentity> IdentifyAsync(DeviceCandidate candidate, CancellationToken token) =>
        Run(candidate, connection => new HardwareMouse(connection).Identify(token), token);

    public Task<MouseConfiguration> ReadAsync(DeviceCandidate candidate, CancellationToken token) =>
        Run(candidate, connection => new HardwareMouse(connection).Read(token), token);

    public Task<MouseConfiguration> ApplyAsync(DeviceCandidate candidate, MouseConfiguration expected,
        MouseSettings settings, CancellationToken token) =>
        Run(candidate, connection => new HardwareMouse(connection).Apply(expected, settings, SaveBackup, token), token);

    private static Task<T> Run<T>(DeviceCandidate candidate, Func<IMouseConnection, T> operation, CancellationToken token)
    {
        if (candidate.VendorId != 0x25a7 || candidate.ProductId is not (0xfa7b or 0xfa7c) || !candidate.HasConfigurationChannel)
            throw new NotSupportedException("Interface de configuração incompatível.");
        return Task.Run(() =>
        {
            lock (LinuxDiscovery.NativeLock)
            {
                token.ThrowIfCancellationRequested();
                var runtime = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR")
                    ?? throw new IOException("Sessão local sem XDG_RUNTIME_DIR.");
                using var sessionLock = new FileStream(Path.Combine(runtime, $"ajazz-{Path.GetFileName(candidate.Path)}.lock"),
                    FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                using var connection = new HidConnection(candidate.Path);
                return operation(connection);
            }
        }, token);
    }

    private static void SaveBackup(MouseConfiguration current)
    {
        var root = Environment.GetEnvironmentVariable("XDG_STATE_HOME");
        if (string.IsNullOrWhiteSpace(root)) root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "state");
        var folder = Path.Combine(root, "ajazz", "backups");
        Directory.CreateDirectory(folder);
        var filename = Path.Combine(folder, $"profile-{DateTime.UtcNow:yyyyMMdd-HHmmss-fffffff}-{Guid.NewGuid():N}.json");
        using var file = new FileStream(filename, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(file, current, new JsonSerializerOptions { WriteIndented = true });
        file.Flush(true);
    }

    private sealed class HidConnection : IMouseConnection
    {
        private readonly HidHandle handle;
        private bool disposed;
        internal HidConnection(string path)
        {
            if (Native.Init() != 0) throw new IOException("Não foi possível inicializar HIDAPI.");
            var pointer = Native.Open(path);
            if (pointer == IntPtr.Zero)
            {
                var error = WideString(Native.Error(IntPtr.Zero));
                Native.Exit();
                throw new IOException("Não foi possível abrir o mouse. Verifique a permissão de acesso. " + error);
            }
            handle = new HidHandle(pointer);
        }

        public byte[] Exchange(byte[] request, CancellationToken token)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (request.Length != 17 || !LegacyProtocol.HasValidChecksum(request))
                throw new ArgumentException("Pacote de comando inválido.", nameof(request));
            token.ThrowIfCancellationRequested();
            var buffer = new byte[64];
            // Drain only the bounded pending queue. Other input reports are discarded, never logged.
            for (var i = 0; i < 32; i++)
                if (Native.Read(handle, buffer, (nuint)buffer.Length, 0) <= 0) break;
            var readOnly = request[1] is 1 or 3 or 4 or 8 or 0x0e;
            var attempts = readOnly ? 3 : 1;
            for (var attempt = 0; attempt < attempts; attempt++)
            {
                token.ThrowIfCancellationRequested();
                if (Native.Send(handle, request, (nuint)request.Length) != request.Length)
                    throw new IOException("Falha ao enviar o comando ao mouse.");
                var clock = Stopwatch.StartNew();
                while (clock.ElapsedMilliseconds < 900)
                {
                    token.ThrowIfCancellationRequested();
                    var count = Native.Read(handle, buffer, (nuint)buffer.Length, 50);
                    if (count < 0) throw new IOException("O mouse foi desconectado ou houve falha na leitura HID.");
                    if (count != 17 || buffer[0] != 9) continue;
                    if (!LegacyProtocol.IsResponse(buffer.AsSpan(0, count), request)) continue;
                    if (buffer[2] != 0) throw new IOException($"O mouse rejeitou o comando 0x{request[1]:X2} (status 0x{buffer[2]:X2}).");
                    return buffer[..count];
                }
            }
            throw new TimeoutException($"O mouse não respondeu à consulta 0x{request[1]:X2}. Mova-o para acordá-lo ou conecte o cabo USB e atualize.");
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            handle.Dispose();
            Native.Exit();
        }
    }

    private sealed class HidHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        internal HidHandle(IntPtr pointer) : base(true) { SetHandle(pointer); }
        protected override bool ReleaseHandle() { Native.Close(handle); return true; }
    }

    private static string WideString(IntPtr pointer)
    {
        if (pointer == IntPtr.Zero) return "";
        var bytes = new List<byte>();
        for (var i = 0; i < 512; i++)
        {
            var code = Marshal.ReadInt32(pointer, i * 4);
            if (code == 0) break;
            bytes.AddRange(BitConverter.GetBytes(code));
        }
        return Encoding.UTF32.GetString(bytes.ToArray());
    }

    private static class Native
    {
        private const string Library = "libhidapi-hidraw.so.0";
        [DllImport(Library, EntryPoint = "hid_init", CallingConvention = CallingConvention.Cdecl)] internal static extern int Init();
        [DllImport(Library, EntryPoint = "hid_exit", CallingConvention = CallingConvention.Cdecl)] internal static extern int Exit();
        [DllImport(Library, EntryPoint = "hid_open_path", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr Open([MarshalAs(UnmanagedType.LPUTF8Str)] string path);
        [DllImport(Library, EntryPoint = "hid_close", CallingConvention = CallingConvention.Cdecl)] internal static extern void Close(IntPtr handle);
        [DllImport(Library, EntryPoint = "hid_send_feature_report", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int Send(HidHandle handle, byte[] bytes, nuint length);
        [DllImport(Library, EntryPoint = "hid_read_timeout", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int Read(HidHandle handle, [Out] byte[] bytes, nuint length, int milliseconds);
        [DllImport(Library, EntryPoint = "hid_error", CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr Error(IntPtr handle);
    }
}
