namespace Ajazz.Application;

/// <summary>In-memory simulation. Has no reference to a hardware transport.</summary>
public sealed class DemoSession
{
    private readonly SemaphoreSlim gate = new(1, 1);
    public DemoProfile Current { get; private set; } = DemoProfile.Default;

    public async Task<DemoProfile> ApplyAsync(int dpi, int pollingHz, CancellationToken cancellationToken)
        => await ApplyAsync(dpi, pollingHz, Current.Color, cancellationToken).ConfigureAwait(false);

    public async Task<DemoProfile> ApplyAsync(int dpi, int pollingHz, string color, CancellationToken cancellationToken)
    {
        var next = Current.WithSettings(dpi, pollingHz, color);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await Task.Delay(700, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            Current = next;
            return Current;
        }
        finally { gate.Release(); }
    }
}
