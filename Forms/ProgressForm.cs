namespace DTScan.Forms;

public sealed class ProgressForm : Form
{
    private readonly Label _lblStatus;
    private readonly ProgressBar _progressBar;
    private bool _allowClose;

    public bool IsCancelled { get; private set; }

    public ProgressForm(string title)
    {
        Text = title;
        ClientSize = new Size(420, 150);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = true;
        BackColor = Color.FromArgb(248, 250, 252);
        Font = new Font("Segoe UI", 10f);
        ShowInTaskbar = true;

        _lblStatus = new Label
        {
            Text = "Đang xử lý…",
            Location = new Point(20, 20),
            Size = new Size(380, 40),
            ForeColor = Color.FromArgb(30, 41, 59),
            Font = new Font("Segoe UI", 10f)
        };
        Controls.Add(_lblStatus);

        _progressBar = new ProgressBar
        {
            Location = new Point(20, 65),
            Size = new Size(380, 24),
            Style = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 30
        };
        Controls.Add(_progressBar);

        var btnMinimize = new Button
        {
            Text = "⬇  Ẩn xuống",
            Location = new Point(180, 100),
            Size = new Size(110, 36),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = Color.FromArgb(71, 85, 105)
        };
        btnMinimize.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        btnMinimize.Click += (_, _) => WindowState = FormWindowState.Minimized;
        Controls.Add(btnMinimize);

        var btnCancel = new Button
        {
            Text = "✕  Hủy",
            Location = new Point(300, 100),
            Size = new Size(100, 36),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            ForeColor = Color.FromArgb(239, 68, 68)
        };
        btnCancel.FlatAppearance.BorderColor = Color.FromArgb(252, 165, 165);
        btnCancel.Click += (_, _) =>
        {
            IsCancelled = true;
            btnCancel.Enabled = false;
            _lblStatus.Text = "Đang hủy…";
        };
        Controls.Add(btnCancel);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_allowClose && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            WindowState = FormWindowState.Minimized;
            return;
        }
        base.OnFormClosing(e);
    }

    public void UpdateStatus(string text)
    {
        if (IsDisposed) return;
        if (InvokeRequired)
            Invoke(() => _lblStatus.Text = text);
        else
            _lblStatus.Text = text;
    }

    public void SetProgress(int current, int total)
    {
        if (IsDisposed) return;
        void Apply()
        {
            if (_progressBar.Style != ProgressBarStyle.Continuous)
            {
                _progressBar.Style = ProgressBarStyle.Continuous;
                _progressBar.Maximum = total;
            }
            _progressBar.Value = Math.Min(current, total);
        }

        if (InvokeRequired)
            Invoke(Apply);
        else
            Apply();
    }

    public void ForceClose()
    {
        _allowClose = true;
        if (IsDisposed) return;
        if (InvokeRequired)
            Invoke(Close);
        else
            Close();
    }
}
