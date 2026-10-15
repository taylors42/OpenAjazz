using System.Text.Json;
using Ajazz.Application;
using Ajazz.Core;
using Ajazz.Infrastructure.Linux;

using var stop = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Cancel(); };
var json = new JsonSerializerOptions { WriteIndented = true };
try
{
    switch (args.FirstOrDefault() ?? "help")
    {
        case "devices" when args.Length == 1 || args is ["devices", "--json"]:
            var snapshot = await new LinuxDiscovery().DiscoverAsync(stop.Token);
            if (args.Contains("--json")) Console.WriteLine(JsonSerializer.Serialize(snapshot, json));
            else
            {
                Console.WriteLine(DiscoverySnapshot.Scope);
                foreach (var device in snapshot.Devices)
                    Console.WriteLine($"{device.Path}  {device.UsbId}  interface {device.InterfaceNumber}  " +
                        $"configuração={device.HasConfigurationChannel}  permissão RW={device.CanReadWrite}");
                if (snapshot.Devices.Count == 0) Console.WriteLine("Nenhum receptor candidato encontrado.");
                if (snapshot.Error != null) Console.Error.WriteLine(snapshot.Error);
            }
            return snapshot.Error == null ? 0 : 1;
        case "apply" when args.Length == 4 && int.TryParse(args[1], out var dpi) && int.TryParse(args[2], out var hz):
            var available = await new LinuxDiscovery().DiscoverAsync(stop.Token);
            var mouse = available.Devices.FirstOrDefault(d => d.HasConfigurationChannel)
                ?? throw new IOException("Mouse não encontrado.");
            var backend = new LinuxMouseAccess();
            var before = await backend.ReadAsync(mouse, stop.Token);
            var after = await backend.ApplyAsync(mouse, before, new MouseSettings(dpi, hz, args[3]), stop.Token);
            Console.WriteLine(JsonSerializer.Serialize(after, json));
            return 0;
        case "info" or "read" when args.Length == 1:
            var found = await new LinuxDiscovery().DiscoverAsync(stop.Token);
            var candidate = found.Devices.FirstOrDefault(d => d.HasConfigurationChannel)
                ?? throw new IOException(found.Error ?? "Nenhuma interface de configuração do AJ199 encontrada.");
            var access = new LinuxMouseAccess();
            if (args[0] == "info")
                Console.WriteLine(JsonSerializer.Serialize(await access.IdentifyAsync(candidate, stop.Token), json));
            else
                Console.WriteLine(JsonSerializer.Serialize(await access.ReadAsync(candidate, stop.Token), json));
            return 0;
        case "demo" when args.Length == 1:
            Console.WriteLine(JsonSerializer.Serialize(await new DemoSession().ApplyAsync(2400, 500, stop.Token), json));
            return 0;
        case "packet" when args is ["packet", "power"]:
            Console.WriteLine("Prévia offline, não enviada: " + LegacyProtocol.Hex(LegacyProtocol.QueryPower()));
            return 0;
        case "help" or "--help" or "-h":
            Console.WriteLine("OpenAjazz PoC\n  devices [--json]  Enumeração HID sem abrir o dispositivo\n  info              Consulta real de identidade, conexão e energia\n  read              Lê o perfil real do mouse\n  apply DPI HZ COR  Grava os ajustes e confirma por leitura; COR no formato #RRGGBB\n  demo              Simulação em memória\n  packet power      Prévia offline de consulta de energia");
            return 0;
        default:
            Console.Error.WriteLine("Comando inválido. Use --help.");
            return 2;
    }
}
catch (OperationCanceledException) { Console.Error.WriteLine("Cancelado."); return 130; }

catch (Exception ex) when (ex is IOException or TimeoutException or NotSupportedException or FormatException or ArgumentException or InvalidOperationException)
{ Console.Error.WriteLine(ex.Message); return 1; }
