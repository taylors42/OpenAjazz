using Ajazz.Core;

namespace Ajazz.Application;

public sealed record DeviceCandidate(string Path, string Name, ushort VendorId, ushort ProductId,
    int InterfaceNumber, string[] Usages, IReadOnlyList<HidReport> Reports,
    bool CanReadWrite, string? DescriptorError)
{
    public bool HasConfigurationChannel => Reports.Any(r => r.Id == 0x08 && r.Kind == "Feature" && r.Bits == 128)
        && Reports.Any(r => r.Id == 0x09 && r.Kind == "Input" && r.Bits == 128);
    public string UsbId => $"{VendorId:X4}:{ProductId:X4}";
}

public sealed record DiscoverySnapshot(DateTimeOffset Timestamp, string Backend,
    IReadOnlyList<DeviceCandidate> Devices, string? Error)
{
    public const string Scope = "Enumeração apenas; identidade CID/MID não validada; nenhum comando HID enviado.";
}

public interface IDeviceDiscovery
{
    Task<DiscoverySnapshot> DiscoverAsync(CancellationToken cancellationToken);
}

public sealed record DemoProfile(int Dpi, int PollingHz, int BatteryPercent, string Origin = "Simulação", string Color = "#FF00FF")
{
    public static DemoProfile Default => new(1600, 1000, 76);
    public DemoProfile WithSettings(int dpi, int pollingHz, string? color = null)
    {
        if (dpi is < 100 or > 26000 || dpi % 100 != 0)
            throw new ArgumentOutOfRangeException(nameof(dpi), "Use DPI de 100 a 26000 em passos de 100 na demonstração.");
        if (pollingHz is not (125 or 250 or 500 or 1000))
            throw new ArgumentOutOfRangeException(nameof(pollingHz));
        color ??= Color;
        if (color.Length != 7 || color[0] != '#' || !color[1..].All(Uri.IsHexDigit))
            throw new ArgumentException("Use uma cor no formato #RRGGBB.", nameof(color));
        return this with { Dpi = dpi, PollingHz = pollingHz, Color = color.ToUpperInvariant() };
    }
}
