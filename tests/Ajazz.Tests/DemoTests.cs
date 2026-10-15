using Ajazz.Application;
using Xunit;

namespace Ajazz.Tests;

public class DemoTests
{
    [Fact]
    public async Task ColorIsValidatedAndCommittedWithSettings()
    {
        var session = new DemoSession();
        await session.ApplyAsync(2400, 500, "#33aaff", CancellationToken.None);
        Assert.Equal("#33AAFF", session.Current.Color);
        using var cancellation = new CancellationTokenSource();
        var pending = session.ApplyAsync(3200, 125, "#FF0000", cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal("#33AAFF", session.Current.Color);
    }

    [Theory]
    [InlineData("red")]
    [InlineData("#12345")]
    [InlineData("#XX00FF")]
    public async Task RejectsMalformedColors(string color)
    {
        var session = new DemoSession();
        await Assert.ThrowsAsync<ArgumentException>(() => session.ApplyAsync(1600, 1000, color, CancellationToken.None));
        Assert.Equal(DemoProfile.Default, session.Current);
    }

    [Fact]
    public void DiagnosticSeparatesSimulationFromHardwareDiscovery()
    {
        using var document = System.Text.Json.JsonDocument.Parse(DiagnosticReport.Serialize(null, DemoProfile.Default));
        var root = document.RootElement;
        Assert.True(root.GetProperty("ProofOfConcept").GetBoolean());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, root.GetProperty("Discovery").ValueKind);
        Assert.Equal("Simulação", root.GetProperty("Simulation").GetProperty("Origin").GetString());
        Assert.False(root.GetProperty("PacketPreviews").GetProperty("SentToHardware").GetBoolean());
    }

    [Fact]
    public async Task CancellationPreservesLastCommittedState()
    {
        var session = new DemoSession();
        await session.ApplyAsync(2400, 500, CancellationToken.None);
        using var cancel = new CancellationTokenSource();
        var pending = session.ApplyAsync(3200, 125, cancel.Token);
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(2400, session.Current.Dpi);
        Assert.Equal(500, session.Current.PollingHz);
    }

    [Theory]
    [InlineData(0, 1000)]
    [InlineData(1550, 1000)]
    [InlineData(27000, 1000)]
    [InlineData(1600, 8000)]
    public async Task RejectsInvalidDemoSettingsWithoutCommitting(int dpi, int polling)
    {
        var session = new DemoSession();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => session.ApplyAsync(dpi, polling, CancellationToken.None));
        Assert.Equal(DemoProfile.Default, session.Current);
    }

    [Fact]
    public async Task ConcurrentSimulatedOperationsAreSerialized()
    {
        var session = new DemoSession();
        var first = session.ApplyAsync(800, 125, CancellationToken.None);
        var second = session.ApplyAsync(2400, 500, CancellationToken.None);
        await Task.WhenAll(first, second);
        Assert.Equal(2400, session.Current.Dpi);
    }
}
