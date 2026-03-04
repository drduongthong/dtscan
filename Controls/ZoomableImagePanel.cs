using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace DTScan.Controls;

/// <summary>
/// Panel hiển thị ảnh hỗ trợ zoom (scroll wheel) và pan (kéo chuột).
/// Zoom được animate mượt bằng lerp interpolation (~60fps).
/// Tự intercept WM_MOUSEWHEEL khi cursor nằm trên control, không cần focus.
/// </summary>
public sealed class ZoomableImagePanel : Control, IMessageFilter
{
    private const int WM_MOUSEWHEEL = 0x020A;
    private const float LerpFactor = 0.22f;
    private const float SnapThreshold = 0.002f;
    private const float PanSnapThreshold = 0.5f;

    private Image? _image;
    private float _zoom = 1f;
    private float _targetZoom = 1f;
    private PointF _pan;
    private PointF _targetPan;
    private Point _dragStart;
    private PointF _panAtDragStart;
    private bool _dragging;
    private System.Windows.Forms.Timer? _animTimer;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Image? Image
    {
        get => _image;
        set { _image = value; Invalidate(); }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public float ZoomFactor
    {
        get => _zoom;
        set
        {
            float clamped = Math.Clamp(value, 0.5f, 20f);
            _zoom = clamped;
            _targetZoom = clamped;
            ZoomChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public PointF PanOffset
    {
        get => _pan;
        set
        {
            _pan = value;
            _targetPan = value;
            Invalidate();
        }
    }

    public event EventHandler? ZoomChanged;

    public ZoomableImagePanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint
            | ControlStyles.UserPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw, true);
        BackColor = Color.White;
        Cursor = Cursors.Hand;
    }

    // ── Message filter: intercept wheel khi cursor trên control ──

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Application.AddMessageFilter(this);
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        Application.RemoveMessageFilter(this);
        base.OnHandleDestroyed(e);
    }

    public bool PreFilterMessage(ref Message m)
    {
        if (m.Msg != WM_MOUSEWHEEL || !Visible || !IsHandleCreated)
            return false;

        var pt = PointToClient(Cursor.Position);
        if (!ClientRectangle.Contains(pt))
            return false;

        int delta = (short)((long)m.WParam >> 16);
        PerformZoom(delta, pt);
        return true;
    }

    // ── Rendering ──

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_image == null) return;

        var g = e.Graphics;
        g.InterpolationMode = _zoom > 3f
            ? InterpolationMode.NearestNeighbor
            : InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = _zoom > 3f
            ? PixelOffsetMode.Half
            : PixelOffsetMode.HighQuality;

        float fitScale = Math.Min(
            (float)Width / _image.Width,
            (float)Height / _image.Height);
        float totalScale = fitScale * _zoom;

        float imgW = _image.Width * totalScale;
        float imgH = _image.Height * totalScale;
        float x = (Width - imgW) / 2f + _pan.X;
        float y = (Height - imgH) / 2f + _pan.Y;

        g.DrawImage(_image, x, y, imgW, imgH);
    }

    // ── Animated zoom ──

    private void PerformZoom(int delta, Point cursorClient)
    {
        float factor = delta > 0 ? 1.2f : 1f / 1.2f;
        float newZoom = Math.Clamp(_targetZoom * factor, 0.5f, 20f);

        if (Math.Abs(newZoom - _targetZoom) < 0.001f) return;

        // Zoom toward cursor position
        float cx = cursorClient.X - Width / 2f;
        float cy = cursorClient.Y - Height / 2f;
        float ratio = newZoom / _targetZoom;
        _targetPan = new PointF(
            cx - (cx - _targetPan.X) * ratio,
            cy - (cy - _targetPan.Y) * ratio);

        _targetZoom = newZoom;
        EnsureAnimation();
    }

    private void EnsureAnimation()
    {
        if (_animTimer == null)
        {
            _animTimer = new System.Windows.Forms.Timer { Interval = 16 };
            _animTimer.Tick += AnimationTick;
        }
        _animTimer.Start();
    }

    private void AnimationTick(object? sender, EventArgs e)
    {
        float dz = _targetZoom - _zoom;
        float dx = _targetPan.X - _pan.X;
        float dy = _targetPan.Y - _pan.Y;

        bool zoomDone = Math.Abs(dz) < SnapThreshold;
        bool panDone = Math.Abs(dx) < PanSnapThreshold && Math.Abs(dy) < PanSnapThreshold;

        if (zoomDone && panDone)
        {
            _zoom = _targetZoom;
            _pan = _targetPan;
            _animTimer!.Stop();
        }
        else
        {
            _zoom += dz * LerpFactor;
            _pan = new PointF(
                _pan.X + dx * LerpFactor,
                _pan.Y + dy * LerpFactor);
        }

        ZoomChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    // ── Pan (drag) ──

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _dragging = true;
            _dragStart = e.Location;
            _panAtDragStart = _targetPan;
            Cursor = Cursors.SizeAll;
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_dragging)
        {
            _pan = new PointF(
                _panAtDragStart.X + e.X - _dragStart.X,
                _panAtDragStart.Y + e.Y - _dragStart.Y);
            _targetPan = _pan;
            Invalidate();
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (_dragging)
        {
            _dragging = false;
            Cursor = Cursors.Hand;
        }
        base.OnMouseUp(e);
    }

    // ── Double-click reset ──

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        _targetZoom = 1f;
        _targetPan = PointF.Empty;
        EnsureAnimation();
        base.OnMouseDoubleClick(e);
    }

    // ── Cleanup ──

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _animTimer?.Stop();
            _animTimer?.Dispose();
        }
        base.Dispose(disposing);
    }
}
