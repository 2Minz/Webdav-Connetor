using System.Diagnostics;
using System.Net.NetworkInformation;
using Microsoft.Win32;

namespace UnionDrive.App;

internal sealed class MainForm : Form
{
    private readonly ConfigurationManager configuration;
    private readonly Logger logger;
    private readonly List<DriveSession> sessions = [];
    private readonly ListView drives = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false, HideSelection = false };
    private readonly Label details = new() { Dock = DockStyle.Fill };
    private readonly Button add = new() { Text = "드라이브 추가", AutoSize = true };
    private readonly Button edit = new() { Text = "설정", AutoSize = true };
    private readonly Button remove = new() { Text = "삭제", AutoSize = true };
    private readonly Button connect = new() { Text = "연결", AutoSize = true };
    private readonly Button disconnect = new() { Text = "연결 해제", AutoSize = true };
    private readonly Button open = new() { Text = "드라이브 열기", AutoSize = true };
    private readonly CheckBox startup = new() { Text = "Windows 로그인 시 자동 시작", AutoSize = true };
    private readonly NotifyIcon tray;
    private readonly ContextMenuStrip menu;
    private readonly bool startupMode;
    private bool allowClose, exiting, configurationLoaded;
    private readonly Font headingFont = new("Segoe UI", 20, FontStyle.Bold);

    public MainForm(bool startupMode = false, ConfigurationManager? configuration = null)
    {
        this.startupMode = startupMode; this.configuration = configuration ?? new(); logger = new(this.configuration.DataRoot);
        Text = "Webdav Connetor"; ClientSize = new Size(780, 450); FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false; Icon = AppIcon.Load();
        StartPosition = FormStartPosition.CenterScreen; Font = SystemFonts.MessageBoxFont;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 1, RowCount = 6 };
        foreach (var height in new[] { 46, 0, 48, 58, 32, 30 })
            layout.RowStyles.Add(new RowStyle(height == 0 ? SizeType.Percent : SizeType.Absolute, height == 0 ? 100 : height));
        layout.Controls.Add(new Label { Text = "MOUNTED DRIVE LIST", Font = headingFont, AutoSize = true }, 0, 0);
        drives.Columns.Add("드라이브", 90); drives.Columns.Add("이름", 145); drives.Columns.Add("서버", 300); drives.Columns.Add("상태", 150);
        layout.Controls.Add(drives, 0, 1);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill };
        buttons.Controls.AddRange([add, edit, remove, connect, disconnect, open]); layout.Controls.Add(buttons, 0, 2);
        layout.Controls.Add(details, 0, 3); layout.Controls.Add(startup, 0, 4);
        var developer = new Label { Text = "Developer - D.M.Kang", AutoSize = true };
        layout.Controls.Add(developer, 0, 5); Controls.Add(layout);
        menu = new ContextMenuStrip(); menu.Items.Add("열기", null, (_, _) => Restore());
        menu.Items.Add("선택 드라이브 연결", null, async (_, _) => await ConnectSelectedAsync());
        menu.Items.Add("선택 드라이브 연결 해제", null, async (_, _) => await DisconnectSelectedAsync());
        menu.Items.Add("오픈소스 라이선스", null, (_, _) => ShowLicenses());
        menu.Items.Add("종료", null, async (_, _) => await RequestExitAsync());
        tray = new NotifyIcon { Text = "Webdav Connetor", Icon = Icon, ContextMenuStrip = menu, Visible = true };
        tray.DoubleClick += (_, _) => Restore(); drives.SelectedIndexChanged += (_, _) => UpdateControls();
        add.Click += (_, _) => EditProfile(null); edit.Click += (_, _) => { if (Selected is { } session) EditProfile(session); };
        remove.Click += (_, _) => DeleteSelected();
        connect.Click += async (_, _) => await ConnectSelectedAsync(); disconnect.Click += async (_, _) => await DisconnectSelectedAsync();
        open.Click += (_, _) => OpenSelected(); startup.Checked = StartupManager.Enabled;
        startup.CheckedChanged += (_, _) =>
        { try { StartupManager.Set(startup.Checked); } catch { details.Text = "자동 시작 설정을 저장하지 못했습니다."; } };
        Shown += OnShown; FormClosing += OnClosing;
        NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailability; SystemEvents.PowerModeChanged += OnPowerMode;
        UpdateControls();
    }
    private DriveSession? Selected => drives.SelectedItems.Count == 0 ? null : drives.SelectedItems[0].Tag as DriveSession;

    private async void OnShown(object? sender, EventArgs e)
    {
        try
        {
            if (EnvironmentDetector.FindRclone() is { } executable) await MountLease.RecoverAsync(configuration.DataRoot, executable);
            ExplorerDriveAppearance.Restore();
            foreach (var profile in configuration.Load()) AddSession(profile);
            configurationLoaded = true;
            details.Text = sessions.Count == 0 ? "드라이브 추가에서 NAS 주소와 계정을 입력하세요." : "드라이브를 선택해 개별 연결·연결 해제·설정을 관리하세요.";
            logger.Write("ApplicationStarted"); UpdateControls(); if (startupMode) Hide();
            await Task.WhenAll(sessions.Where(s => s.Profile.AutoConnect).Select(s => s.ConnectAsync()));
        }
        catch (Exception ex) { details.Text = "시작 준비 실패: " + MountManager.SafeMessage(ex) + " 설정 파일과 캐시는 보존되었습니다."; }
    }
    private void AddSession(ConnectionProfile profile)
    {
        var session = new DriveSession(profile, configuration.DataRoot, logger); session.Changed += UpdateControls; sessions.Add(session);
        var item = new ListViewItem(profile.DriveLetter) { Tag = session };
        item.SubItems.Add(profile.Name); item.SubItems.Add(profile.ServerUrl); item.SubItems.Add("연결 끊김"); drives.Items.Add(item); item.Selected = true;
    }
    private void UpdateControls()
    {
        if (IsDisposed) return;
        foreach (ListViewItem item in drives.Items)
        {
            var session = (DriveSession)item.Tag!;
            item.SubItems[3].Text = session.Busy ? "작업 중" : session.Failed ? "오류" : session.Mount.State switch
            { MountState.Connected => "연결됨", MountState.Error => "오류", MountState.Connecting => "연결 중", _ => "연결 끊김" };
        }
        var selected = Selected; var idle = selected is { Busy: false } && !exiting;
        add.Enabled = configurationLoaded && !exiting;
        edit.Enabled = remove.Enabled = idle && !selected!.Mount.HasProcess && configurationLoaded;
        connect.Enabled = idle && !selected!.Mount.HasProcess; disconnect.Enabled = idle && selected!.Mount.HasProcess;
        open.Enabled = idle && selected!.Mount.HasProcess && selected.Mount.State == MountState.Connected;
        if (selected is not null) details.Text = selected.Message;
        tray.Text = $"Webdav Connetor · {sessions.Count(s => s.Mount.State == MountState.Connected)}개 연결";
    }
    private void EditProfile(DriveSession? existing)
    {
        if (!configurationLoaded || exiting || existing is { Busy: true } || existing?.Mount.HasProcess == true) return;
        try
        {
            var free = Enumerable.Range('D', 23).Reverse().Select(letter => $"{(char)letter}:").FirstOrDefault(letter => sessions.All(s => s.Profile.DriveLetter != letter));
            if (existing is null && free is null) throw new InvalidOperationException("사용 가능한 드라이브 문자가 없습니다.");
            var profile = existing?.Profile ?? new ConnectionProfile { DriveLetter = free! };
            CacheGuard.AssertClean(Path.Combine(configuration.DataRoot, "Cache", profile.Id.ToString("N")));
            var previousPassword = CredentialManager.Read(profile.Id);
            using var dialog = new ProfileForm(profile, previousPassword is not null);
            if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Profile is not { } changed) return;
            var profiles = sessions.Where(s => s != existing).Select(s => s.Profile).Append(changed).ToList(); ConfigurationManager.Validate(profiles);
            if (existing is not null && (profile.ServerUrl != changed.ServerUrl || profile.Username != changed.Username))
            {
                // Clean read-cache from an old account/server must never be reused for a new endpoint.
                foreach (var _ in CacheGuard.Files(configuration.DataRoot)) { }
                var cachePath = Path.Combine(configuration.DataRoot, "Cache", profile.Id.ToString("N"));
                if (Directory.Exists(cachePath)) Directory.Delete(cachePath, true);
            }
            var changedPassword = dialog.NewPassword.Length > 0;
            if (changedPassword) CredentialManager.Save(changed.Id, changed.Username, dialog.NewPassword);
            try { configuration.Save(profiles); }
            catch
            {
                if (changedPassword)
                {
                    if (previousPassword is null) CredentialManager.Delete(changed.Id);
                    else CredentialManager.Save(profile.Id, profile.Username, previousPassword);
                }
                throw;
            }
            if (existing is not null) RemoveSession(existing);
            AddSession(changed); UpdateControls(); logger.Write("ProfileSaved", changed.Id);
        }
        catch (Exception ex) { ShowError(ex); }
    }
    private void DeleteSelected()
    {
        if (Selected is not { } session || session.Busy || session.Mount.HasProcess) return;
        if (MessageBox.Show(this, "이 드라이브 설정과 저장된 비밀번호를 삭제할까요?", "드라이브 삭제", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        try
        {
            CacheGuard.AssertClean(Path.Combine(configuration.DataRoot, "Cache", session.Profile.Id.ToString("N")));
            configuration.Save(sessions.Where(s => s != session).Select(s => s.Profile).ToList()); CredentialManager.Delete(session.Profile.Id);
            RemoveSession(session); UpdateControls();
        }
        catch (Exception ex) { ShowError(ex); }
    }
    private void RemoveSession(DriveSession session)
    {
        foreach (var item in drives.Items.Cast<ListViewItem>().Where(item => item.Tag == session).ToArray()) drives.Items.Remove(item);
        sessions.Remove(session); session.Changed -= UpdateControls; session.Dispose();
    }
    private async Task ConnectSelectedAsync() { if (!exiting && Selected is { } session) await session.ConnectAsync(); }
    private async Task DisconnectSelectedAsync()
    { if (Selected is not { } session) return; try { await session.DisconnectAsync(); } catch (Exception ex) { ShowError(ex); } }
    private void OpenSelected()
    {
        if (Selected is not { } session || !session.Mount.HasProcess) return;
        try
        { var info = new ProcessStartInfo("explorer.exe") { UseShellExecute = true }; info.ArgumentList.Add(session.Profile.DriveLetter + "\\"); Process.Start(info); }
        catch { details.Text = "Windows 탐색기를 열 수 없습니다."; }
    }
    private void Restore() { Show(); WindowState = FormWindowState.Normal; Activate(); }
    private void ShowError(Exception ex) => details.Text = MountManager.SafeMessage(ex);
    private void ShowLicenses() => MessageBox.Show(this,
        "Webdav Connetor: MIT License\n\nrclone: MIT License · https://rclone.org\n\nWinFsp - Windows File System Proxy, Copyright (C) Bill Zissimopoulos\nhttps://github.com/winfsp/winfsp\nGPLv3 with FLOSS exception\n\n라이선스 전문은 설치 폴더의 licenses 디렉터리에 있습니다.", "오픈소스 라이선스");
    private void OnNetworkAvailability(object? sender, NetworkAvailabilityEventArgs e) { if (e.IsAvailable) NotifyNetworkRestored(); }
    private void OnPowerMode(object sender, PowerModeChangedEventArgs e) { if (e.Mode == PowerModes.Resume) NotifyNetworkRestored(); }
    private void NotifyNetworkRestored()
    {
        if (!IsHandleCreated || IsDisposed) return;
        try { BeginInvoke(() => { foreach (var session in sessions) session.NetworkRestored(); }); } catch (InvalidOperationException) { }
    }
    public async Task RequestExitAsync()
    {
        if (exiting) return; foreach (var session in sessions) session.StopRetrying();
        if (sessions.Any(s => s.Busy)) { Restore(); details.Text = "드라이브 작업이 끝난 후 종료를 다시 선택하세요."; return; }
        exiting = true; UpdateControls();
        try
        {
            await Task.WhenAll(sessions.Select(s => s.DisconnectAsync()));
            if (sessions.Any(s => s.Mount.HasProcess)) throw new InvalidOperationException("전송 중인 파일이 있어 종료를 보류했습니다.");
            allowClose = true; logger.Write("ApplicationStopped"); Close();
        }
        catch (Exception ex) { exiting = false; Restore(); ShowError(ex); UpdateControls(); }
    }
    private void OnClosing(object? sender, FormClosingEventArgs e)
    {
        if (allowClose) return;
        if (e.CloseReason is CloseReason.WindowsShutDown or CloseReason.TaskManagerClosing)
        {
            var shutdown = Stopwatch.StartNew();
            foreach (var session in sessions)
            {
                var remaining = TimeSpan.FromSeconds(3) - shutdown.Elapsed;
                if (remaining <= TimeSpan.Zero) break;
                session.Mount.RequestSystemShutdown(remaining);
            }
            return;
        }
        e.Cancel = true; Hide();
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailability; SystemEvents.PowerModeChanged -= OnPowerMode;
            foreach (var session in sessions) session.Dispose(); sessions.Clear(); tray.Visible = false; tray.Dispose(); menu.Dispose();
        }
        var appIcon = Icon;
        base.Dispose(disposing);
        if (disposing) appIcon?.Dispose();
        if (disposing) headingFont.Dispose();
    }
}
