using DTScan.Services;

namespace DTScan;

public partial class Form1 : Form
{
    private readonly ProfileManager _profileManager = new();
    private readonly ScannerService _scannerService = new();
    private readonly SaveService _saveService = new();
    private readonly ScanHistoryService _scanHistoryService = new();
    private readonly PageStore _pageStore = new();
    private readonly List<string?> _pageSessionIds = [];
    private readonly Dictionary<string, int> _sessionPageCounts = new();
    private int _nextBatchStart;
    private int _thumbnailWidth = 200;
    private System.Windows.Forms.Timer? _zoomDebounce;
    private CancellationTokenSource? _zoomCts;

    // Ghi nhớ trạng thái zoom/pan của preview trong Save dialog
    private static float _savePreviewZoom = 1f;
    private static PointF _savePreviewPan = PointF.Empty;

    private enum PackAction { Save, Skip, Cancel }

    public Form1()
    {
        InitializeComponent();
        WireEvents();
        _profileManager.Load();
        RefreshProfileCombo();
        UpdateUI();
        ApplyTheme();
    }

    // ═══════════════════════════════════════════════
    //  KEYBOARD SHORTCUTS
    // ═══════════════════════════════════════════════

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        var shortcuts = _profileManager.ActiveProfile.Shortcuts;

        foreach (var (action, shortcutName) in shortcuts)
        {
            if (string.IsNullOrEmpty(shortcutName) ||
                string.Equals(shortcutName, "None", StringComparison.OrdinalIgnoreCase))
                continue;

            if (Enum.TryParse<Keys>(shortcutName, true, out var key) && keyData == key)
            {
                ExecuteShortcutAction(action);
                return true;
            }
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void ExecuteShortcutAction(string action)
    {
        switch (action)
        {
            case "Scan": btnScan.PerformClick(); break;
            case "Import": btnImport.PerformClick(); break;
            case "Save": btnSave.PerformClick(); break;
            case "SelectAll": SetAllSelection(true); break;
            case "DeselectAll": SetAllSelection(false); break;
            case "AutoSelect": AutoSelectBatch(); break;
            case "RotateLeft": RotateSelected(RotateFlipType.Rotate270FlipNone); break;
            case "RotateRight": RotateSelected(RotateFlipType.Rotate90FlipNone); break;
            case "Delete": BtnDelete_Click(null, EventArgs.Empty); break;
            case "QuickLabel": BtnQuickLabel_Click(null, EventArgs.Empty); break;
            case "Settings": BtnSettings_Click(null, EventArgs.Empty); break;
        }
    }

    private string ShortcutSuffix(string action)
    {
        var shortcuts = _profileManager.ActiveProfile.Shortcuts;
        if (shortcuts.TryGetValue(action, out var key) &&
            !string.IsNullOrEmpty(key) &&
            !string.Equals(key, "None", StringComparison.OrdinalIgnoreCase))
            return $"  [{key}]";
        return "";
    }

    // ═══════════════════════════════════════════════
    //  EVENT WIRING
    // ═══════════════════════════════════════════════

    private void WireEvents()
    {
        btnScan.Click += BtnScan_Click;
        btnImport.Click += BtnImport_Click;
        btnSave.Click += BtnSave_Click;
        btnSelectAll.Click += (_, _) => SetAllSelection(true);
        btnDeselectAll.Click += (_, _) => SetAllSelection(false);
        btnAutoSelect.Click += BtnAutoSelect_Click;
        btnRotateLeft.Click += (_, _) => RotateSelected(RotateFlipType.Rotate270FlipNone);
        btnRotateRight.Click += (_, _) => RotateSelected(RotateFlipType.Rotate90FlipNone);
        btnDelete.Click += BtnDelete_Click;
        btnQuickLabel.Click += BtnQuickLabel_Click;
        btnHistory.Click += BtnHistory_Click;
        btnSettings.Click += BtnSettings_Click;
        cmbProfile.SelectedIndexChanged += CmbProfile_SelectedIndexChanged;
        trkZoom.ValueChanged += (_, _) => ApplyZoom();
        btnZoomIn.Click += (_, _) => { trkZoom.Value = Math.Min(trkZoom.Maximum, trkZoom.Value + 20); };
        btnZoomOut.Click += (_, _) => { trkZoom.Value = Math.Max(trkZoom.Minimum, trkZoom.Value - 20); };

        // Ctrl+Wheel trên vùng thumbnail → zoom
        var wheelFilter = new CtrlWheelZoomFilter(this);
        Application.AddMessageFilter(wheelFilter);
        FormClosed += (_, _) => Application.RemoveMessageFilter(wheelFilter);
        FormClosed += (_, _) => { _zoomCts?.Cancel(); _zoomCts?.Dispose(); _pageStore.Dispose(); };

        // Kéo thả file ảnh vào form
        AllowDrop = true;
        DragEnter += OnFileDragEnter;
        DragDrop += OnFileDragDrop;

        flpPages.AllowDrop = true;
        flpPages.DragEnter += OnFileDragEnter;
        flpPages.DragDrop += OnFileDragDrop;

        pnlEmptyState.AllowDrop = true;
        pnlEmptyState.DragEnter += OnFileDragEnter;
        pnlEmptyState.DragDrop += OnFileDragDrop;
    }

    // ═══════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════

    private sealed class CtrlWheelZoomFilter(Form1 owner) : IMessageFilter
    {
        private const int WM_MOUSEWHEEL = 0x020A;

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg != WM_MOUSEWHEEL) return false;
            if ((Control.ModifierKeys & Keys.Control) == 0) return false;

            var pt = owner.flpPages.PointToClient(Cursor.Position);
            if (!owner.flpPages.ClientRectangle.Contains(pt)) return false;

            int delta = (short)((long)m.WParam >> 16);
            int step = delta > 0 ? 20 : -20;
            owner.trkZoom.Value = Math.Clamp(
                owner.trkZoom.Value + step,
                owner.trkZoom.Minimum,
                owner.trkZoom.Maximum);
            return true;
        }
    }

    private static Task RunOnStaThreadAsync(Action action)
    {
        var tcs = new TaskCompletionSource();
        var thread = new Thread(() =>
        {
            try
            {
                action();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        return tcs.Task;
    }
}
