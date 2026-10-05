namespace UnionDrive.App;

internal sealed class ProfileForm : Form
{
    private readonly ConnectionProfile original;
    private readonly TextBox name = new(), url = new(), folder = new(), user = new(), password = new(), volume = new();
    private readonly NumericUpDown port = new() { Minimum = 1, Maximum = 65535, Value = 5006 };
    private readonly ComboBox drive = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox auto = new() { Text = "프로그램 시작 시 이 드라이브 자동 연결", AutoSize = true };
    private readonly NumericUpDown cache = new() { Minimum = 1, Maximum = 1024 };
    private readonly NumericUpDown age = new() { Minimum = 1, Maximum = 720 };
    private readonly NumericUpDown directory = new() { Minimum = 5, Maximum = 3600 };
    private readonly bool hasCredential;
    public ConnectionProfile? Profile { get; private set; }
    public string NewPassword => password.Text;

    public ProfileForm(ConnectionProfile profile, bool hasCredential)
    {
        original = profile; this.hasCredential = hasCredential;
        Text = "드라이브 설정"; ClientSize = new Size(600, 410);
        FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;
        Icon = AppIcon.Load();
        StartPosition = FormStartPosition.CenterParent; Font = SystemFonts.MessageBoxFont;
        password.UseSystemPasswordChar = true;
        password.PlaceholderText = hasCredential ? "변경할 때만 입력 (비우면 기존 비밀번호 유지)" : "NAS 비밀번호";
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 2, RowCount = 10 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        void Row(string label, Control control)
        {
            var row = layout.RowStyles.Count; layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
            control.Dock = DockStyle.Fill; layout.Controls.Add(control, 1, row);
        }
        var address = new TableLayoutPanel { ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };
        address.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        address.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40));
        address.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        url.Dock = port.Dock = DockStyle.Fill;
        url.PlaceholderText = "uniontech.synology.me";
        folder.PlaceholderText = "비우면 서버 루트에 연결";
        address.Controls.Add(url, 0, 0);
        address.Controls.Add(new Label { Text = "포트", AutoSize = true, Anchor = AnchorStyles.Left }, 1, 0);
        address.Controls.Add(port, 2, 0);
        Row("이름", name); Row("HTTPS 서버 주소", address); Row("공유 폴더", folder);
        Row("사용자 이름", user); Row("비밀번호", password);
        Row("드라이브 문자", drive); Row("볼륨 이름", volume); Row("자동 연결", auto);
        var advanced = new Button { Text = "세부 설정", AutoSize = true };
        advanced.Click += (_, _) =>
        {
            using var dialog = new AdvancedSettingsForm((int)cache.Value, (int)age.Value, (int)directory.Value);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            cache.Value = dialog.CacheSize; age.Value = dialog.CacheAge; directory.Value = dialog.DirectoryTime;
        };
        Row("", advanced);
        var save = new Button { Text = "저장", AutoSize = true };
        var cancel = new Button { Text = "취소", AutoSize = true, DialogResult = DialogResult.Cancel };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
        buttons.Controls.AddRange([cancel, save]); layout.Controls.Add(buttons, 0, 9); layout.SetColumnSpan(buttons, 2);
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); Controls.Add(layout);
        name.Text = profile.Name; user.Text = profile.Username; volume.Text = profile.VolumeName;
        if (Uri.TryCreate(profile.ServerUrl, UriKind.Absolute, out var endpoint))
        { url.Text = endpoint.Host; port.Value = endpoint.Port; folder.Text = Uri.UnescapeDataString(endpoint.AbsolutePath).Trim('/'); }
        drive.Items.AddRange(Enumerable.Range('D', 23).Select(letter => (object)$"{(char)letter}:").ToArray()); drive.SelectedItem = profile.DriveLetter;
        auto.Checked = profile.AutoConnect; cache.Value = profile.CacheSizeGiB; age.Value = profile.CacheAgeHours; directory.Value = profile.DirectoryCacheSeconds;
        AcceptButton = save; CancelButton = cancel;
        save.Click += (_, _) =>
        {
            try
            {
                var result = original with
                {
                    Name = name.Text.Trim(), ServerUrl = BuildServerUrl(), Username = user.Text.Trim(), DriveLetter = drive.SelectedItem?.ToString() ?? "",
                    VolumeName = volume.Text.Trim(), AutoConnect = auto.Checked, CacheSizeGiB = (int)cache.Value,
                    CacheAgeHours = (int)age.Value, DirectoryCacheSeconds = (int)directory.Value
                };
                result.Validate();
                if (!hasCredential && password.Text.Length == 0) throw new InvalidOperationException("비밀번호를 입력하세요.");
                Profile = result; DialogResult = DialogResult.OK;
            }
            catch (Exception ex) { MessageBox.Show(this, MountManager.SafeMessage(ex), "설정 확인", MessageBoxButtons.OK, MessageBoxIcon.Information); }
        };
    }
    private string BuildServerUrl()
    {
        var host = url.Text.Trim();
        if (Uri.CheckHostName(host) == UriHostNameType.Unknown)
            throw new InvalidOperationException("서버 주소에는 HTTPS 접두사·포트·폴더 없이 주소만 입력하세요.");
        var path = folder.Text.Trim().Trim('/').Replace('\\', '/');
        if (path.Split('/').Any(part => part is "." or ".."))
            throw new InvalidOperationException("공유 폴더에 상대 경로는 사용할 수 없습니다.");
        var builder = new UriBuilder("https", host, (int)port.Value)
        { Path = "/" + string.Join("/", path.Split('/').Select(Uri.EscapeDataString)) };
        return builder.Uri.AbsoluteUri;
    }
    protected override void Dispose(bool disposing)
    { var icon = Icon; if (disposing) { password.Clear(); cache.Dispose(); age.Dispose(); directory.Dispose(); } base.Dispose(disposing); if (disposing) icon?.Dispose(); }
}
