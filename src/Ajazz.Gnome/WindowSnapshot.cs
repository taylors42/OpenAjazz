namespace Ajazz.Gnome;

internal static class WindowSnapshot
{
    // Snapshot only this application's widget tree; never captures the user's desktop.
    internal static void Save(Gtk.Window window, string path)
    {
        using var paintable = Gtk.WidgetPaintable.New(window);
        using var snapshot = Gtk.Snapshot.New();
        paintable.Snapshot(snapshot, window.GetWidth(), window.GetHeight());
        var node = snapshot.ToNode() ?? throw new InvalidOperationException("Janela ainda não renderizada.");
        var renderer = window.GetNative()?.GetRenderer() ?? throw new InvalidOperationException("Renderer indisponível.");
        using var texture = renderer.RenderTexture(node, null);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        if (!texture.SaveToPng(path)) throw new IOException("Falha ao salvar snapshot da janela.");
    }
}
