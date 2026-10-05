namespace UnionDrive.App;

internal sealed class AdvancedSettingsForm : Form
{
    private readonly NumericUpDown cache = new() { Minimum = 1, Maximum = 1024 };
    private readonly NumericUpDown age = new() { Minimum = 1, Maximum = 720 };
    private readonly NumericUpDown directory = new() { Minimum = 5, Maximum = 3600 };
    public int CacheSize => (int)cache.Value;
    public int CacheAge => (int)age.Value;
    public int DirectoryTime => (int)directory.Value;
    public AdvancedSettingsForm(int size, int hours, int seconds)
    {
        Text = "세부 설정"; ClientSize = new Size(360, 190);
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent; Font = SystemFonts.MessageBoxFont; Icon = AppIcon.Load();
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 2, RowCount = 4 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        var labels = new[] { "캐시 크기 (GB)", "캐시 보관 (시간)", "폴더 갱신 (초)" };
        var fields = new[] { cache, age, directory };
        for (var i = 0; i < fields.Length; i++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            layout.Controls.Add(new Label { Text = labels[i], AutoSize = true, Anchor = AnchorStyles.Left }, 0, i);
            fields[i].Dock = DockStyle.Fill; layout.Controls.Add(fields[i], 1, i);
        }
        cache.Value = size; age.Value = hours; directory.Value = seconds;
        var ok = new Button { Text = "확인", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "취소", DialogResult = DialogResult.Cancel, AutoSize = true };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        buttons.Controls.AddRange([cancel, ok]); layout.Controls.Add(buttons, 0, 3); layout.SetColumnSpan(buttons, 2);
        Controls.Add(layout); AcceptButton = ok; CancelButton = cancel;
    }
    protected override void Dispose(bool disposing)
    { var icon = Icon; base.Dispose(disposing); if (disposing) icon?.Dispose(); }
}
