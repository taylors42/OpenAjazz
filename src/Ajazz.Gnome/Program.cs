using Ajazz.Gnome;

if (args.Any(a => a is "--help" or "-h"))
{
    Console.WriteLine("OpenAjazz · PoC GNOME\n  dotnet run --project src/Ajazz.Gnome\n  --smoke-test   Exercita enumeração, simulação e cancelamento; encerra automaticamente.\n  --hardware-smoke-test   Lê o mouse real; retorna 3 se o perfil estiver indisponível.");
    return 0;
}
if (args.Any(a => a is not ("--smoke-test" or "--hardware-smoke-test")))
{
    Console.Error.WriteLine("Argumento inválido. Use --help.");
    return 2;
}
var smokeTest = args.Contains("--smoke-test");
var hardwareSmokeTest = args.Contains("--hardware-smoke-test");
if (smokeTest && hardwareSmokeTest) { Console.Error.WriteLine("Escolha apenas um modo de teste."); return 2; }
var result = 0;
var application = Adw.Application.New("io.github.ajazzlinux.Poc", Gio.ApplicationFlags.FlagsNone);
MainWindow? main = null;
application.OnActivate += (_, _) =>
{
    main ??= new MainWindow(application, smokeTest);
    main.Window.Present();
    if (smokeTest)
    {
        smokeTest = false;
        _ = SmokeAsync();
    }
    else if (hardwareSmokeTest)
    {
        hardwareSmokeTest = false;
        _ = HardwareSmokeAsync();
    }
};
async Task HardwareSmokeAsync()
{
    try
    {
        var loaded = await main!.HardwareSmokeAsync();
        Console.WriteLine(loaded ? "HARDWARE_PROFILE_READ_OK" : "HARDWARE_PROFILE_UNAVAILABLE");
        if (!loaded) result = 3;
    }
    catch (Exception ex) { Console.Error.WriteLine(ex); result = 1; }
    finally { main!.Shutdown(); application.Quit(); }
}
async Task SmokeAsync()
{
    try { await main!.SmokeAsync(); Console.WriteLine("GNOME_SMOKE_OK"); }
    catch (Exception ex) { Console.Error.WriteLine(ex); result = 1; }
    finally { main!.Shutdown(); application.Quit(); }
}
application.OnShutdown += (_, _) => main?.Shutdown();
var exit = application.RunWithSynchronizationContext(null);
GC.KeepAlive(main);
return result == 0 ? exit : result;
