using Ajazz.Application;
using Ajazz.Core;
using Xunit;

namespace Ajazz.Tests;

public class HardwareTests
{
    [Fact]
    public void CapturedAj199ProfileUsesOriginalDriverZeroBasedStage()
    {
        var bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures/aj199-profile.bin"));
        var profile = MouseConfigurationCodec.Decode(new(0x40, 1, 1, 35), bytes);
        Assert.Equal(new MouseSettings(1600, 1000, "#00FF00"), profile.Settings);
        Assert.Equal(1, profile.ActiveStage);
        Assert.Equal(1, profile.StageCount);
        Assert.Empty(MouseConfigurationCodec.PlanChanges(profile, profile.Settings));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    public void WritesOnlyRawActiveStageEvenWhenOutsideCyclingCount(byte stage)
    {
        // The captured receiver has count=1 and active=1. Preserve both fields:
        // the Windows driver indexes the raw active slot independently of count.
        var memory = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures/aj199-profile.bin"));
        MouseConfigurationCodec.Record([stage]).CopyTo(memory, 4);
        var original = Decode(memory);
        var patches = MouseConfigurationCodec.PlanChanges(original, new(750, 1000, "#123456"));
        Assert.Equal(new ushort[] { (ushort)(0x0c + stage * 4), (ushort)(0x2c + stage * 4) },
            patches.Select(p => p.Address).ToArray());
        Assert.All(patches, p => Assert.Equal(4, p.After.Length));
    }

    [Fact]
    public void RejectsActiveSlotOutsideHardwareTable()
    {
        var memory = MakeMemory();
        MouseConfigurationCodec.Record([8]).CopyTo(memory, 4);
        Assert.Throws<FormatException>(() => Decode(memory));
    }

    [Fact]
    public void DisconnectedMouseCannotReadOrWriteSettings()
    {
        using var connection = new FakeMouse { Connected = false };
        var service = new HardwareMouse(connection);
        var error = Assert.Throws<IOException>(() => service.Read(CancellationToken.None));
        Assert.Contains("desconectado", error.Message);
        Assert.DoesNotContain(connection.Commands, c => c[1] is 7 or 8);
    }

    [Fact]
    public void CapturedIdentityReplyMatchesRequestAndChecksum()
    {
        var response = Convert.FromHexString("0901000000086E66BE054001000000006B");
        Assert.True(LegacyProtocol.IsResponse(response, LegacyProtocol.Identify([17, 29, 43, 61])));
        response[10] ^= 1;
        Assert.False(LegacyProtocol.IsResponse(response, LegacyProtocol.Identify([17, 29, 43, 61])));
    }

    [Theory]
    [InlineData(100)]
    [InlineData(800)]
    [InlineData(1600)]
    [InlineData(12800)]
    [InlineData(16000)]
    [InlineData(26000)]
    public void EncodesBothDpiAxesAndRoundTripsHighBits(int dpi)
    {
        var memory = MakeMemory();
        var original = Decode(memory);
        var patches = MouseConfigurationCodec.PlanChanges(original, new(dpi, 500, "#33AAFF"));
        foreach (var patch in patches) patch.After.CopyTo(memory, patch.Address);
        var changed = Decode(memory);
        Assert.Equal(dpi, changed.Settings.Dpi);
        Assert.Equal(dpi, changed.DpiY);
        Assert.Equal(500, changed.Settings.PollingHz);
        Assert.Equal("#33AAFF", changed.Settings.Color);
        for (var i = 0; i < memory.Length; i++)
            if (!patches.Any(p => i >= p.Address && i < p.Address + p.After.Length))
                Assert.Equal(original.Memory[i], memory[i]);
    }

    [Fact]
    public void RejectsCorruptOrIncompatibleProfiles()
    {
        var memory = MakeMemory();
        memory[0x10] ^= 1;
        Assert.Throws<FormatException>(() => Decode(memory));
        Assert.Throws<NotSupportedException>(() => MouseConfigurationCodec.Decode(new(0x41, 1, 1, 50), MakeMemory()));
    }

    [Fact]
    public void SuccessfulWriteIsBackedUpAndReadBackWithoutResetCommand()
    {
        using var connection = new FakeMouse();
        var service = new HardwareMouse(connection);
        var original = service.Read(CancellationToken.None);
        var backedUp = false;
        connection.BeforeWrite = () => Assert.True(backedUp);
        var changed = service.Apply(original, new(2400, 500, "#33AAFF"),
            snapshot => { Assert.Equal(original.Memory, snapshot.Memory); backedUp = true; }, CancellationToken.None);
        Assert.Equal(new MouseSettings(2400, 500, "#33AAFF"), changed.Settings);
        Assert.Equal(3, connection.Commands.Count(c => c[1] == 7));
        Assert.DoesNotContain(connection.Commands, c => c[1] == 9);
        Assert.Equal(LegacyProtocol.SetDriverActive(false), connection.Commands[^1]);
    }

    [Fact]
    public void CannotWriteIfBackupFails()
    {
        using var connection = new FakeMouse();
        var service = new HardwareMouse(connection);
        var original = service.Read(CancellationToken.None);
        Assert.Throws<IOException>(() => service.Apply(original, new(2400, 500, "#33AAFF"),
            _ => throw new IOException("Disk unavailable"), CancellationToken.None));
        Assert.DoesNotContain(connection.Commands, c => c[1] == 7);
    }

    [Fact]
    public void RefusesStaleStateBeforeWriting()
    {
        using var connection = new FakeMouse();
        var service = new HardwareMouse(connection);
        var original = service.Read(CancellationToken.None);
        connection.Memory[6] ^= 1;
        Assert.Throws<InvalidOperationException>(() => service.Apply(original, new(2400, 500, "#33AAFF"),
            _ => { }, CancellationToken.None));
        Assert.DoesNotContain(connection.Commands, c => c[1] == 7);
    }

    [Fact]
    public void NeverReportsSuccessIfReadbackDiffers()
    {
        using var connection = new FakeMouse { IgnoreWrites = true };
        var service = new HardwareMouse(connection);
        var original = service.Read(CancellationToken.None);
        Assert.Throws<IOException>(() => service.Apply(original, new(2400, 500, "#33AAFF"),
            _ => { }, CancellationToken.None));
    }

    [Fact]
    public void ReadTimeoutClosesSessionWithoutAnyConfigurationWrite()
    {
        using var connection = new FakeMouse { TimeoutOnRead = true };
        var service = new HardwareMouse(connection);
        Assert.Throws<TimeoutException>(() => service.Read(CancellationToken.None));
        Assert.DoesNotContain(connection.Commands, c => c[1] == 7);
        Assert.Equal(LegacyProtocol.SetDriverActive(false), connection.Commands[^1]);
    }

    [Fact]
    public void ReadReplyMustMatchOpcodeAndBothAddressBytes()
    {
        var request = LegacyProtocol.ReadBlock(0x1200, 4);
        var reply = Reply(request, [1, 2, 3, 4]);
        Assert.True(LegacyProtocol.IsResponse(reply, request));
        reply[3] = 0;
        reply[^1] = unchecked((byte)(reply[^1] + 0x12));
        Assert.False(LegacyProtocol.IsResponse(reply, request));
    }

    private static MouseConfiguration Decode(byte[] memory) => MouseConfigurationCodec.Decode(new(0x40, 1, 1, 35), memory);
    private static byte[] MakeMemory()
    {
        var memory = Enumerable.Repeat((byte)0x9a, MouseConfigurationCodec.MemoryLength).ToArray();
        MouseConfigurationCodec.Record([1]).CopyTo(memory, 0);
        MouseConfigurationCodec.Record([6]).CopyTo(memory, 2);
        MouseConfigurationCodec.Record([1]).CopyTo(memory, 4);
        MouseConfigurationCodec.Record([31, 31, 0]).CopyTo(memory, 0x10); // active stage 1 = 1600 DPI
        MouseConfigurationCodec.Record([0, 255, 0]).CopyTo(memory, 0x30);
        return memory;
    }
    private static byte[] Reply(byte[] request, byte[] payload)
    {
        var response = new byte[17];
        response[0] = 9; response[1] = request[1];
        response[3] = request[3]; response[4] = request[4];
        response[5] = (byte)payload.Length;
        payload.CopyTo(response, 6);
        response[16] = unchecked((byte)(0x55 - response.Take(16).Sum(b => b)));
        return response;
    }

    private sealed class FakeMouse : IMouseConnection
    {
        internal byte[] Memory = MakeMemory();
        internal List<byte[]> Commands = [];
        internal Action? BeforeWrite;
        internal bool Connected = true;
        internal bool IgnoreWrites;
        internal bool TimeoutOnRead;
        public byte[] Exchange(byte[] request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.True(LegacyProtocol.HasValidChecksum(request));
            Commands.Add(request);
            var address = request[3] << 8 | request[4];
            switch (request[1])
            {
                case 1: return Reply(request, [0x6e, 0x66, 0xbe, 5, 0x40, 1, 0, 0]);
                case 3: return Reply(request, [Connected ? (byte)1 : (byte)0]);
                case 4: return Reply(request, [2, 35]);
                case 2: return Reply(request, [request[6]]);
                case 14: return Reply(request, [0]);
                case 8:
                    if (TimeoutOnRead) throw new TimeoutException("No configuration response");
                    return Reply(request, Memory.AsSpan(address, request[5]).ToArray());
                case 7:
                    BeforeWrite?.Invoke();
                    if (!IgnoreWrites) request.AsSpan(6, request[5]).CopyTo(Memory.AsSpan(address));
                    return Reply(request, []);
                default: throw new InvalidOperationException("Unexpected opcode");
            }
        }
        public void Dispose() { }
    }
}
