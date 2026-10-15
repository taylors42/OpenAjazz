using Ajazz.Application;
using Ajazz.Core;
using Ajazz.Infrastructure.Linux;

namespace Ajazz.Gnome;

internal sealed class MainWindow
{
    private static readonly int[] Rates = [125, 250, 500, 1000];
    private readonly DemoSession demo = new();
    private readonly bool demoMode;
    private readonly LinuxMouseAccess hardware = new();
    private MouseConfiguration? configuration;
    private DeviceCandidate? candidate;
    private readonly CancellationTokenSource lifetime = new();
    private CancellationTokenSource? operation;
    private bool closed;
    private bool closeRequested;
    private DiscoverySnapshot? snapshot;
    private readonly Adw.WindowTitle title = Adw.WindowTitle.New("AJAZZ AJ199", "Procurando mouse…");
    private readonly Gtk.Button refresh = Gtk.Button.NewFromIconName("view-refresh-symbolic");
    private readonly Gtk.Button apply = Gtk.Button.NewWithLabel("Aplicar");
    private readonly Gtk.Button undo = Gtk.Button.NewWithLabel("Desfazer");
    private readonly Gtk.Button cancel = Gtk.Button.NewWithLabel("Cancelar");
    private readonly Gtk.Spinner spinner = Gtk.Spinner.New();
    private readonly Gtk.Label dpiUnavailable = Label("—");
    private readonly Gtk.Label rateUnavailable = Label("—");
    private readonly Gtk.Label colorUnavailable = Label("—");
    private readonly Gtk.SpinButton dpi = Gtk.SpinButton.NewWithRange(100, 26000, 50);
    private readonly Gtk.DropDown polling = Gtk.DropDown.NewFromStrings(["125 Hz", "250 Hz", "500 Hz", "1000 Hz"]);
    private readonly Gtk.ColorDialogButton color;
    private readonly Gtk.Label status = Label("Lendo as configurações do mouse…");
    private readonly Gtk.Label notice = Label("Carregando configurações reais do mouse…");
    internal Adw.ApplicationWindow Window { get; }

    internal MainWindow(Adw.Application app, bool demoMode = false)
    {
        this.demoMode = demoMode;
        if (demoMode) apply.SetLabel("Aplicar à demonstração");
        Window = Adw.ApplicationWindow.New(app);
        Window.Title = "OpenAjazz · AJ199";
        Window.SetDefaultSize(640, 640);
        Window.OnCloseRequest += (_, _) =>
        {
            if (!demoMode && operation != null)
            {
                closeRequested = true;
                operation.Cancel();
                status.SetText("Concluindo a operação do mouse antes de fechar…");
                return true;
            }
            Shutdown();
            return false;
        };
        var dialog = Gtk.ColorDialog.New();
        dialog.Title = "Cor do indicador de DPI";
        dialog.WithAlpha = false;
        color = Gtk.ColorDialogButton.New(dialog);
        color.TooltipText = "Escolher a cor do indicador de DPI";

        var root = Gtk.Box.New(Gtk.Orientation.Vertical, 0);
        var header = Adw.HeaderBar.New();
        header.TitleWidget = title;
        refresh.TooltipText = "Procurar mouse novamente";
        header.PackEnd(refresh);
        root.Append(header);

        var content = Gtk.Box.New(Gtk.Orientation.Vertical, 20);
        content.MarginStart = content.MarginEnd = 24;
        content.MarginTop = content.MarginBottom = 24;
        var heading = Label("Configurações do mouse");
        heading.AddCssClass("title-1");
        content.Append(heading);
        if (demoMode) notice.SetText("Demonstração: os ajustes não são gravados no mouse.");
        notice.Wrap = true;
        notice.AddCssClass("dim-label");
        content.Append(notice);

        var sensitivity = Adw.PreferencesGroup.New();
        sensitivity.Title = "Sensibilidade";
        var dpiRow = Row("DPI", "Velocidade do sensor");
        AddControl(dpiRow, dpi);
        dpiRow.AddSuffix(dpiUnavailable);
        sensitivity.Add(dpiRow);
        dpi.Numeric = true;
        dpi.SnapToTicks = true;
        dpi.TooltipText = "DPI entre 100 e 26000, em passos de 50";
        content.Append(sensitivity);

        var performance = Adw.PreferencesGroup.New();
        performance.Title = "Desempenho";
        var rateRow = Row("Taxa de resposta", "Frequência de atualização em Hz");
        AddControl(rateRow, polling);
        rateRow.AddSuffix(rateUnavailable);
        performance.Add(rateRow);
        content.Append(performance);

        var appearance = Adw.PreferencesGroup.New();
        appearance.Title = "Cores";
        var colorRow = Row("Indicador de DPI", "Cor associada à sensibilidade");
        AddControl(colorRow, color);
        colorRow.AddSuffix(colorUnavailable);
        appearance.Add(colorRow);
        content.Append(appearance);

        var footer = Gtk.Box.New(Gtk.Orientation.Horizontal, 8);
        footer.Halign = Gtk.Align.End;
        apply.AddCssClass("suggested-action");
        footer.Append(spinner);
        footer.Append(cancel);
        footer.Append(undo);
        footer.Append(apply);
        content.Append(footer);
        status.Wrap = true;
        status.AddCssClass("dim-label");
        content.Append(status);
        var clamp = Adw.Clamp.New();
        clamp.MaximumSize = 640;
        clamp.Child = content;
        var scroll = Gtk.ScrolledWindow.New();
        scroll.SetPolicy(Gtk.PolicyType.Never, Gtk.PolicyType.Automatic);
        scroll.Vexpand = true;
        scroll.Child = clamp;
        root.Append(scroll);
        Window.Content = root;

        if (demoMode) RestoreControls();
        else { dpi.Text_ = ""; polling.Selected = uint.MaxValue; }
        SetBusy(false);
        refresh.OnClicked += async (_, _) => await RefreshAsync();
        apply.OnClicked += async (_, _) => await ApplyDemoAsync();
        undo.OnClicked += (_, _) =>
        {
            RestoreControls();
            status.SetText("Alterações pendentes desfeitas.");
        };
        cancel.OnClicked += (_, _) => operation?.Cancel();
        cancel.Visible = false;
        _ = RefreshAsync();
        if (!demoMode) _ = RetryUnavailableAsync();
    }

    private async Task RetryUnavailableAsync()
    {
        try
        {
            while (!closed)
            {
                await Task.Delay(TimeSpan.FromSeconds(3), lifetime.Token);
                if (!closed && configuration == null && operation == null && refresh.Sensitive)
                    await RefreshAsync();
            }
        }
        catch (OperationCanceledException) { }
    }

    private void RestoreControls()
    {
        var settings = demoMode ? new MouseSettings(demo.Current.Dpi, demo.Current.PollingHz, demo.Current.Color) : configuration?.Settings;
        if (settings == null) return;
        dpi.Value = settings.Dpi;
        polling.Selected = (uint)Array.IndexOf(Rates, settings.PollingHz);
        SetColor(settings.Color);
    }

    private void SetColor(string hex)
    {
        using var rgba = new Gdk.RGBA();
        if (!rgba.Parse(hex)) throw new ArgumentException("Cor inválida.", nameof(hex));
        color.Rgba = rgba;
    }

    private string SelectedColor()
    {
        using var rgba = color.Rgba;
        return $"#{ToByte(rgba.Red):X2}{ToByte(rgba.Green):X2}{ToByte(rgba.Blue):X2}";
    }

    private static int ToByte(float channel) => (int)Math.Round(Math.Clamp(channel, 0, 1) * 255);

    internal async Task RefreshAsync()
    {
        if (closed || !refresh.Sensitive || operation != null) return;
        SetBusy(true);
        title.Subtitle = "Procurando mouse…";
        try
        {
            snapshot = await new LinuxDiscovery().DiscoverAsync(lifetime.Token);
            if (closed) return;
            if (snapshot.Error != null) throw new IOException(snapshot.Error);
            candidate = snapshot.Devices.FirstOrDefault(d => d.HasConfigurationChannel);
            if (demoMode)
            {
                title.Subtitle = "Demonstração";
                status.SetText("Valores de demonstração; nenhum comando de configuração será enviado.");
                return;
            }
            configuration = null;
            if (candidate == null) throw new IOException("Mouse não encontrado. Conecte o receptor ou o cabo USB e atualize.");
            if (!candidate.CanReadWrite) throw new IOException("Acesso ao mouse bloqueado. Instale a regra de acesso e reconecte o receptor.");
            title.Subtitle = "Lendo configurações…";
            configuration = await hardware.ReadAsync(candidate, lifetime.Token);
            if (closed) return;
            RestoreControls();
            title.Subtitle = "Conectado";
            notice.SetText($"Ajustes do estágio de DPI ativo ({configuration.ActiveStage + 1}).");
            apply.SetLabel("Aplicar");
            status.SetText("Configurações lidas do mouse. Altere os valores e clique em Aplicar.");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            configuration = null;
            if (!closed)
            {
                title.Subtitle = "Configurações indisponíveis";
                notice.SetText("Não foi possível carregar as configurações reais.");
                status.SetText(ex.Message);
                dpi.Text_ = "";
                polling.Selected = uint.MaxValue;
            }
        }
        finally { if (!closed) SetBusy(false); }
    }

    internal async Task ApplyDemoAsync()
    {
        if (closed || operation != null || (!demoMode && (configuration == null || candidate == null))) return;
        operation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        SetBusy(true);
        status.SetText(demoMode ? "Atualizando demonstração…" : "Aplicando e verificando no mouse…");
        try
        {
            dpi.Update();
            if (demoMode)
            {
                var value = await demo.ApplyAsync((int)dpi.Value, Rates[polling.Selected], SelectedColor(), operation.Token);
                if (!closed) status.SetText($"Demonstração: {value.Dpi} DPI · {value.PollingHz} Hz · {value.Color}.");
            }
            else
            {
                var requested = new MouseSettings((int)dpi.Value, Rates[polling.Selected], SelectedColor());
                configuration = await hardware.ApplyAsync(candidate!, configuration!, requested, operation.Token);
                if (!closed)
                {
                    RestoreControls();
                    status.SetText($"Gravado e relido: {configuration.Settings.Dpi} DPI · {configuration.Settings.PollingHz} Hz · {configuration.Settings.Color}.");
                }
            }
        }
        catch (OperationCanceledException)
        { if (!closed) status.SetText("Cancelado antes da gravação. Recarregue para conferir os valores do mouse."); }
        catch (Exception ex)
        {
            if (!demoMode) configuration = null;
            if (!closed) status.SetText($"Não foi possível confirmar: {ex.Message}");
        }
        finally
        {
            operation.Dispose();
            operation = null;
            if (!closed) SetBusy(false);
            if (closeRequested) { Shutdown(); Window.Close(); }
        }
    }

    private void SetBusy(bool busy)
    {
        var loaded = demoMode || configuration != null;
        var editable = !busy && loaded;
        dpi.Visible = polling.Visible = color.Visible = loaded;
        dpiUnavailable.Visible = rateUnavailable.Visible = colorUnavailable.Visible = !loaded;
        apply.Sensitive = undo.Sensitive = dpi.Sensitive = polling.Sensitive = color.Sensitive = editable;
        refresh.Sensitive = !busy;
        cancel.Visible = busy && operation != null && demoMode;
        if (busy) spinner.Start(); else spinner.Stop();
    }

    internal async Task SmokeAsync()
    {
        while (!refresh.Sensitive) await Task.Delay(25);
        dpi.Value = 2400;
        polling.Selected = 2;
        SetColor("#33AAFF");
        await ApplyDemoAsync();
        if (demo.Current.Dpi != 2400 || demo.Current.PollingHz != 500 || demo.Current.Color != "#33AAFF")
            throw new InvalidOperationException("Prévia incorreta.");
        dpi.Value = 3200;
        SetColor("#FF0000");
        var running = ApplyDemoAsync();
        operation?.Cancel();
        await running;
        if (demo.Current.Dpi != 2400 || demo.Current.Color != "#33AAFF" || !apply.Sensitive || cancel.Visible)
            throw new InvalidOperationException("Cancelamento inconsistente.");
        RestoreControls();
        if (dpi.Value != 2400 || SelectedColor() != "#33AAFF")
            throw new InvalidOperationException("Restauração inconsistente.");
        status.SetText("Demonstração: 2400 DPI · 500 Hz · #33AAFF.");
        await Task.Delay(400);
        if (Environment.GetEnvironmentVariable("AJAZZ_POC_SCREENSHOT") is { Length: > 0 } path)
        {
            WindowSnapshot.Save(Window, path);
            await File.WriteAllTextAsync(path + ".diagnostic.json", DiagnosticReport.Serialize(snapshot, demo.Current));
        }
        Console.WriteLine($"Discovery: {snapshot?.Devices.Count} interfaces; backend error: {snapshot?.Error ?? "none"}");
    }

    internal async Task<bool> HardwareSmokeAsync()
    {
        while (!refresh.Sensitive) await Task.Delay(25);
        await Task.Delay(400);
        if (Environment.GetEnvironmentVariable("AJAZZ_POC_SCREENSHOT") is { Length: > 0 } path)
            WindowSnapshot.Save(Window, path);
        Console.WriteLine(status.GetText());
        return configuration != null;
    }

    internal void Shutdown()
    {
        if (closed) return;
        closed = true;
        lifetime.Cancel();
    }

    private static void AddControl(Adw.ActionRow row, Gtk.Widget control)
    {
        control.Valign = Gtk.Align.Center;
        row.AddSuffix(control);
        row.ActivatableWidget = control;
    }
    private static Adw.ActionRow Row(string title, string subtitle)
    {
        var row = Adw.ActionRow.New();
        row.Title = title;
        row.Subtitle = subtitle;
        row.UseMarkup = false;
        return row;
    }
    private static Gtk.Label Label(string text)
    {
        var label = Gtk.Label.New(text);
        label.Xalign = 0;
        return label;
    }
}
