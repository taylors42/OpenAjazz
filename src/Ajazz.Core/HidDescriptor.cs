namespace Ajazz.Core;

public sealed record HidReport(byte Id, string Kind, int Bits)
{
    public int PayloadBytes => (Bits + 7) / 8;
}

public static class HidDescriptor
{
    public static IReadOnlyList<HidReport> Parse(ReadOnlySpan<byte> data)
    {
        var state = (Size: 0, Count: 0, Id: (byte)0);
        var stack = new Stack<(int Size, int Count, byte Id)>();
        var reports = new Dictionary<(byte Id, string Kind), int>();
        var pos = 0;
        while (pos < data.Length)
        {
            var prefix = data[pos++];
            if (prefix == 0xfe)
            {
                if (pos + 2 > data.Length) throw new FormatException("Item HID longo truncado.");
                var length = data[pos];
                pos += 2;
                if (pos + length > data.Length) throw new FormatException("Payload HID truncado.");
                pos += length;
                continue;
            }
            var size = (prefix & 3) switch { 3 => 4, var n => n };
            if (pos + size > data.Length) throw new FormatException("Item HID truncado.");
            uint value = 0;
            for (var i = 0; i < size; i++) value |= (uint)data[pos++] << (8 * i);
            var type = (prefix >> 2) & 3;
            var tag = prefix >> 4;
            if (type == 1)
            {
                switch (tag)
                {
                    case 7: state.Size = checked((int)value); break;
                    case 8:
                        if (value is 0 or > 255) throw new FormatException("Report ID inválido.");
                        state.Id = (byte)value; break;
                    case 9: state.Count = checked((int)value); break;
                    case 10: stack.Push(state); break;
                    case 11:
                        if (!stack.TryPop(out state)) throw new FormatException("POP HID sem PUSH.");
                        break;
                }
            }
            else if (type == 0 && tag is 8 or 9 or 11)
            {
                var kind = tag switch { 8 => "Input", 9 => "Output", _ => "Feature" };
                var key = (state.Id, kind);
                reports[key] = checked(reports.GetValueOrDefault(key) + checked(state.Size * state.Count));
            }
        }
        return reports.OrderBy(x => x.Key.Id).ThenBy(x => x.Key.Kind)
            .Select(x => new HidReport(x.Key.Id, x.Key.Kind, x.Value)).ToArray();
    }
}
