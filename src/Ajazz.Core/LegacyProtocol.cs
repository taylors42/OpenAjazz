namespace Ajazz.Core;

/// <summary>Legacy request/response codec. No device I/O.</summary>
public static class LegacyProtocol
{
    public const int ReportLength = 17;

    public static byte[] QueryPower() => Encode([0x04]);

    public static byte[] ReadBlock(ushort address, byte count)
    {
        if (count is < 1 or > 10)
            throw new ArgumentOutOfRangeException(nameof(count), "Um bloco deve conter de 1 a 10 bytes.");
        return Encode([0x08, 0x00, (byte)(address >> 8), (byte)address, count]);
    }

    public static byte[] Identify(ReadOnlySpan<byte> challenge)
    {
        if (challenge.Length != 4) throw new ArgumentException("O desafio deve ter quatro bytes.", nameof(challenge));
        byte[] body = [1, 0, 0, 0, 8, 0, 0, 0, 0, 0, 0, 0, 0];
        challenge.CopyTo(body.AsSpan(5));
        return Encode(body);
    }

    public static byte[] SetDriverActive(bool active) => Encode([2, 0, 0, 0, 1, active ? (byte)1 : (byte)0]);
    public static byte[] QueryConnectionStatus() => Encode([3]);
    public static byte[] QueryProfile() => Encode([0x0e]);

    public static byte[] WriteBlock(ushort address, ReadOnlySpan<byte> data)
    {
        if (data.Length is < 1 or > 10) throw new ArgumentOutOfRangeException(nameof(data));
        var body = new byte[5 + data.Length];
        body[0] = 7; body[2] = (byte)(address >> 8); body[3] = (byte)address; body[4] = (byte)data.Length;
        data.CopyTo(body.AsSpan(5));
        return Encode(body);
    }

    public static bool IsResponse(ReadOnlySpan<byte> response, ReadOnlySpan<byte> request)
    {
        if (response.Length != ReportLength || response[0] != 9 || request.Length != ReportLength) return false;
        var sum = 0;
        foreach (var b in response) sum += b;
        return (sum & 255) == 0x55 && response[1] == request[1]
            && response[3] == request[3] && response[4] == request[4];
    }

    public static byte[] Encode(ReadOnlySpan<byte> body)
    {
        if (body.Length is < 1 or > 15)
            throw new ArgumentOutOfRangeException(nameof(body));
        var report = new byte[ReportLength];
        report[0] = 0x08;
        body.CopyTo(report.AsSpan(1));
        var sum = 0;
        foreach (var value in report.AsSpan(0, 16)) sum += value;
        report[16] = unchecked((byte)(0x55 - sum));
        return report;
    }

    public static bool HasValidChecksum(ReadOnlySpan<byte> report)
    {
        if (report.Length != ReportLength || report[0] != 0x08) return false;
        var sum = 0;
        foreach (var value in report) sum += value;
        return (sum & 0xff) == 0x55;
    }

    public static string Hex(ReadOnlySpan<byte> bytes) => Convert.ToHexString(bytes).Chunk(2)
        .Select(chars => new string(chars)).Aggregate("", (s, item) => s.Length == 0 ? item : s + " " + item);
}
