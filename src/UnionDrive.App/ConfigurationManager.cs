using System.Text.Json;

namespace UnionDrive.App;

internal sealed class ConfigurationManager
{
    public string DataRoot { get; }
    public string ConfigPath => Path.Combine(DataRoot, "config.json");
    public ConfigurationManager(string? root = null) => DataRoot = root ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UnionDrive");

    public List<ConnectionProfile> Load()
    {
        if (!File.Exists(ConfigPath)) return [];
        var profiles = JsonSerializer.Deserialize<List<ConnectionProfile>>(File.ReadAllText(ConfigPath))
            ?? throw new InvalidOperationException("설정 파일을 읽을 수 없습니다. 원본을 보관한 후 복구하세요.");
        Validate(profiles);
        return profiles;
    }

    public static void Validate(IReadOnlyCollection<ConnectionProfile> profiles)
    {
        if (profiles.Count > 23) throw new InvalidOperationException("드라이브 프로필은 최대 23개입니다.");
        foreach (var profile in profiles) profile.Validate();
        if (profiles.Select(p => p.Id).Distinct().Count() != profiles.Count ||
            profiles.Select(p => p.DriveLetter).Distinct(StringComparer.OrdinalIgnoreCase).Count() != profiles.Count)
            throw new InvalidOperationException("중복된 프로필 ID 또는 드라이브 문자가 있습니다.");
    }

    public void Save(IReadOnlyCollection<ConnectionProfile> profiles)
    {
        Validate(profiles);
        Directory.CreateDirectory(DataRoot);
        var temp = ConfigPath + ".tmp";
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, profiles, new JsonSerializerOptions { WriteIndented = true });
            stream.Flush(flushToDisk: true);
        }
        File.Move(temp, ConfigPath, overwrite: true);
    }
}
