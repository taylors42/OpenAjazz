using System.Text.Json;
using Ajazz.Core;

namespace Ajazz.Application;

public static class DiagnosticReport
{
    public static string Serialize(DiscoverySnapshot? discovery, DemoProfile simulation) =>
        JsonSerializer.Serialize(new
        {
            ProofOfConcept = true,
            Scope = DiscoverySnapshot.Scope,
            Discovery = discovery,
            Simulation = simulation,
            PacketPreviews = new
            {
                Power = LegacyProtocol.Hex(LegacyProtocol.QueryPower()),
                ReadDpiBlock = LegacyProtocol.Hex(LegacyProtocol.ReadBlock(0x000c, 10)),
                SentToHardware = false
            }
        }, new JsonSerializerOptions { WriteIndented = true });
}
