using DTScan.Controls;
using DTScan.Forms;
using DTScan.Models;
using DTScan.Services;

namespace DTScan;

public partial class Form1
{
    // ═══════════════════════════════════════════════
    //  PROFILE
    // ═══════════════════════════════════════════════

    private void BtnSettings_Click(object? sender, EventArgs e)
    {
        using var editor = new ProfileEditorForm(_profileManager, _scannerService);
        if (editor.ShowDialog(this) == DialogResult.OK)
        {
            RefreshProfileCombo();
            UpdateUI();
            ApplyTheme();
        }
    }

    private void CmbProfile_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (cmbProfile.SelectedItem is UserProfile p)
        {
            _profileManager.ActiveProfile = p;
            _profileManager.Save();
            _nextBatchStart = 0;
            UpdateUI();
            ApplyTheme();
        }
    }

    private void RefreshProfileCombo()
    {
        cmbProfile.SelectedIndexChanged -= CmbProfile_SelectedIndexChanged;
        cmbProfile.Items.Clear();
        cmbProfile.DisplayMember = "Name";
        foreach (var p in _profileManager.Profiles)
            cmbProfile.Items.Add(p);
        cmbProfile.SelectedItem = _profileManager.ActiveProfile;
        cmbProfile.SelectedIndexChanged += CmbProfile_SelectedIndexChanged;
    }

    // ═══════════════════════════════════════════════
    //  THEME
    // ═══════════════════════════════════════════════

    private void ApplyTheme()
    {
        var theme = ThemeColors.Get(_profileManager.ActiveProfile.ThemeColor);

        // Scan button (accent)
        btnScan.BackColor = theme.Accent;
        btnScan.FlatAppearance.MouseOverBackColor = theme.AccentHover;
        btnScan.FlatAppearance.MouseDownBackColor = theme.AccentDown;

        // Right sidebar accent border
        pnlRightBorder.BackColor = theme.Accent;

        // Profile header
        pnlProfileHeader.BackColor = theme.AccentDark;
        lblCurrentUser.ForeColor = theme.AccentLight;

        // Settings button
        btnSettings.BackColor = theme.AccentDark;
        btnSettings.ForeColor = theme.AccentLight;
        btnSettings.FlatAppearance.MouseOverBackColor = theme.AccentDarkHover;
        btnSettings.FlatAppearance.MouseDownBackColor = theme.AccentDarkDown;
    }

    // ═══════════════════════════════════════════════
    //  UI UPDATE
    // ═══════════════════════════════════════════════

    private void UpdateUI()
    {
        var thumbnails = GetThumbnails();
        int total = thumbnails.Count;
        int selected = thumbnails.Count(t => t.IsSelected);
        int labeled = thumbnails.Count(t => !string.IsNullOrWhiteSpace(t.DocumentLabel));
        var profile = _profileManager.ActiveProfile;

        pnlEmptyState.Visible = total == 0;
        flpPages.Visible = total > 0;

        tslPageCount.Text = $"Pages: {total}";
        tslSelected.Text = $"Selected: {selected}";
        tslBatch.Text = $"Batch: {profile.PagesPerBatch} | Labeled: {labeled}";
        tslUser.Text = $"User: {profile.Name}";

        btnAutoSelect.Text = $"⚡   Auto Select ({profile.PagesPerBatch}){ShortcutSuffix("AutoSelect")}";

        btnScan.Text = (profile.ScanSource switch
        {
            ScanSource.Feeder => "📷   Scan (Feeder)",
            ScanSource.Duplex => "📷   Scan (Duplex)",
            _ => "📷   Scan"
        }) + ShortcutSuffix("Scan");

        btnImport.Text = $"📂   Import{ShortcutSuffix("Import")}";
        btnSave.Text = $"💾   Save{ShortcutSuffix("Save")}";
        btnSelectAll.Text = $"☑   Select All{ShortcutSuffix("SelectAll")}";
        btnDeselectAll.Text = $"☐   Deselect All{ShortcutSuffix("DeselectAll")}";
        btnRotateLeft.Text = $"↶   Rotate Left{ShortcutSuffix("RotateLeft")}";
        btnRotateRight.Text = $"↷   Rotate Right{ShortcutSuffix("RotateRight")}";
        btnDelete.Text = $"🗑   Delete{ShortcutSuffix("Delete")}";
        btnQuickLabel.Text = $"📝   Quick Label{ShortcutSuffix("QuickLabel")}";
        btnSettings.Text = $"⚙   Settings{ShortcutSuffix("Settings")}";

        // ── Profile info panel ──
        lblProfileFormat.Text = $"📁  {profile.DefaultFormat} · {profile.ScanDpi} DPI · {profile.ColorMode}";

        string source = profile.ScanSource switch
        {
            ScanSource.Feeder => "Feeder",
            ScanSource.Duplex => "Duplex",
            _ => "Flatbed"
        };
        lblProfileBatch.Text = $"📄  Batch: {profile.PagesPerBatch} · {source} · {profile.ScanDriver}";

        var subFolder = PlaceholderResolver.Resolve(profile.SubFolderPattern, profile);
        lblProfilePath.Text = $"📂  {Path.Combine(profile.SavePath, subFolder)}";

        var flags = new List<string>();
        if (profile.AutoSave) flags.Add("AutoSave");
        if (profile.DeleteAfterSave) flags.Add("AutoDelete");
        if (profile.AutoSelectNextBatch) flags.Add("AutoNext");
        lblProfileFlags.Text = flags.Count > 0 ? $"⚙  {string.Join(" · ", flags)}" : "";
    }

    private void SetStatus(string text, Color color)
    {
        tslStatus.Text = text;
        tslStatus.ForeColor = color;
    }

    // ═══════════════════════════════════════════════
    //  PREVIEW
    // ═══════════════════════════════════════════════

    private void ShowPreview(Image image)
    {
        using var form = new Form
        {
            Text = "DTScan — Preview",
            WindowState = FormWindowState.Maximized,
            BackColor = Color.FromArgb(15, 23, 42),
            KeyPreview = true
        };
        var zp = new ZoomableImagePanel
        {
            Dock = DockStyle.Fill,
            Image = image,
            BackColor = Color.FromArgb(15, 23, 42)
        };
        form.KeyDown += (_, args) => { if (args.KeyCode == Keys.Escape) form.Close(); };
        form.Controls.Add(zp);
        form.ShowDialog(this);
    }
}
