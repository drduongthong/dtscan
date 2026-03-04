using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace DTScan.Controls;

public sealed class PageThumbnailControl : Control
{
    private static readonly Color SelectedBorder = Color.FromArgb(59, 130, 246);
    private static readonly Color HoverBg = Color.FromArgb(241, 245, 249);
    private static readonly Color NormalBg = Color.White;
    private static readonly Color CheckGreen = Color.FromArgb(16, 185, 129);
    private static readonly Color LabelAccent = Color.FromArgb(99, 102, 241);

    private bool _isSelected;
    private bool _isHovered;
    private readonly TextBox _txtLabel;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Image? PageImage { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int PageNumber { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; Invalidate(); }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string DocumentLabel
    {
        get => _txtLabel.Text;
        set => _txtLabel.Text = value;
    }

    public event EventHandler? SelectionChanged;
    public event EventHandler? LabelChanged;

    public PageThumbnailControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint
            | ControlStyles.UserPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw, true);

        // _txtLabel phải khởi tạo TRƯỚC khi set Size, vì Size trigger OnResize
        _txtLabel = new TextBox
        {
            BorderStyle = BorderStyle.FixedSingle,
            TextAlign = HorizontalAlignment.Center,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = LabelAccent,
            BackColor = Color.FromArgb(248, 250, 252),
            PlaceholderText = "Nhãn / Số...",
            Location = new Point(10, 232 - 30),
            Size = new Size(155 - 20, 22),
            Cursor = Cursors.IBeam
        };
        _txtLabel.GotFocus += (_, _) =>
        {
            _txtLabel.BackColor = Color.White;
            _txtLabel.BorderStyle = BorderStyle.FixedSingle;
        };
        _txtLabel.LostFocus += (_, _) =>
        {
            _txtLabel.BackColor = Color.FromArgb(248, 250, 252);
        };
        _txtLabel.TextChanged += (_, _) => LabelChanged?.Invoke(this, EventArgs.Empty);
        Controls.Add(_txtLabel);

        Size = new Size(200, 300);
        Margin = new Padding(8);
        Cursor = Cursors.Hand;
    }

    public void FocusLabel()
    {
        _txtLabel.Focus();
        _txtLabel.SelectAll();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);

        // Background & border
        using var bgBrush = new SolidBrush(_isHovered ? HoverBg : NormalBg);
        using var borderPen = new Pen(_isSelected ? SelectedBorder : Color.FromArgb(226, 232, 240),
            _isSelected ? 2.5f : 1f);
        using var path = RoundedRect(rect, 8);
        g.FillPath(bgBrush, path);
        g.DrawPath(borderPen, path);

        // Thumbnail area (above the two bottom rows)
        var imgRect = new Rectangle(8, 8, Width - 16, Height - 64);
        if (PageImage != null)
        {
            float scale = Math.Min((float)imgRect.Width / PageImage.Width,
                                   (float)imgRect.Height / PageImage.Height);
            int w = (int)(PageImage.Width * scale);
            int h = (int)(PageImage.Height * scale);
            int x = imgRect.X + (imgRect.Width - w) / 2;
            int y = imgRect.Y + (imgRect.Height - h) / 2;

            using var shadowBrush = new SolidBrush(Color.FromArgb(30, 0, 0, 0));
            g.FillRectangle(shadowBrush, x + 2, y + 2, w, h);
            g.DrawImage(PageImage, x, y, w, h);
        }
        else
        {
            using var placeholder = new SolidBrush(Color.FromArgb(241, 245, 249));
            g.FillRectangle(placeholder, imgRect);
        }

        // Row 1: checkbox + page number
        int row1Y = Height - 52;

        // Checkbox
        var cbRect = new Rectangle(10, row1Y + 2, 16, 16);
        if (_isSelected)
        {
            using var cbBrush = new SolidBrush(CheckGreen);
            using var cbPath = RoundedRect(cbRect, 3);
            g.FillPath(cbBrush, cbPath);
            using var checkPen = new Pen(Color.White, 2f);
            g.DrawLine(checkPen, cbRect.X + 3, cbRect.Y + 8, cbRect.X + 6, cbRect.Y + 12);
            g.DrawLine(checkPen, cbRect.X + 6, cbRect.Y + 12, cbRect.X + 13, cbRect.Y + 4);
        }
        else
        {
            using var cbPen = new Pen(Color.FromArgb(203, 213, 225), 1.5f);
            using var cbPath = RoundedRect(cbRect, 3);
            g.DrawPath(cbPen, cbPath);
        }

        // Page number text
        using var font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
        using var textBrush = new SolidBrush(Color.FromArgb(100, 116, 139));
        g.DrawString($"Trang {PageNumber}", font, textBrush,
            new PointF(cbRect.Right + 5, row1Y + 1));

        // Label indicator dot (show purple dot if label has value)
        if (_txtLabel is not null && !string.IsNullOrWhiteSpace(_txtLabel.Text))
        {
            using var dotBrush = new SolidBrush(LabelAccent);
            g.FillEllipse(dotBrush, Width - 22, row1Y + 3, 10, 10);
        }

        // Row 2 is the TextBox (_txtLabel) – positioned by child control
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (_txtLabel is null) return;
        _txtLabel.Location = new Point(10, Height - 30);
        _txtLabel.Size = new Size(Width - 20, 22);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _isHovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        // Don't un-hover if mouse moved into the child TextBox
        var pos = PointToClient(MousePosition);
        if (!ClientRectangle.Contains(pos))
        {
            _isHovered = false;
            Invalidate();
        }
        base.OnMouseLeave(e);
    }

    protected override void OnClick(EventArgs e)
    {
        // Only toggle selection when clicking outside the TextBox area
        var pos = PointToClient(MousePosition);
        if (pos.Y < Height - 34)
        {
            IsSelected = !IsSelected;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
        base.OnClick(e);
    }

    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        int d = radius * 2;
        var gp = new GraphicsPath();
        gp.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        gp.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        gp.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        gp.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        gp.CloseFigure();
        return gp;
    }
}
