using Ajazz.Core;

namespace Ajazz.Application;

public interface IMouseConnection : IDisposable
{
    byte[] Exchange(byte[] request, CancellationToken cancellationToken);
}

/// <summary>One serialized transaction; success means the profile was read back and matched.</summary>
public sealed class HardwareMouse(IMouseConnection connection)
{
    public MouseIdentity Identify(CancellationToken cancellationToken)
    {
        var response = connection.Exchange(LegacyProtocol.Identify([17, 29, 43, 61]), cancellationToken);
        if (response[5] != 8 || response[10] != 0x40 || response[11] != 0x01)
            throw new NotSupportedException("O dispositivo respondeu com uma identidade diferente do AJ199 legado.");
        var link = connection.Exchange(LegacyProtocol.QueryConnectionStatus(), cancellationToken);
        var power = connection.Exchange(LegacyProtocol.QueryPower(), cancellationToken);
        return new(response[10], response[11], link[6], power[6] <= 100 ? power[6] : null);
    }

    public MouseConfiguration Read(CancellationToken cancellationToken)
    {
        var identity = Identify(cancellationToken);
        if (!identity.Connected)
            throw new IOException("O receptor está conectado, mas informa que o mouse está desconectado. Ligue o mouse, mova-o para acordar ou conecte o cabo USB e atualize.");
        connection.Exchange(LegacyProtocol.SetDriverActive(true), cancellationToken);
        try { return ReadProfile(identity, cancellationToken); }
        finally { CloseSession(); }
    }

    public MouseConfiguration Apply(MouseConfiguration expected, MouseSettings desired,
        Action<MouseConfiguration> backup, CancellationToken cancellationToken)
    {
        MouseConfigurationCodec.Validate(desired);
        var identity = Identify(cancellationToken);
        if (!identity.Connected)
            throw new IOException("O receptor está conectado, mas informa que o mouse está desconectado. Ligue o mouse, mova-o para acordar ou conecte o cabo USB e atualize.");
        connection.Exchange(LegacyProtocol.SetDriverActive(true), cancellationToken);
        try
        {
            var current = ReadProfile(identity, cancellationToken);
            if (!expected.Memory.AsSpan().SequenceEqual(current.Memory))
                throw new InvalidOperationException("O perfil mudou no mouse. Recarregue os valores antes de aplicar.");
            var patches = MouseConfigurationCodec.PlanChanges(current, desired);
            if (patches.Count == 0) return current;
            backup(current); // Must succeed before the first configuration write.
            cancellationToken.ThrowIfCancellationRequested();
            // Once the first write begins, finish/verify the bounded transaction even on window close.
            foreach (var patch in patches)
                connection.Exchange(LegacyProtocol.WriteBlock(patch.Address, patch.After), CancellationToken.None);
            var verified = ReadProfile(identity, CancellationToken.None);
            foreach (var patch in patches)
                if (!verified.Memory.AsSpan(patch.Address, patch.After.Length).SequenceEqual(patch.After))
                    throw new IOException("O mouse não confirmou os ajustes. Recarregue o perfil; não foi informado sucesso.");
            return verified;
        }
        finally { CloseSession(); }
    }

    private MouseConfiguration ReadProfile(MouseIdentity identity, CancellationToken cancellationToken)
    {
        var active = connection.Exchange(LegacyProtocol.QueryProfile(), cancellationToken);
        if (active[6] != 0) throw new NotSupportedException("Esta versão suporta somente o perfil de hardware 0.");
        var memory = new byte[MouseConfigurationCodec.MemoryLength];
        for (var offset = 0; offset < memory.Length; offset += 10)
        {
            var length = (byte)Math.Min(10, memory.Length - offset);
            var response = connection.Exchange(LegacyProtocol.ReadBlock((ushort)offset, length), cancellationToken);
            if (response[5] != length) throw new FormatException("Tamanho inesperado na leitura do perfil.");
            response.AsSpan(6, length).CopyTo(memory.AsSpan(offset));
        }
        return MouseConfigurationCodec.Decode(identity, memory);
    }

    private void CloseSession()
    {
        try { connection.Exchange(LegacyProtocol.SetDriverActive(false), CancellationToken.None); }
        catch (Exception ex) when (ex is IOException or TimeoutException) { /* Preserve original transaction error. */ }
    }
}
