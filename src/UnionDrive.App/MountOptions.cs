namespace UnionDrive.App;

internal sealed record MountOptions
{
    public string Remote { get; init; } = "Union:";
    public string Drive { get; init; } = "Z:";
    public string Volume { get; init; } = "UNION";
    public string DirectoryCacheTime { get; init; } = "30s";
    public string CacheSize { get; init; } = "10G";
    public string CacheAge { get; init; } = "24h";
    public bool ManagedProfile { get; init; }
    public string? CacheDirectory { get; init; }
    public string? DataRoot { get; init; }

    public IEnumerable<string> Arguments()
    {
        var arguments = new List<string>
        { "mount", Remote, Drive, "--vfs-cache-mode", "full", "--network-mode",
          "--volname", Volume, "--dir-cache-time", DirectoryCacheTime,
          "--vfs-cache-max-size", CacheSize, "--vfs-cache-max-age", CacheAge,
          "--buffer-size", "4M" };
        if (ManagedProfile) arguments.AddRange(["--config", "NUL"]);
        if (CacheDirectory is not null) arguments.AddRange(["--cache-dir", CacheDirectory]);
        return arguments;
    }
}
