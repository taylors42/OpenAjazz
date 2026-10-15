using System.Runtime.InteropServices;
using Ajazz.Application;
using Ajazz.Core;

namespace Ajazz.Infrastructure.Linux;

/// <summary>Read-only discovery: native imports deliberately contain no device open/read/write calls.</summary>
public sealed class LinuxDiscovery : IDeviceDiscovery
{
    internal static readonly object NativeLock = new();
    private const string Backend = "libhidapi-hidraw.so.0 + sysfs";

    public Task<DiscoverySnapshot> DiscoverAsync(CancellationToken cancellationToken) =>
        Task.Run(() => Enumerate(cancellationToken), cancellationToken);

    private static DiscoverySnapshot Enumerate(CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsLinux() || IntPtr.Size != 8)
            return new(DateTimeOffset.Now, Backend, [], "Esta PoC suporta Linux de 64 bits.");
        lock (NativeLock)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (Native.Init() != 0) return new(DateTimeOffset.Now, Backend, [], "HIDAPI não pôde inicializar.");
                try
                {
                    var head = Native.Enumerate(0x25a7, 0);
                    try
                    {
                        var entries = new List<(string Path, Native.DeviceInfo Info)>();
                        for (var current = head; current != IntPtr.Zero;)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            var info = Marshal.PtrToStructure<Native.DeviceInfo>(current);
                            var path = Marshal.PtrToStringUTF8(info.Path);
                            if (path != null && info.ProductId is 0xfa7b or 0xfa7c)
                                entries.Add((path, info));
                            current = info.Next;
                        }
                        var devices = entries.GroupBy(e => e.Path).OrderBy(g => g.Key).Select(group =>
                        {
                            var info = group.First().Info;
                            var node = System.IO.Path.GetFileName(group.Key);
                            // Only known hidraw nodes from HIDAPI are resolved; never open /dev/hidraw here.
                            if (!node.StartsWith("hidraw", StringComparison.Ordinal) || node.Contains('/'))
                                throw new IOException("Path HIDAPI inesperado.");
                            var sysfs = $"/sys/class/hidraw/{node}/device";
                            var name = "AJ199 — candidato";
                            string? error = null;
                            IReadOnlyList<HidReport> reports = [];
                            try
                            {
                                name = File.ReadLines($"{sysfs}/uevent")
                                    .FirstOrDefault(l => l.StartsWith("HID_NAME="))?[9..] ?? name;
                                // sysfs advertises a nominal size that may exceed the binary payload.
                                using var source = File.OpenRead($"{sysfs}/report_descriptor");
                                using var buffer = new MemoryStream();
                                source.CopyTo(buffer);
                                reports = HidDescriptor.Parse(buffer.ToArray());
                            }
                            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or OverflowException)
                            { error = ex.Message; }
                            return new DeviceCandidate(group.Key, name, info.VendorId, info.ProductId,
                                info.InterfaceNumber, group.Select(e => $"0x{e.Info.UsagePage:X4}/0x{e.Info.Usage:X4}")
                                    .Distinct().ToArray(), reports, Native.Access(group.Key, 6) == 0, error);
                        }).ToArray();
                        return new(DateTimeOffset.Now, Backend, devices, null);
                    }
                    finally { if (head != IntPtr.Zero) Native.FreeEnumeration(head); }
                }
                finally { Native.Exit(); }
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException or IOException)
            { return new(DateTimeOffset.Now, Backend, [], ex.Message); }
        }
    }

    private static class Native
    {
        // Matches hid_device_info in HIDAPI >= 0.13 on Linux LP64. wchar_t pointers are not decoded.
        [StructLayout(LayoutKind.Sequential)]
        internal struct DeviceInfo
        {
            internal IntPtr Path;
            internal ushort VendorId, ProductId;
            internal IntPtr SerialNumber;
            internal ushort ReleaseNumber;
            internal IntPtr ManufacturerString, ProductString;
            internal ushort UsagePage, Usage;
            internal int InterfaceNumber;
            internal IntPtr Next;
            internal int BusType;
        }
        [DllImport("libhidapi-hidraw.so.0", EntryPoint = "hid_init", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int Init();
        [DllImport("libhidapi-hidraw.so.0", EntryPoint = "hid_exit", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int Exit();
        [DllImport("libhidapi-hidraw.so.0", EntryPoint = "hid_enumerate", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr Enumerate(ushort vendorId, ushort productId);
        [DllImport("libhidapi-hidraw.so.0", EntryPoint = "hid_free_enumeration", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void FreeEnumeration(IntPtr devices);
        [DllImport("libc", EntryPoint = "access", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int Access([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int mode);
    }
}
