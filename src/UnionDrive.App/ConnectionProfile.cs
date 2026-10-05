namespace UnionDrive.App;

internal sealed record ConnectionProfile
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "NAS";
    public string ServerUrl { get; init; } = "";
    public string Username { get; init; } = "";
    public string DriveLetter { get; init; } = "Z:";
    public string VolumeName { get; init; } = "UNION";
    public bool AutoConnect { get; init; }
    public int CacheSizeGiB { get; init; } = 10;
    public int CacheAgeHours { get; init; } = 24;
    public int DirectoryCacheSeconds { get; init; } = 30;

    public void Validate()
    {
        if (Id == Guid.Empty) throw new InvalidOperationException("프로필 ID가 올바르지 않습니다.");
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > 80) throw new InvalidOperationException("드라이브 이름은 1~80자로 입력하세요.");
        if (!Uri.TryCreate(ServerUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new InvalidOperationException("서버 URL은 인증정보·쿼리 없이 https:// 주소로 입력하세요. 공유 폴더 경로도 지정할 수 있습니다.");
        if (string.IsNullOrWhiteSpace(Username) || Username.Contains('\n') || Username.Contains('\r'))
            throw new InvalidOperationException("사용자 이름을 입력하세요.");
        if (DriveLetter.Length != 2 || DriveLetter[0] is < 'D' or > 'Z' || DriveLetter[1] != ':')
            throw new InvalidOperationException("드라이브 문자는 D:~Z: 중에서 선택하세요.");
        if (string.IsNullOrWhiteSpace(VolumeName) || VolumeName.Length > 32 || VolumeName.Any(char.IsControl))
            throw new InvalidOperationException("볼륨 이름은 1~32자로 입력하세요.");
        if (CacheSizeGiB is < 1 or > 1024 || CacheAgeHours is < 1 or > 720 || DirectoryCacheSeconds is < 5 or > 3600)
            throw new InvalidOperationException("캐시 크기/보관 시간/폴더 갱신 주기를 확인하세요.");
    }

    public MountOptions MountOptions(string dataRoot) => new()
    {
        Remote = $"ud{Id:N}:", Drive = DriveLetter, Volume = VolumeName,
        CacheSize = $"{CacheSizeGiB}G", CacheAge = $"{CacheAgeHours}h",
        DirectoryCacheTime = $"{DirectoryCacheSeconds}s", ManagedProfile = true,
        CacheDirectory = Path.Combine(dataRoot, "Cache", Id.ToString("N")), DataRoot = dataRoot
    };
}
