using Ajazz.Core;
using Xunit;

namespace Ajazz.Tests;

public class ProtocolTests
{
    // Synthetic golden vectors derived from the disassembly, not USB captures.
    [Fact]
    public void PowerQueryMatchesStaticAnalysis()
    {
        Assert.Equal(Convert.FromHexString("0804000000000000000000000000000049"), LegacyProtocol.QueryPower());
    }

    [Fact]
    public void BlockReadEncodesBigEndianAddressAndLength()
    {
        Assert.Equal(Convert.FromHexString("08080012340A00000000000000000000F5"), LegacyProtocol.ReadBlock(0x1234, 10));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    [InlineData(255)]
    public void RejectsInvalidReadSizes(byte count) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => LegacyProtocol.ReadBlock(0, count));

    [Fact]
    public void ChecksumWrapsAndDetectsCorruptionAndTruncation()
    {
        var report = LegacyProtocol.Encode(Enumerable.Repeat((byte)0xff, 15).ToArray());
        Assert.True(LegacyProtocol.HasValidChecksum(report));
        report[5] ^= 1;
        Assert.False(LegacyProtocol.HasValidChecksum(report));
        Assert.False(LegacyProtocol.HasValidChecksum(report.AsSpan(1)));
    }

    [Fact]
    public void LocalReceiverDescriptorExposesExpectedChannels()
    {
        var descriptor = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures/receiver-interface1.bin"));
        var reports = HidDescriptor.Parse(descriptor);
        Assert.Contains(new HidReport(8, "Feature", 128), reports);
        Assert.Contains(new HidReport(9, "Input", 128), reports);
        Assert.Contains(new HidReport(6, "Feature", 56), reports);
        Assert.Equal(7, reports.Count);
    }

    [Theory]
    [InlineData("FE02")]
    [InlineData("FE0200AA")]
    [InlineData("77FF")]
    [InlineData("B4")]
    public void RejectsMalformedDescriptors(string hex) =>
        Assert.Throws<FormatException>(() => HidDescriptor.Parse(Convert.FromHexString(hex)));

    [Fact]
    public void DescriptorPushAndPopRestoreGlobalReportState()
    {
        var reports = HidDescriptor.Parse(Convert.FromHexString("750895028501A4751095018502B102B48102"));
        Assert.Contains(new HidReport(1, "Input", 16), reports);
        Assert.Contains(new HidReport(2, "Feature", 16), reports);
    }
}
