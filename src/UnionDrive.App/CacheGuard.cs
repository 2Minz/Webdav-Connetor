using System.Text.Json;

namespace UnionDrive.App;

internal static class CacheGuard
{
    public static IEnumerable<string> Files(string root)
    {
        if (!Directory.Exists(root)) yield break;
        if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("데이터 폴더에 연결된 경로가 있어 안전한 정리를 중지했습니다.");
        foreach (var entry in Directory.EnumerateFileSystemEntries(root))
        {
            var attributes = File.GetAttributes(entry);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("데이터 폴더에 연결된 경로가 있어 안전한 정리를 중지했습니다.");
            if ((attributes & FileAttributes.Directory) != 0)
            { foreach (var file in Files(entry)) yield return file; }
            else yield return entry;
        }
    }
    public static void AssertClean(string cacheRoot)
    {
        foreach (var file in Files(cacheRoot))
        {
            var relative = Path.GetRelativePath(cacheRoot, file);
            if (!relative.Split(Path.DirectorySeparatorChar).Contains("vfsMeta", StringComparer.OrdinalIgnoreCase)) continue;
            try
            {
                using var metadata = JsonDocument.Parse(File.ReadAllText(file));
                if (!metadata.RootElement.TryGetProperty("Dirty", out var dirty) || dirty.ValueKind != JsonValueKind.False)
                    throw new InvalidOperationException("NAS로 전송되지 않은 캐시가 있습니다. 다시 연결해 전송을 완료한 후 설정 변경·삭제를 시도하세요.");
            }
            catch (JsonException) { throw new InvalidOperationException("캐시 상태를 확인할 수 없어 삭제를 중지했습니다."); }
        }
    }
}
