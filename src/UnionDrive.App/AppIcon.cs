namespace UnionDrive.App;

internal static class AppIcon
{
    public static Icon Load()
    {
        using var stream = typeof(AppIcon).Assembly.GetManifestResourceStream("UnionDrive.App.Assets.app.ico")!;
        using var source = new Icon(stream, new Size(32, 32));
        return (Icon)source.Clone();
    }
}
