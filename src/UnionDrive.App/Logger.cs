namespace UnionDrive.App;

internal sealed class Logger(string root)
{
    private readonly object gate = new();
    public void Write(string eventName, Guid? profileId = null)
    {
        // Callers supply event identifiers only, never URLs, usernames, credentials or raw errors.
        try
        {
            lock (gate)
            {
                var directory = Path.Combine(root, "Logs");
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, "UnionDrive.log");
                if (File.Exists(path) && new FileInfo(path).Length > 512 * 1024)
                {
                    for (var index = 2; index >= 1; index--)
                        if (File.Exists(path + "." + index)) File.Move(path + "." + index, path + "." + (index + 1), true);
                    File.Move(path, path + ".1", true);
                }
                File.AppendAllText(path, $"{DateTimeOffset.Now:O} {eventName} {profileId}\n");
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
