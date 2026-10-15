namespace Ajazz.Core;

public sealed record MouseSettings(int Dpi, int PollingHz, string Color);
public sealed record MouseIdentity(byte Cid, byte Mid, int ConnectionStatus, int? BatteryPercent)
{
    public bool Connected => ConnectionStatus == 1;
}
public sealed record MouseConfiguration(MouseIdentity Identity, MouseSettings Settings, int ActiveStage,
    int StageCount, int DpiY, byte[] Memory);
public sealed record MemoryPatch(ushort Address, byte[] Before, byte[] After);

/// <summary>Legacy PAW3395 profile layout recovered from OemDrv; checks every record before permitting writes.</summary>
public static class MouseConfigurationCodec
{
    public const int MemoryLength = 0x54;

    public static MouseConfiguration Decode(MouseIdentity identity, ReadOnlySpan<byte> memory)
    {
        if (identity.Cid != 0x40 || identity.Mid != 0x01)
            throw new NotSupportedException("Este mouse não é o AJ199 legado esperado (40:01).");
        if (memory.Length < MemoryLength) throw new FormatException("Perfil incompleto.");
        CheckRecord(memory[..2]);
        CheckRecord(memory.Slice(2, 2));
        CheckRecord(memory.Slice(4, 2));
        var count = memory[2];
        // OemDrv loads address 0x04 directly into g_nCurDpiLevel (0x40f9d7)
        // and uses it as a zero-based array index (0x404810). Do not subtract 1.
        var stage = memory[4];
        if (count is < 1 or > 8 || stage > 7) throw new FormatException("Estágio de DPI inválido.");
        var dpiRecord = memory.Slice(0x0c + stage * 4, 4);
        CheckRecord(dpiRecord);
        var color = memory.Slice(0x2c + stage * 4, 4);
        CheckRecord(color);
        var flags = dpiRecord[2];
        var xCode = dpiRecord[0] | ((flags & 0x0c) << 6);
        var yCode = dpiRecord[1] | ((flags & 0xc0) << 2);
        var x = (xCode + 1) * 50 * ((flags & 1) != 0 ? 2 : 1);
        var y = (yCode + 1) * 50 * ((flags & 0x10) != 0 ? 2 : 1);
        if (x is < 50 or > 26000 || y is < 50 or > 26000)
            throw new FormatException("DPI fora da faixa do PAW3395.");
        var hz = memory[0] switch { 1 => 1000, 2 => 500, 4 => 250, 8 => 125, _ => throw new FormatException("Taxa de resposta desconhecida.") };
        return new(identity, new(x, hz, $"#{color[0]:X2}{color[1]:X2}{color[2]:X2}"), stage, count, y, memory[..MemoryLength].ToArray());
    }

    public static IReadOnlyList<MemoryPatch> PlanChanges(MouseConfiguration current, MouseSettings desired)
    {
        // Revalidate snapshot, including its identity and all records touched.
        var checkedCurrent = Decode(current.Identity, current.Memory);
        if (checkedCurrent.ActiveStage != current.ActiveStage) throw new FormatException("Snapshot inconsistente.");
        Validate(desired);
        var result = new List<MemoryPatch>();
        void Add(ushort address, byte[] data)
        {
            var before = current.Memory.AsSpan(address, data.Length).ToArray();
            if (!before.AsSpan().SequenceEqual(data)) result.Add(new(address, before, data));
        }
        if (desired.Dpi != current.Settings.Dpi)
        {
            var code = desired.Dpi / 50 - 1;
            var flags = (byte)(((code >> 8) & 3) << 2 | ((code >> 8) & 3) << 6);
            // A single DPI control intentionally sets both axes to the same value.
            Add((ushort)(0x0c + current.ActiveStage * 4), Record([(byte)code, (byte)code, flags]));
        }
        var hzCode = desired.PollingHz switch { 1000 => (byte)1, 500 => (byte)2, 250 => (byte)4, _ => (byte)8 };
        Add(0, Record([hzCode]));
        Add((ushort)(0x2c + current.ActiveStage * 4), Record(Convert.FromHexString(desired.Color[1..])));
        return result;
    }

    public static void Validate(MouseSettings settings)
    {
        if (settings.Dpi is < 100 or > 26000 || settings.Dpi % 50 != 0)
            throw new ArgumentOutOfRangeException(nameof(settings), "Use DPI de 100 a 26000, em passos de 50.");
        if (settings.PollingHz is not (125 or 250 or 500 or 1000))
            throw new ArgumentOutOfRangeException(nameof(settings), "Escolha 125, 250, 500 ou 1000 Hz.");
        if (settings.Color.Length != 7 || settings.Color[0] != '#' || !settings.Color[1..].All(Uri.IsHexDigit))
            throw new ArgumentException("Use uma cor no formato #RRGGBB.", nameof(settings));
    }

    public static byte[] Record(ReadOnlySpan<byte> data)
    {
        var result = new byte[data.Length + 1];
        data.CopyTo(result);
        var sum = 0;
        foreach (var value in data) sum += value;
        result[^1] = unchecked((byte)(0x55 - sum));
        return result;
    }

    private static void CheckRecord(ReadOnlySpan<byte> bytes)
    {
        var sum = 0;
        foreach (var b in bytes) sum += b;
        if ((sum & 255) != 0x55) throw new FormatException("Checksum do registro de configuração inválido.");
    }
}
