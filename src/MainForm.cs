using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Calculator
{
    enum Kind { Number, Function, Equals, Memory }

    sealed class Theme
    {
        public Color Back, Text, Dim, Border;
        public Color NumNormal, NumHover, NumPress;
        public Color FnNormal, FnHover, FnPress;
        public Color EqNormal, EqHover, EqPress, EqText;
        public bool Dark;

        static Color C(int rgb) { return Color.FromArgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255); }

        public static Theme Dark_()
        {
            return new Theme
            {
                Dark = true,
                Back = C(0x202020), Text = C(0xFFFFFF), Dim = C(0xC5C5C5), Border = C(0x202020),
                NumNormal = C(0x3B3B3B), NumHover = C(0x323232), NumPress = C(0x2A2A2A),
                FnNormal = C(0x323232), FnHover = C(0x3B3B3B), FnPress = C(0x2A2A2A),
                EqNormal = C(0x4CC2FF), EqHover = C(0x47B1E8), EqPress = C(0x42A1D0), EqText = C(0x000000)
            };
        }

        public static Theme Light()
        {
            return new Theme
            {
                Dark = false,
                Back = C(0xF3F3F3), Text = C(0x1B1B1B), Dim = C(0x5F5F5F), Border = C(0xE5E5E5),
                NumNormal = C(0xFFFFFF), NumHover = C(0xF9F9F9), NumPress = C(0xF3F3F3),
                FnNormal = C(0xF9F9F9), FnHover = C(0xF0F0F0), FnPress = C(0xE8E8E8),
                EqNormal = C(0x005FB8), EqHover = C(0x1A6FC0), EqPress = C(0x3580C8), EqText = C(0xFFFFFF)
            };
        }

        /// <summary>0 follows Windows, 1 is always light, 2 is always dark.</summary>
        public static Theme ForMode(int mode)
        {
            if (mode == 1) return Light();
            if (mode == 2) return Dark_();
            return FromSystem();
        }

        public static Theme FromSystem()
        {
            try
            {
                object v = Registry.GetValue(
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                    "AppsUseLightTheme", 1);
                return (v is int && (int)v == 0) ? Dark_() : Light();
            }
            catch { return Light(); }
        }
    }

    /// <summary>Colors for context menus so they follow the light/dark theme.</summary>
    sealed class ThemedColors : ProfessionalColorTable
    {
        readonly Color back, border, hot, sep;

        static Color C(int rgb) { return Color.FromArgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255); }

        public ThemedColors(Theme t)
        {
            back = t.Dark ? C(0x2C2C2C) : C(0xFCFCFC);
            border = t.Dark ? C(0x454545) : C(0xD0D0D0);
            hot = t.Dark ? C(0x3D3D3D) : C(0xEAEAEA);
            sep = t.Dark ? C(0x454545) : C(0xE0E0E0);
        }

        public override Color ToolStripDropDownBackground { get { return back; } }
        public override Color ImageMarginGradientBegin { get { return back; } }
        public override Color ImageMarginGradientMiddle { get { return back; } }
        public override Color ImageMarginGradientEnd { get { return back; } }
        public override Color MenuBorder { get { return border; } }
        public override Color MenuItemBorder { get { return hot; } }
        public override Color MenuItemSelected { get { return hot; } }
        public override Color MenuItemSelectedGradientBegin { get { return hot; } }
        public override Color MenuItemSelectedGradientEnd { get { return hot; } }
        public override Color SeparatorDark { get { return sep; } }
        public override Color SeparatorLight { get { return sep; } }
    }

    /// <summary>Caches GDI+ fonts, brushes and pens so resize repaints do not allocate native handles on every frame.</summary>
    static class PaintCache
    {
        struct FontKey : IEquatable<FontKey>
        {
            public readonly string Family;
            public readonly int SizePx;
            public readonly FontStyle Style;
            public FontKey(string family, int sizePx, FontStyle style) { Family = family; SizePx = sizePx; Style = style; }
            public bool Equals(FontKey other) { return SizePx == other.SizePx && Style == other.Style && Family == other.Family; }
            public override bool Equals(object obj) { return obj is FontKey && Equals((FontKey)obj); }
            public override int GetHashCode() { return (Family.GetHashCode() * 397 ^ SizePx) * 31 ^ (int)Style; }
        }

        static readonly System.Collections.Generic.Dictionary<FontKey, Font> fonts =
            new System.Collections.Generic.Dictionary<FontKey, Font>();
        static readonly System.Collections.Generic.Dictionary<int, SolidBrush> brushes =
            new System.Collections.Generic.Dictionary<int, SolidBrush>();
        static readonly System.Collections.Generic.Dictionary<int, Pen> pens =
            new System.Collections.Generic.Dictionary<int, Pen>();
        static readonly System.Collections.Generic.Dictionary<long, GraphicsPath> paths =
            new System.Collections.Generic.Dictionary<long, GraphicsPath>();

        public static GraphicsPath GetRoundPath(int width, int height, out int radius)
        {
            int r = (int)(Math.Min(width, height) * 0.11f) + 2;
            int maxR = Math.Max(1, (Math.Min(width, height) - 2) / 2);
            if (r > maxR) r = maxR;
            radius = r;
            long key = ((long)width << 32) | (uint)height;
            GraphicsPath path;
            if (!paths.TryGetValue(key, out path))
            {
                if (paths.Count >= 128)
                {
                    foreach (GraphicsPath old in paths.Values) old.Dispose();
                    paths.Clear();
                }
                int d = r * 2;
                path = new GraphicsPath();
                path.AddArc(0, 0, d, d, 180, 90);
                path.AddArc(width - 1 - d, 0, d, d, 270, 90);
                path.AddArc(width - 1 - d, height - 1 - d, d, d, 0, 90);
                path.AddArc(0, height - 1 - d, d, d, 90, 90);
                path.CloseFigure();
                paths[key] = path;
            }
            return path;
        }

        public static Font GetFont(string family, float sizePx, FontStyle style)
        {
            int px = Math.Max(1, (int)Math.Round(sizePx));
            var key = new FontKey(family, px, style);
            Font f;
            if (!fonts.TryGetValue(key, out f))
            {
                if (fonts.Count >= 160)
                {
                    foreach (Font old in fonts.Values) old.Dispose();
                    fonts.Clear();
                }
                f = new Font(family, px, style, GraphicsUnit.Pixel);
                fonts[key] = f;
            }
            return f;
        }

        public static SolidBrush GetBrush(Color color)
        {
            int argb = color.ToArgb();
            SolidBrush br;
            if (!brushes.TryGetValue(argb, out br))
            {
                if (brushes.Count >= 64)
                {
                    foreach (SolidBrush old in brushes.Values) old.Dispose();
                    brushes.Clear();
                }
                br = new SolidBrush(color);
                brushes[argb] = br;
            }
            return br;
        }

        public static Pen GetPen(Color color)
        {
            int argb = color.ToArgb();
            Pen pen;
            if (!pens.TryGetValue(argb, out pen))
            {
                if (pens.Count >= 32)
                {
                    foreach (Pen old in pens.Values) old.Dispose();
                    pens.Clear();
                }
                pen = new Pen(color);
                pens[argb] = pen;
            }
            return pen;
        }
    }

    /// <summary>Flat rounded button that never takes keyboard focus.</summary>
    sealed class CalcButton : Control
    {
        public Kind Kind;
        public Theme Theme;
        public string FontFamilyName = "Segoe UI";
        public static string IconFont = "Segoe MDL2 Assets";
        /// <summary>Toggle buttons draw in the "on" state (pressed-in, accent text).</summary>
        public bool Active;
        /// <summary>Menu items: text starts at the left edge instead of the centre.</summary>
        public bool LeftAlign;
        bool hover, pressed;

        public CalcButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.Opaque |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.StandardClick | ControlStyles.StandardDoubleClick, true);
            TabStop = true;
            AccessibleRole = AccessibleRole.PushButton;
            Cursor = Cursors.Default;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { pressed = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width <= 2 || Height <= 2) return;
            Graphics g = e.Graphics;
            g.Clear(Theme.Back);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            DrawFace(g, ClientRectangle, Text, Kind, Theme, FontFamilyName,
                Enabled, hover, pressed, Active, LeftAlign, Focused && ShowFocusCues);
        }

        internal static void DrawFace(Graphics g, Rectangle rect, string text, Kind kind, Theme theme,
            string fontFamilyName, bool enabled, bool hover, bool pressed, bool active, bool leftAlign, bool showFocus)
        {
            if (rect.Width <= 2 || rect.Height <= 2) return;

            Color fill, fore = theme.Text;
            switch (kind)
            {
                case Kind.Equals:
                    fill = pressed ? theme.EqPress : hover ? theme.EqHover : theme.EqNormal;
                    fore = theme.EqText;
                    break;
                case Kind.Number:
                    fill = pressed ? theme.NumPress : hover ? theme.NumHover : theme.NumNormal;
                    break;
                case Kind.Memory:   // text-only until hovered, like the memory strip in Windows 11
                    fill = !enabled ? theme.Back : pressed ? theme.FnPress : hover ? theme.FnHover : theme.Back;
                    break;
                default:
                    fill = pressed ? theme.FnPress : hover ? theme.FnHover : theme.FnNormal;
                    break;
            }
            if (pressed && kind != Kind.Equals) fore = theme.Dim;
            if (!enabled) fore = Blend(theme.Dim, theme.Back, 0.45f);
            if (active && kind == Kind.Function)
            {
                fill = theme.FnPress;
                fore = theme.EqNormal;
            }

            int r;
            GraphicsPath path = PaintCache.GetRoundPath(rect.Width, rect.Height, out r);
            if (rect.X != 0 || rect.Y != 0) g.TranslateTransform(rect.X, rect.Y);
            g.FillPath(PaintCache.GetBrush(fill), path);
            if (!theme.Dark && kind != Kind.Equals && kind != Kind.Memory)
                g.DrawPath(PaintCache.GetPen(theme.Border), path);
            if (rect.X != 0 || rect.Y != 0) g.TranslateTransform(-rect.X, -rect.Y);

            float px = Math.Max(10f, Math.Min(rect.Height * 0.36f, rect.Width * 0.30f));
            if (kind == Kind.Memory) px = Math.Max(12f, rect.Height * 0.46f);
            FontStyle style = kind == Kind.Number ? FontStyle.Bold : FontStyle.Regular;
            string family = text == "⌫" ? "Segoe UI Symbol" : fontFamilyName;
            if (text.Length == 1 && text[0] >= '\uE000' && text[0] <= '\uF8FF')
            {
                family = IconFont;
                px *= 1.2f;
            }
            Rectangle textRect = rect;
            TextFormatFlags align = TextFormatFlags.HorizontalCenter;
            if (leftAlign) { textRect = new Rectangle(rect.X + 14, rect.Y, rect.Width - 14, rect.Height); align = TextFormatFlags.Left; }
            Font f = PaintCache.GetFont(family, px, style);
            TextRenderer.DrawText(g, text, f, textRect, fore,
                align | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);

            if (showFocus && rect.Width > 8 && rect.Height > 8)
                using (GraphicsPath ring = Round(new Rectangle(rect.X + 3, rect.Y + 3, rect.Width - 7, rect.Height - 7), Math.Max(1, r - 2)))
                using (var pen = new Pen(kind == Kind.Equals ? theme.EqText : theme.EqNormal, 2f))
                    g.DrawPath(pen, ring);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            hover = pressed = false;
            Invalidate();
            base.OnEnabledChanged(e);
        }

        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }

        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnTextChanged(EventArgs e)
        {
            AccessibleName = Text;
            base.OnTextChanged(e);
        }

        /// <summary>Space and Enter press the focused button, as a standard Windows button does.</summary>
        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            if (e.KeyChar == ' ' || e.KeyChar == '\r')
            {
                e.Handled = true;
                OnClick(EventArgs.Empty);
            }
            base.OnKeyPress(e);
        }

        static Color Blend(Color a, Color b, float t)
        {
            return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        static GraphicsPath Round(Rectangle rc, int r)
        {
            int d = r * 2;
            var p = new GraphicsPath();
            p.AddArc(rc.X, rc.Y, d, d, 180, 90);
            p.AddArc(rc.Right - d, rc.Y, d, d, 270, 90);
            p.AddArc(rc.Right - d, rc.Bottom - d, d, d, 0, 90);
            p.AddArc(rc.X, rc.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    /// <summary>Two-line display: dim expression on top, large value below.</summary>
    sealed class DisplayBox : Control
    {
        public Theme Theme;
        public string FontFamilyName = "Segoe UI";
        string expr = "", value = "0";
        /// <summary>Space kept free at the right so the text does not run under the journal button.</summary>
        public int RightInset;
        bool hasMemory;

        public DisplayBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.Opaque |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style |= 0x02000000;   // WS_CLIPCHILDREN
                return cp;
            }
        }

        public void SetText(string expression, string val)
        {
            expr = expression;
            value = val;
            Invalidate();
        }

        public void SetMemory(bool on)
        {
            if (hasMemory == on) return;
            hasMemory = on;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width <= 0 || Height <= 0) return;
            Graphics g = e.Graphics;
            g.Clear(Theme.Back);
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            const TextFormatFlags flags = TextFormatFlags.Right | TextFormatFlags.NoPadding |
                                          TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
            int pad = Math.Max(8, Width / 28);
            int avail = Width - pad * 2 - RightInset;
            if (avail <= 0) return;

            float small = Math.Max(11f, Height * 0.13f);
            Font fs = PaintCache.GetFont(FontFamilyName, small, FontStyle.Regular);
            TextRenderer.DrawText(g, expr, fs, new Rectangle(pad, Height / 10, avail, (int)(small * 1.5f)),
                Theme.Dim, flags | TextFormatFlags.Top);

            if (hasMemory)
            {
                Font fm = PaintCache.GetFont(FontFamilyName, small * 1.15f, FontStyle.Bold);
                TextRenderer.DrawText(g, "M", fm, new Rectangle(pad, Height - (int)(small * 2.4f), (int)(small * 2f), (int)(small * 1.5f)),
                    Theme.EqNormal, TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.Top);
            }

            const TextFormatFlags measureFlags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
            var inf = new Size(int.MaxValue, int.MaxValue);
            int big = Math.Max(14, (int)Math.Round(Height * 0.42f));
            Font fv = PaintCache.GetFont(FontFamilyName, big, FontStyle.Bold);
            int textW = TextRenderer.MeasureText(g, value, fv, inf, measureFlags).Width;
            if (textW > avail && big > 14)
            {
                big = Math.Max(14, (int)Math.Floor(big * (double)avail / textW));
                fv = PaintCache.GetFont(FontFamilyName, big, FontStyle.Bold);
                while (big > 14 && TextRenderer.MeasureText(g, value, fv, inf, measureFlags).Width > avail)
                {
                    big--;
                    fv = PaintCache.GetFont(FontFamilyName, big, FontStyle.Bold);
                }
            }
            TextRenderer.DrawText(g, value, fv, new Rectangle(pad, 0, avail, Height - Height / 12),
                Theme.Text, flags | TextFormatFlags.Bottom);
        }
    }

    /// <summary>Owner-drawn list of past calculations; clicking an entry raises ItemChosen.</summary>
    sealed class HistoryList : ListBox
    {
        public Theme Theme;
        public string FontFamilyName = "Segoe UI";
        public event Action<HistoryItem> ItemChosen;
        int hot = -1;

        public HistoryList()
        {
            DrawMode = DrawMode.OwnerDrawFixed;
            BorderStyle = BorderStyle.None;
            IntegralHeight = false;
            DoubleBuffered = true;
            TabStop = false;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ItemHeight = (int)(66 * DeviceDpi / 96f);
        }

        public void SetItems(System.Collections.Generic.IList<HistoryItem> items)
        {
            BeginUpdate();
            Items.Clear();
            foreach (HistoryItem it in items) Items.Add(it);
            EndUpdate();
            hot = -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int idx = IndexFromPoint(e.Location);
            if (idx != hot) { hot = idx; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hot = -1;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            int idx = IndexFromPoint(e.Location);
            if (e.Button == MouseButtons.Left && idx >= 0 && ItemChosen != null)
                ItemChosen((HistoryItem)Items[idx]);
            base.OnMouseDown(e);
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Bounds.Width <= 0 || e.Bounds.Height <= 0) return;
            Graphics g = e.Graphics;
            g.FillRectangle(PaintCache.GetBrush(Theme.Back), e.Bounds);
            if (e.Index < 0) return;
            var item = (HistoryItem)Items[e.Index];

            Rectangle r = Rectangle.Inflate(e.Bounds, -2, -2);
            if (e.Index == hot)
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = new GraphicsPath())
                {
                    int d = 8;
                    path.AddArc(r.X, r.Y, d, d, 180, 90);
                    path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                    path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                    path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                    path.CloseFigure();
                    g.FillPath(PaintCache.GetBrush(Theme.FnHover), path);
                }
            }

            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            const TextFormatFlags flags = TextFormatFlags.Right | TextFormatFlags.NoPadding |
                                          TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
            var inner = Rectangle.Inflate(r, -10, -6);
            float small = Math.Max(11f, r.Height * 0.20f);
            float big = Math.Max(14f, r.Height * 0.36f);
            TextRenderer.DrawText(g, item.Expression, PaintCache.GetFont(FontFamilyName, small, FontStyle.Regular), inner, Theme.Dim, flags | TextFormatFlags.Top);
            TextRenderer.DrawText(g, item.ResultText, PaintCache.GetFont(FontFamilyName, big, FontStyle.Bold), inner, Theme.Text, flags | TextFormatFlags.Bottom);
        }
    }

    /// <summary>Batches child control moves/resizes into a single atomic Win32 DeferWindowPos call.</summary>
    static class Win32Layout
    {
        [DllImport("user32.dll")]
        static extern IntPtr BeginDeferWindowPos(int nNumWindows);

        [DllImport("user32.dll")]
        static extern IntPtr DeferWindowPos(IntPtr hWinPosInfo, IntPtr hWnd, IntPtr hWndInsertAfter,
            int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        static extern bool EndDeferWindowPos(IntPtr hWinPosInfo);

        const uint SWP_NOZORDER = 0x0004;
        const uint SWP_NOACTIVATE = 0x0010;
        const uint SWP_NOCOPYBITS = 0x0100;

        public static void Apply(Control parent, Control[] controls, Rectangle[] bounds, int count)
        {
            int changed = 0;
            bool canDefer = parent.IsHandleCreated;
            for (int i = 0; i < count; i++)
            {
                if (controls[i].Bounds != bounds[i]) changed++;
                if (canDefer && !controls[i].IsHandleCreated) canDefer = false;
            }
            if (changed == 0) return;

            if (canDefer)
            {
                IntPtr hdwp = BeginDeferWindowPos(changed);
                if (hdwp != IntPtr.Zero)
                {
                    for (int i = 0; i < count; i++)
                    {
                        Rectangle b = bounds[i];
                        if (controls[i].Bounds == b) continue;
                        hdwp = DeferWindowPos(hdwp, controls[i].Handle, IntPtr.Zero,
                            b.X, b.Y, b.Width, b.Height, SWP_NOZORDER | SWP_NOACTIVATE | SWP_NOCOPYBITS);
                        if (hdwp == IntPtr.Zero) break;
                    }
                    if (hdwp != IntPtr.Zero && EndDeferWindowPos(hdwp)) return;
                }
            }

            parent.SuspendLayout();
            for (int i = 0; i < count; i++)
            {
                Rectangle b = bounds[i];
                if (controls[i].Bounds != b) controls[i].SetBounds(b.X, b.Y, b.Width, b.Height);
            }
            parent.ResumeLayout(false);
        }
    }

    /// <summary>Double-buffered panel that clips child controls so resizing does not flash behind them.</summary>
    class BufferedPanel : Panel
    {
        public BufferedPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style |= 0x02000000;   // WS_CLIPCHILDREN
                return cp;
            }
        }
    }

    /// <summary>Single key cell inside a KeypadGrid (no per-key HWND overhead during resize).</summary>
    sealed class KeyCell
    {
        public readonly int Col, Row;
        public readonly Kind Kind;
        public readonly Padding Margin;
        public readonly Action Click;
        internal KeypadGrid Owner;
        internal Rectangle Bounds;
        string text, accessibleName;
        bool enabled = true, active;

        public KeyCell(int col, int row, string text, Kind kind, Padding margin, Action onClick)
        {
            Col = col;
            Row = row;
            this.text = text;
            Kind = kind;
            Margin = margin;
            Click = onClick;
        }

        public string Text
        {
            get { return text; }
            set { if (text != value) { text = value; Invalidate(); } }
        }

        public bool Enabled
        {
            get { return enabled; }
            set { if (enabled != value) { enabled = value; Invalidate(); } }
        }

        public bool Active
        {
            get { return active; }
            set { if (active != value) { active = value; Invalidate(); } }
        }

        public string AccessibleName
        {
            get { return accessibleName ?? text; }
            set { accessibleName = value; }
        }

        public void Invalidate()
        {
            if (Owner != null && !Bounds.IsEmpty) Owner.Invalidate(Bounds);
        }
    }

    /// <summary>Owner-drawn uniform button grid: 1 HWND instead of N, with full Tab focus and accessibility.</summary>
    sealed class KeypadGrid : Control
    {
        public Theme Theme;
        public string FontFamilyName = "Segoe UI";
        readonly int cols, rows;
        readonly KeyCell[] cells;
        int count;
        int hotIndex = -1, pressedIndex = -1, focusedIndex = -1;

        public KeypadGrid(int columns, int rows)
        {
            cols = columns;
            this.rows = rows;
            cells = new KeyCell[columns * rows];
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.Opaque |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            TabStop = true;
            Cursor = Cursors.Default;
        }

        public int CellCount { get { return count; } }
        public KeyCell CellAt(int index) { return cells[index]; }

        public KeyCell AddCell(int col, int row, string text, Kind kind, Padding margin, Action onClick)
        {
            var cell = new KeyCell(col, row, text, kind, margin, onClick) { Owner = this };
            cells[count++] = cell;
            ComputeBounds();
            return cell;
        }

        void ComputeBounds()
        {
            int w = ClientSize.Width, h = ClientSize.Height;
            if (w <= 0 || h <= 0 || count == 0) return;
            for (int i = 0; i < count; i++)
            {
                KeyCell c = cells[i];
                int left = c.Col * w / cols, right = (c.Col + 1) * w / cols;
                int top = c.Row * h / rows, bottom = (c.Row + 1) * h / rows;
                Padding m = c.Margin;
                c.Bounds = new Rectangle(
                    left + m.Left,
                    top + m.Top,
                    Math.Max(1, right - left - m.Horizontal),
                    Math.Max(1, bottom - top - m.Vertical));
            }
        }

        int HitTest(Point p)
        {
            for (int i = 0; i < count; i++)
                if (cells[i].Bounds.Contains(p)) return i;
            return -1;
        }

        void InvalidateCell(int index)
        {
            if (index >= 0 && index < count && !cells[index].Bounds.IsEmpty)
                Invalidate(cells[index].Bounds);
        }

        int NextEnabled(int start, int step)
        {
            for (int i = start + step; i >= 0 && i < count; i += step)
                if (cells[i].Enabled) return i;
            return -1;
        }

        protected override void OnResize(EventArgs e)
        {
            ComputeBounds();
            base.OnResize(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width <= 0 || Height <= 0 || Theme == null) return;
            Graphics g = e.Graphics;
            Rectangle clip = e.ClipRectangle;
            g.FillRectangle(PaintCache.GetBrush(Theme.Back), clip);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            bool showFocus = Focused && ShowFocusCues;
            for (int i = 0; i < count; i++)
            {
                KeyCell c = cells[i];
                if (!c.Bounds.IntersectsWith(clip)) continue;
                bool isHot = (pressedIndex < 0 || pressedIndex == i) && i == hotIndex;
                bool isPressed = i == pressedIndex && isHot;
                CalcButton.DrawFace(g, c.Bounds, c.Text, c.Kind, Theme, FontFamilyName,
                    c.Enabled, isHot, isPressed, c.Active, false, showFocus && i == focusedIndex);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int idx = HitTest(e.Location);
            if (idx != hotIndex)
            {
                int old = hotIndex;
                hotIndex = idx;
                InvalidateCell(old);
                InvalidateCell(hotIndex);
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            int oldHot = hotIndex, oldPress = pressedIndex;
            hotIndex = -1;
            pressedIndex = -1;
            InvalidateCell(oldHot);
            if (oldPress != oldHot) InvalidateCell(oldPress);
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                int idx = HitTest(e.Location);
                if (idx >= 0 && cells[idx].Enabled)
                {
                    pressedIndex = idx;
                    InvalidateCell(idx);
                }
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            int idx = pressedIndex;
            if (idx >= 0)
            {
                pressedIndex = -1;
                InvalidateCell(idx);
                if (e.Button == MouseButtons.Left && HitTest(e.Location) == idx && cells[idx].Enabled && cells[idx].Click != null)
                    cells[idx].Click();
            }
            base.OnMouseUp(e);
        }

        protected override void OnGotFocus(EventArgs e)
        {
            bool reverse = (ModifierKeys & Keys.Shift) != 0;
            focusedIndex = reverse ? NextEnabled(count, -1) : NextEnabled(-1, 1);
            InvalidateCell(focusedIndex);
            base.OnGotFocus(e);
        }

        protected override void OnLostFocus(EventArgs e)
        {
            int old = focusedIndex;
            focusedIndex = -1;
            InvalidateCell(old);
            base.OnLostFocus(e);
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            if ((keyData & Keys.KeyCode) == Keys.Tab && (keyData & (Keys.Control | Keys.Alt)) == 0)
            {
                int step = (keyData & Keys.Shift) != 0 ? -1 : 1;
                int next = NextEnabled(focusedIndex, step);
                if (next >= 0)
                {
                    int old = focusedIndex;
                    focusedIndex = next;
                    InvalidateCell(old);
                    InvalidateCell(focusedIndex);
                    return true;
                }
            }
            return base.ProcessDialogKey(keyData);
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            if ((e.KeyChar == ' ' || e.KeyChar == '\r') && focusedIndex >= 0 && focusedIndex < count && cells[focusedIndex].Enabled)
            {
                e.Handled = true;
                if (cells[focusedIndex].Click != null) cells[focusedIndex].Click();
            }
            base.OnKeyPress(e);
        }

        protected override AccessibleObject CreateAccessibilityInstance()
        {
            return new KeypadAccessibleObject(this);
        }

        sealed class KeypadAccessibleObject : ControlAccessibleObject
        {
            readonly KeypadGrid grid;
            public KeypadAccessibleObject(KeypadGrid owner) : base(owner) { grid = owner; }
            public override int GetChildCount() { return grid.count; }
            public override AccessibleObject GetChild(int index)
            {
                return index >= 0 && index < grid.count ? new CellAccessibleObject(grid, index, this) : null;
            }
            public override AccessibleObject HitTest(int x, int y)
            {
                int idx = grid.HitTest(grid.PointToClient(new Point(x, y)));
                return idx >= 0 ? GetChild(idx) : base.HitTest(x, y);
            }
            public override AccessibleObject GetFocused()
            {
                return grid.Focused && grid.focusedIndex >= 0 ? GetChild(grid.focusedIndex) : base.GetFocused();
            }
        }

        sealed class CellAccessibleObject : AccessibleObject
        {
            readonly KeypadGrid grid;
            readonly int index;
            readonly AccessibleObject parent;
            public CellAccessibleObject(KeypadGrid grid, int index, AccessibleObject parent)
            {
                this.grid = grid;
                this.index = index;
                this.parent = parent;
            }
            public override AccessibleObject Parent { get { return parent; } }
            public override string Name { get { return grid.cells[index].AccessibleName; } }
            public override AccessibleRole Role { get { return AccessibleRole.PushButton; } }
            public override Rectangle Bounds { get { return grid.RectangleToScreen(grid.cells[index].Bounds); } }
            public override AccessibleStates State
            {
                get
                {
                    AccessibleStates s = AccessibleStates.Focusable;
                    if (!grid.cells[index].Enabled) s |= AccessibleStates.Unavailable;
                    if (grid.Focused && grid.focusedIndex == index) s |= AccessibleStates.Focused;
                    if (grid.pressedIndex == index) s |= AccessibleStates.Pressed;
                    return s;
                }
            }
            public override void DoDefaultAction()
            {
                if (grid.cells[index].Enabled && grid.cells[index].Click != null) grid.cells[index].Click();
            }
        }
    }

    /// <summary>Stacks display (21%), memory strip, optional scientific panel and main keypad (79%).</summary>
    sealed class RootPanel : BufferedPanel
    {
        readonly Control display, memStrip, sciPanel, grid;
        readonly Control[] scratchCtrls = new Control[4];
        readonly Rectangle[] scratchBounds = new Rectangle[4];
        int sciHeight;

        public int SciHeight
        {
            get { return sciHeight; }
            set { if (sciHeight != value) { sciHeight = value; PerformLayout(); } }
        }

        public RootPanel(Control display, Control memStrip, Control sciPanel, Control grid)
        {
            this.display = display;
            this.memStrip = memStrip;
            this.sciPanel = sciPanel;
            this.grid = grid;
            Controls.AddRange(new[] { display, memStrip, sciPanel, grid });
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            int x = Padding.Left, y = Padding.Top;
            int w = ClientSize.Width - Padding.Horizontal;
            int h = ClientSize.Height - Padding.Vertical;
            if (w <= 0 || h <= 0) return;

            int memH = (int)Math.Round(28f * DeviceDpi / 96f);
            int sciH = sciPanel.Visible ? sciHeight : 0;
            int avail = Math.Max(0, h - memH - sciH);
            int displayH = (int)Math.Round(avail * 0.21f);
            int gridH = Math.Max(0, avail - displayH);

            int n = 0;
            scratchCtrls[n] = display;
            scratchBounds[n++] = new Rectangle(x, y, w, Math.Max(1, displayH));

            scratchCtrls[n] = memStrip;
            scratchBounds[n++] = new Rectangle(x, y + displayH, w, Math.Max(1, memH));

            if (sciH > 0)
            {
                scratchCtrls[n] = sciPanel;
                scratchBounds[n++] = new Rectangle(x, y + displayH + memH, w, sciH);
            }

            scratchCtrls[n] = grid;
            scratchBounds[n++] = new Rectangle(x, y + displayH + memH + sciH, w, Math.Max(1, gridH));

            Win32Layout.Apply(this, scratchCtrls, scratchBounds, n);
        }
    }

    sealed class MainForm : Form
    {
        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        static extern int SetWindowTheme(IntPtr hwnd, string appName, string idList);

        [DllImport("user32.dll")]
        static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern bool IsZoomed(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        static extern int GetSystemMetricsForDpi(int nIndex, int dpi);

        [DllImport("shell32.dll")]
        static extern IntPtr SHAppBarMessage(int dwMessage, ref APPBARDATA pData);

        [StructLayout(LayoutKind.Sequential)]
        struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        struct APPBARDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public int uCallbackMessage;
            public int uEdge;
            public RECT rc;
            public IntPtr lParam;
        }

        const int WM_WINDOWPOSCHANGED = 0x0047;
        const int WM_NCCALCSIZE = 0x83, WM_NCHITTEST = 0x84, WM_NCLBUTTONDOWN = 0xA1;
        const int WVR_REDRAW = 0x0300;
        const int HTCLIENT = 1, HTCAPTION = 2, HTLEFT = 10, HTRIGHT = 11, HTTOP = 12, HTTOPLEFT = 13, HTTOPRIGHT = 14;
        const int HTBOTTOM = 15, HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17;
        const int SM_CXSIZEFRAME = 32, SM_CYSIZEFRAME = 33, SM_CXPADDEDBORDER = 92;
        const int ABM_GETSTATE = 4, ABM_GETTASKBARPOS = 5, ABS_AUTOHIDE = 1;
        const int ABE_LEFT = 0, ABE_TOP = 1, ABE_RIGHT = 2, ABE_BOTTOM = 3;

        static readonly System.Reflection.FieldInfo RestoredBoundsSpecifiedField =
            typeof(Form).GetField("restoredWindowBoundsSpecified",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style |= 0x02000000;   // WS_CLIPCHILDREN: never paint over child controls during resize
                return cp;
            }
        }

        readonly CalcEngine engine = new CalcEngine();
        readonly SciEngine sci = new SciEngine();
        bool scientific;   // which engine the keys go to
        bool second;       // "2nd" key: inverse trigonometry and exponentials
        readonly DisplayBox display = new DisplayBox();
        readonly BufferedPanel historyPanel = new BufferedPanel();
        readonly BufferedPanel historyHeader = new BufferedPanel();
        readonly Label historyTitle = new Label();
        readonly HistoryList historyList = new HistoryList();
        CalcButton clearHistoryButton, historyToggle, pinToggle;
        KeyCell memClear, memRecall, percentButton;
        const int SciPanelHeight = 3 * 40;   // logical pixels the function panel adds to the window
        RootPanel rootPanel;
        KeypadGrid memStrip, sciPanel, mainGrid;
        readonly Control[] captionRight = new Control[3];
        readonly Rectangle[] captionBounds = new Rectangle[3];
        readonly System.Collections.Generic.Dictionary<string, KeyCell> sciButtons =
            new System.Collections.Generic.Dictionary<string, KeyCell>();
        readonly Settings settings;
        readonly ToolTip tips = new ToolTip();
        readonly ContextMenuStrip displayMenu = new ContextMenuStrip();
        readonly ContextMenuStrip historyMenu = new ContextMenuStrip();
        readonly BufferedPanel navPanel = new BufferedPanel();
        readonly ScrollList navList = new ScrollList();
        readonly Label navHeader = new Label(), navSection = new Label(), navOther = new Label(), captionTitle = new Label();
        readonly BufferedPanel captionBar = new BufferedPanel();
        readonly PictureBox captionIcon = new PictureBox();
        CalcButton navToggle, plainButton, sciButton, minButton, maxButton, closeButton;
        ConverterForm converter;
        HistoryItem historyMenuTarget;
        int shownHistoryVersion = -1;
        readonly System.Collections.Generic.List<CalcButton> buttons = new System.Collections.Generic.List<CalcButton>();
        readonly string family;
        Theme theme;

        static readonly string[,] KeyLayout =
        {
            { "%", "CE", "C", "⌫" },
            { "1/x", "x²", "√x", "÷" },
            { "7", "8", "9", "×" },
            { "4", "5", "6", "−" },
            { "1", "2", "3", "+" },
            { "±", "0", ",", "=" }
        };

        static readonly string[,] SciLayout =
        {
            { "2nd", "π", "e", "(", ")" },
            { "sin", "cos", "tan", "n!", "xʸ" },
            { "ln", "log", "|x|", "∛x", "DEG" }
        };

        /// <summary>Names that screen readers announce instead of the symbol on the key.</summary>
        static readonly System.Collections.Generic.Dictionary<string, string> SpokenNames =
            new System.Collections.Generic.Dictionary<string, string>
            {
                { "%", "Процент" }, { "mod", "Остаток от деления" }, { "CE", "Очистить запись" }, { "C", "Очистить" },
                { "⌫", "Стереть" }, { "1/x", "Обратное число" }, { "x²", "В квадрат" }, { "√x", "Квадратный корень" },
                { "÷", "Разделить" }, { "×", "Умножить" }, { "−", "Минус" }, { "+", "Плюс" }, { "=", "Равно" },
                { "±", "Сменить знак" }, { ",", "Запятая" }, { "MC", "Очистить память" }, { "MR", "Вызвать из памяти" },
                { "M+", "Прибавить к памяти" }, { "M−", "Вычесть из памяти" }, { "MS", "Сохранить в память" },
                { "π", "Пи" }, { "e", "Е" }, { "(", "Открыть скобку" }, { ")", "Закрыть скобку" },
                { "sin", "Синус" }, { "cos", "Косинус" }, { "tan", "Тангенс" }, { "n!", "Факториал" },
                { "xʸ", "Степень" }, { "ln", "Натуральный логарифм" }, { "log", "Десятичный логарифм" },
                { "|x|", "Модуль" }, { "∛x", "Кубический корень" }, { "2nd", "Вторая функция" }, { "DEG", "Единицы углов" }
            };

        /// <summary>The extra keys of scientific mode, shown above the keypad.</summary>
        KeypadGrid BuildSciPanel()
        {
            int rows = SciLayout.GetLength(0), cols = SciLayout.GetLength(1);
            var panel = new KeypadGrid(cols, rows) { Visible = false, FontFamilyName = family };

            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    string key = SciLayout[r, c];
                    KeyCell cell = panel.AddCell(c, r, key, Kind.Function, new Padding(2), delegate { Press(key); });
                    sciButtons[key] = cell;
                }
            return panel;
        }

        public MainForm()
        {
            family = PickFont();
            theme = Theme.FromSystem();

            Text = "Калькулятор";
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(340, 552);   // 340x520 content plus the 32px custom caption bar
            MinimumSize = new Size(342, 474);  // includes the native invisible DWM resize frame on left, right and bottom
            StartPosition = FormStartPosition.CenterScreen;
            settings = Settings.Load();
            TopMost = settings.TopMost;
            sci.Angle = settings.Angle;
            KeyPreview = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Opaque, true);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            display.FontFamilyName = family;

            memStrip = new KeypadGrid(5, 1) { FontFamilyName = family };
            string[] memKeys = { "MC", "MR", "M+", "M−", "MS" };
            for (int i = 0; i < memKeys.Length; i++)
            {
                string key = memKeys[i];
                KeyCell cell = memStrip.AddCell(i, 0, key, Kind.Memory, new Padding(2, 0, 2, 2), delegate { Press(key); });
                if (key == "MC") memClear = cell;
                if (key == "MR") memRecall = cell;
            }

            sciPanel = BuildSciPanel();

            mainGrid = new KeypadGrid(4, 6) { FontFamilyName = family };
            for (int r = 0; r < 6; r++)
                for (int c = 0; c < 4; c++)
                {
                    string label = KeyLayout[r, c];
                    if (label == ",") label = CalcEngine.DecimalSeparator;
                    Kind kind = label == "=" ? Kind.Equals
                              : (r >= 2 && c < 3 && r < 5) || (r == 5 && c == 1) || (r == 5 && c == 2) ? Kind.Number
                              : Kind.Function;
                    string key = KeyLayout[r, c];
                    KeyCell cell = mainGrid.AddCell(c, r, label, kind, new Padding(2), delegate { Press(key); });
                    if (key == "%") percentButton = cell;
                }

            rootPanel = new RootPanel(display, memStrip, sciPanel, mainGrid)
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(4, 0, 4, 4)
            };

            navToggle = new CalcButton
            {
                Text = "\uE700",
                Kind = Kind.Function,
                Size = new Size(40, 34),
                Location = new Point(6, 8),
                FontFamilyName = family
            };
            navToggle.Click += delegate { ToggleNav(); };
            display.Controls.Add(navToggle);
            buttons.Add(navToggle);
            tips.SetToolTip(navToggle, "Меню");

            historyToggle = new CalcButton
            {
                Text = "",
                Kind = Kind.Function,
                Size = new Size(40, 34),
                Location = new Point(50, 8),
                FontFamilyName = family
            };
            historyToggle.Click += delegate { ToggleHistory(); };
            display.RightInset = historyToggle.Width + 6;
            display.Controls.Add(historyToggle);
            buttons.Add(historyToggle);
            tips.SetToolTip(historyToggle, "Журнал (Ctrl+H)");

            pinToggle = new CalcButton
            {
                Text = "",
                Kind = Kind.Function,
                Size = new Size(40, 34),
                Location = new Point(170, 8),
                FontFamilyName = family,
                Active = TopMost
            };
            pinToggle.Click += delegate { ToggleTopMost(); };
            buttons.Add(pinToggle);
            tips.SetToolTip(pinToggle, "Поверх всех окон (Ctrl+T)");

            BuildHistoryPanel();
            BuildMenus();
            BuildNavPanel();
            BuildCaption();
            display.Resize += delegate { LayoutTopRow(); };
            LayoutTopRow();
            sci.Calculated += delegate(string line, string result) { engine.Record(line, result); };
            Controls.Add(rootPanel);       // Fill goes first so the docked panels claim their space before it
            Controls.Add(historyPanel);
            Controls.Add(navPanel);
            Controls.Add(captionBar);      // added last, so the caption takes the top edge first

            historyToggle.AccessibleName = "Журнал";
            pinToggle.AccessibleName = "Поверх всех окон";
            foreach (CalcButton b in buttons)
            {
                string spoken;
                if (SpokenNames.TryGetValue(b.Text, out spoken)) b.AccessibleName = spoken;
            }
            foreach (KeypadGrid kp in new[] { memStrip, sciPanel, mainGrid })
                for (int i = 0; i < kp.CellCount; i++)
                {
                    KeyCell cell = kp.CellAt(i);
                    string spoken;
                    if (SpokenNames.TryGetValue(cell.Text, out spoken)) cell.AccessibleName = spoken;
                }
            ApplyTheme();
            SyncHistory();
            Refresh_();
            SystemEvents.UserPreferenceChanged += OnPrefChanged;
        }

        void BuildHistoryPanel()
        {
            historyPanel.Dock = DockStyle.Right;
            historyPanel.Width = 280;
            historyPanel.Padding = new Padding(4, 0, 4, 4);
            historyPanel.Visible = false;

            historyList.Dock = DockStyle.Fill;
            historyList.FontFamilyName = family;
            historyList.ItemChosen += delegate(HistoryItem item)
            {
                if (scientific) sci.Recall(item.Expression, item.Result);
                else engine.Recall(item);
                Refresh_();
                ActiveControl = null;
            };
            historyList.HandleCreated += delegate { ApplyListTheme(); };

            historyHeader.Dock = DockStyle.Top;
            historyHeader.Height = 50;
            historyHeader.Padding = new Padding(8, 8, 0, 8);

            clearHistoryButton = new CalcButton
            {
                Text = "Очистить",
                Kind = Kind.Function,
                Dock = DockStyle.Right,
                Width = 96,
                FontFamilyName = family
            };
            clearHistoryButton.Click += delegate { engine.ClearHistory(); SyncHistory(); };
            buttons.Add(clearHistoryButton);

            historyTitle.Text = "Журнал";
            historyTitle.Dock = DockStyle.Fill;
            historyTitle.TextAlign = ContentAlignment.MiddleLeft;
            historyTitle.Font = new Font(family, 14f, FontStyle.Bold, GraphicsUnit.Pixel);

            historyHeader.Controls.Add(historyTitle);
            historyHeader.Controls.Add(clearHistoryButton);

            historyPanel.Controls.Add(historyList);
            historyPanel.Controls.Add(historyHeader);
            historyPanel.Paint += PaintHistoryEmpty;
            historyPanel.Resize += delegate { historyPanel.Invalidate(); };
        }

        void PaintHistoryEmpty(object sender, PaintEventArgs e)
        {
            if (historyList.Visible) return;
            var area = new Rectangle(0, historyHeader.Height, historyPanel.Width, historyPanel.Height - historyHeader.Height);
            TextRenderer.DrawText(e.Graphics, "Журнал пуст", PaintCache.GetFont(family, 16f, FontStyle.Regular), area, theme.Dim,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        void BuildMenus()
        {
            displayMenu.ShowImageMargin = false;
            var copy = new ToolStripMenuItem("Копировать") { ShortcutKeyDisplayString = "Ctrl+C" };
            var paste = new ToolStripMenuItem("Вставить") { ShortcutKeyDisplayString = "Ctrl+V" };
            copy.Click += delegate { CopyDisplay(); };
            paste.Click += delegate { PasteFromClipboard(); };
            displayMenu.Items.Add(copy);
            displayMenu.Items.Add(paste);
            displayMenu.Opening += delegate
            {
                copy.Enabled = !(scientific ? sci.HasError : engine.HasError);
                paste.Enabled = ClipboardHasNumber();
            };
            display.ContextMenuStrip = displayMenu;

            historyMenu.ShowImageMargin = false;
            var copyResult = new ToolStripMenuItem("Копировать результат");
            var copyExample = new ToolStripMenuItem("Копировать пример целиком");
            var remove = new ToolStripMenuItem("Удалить запись");
            copyResult.Click += delegate { SetClipboard(historyMenuTarget.Result.Replace(".", CalcEngine.DecimalSeparator)); };
            copyExample.Click += delegate { SetClipboard(historyMenuTarget.Expression + " " + historyMenuTarget.ResultText); };
            remove.Click += delegate { engine.RemoveHistory(historyMenuTarget); SyncHistory(); };
            historyMenu.Items.Add(copyResult);
            historyMenu.Items.Add(copyExample);
            historyMenu.Items.Add(new ToolStripSeparator());
            historyMenu.Items.Add(remove);
            historyMenu.Opening += delegate(object s, System.ComponentModel.CancelEventArgs e)
            {
                int idx = historyList.IndexFromPoint(historyList.PointToClient(Cursor.Position));
                if (idx < 0) { e.Cancel = true; return; }
                historyMenuTarget = (HistoryItem)historyList.Items[idx];
            };
            historyList.ContextMenuStrip = historyMenu;
        }

        /// <summary>The menu opens from the left edge like the history panel does from the right.</summary>
        void BuildNavPanel()
        {
            navPanel.Dock = DockStyle.Left;
            navPanel.Width = 220;
            navPanel.Padding = new Padding(4, 0, 4, 4);
            navPanel.Visible = false;

            navList.Dock = DockStyle.Fill;

            navHeader.Text = "Меню";
            navHeader.Dock = DockStyle.Top;
            navHeader.Height = 50;
            navHeader.TextAlign = ContentAlignment.MiddleLeft;
            navHeader.Padding = new Padding(8, 0, 0, 0);
            navHeader.Font = new Font(family, 14f, FontStyle.Bold, GraphicsUnit.Pixel);

            plainButton = NavItem("Обычный режим", delegate { SetScientific(false); });
            sciButton = NavItem("Инженерный режим", delegate { SetScientific(true); });

            navSection.Text = "Конвертер единиц";
            navSection.Size = new Size(176, 30);
            navSection.TextAlign = ContentAlignment.BottomLeft;
            navSection.Font = new Font(family, 12f, FontStyle.Regular, GraphicsUnit.Pixel);
            navSection.Margin = new Padding(0, 10, 0, 0);
            navList.Controls.Add(navSection);
            foreach (UnitCategory category in Units.Categories)
            {
                UnitCategory chosen = category;
                NavItem(category.Name, delegate { ShowConverter(chosen); });
            }

            navOther.Text = "Другое";
            navOther.Size = new Size(176, 30);
            navOther.TextAlign = ContentAlignment.BottomLeft;
            navOther.Font = navSection.Font;
            navOther.ForeColor = navSection.ForeColor;
            navOther.Margin = new Padding(0, 10, 0, 0);
            navList.Controls.Add(navOther);
            NavItem("Настройки…", delegate { ShowSettings(); });
            NavItem("О программе…", delegate { ShowAbout(); });

            navPanel.Controls.Add(navList);
            navPanel.Controls.Add(navHeader);
            UpdateNavState();
        }

        CalcButton NavItem(string text, EventHandler onClick)
        {
            var b = new CalcButton
            {
                Text = text,
                Kind = Kind.Function,
                LeftAlign = true,
                Size = new Size(176, 36),
                Margin = new Padding(0, 2, 0, 2),
                FontFamilyName = family
            };
            b.Click += onClick;
            buttons.Add(b);
            navList.Controls.Add(b);
            return b;
        }

        /// <summary>The window grows to the left, so the calculator and the menu button stay where they were.</summary>
        void ToggleNav()
        {
            bool show = !navPanel.Visible;
            int w = navPanel.Width;
            SuspendLayout();
            if (WindowState == FormWindowState.Normal)
            {
                // Setting MinimumSize resizes the window at once, so the order depends on the direction.
                int delta = show ? w : -w;
                var target = new Rectangle(Left - delta, Top, Width + delta, Height);
                var minimum = new Size(MinimumSize.Width + delta, MinimumSize.Height);
                if (show) { Bounds = target; MinimumSize = minimum; }
                else { MinimumSize = minimum; Bounds = target; }
            }
            navPanel.Visible = show;
            ResumeLayout(true);
            ActiveControl = null;
        }

        /// <summary>Own title bar: icon, name, pin, then minimize, maximize and close. The window keeps its frame for resizing.</summary>
        void BuildCaption()
        {
            captionBar.Dock = DockStyle.Top;
            captionBar.Height = 32;

            if (Icon != null) captionIcon.Image = Icon.ToBitmap();
            captionIcon.SizeMode = PictureBoxSizeMode.Zoom;
            captionIcon.Size = new Size(16, 16);
            captionIcon.Location = new Point(10, 8);

            captionTitle.Text = "Калькулятор";
            captionTitle.Font = new Font(family, 12f, FontStyle.Regular, GraphicsUnit.Pixel);
            captionTitle.TextAlign = ContentAlignment.MiddleLeft;
            captionTitle.Location = new Point(32, 0);
            captionTitle.Size = new Size(92, 32);

            pinToggle.Location = new Point(128, 0);
            pinToggle.Size = new Size(40, 32);

            minButton = CaptionButton("\uE921", delegate { WindowState = FormWindowState.Minimized; });
            maxButton = CaptionButton("\uE922", delegate { ToggleMaximize(); });
            closeButton = CaptionButton("\uE8BB", delegate { Close(); });

            captionBar.Controls.AddRange(new Control[] { captionIcon, captionTitle, pinToggle, minButton, maxButton, closeButton });
            captionBar.Resize += delegate { LayoutCaption(); };
            Resize += delegate { maxButton.Text = WindowState == FormWindowState.Maximized ? "\uE923" : "\uE922"; };
            foreach (Control c in new Control[] { captionBar, captionIcon, captionTitle })
            {
                c.MouseDown += DragCaption;
                c.DoubleClick += delegate { ToggleMaximize(); };
            }
            LayoutCaption();
        }

        CalcButton CaptionButton(string icon, EventHandler onClick)
        {
            var b = new CalcButton { Text = icon, Kind = Kind.Function, Size = new Size(46, 32), FontFamilyName = family };
            b.Click += onClick;
            buttons.Add(b);
            return b;
        }

        void LayoutCaption()
        {
            int w = closeButton.Width, h = captionBar.Height;
            int closeX = captionBar.Width - w;
            captionRight[0] = closeButton; captionBounds[0] = new Rectangle(closeX, 0, w, h);
            captionRight[1] = maxButton;   captionBounds[1] = new Rectangle(closeX - w, 0, w, h);
            captionRight[2] = minButton;   captionBounds[2] = new Rectangle(closeX - w * 2, 0, w, h);
            Win32Layout.Apply(captionBar, captionRight, captionBounds, 3);
        }

        void DragCaption(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
        }

        void ToggleMaximize()
        {
            WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
        }

        Size GetFrameBorder()
        {
            int dpi = DeviceDpi > 0 ? DeviceDpi : 96;
            try
            {
                int pad = GetSystemMetricsForDpi(SM_CXPADDEDBORDER, dpi);
                return new Size(
                    GetSystemMetricsForDpi(SM_CXSIZEFRAME, dpi) + pad,
                    GetSystemMetricsForDpi(SM_CYSIZEFRAME, dpi) + pad);
            }
            catch (EntryPointNotFoundException)
            {
                int pad = GetSystemMetrics(SM_CXPADDEDBORDER);
                return new Size(
                    GetSystemMetrics(SM_CXSIZEFRAME) + pad,
                    GetSystemMetrics(SM_CYSIZEFRAME) + pad);
            }
        }

        protected override Size SizeFromClientSize(Size clientSize)
        {
            Size frame = GetFrameBorder();
            return new Size(clientSize.Width + frame.Width * 2, clientSize.Height + frame.Height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (theme != null) e.Graphics.FillRectangle(PaintCache.GetBrush(theme.Back), e.ClipRectangle);
        }

        /// <summary>Leaves a 1px edge on a maximized window when the taskbar auto-hides so it can still pop up.</summary>
        void TrimAutoHideTaskbar(IntPtr lParam)
        {
            var abd = new APPBARDATA { cbSize = Marshal.SizeOf(typeof(APPBARDATA)) };
            if ((SHAppBarMessage(ABM_GETSTATE, ref abd).ToInt64() & ABS_AUTOHIDE) == 0) return;
            if (SHAppBarMessage(ABM_GETTASKBARPOS, ref abd) == IntPtr.Zero) return;

            Rectangle mon = Screen.FromHandle(Handle).Bounds;
            int left = Marshal.ReadInt32(lParam, 0), top = Marshal.ReadInt32(lParam, 4);
            int right = Marshal.ReadInt32(lParam, 8), bottom = Marshal.ReadInt32(lParam, 12);
            if (abd.uEdge == ABE_BOTTOM && bottom >= mon.Bottom) Marshal.WriteInt32(lParam, 12, bottom - 1);
            else if (abd.uEdge == ABE_TOP && top <= mon.Top) Marshal.WriteInt32(lParam, 4, top + 1);
            else if (abd.uEdge == ABE_LEFT && left <= mon.Left) Marshal.WriteInt32(lParam, 0, left + 1);
            else if (abd.uEdge == ABE_RIGHT && right >= mon.Right) Marshal.WriteInt32(lParam, 8, right - 1);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCCALCSIZE)
            {
                if (IsIconic(Handle))
                {
                    base.WndProc(ref m);
                    return;
                }

                // Both wParam == 0 (RECT*) and wParam != 0 (NCCALCSIZE_PARAMS*, starts with rgrc[0])
                // carry the proposed window rectangle in the first 16 bytes of lParam.
                int origLeft = Marshal.ReadInt32(m.LParam, 0);
                int origTop = Marshal.ReadInt32(m.LParam, 4);
                int origRight = Marshal.ReadInt32(m.LParam, 8);
                int origBottom = Marshal.ReadInt32(m.LParam, 12);
                bool resized = false;
                if (m.WParam != IntPtr.Zero)
                {
                    int oldW = Marshal.ReadInt32(m.LParam, 24) - Marshal.ReadInt32(m.LParam, 16);
                    int oldH = Marshal.ReadInt32(m.LParam, 28) - Marshal.ReadInt32(m.LParam, 20);
                    resized = origRight - origLeft != oldW || origBottom - origTop != oldH;
                }

                // Keep the native DPI-aware left, right and bottom DWM resize borders so Windows 11 Snap / Split Screen
                // and rounded corners align accurately, and strip only the native title bar from the top edge.
                base.WndProc(ref m);

                int newBottom = Marshal.ReadInt32(m.LParam, 12);
                int frameY = Math.Max(0, origBottom - newBottom);
                bool maximized = IsZoomed(Handle);
                Marshal.WriteInt32(m.LParam, 4, maximized ? origTop + frameY : origTop);
                if (maximized) TrimAutoHideTaskbar(m.LParam);

                m.Result = (m.WParam != IntPtr.Zero && resized) ? (IntPtr)WVR_REDRAW : IntPtr.Zero;
                return;
            }

            base.WndProc(ref m);

            if (m.Msg == WM_WINDOWPOSCHANGED)
            {
                // WinForms computes restored bounds using the standard caption height, which grows the window on restore.
                if (RestoredBoundsSpecifiedField != null)
                    try { RestoredBoundsSpecifiedField.SetValue(this, BoundsSpecified.None); } catch { }
                return;
            }

            if (m.Msg == WM_NCHITTEST && m.Result == (IntPtr)HTCLIENT && !IsZoomed(Handle))
            {
                Size frame = GetFrameBorder();
                int topBorder = Math.Max(5, frame.Height - 2);
                int corner = Math.Max(12, frame.Width * 2);
                long lp = m.LParam.ToInt64();
                Point p = PointToClient(new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF)));
                if (p.Y < topBorder)
                {
                    if (p.X < corner) m.Result = (IntPtr)HTTOPLEFT;
                    else if (p.X >= ClientSize.Width - corner) m.Result = (IntPtr)HTTOPRIGHT;
                    else m.Result = (IntPtr)HTTOP;
                }
            }
        }

        void UpdateNavState()
        {
            plainButton.Active = !scientific;
            sciButton.Active = scientific;
            plainButton.Invalidate();
            sciButton.Invalidate();
        }

        /// <summary>The journal button sits at the right edge of the top row; the menu button at the left.</summary>
        void LayoutTopRow()
        {
            historyToggle.Left = display.Width - historyToggle.Width - 6;
        }

        void ShowConverter(UnitCategory category)
        {
            if (converter == null || converter.IsDisposed) converter = new ConverterForm(theme, family);
            converter.SelectCategory(category);
            converter.Show(this);
            converter.BringToFront();
        }

        void ShowSettings()
        {
            using (var dialog = new SettingsForm(settings, theme, family))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                settings.ThemeMode = dialog.ThemeMode;
                settings.AutoUpdate = dialog.AutoUpdate;
                ApplyTheme();
            }
        }

        /// <summary>Called on a background thread when an update is ready; the dialog is shown on the window's thread.</summary>
        void OnUpdateReady(string version)
        {
            if (!IsHandleCreated || IsDisposed) return;
            BeginInvoke((MethodInvoker)delegate
            {
                using (var dialog = new UpdateForm(theme, family, version))
                {
                    if (dialog.ShowDialog(this) != DialogResult.Yes) return;
                    Updater.RestartAfterInstall();
                    Close();
                }
            });
        }

        void ShowAbout()
        {
            using (var dialog = new AboutForm(theme, family))
                dialog.ShowDialog(this);
        }

        void ApplyMenuTheme(ContextMenuStrip menu)
        {
            menu.Renderer = new ToolStripProfessionalRenderer(new ThemedColors(theme)) { RoundedEdges = false };
            menu.Font = new Font(family, 14f, FontStyle.Regular, GraphicsUnit.Pixel);
            menu.BackColor = theme.Dark ? Color.FromArgb(0x2C, 0x2C, 0x2C) : Color.FromArgb(0xFC, 0xFC, 0xFC);
            menu.ForeColor = theme.Text;
            foreach (ToolStripItem item in menu.Items)
            {
                item.ForeColor = theme.Text;
                var parent = item as ToolStripDropDownItem;
                if (parent == null) continue;
                parent.DropDown.Renderer = menu.Renderer;
                parent.DropDown.BackColor = menu.BackColor;
                parent.DropDown.Font = menu.Font;
                foreach (ToolStripItem sub in parent.DropDownItems) sub.ForeColor = theme.Text;
            }
        }

        static void SetClipboard(string text)
        {
            try { if (!string.IsNullOrEmpty(text)) Clipboard.SetText(text); } catch { }
        }

        void CopyDisplay() { SetClipboard(scientific ? sci.CopyText : engine.CopyText); }

        static bool ClipboardHasNumber()
        {
            try
            {
                decimal v;
                return Clipboard.ContainsText() && CalcEngine.TryParseNumber(Clipboard.GetText(), out v);
            }
            catch { return false; }
        }

        void PasteFromClipboard()
        {
            try
            {
                if (!Clipboard.ContainsText()) return;
                string text = Clipboard.GetText();
                if (scientific ? sci.Paste(text) : engine.Paste(text)) Refresh_();
            }
            catch { }
        }

        /// <summary>Keeps the captions in step with the mode: % becomes mod, 2nd swaps functions, DEG follows the unit.</summary>
        void UpdateSciLabels()
        {
            percentButton.Text = scientific ? "mod" : "%";
            sciButtons["sin"].Text = second ? "sin⁻¹" : "sin";
            sciButtons["cos"].Text = second ? "cos⁻¹" : "cos";
            sciButtons["tan"].Text = second ? "tan⁻¹" : "tan";
            sciButtons["ln"].Text = second ? "eˣ" : "ln";
            sciButtons["log"].Text = second ? "10ˣ" : "log";
            sciButtons["DEG"].Text = sci.AngleLabel;
            sciButtons["2nd"].Active = second;
            sciButtons["2nd"].Invalidate();
        }

        /// <summary>Switches between the standard and the scientific keypad; the window grows by the function panel.</summary>
        void SetScientific(bool on)
        {
            if (scientific == on) return;
            scientific = on;
            int delta = (int)Math.Round(SciPanelHeight * DeviceDpi / 96f);
            SuspendLayout();
            rootPanel.SciHeight = on ? delta : 0;
            sciPanel.Visible = on;
            if (WindowState == FormWindowState.Normal)
            {
                MinimumSize = new Size(MinimumSize.Width, MinimumSize.Height + (on ? delta : -delta));
                Height += on ? delta : -delta;
            }
            ResumeLayout(true);
            UpdateNavState();
            UpdateSciLabels();
            Refresh_();
            ActiveControl = null;
        }

        void ToggleTopMost()
        {
            TopMost = !TopMost;
            pinToggle.Active = TopMost;
            pinToggle.Invalidate();
            ActiveControl = null;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            ApplySavedBounds();
            if (settings.HistoryOpen && !historyPanel.Visible) ToggleHistory();
            if (settings.Scientific) SetScientific(true);
            Updater.Ready += OnUpdateReady;
            if (settings.AutoUpdate) Updater.CheckInBackground();
        }

        /// <summary>Restores the last window position and size, unless the position is no longer on any screen.</summary>
        void ApplySavedBounds()
        {
            if (!settings.HasBounds) return;
            float k = DeviceDpi / 96f;
            int w = Math.Max(MinimumSize.Width, (int)Math.Round(settings.Width * k));
            int h = Math.Max(MinimumSize.Height, (int)Math.Round(settings.Height * k));
            var target = new Rectangle(settings.Left, settings.Top, w, h);
            Size frame = GetFrameBorder();
            var titleBar = new Rectangle(target.Left + frame.Width, target.Top,
                Math.Max(1, target.Width - frame.Width * 2), Math.Max(1, (int)(40 * k)));
            foreach (Screen s in Screen.AllScreens)
            {
                if (!s.WorkingArea.IntersectsWith(titleBar)) continue;
                StartPosition = FormStartPosition.Manual;
                Bounds = target;
                return;
            }
        }

        // A window shown again after it was hidden may paint only part of itself until the next resize.
        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible)
            {
                Invalidate(true);
                Update();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            bool normal = WindowState == FormWindowState.Normal;
            Rectangle b = normal ? Bounds : RestoreBounds;
            int width = b.Width - (normal && historyPanel.Visible ? historyPanel.Width : 0) - (normal && navPanel.Visible ? navPanel.Width : 0);
            // The window is stored without the function panel; it is added again when the mode is restored.
            int sciExtra = normal && scientific ? rootPanel.SciHeight : 0;
            float k = 96f / DeviceDpi;
            new Settings
            {
                HasBounds = true,
                Left = b.Left,
                Top = b.Top,
                Width = (int)Math.Round(width * k),
                Height = (int)Math.Round((b.Height - sciExtra) * k),
                TopMost = TopMost,
                HistoryOpen = historyPanel.Visible,
                Scientific = scientific,
                Angle = sci.Angle,
                AutoUpdate = settings.AutoUpdate,
                ThemeMode = settings.ThemeMode
            }.Save();
        }

        void ToggleHistory()
        {
            bool show = !historyPanel.Visible;
            int w = historyPanel.Width;
            SuspendLayout();
            if (WindowState == FormWindowState.Normal)
            {
                int target = Width + (show ? w : -w);
                MinimumSize = new Size(MinimumSize.Width + (show ? w : -w), MinimumSize.Height);
                Width = target;
            }
            historyPanel.Visible = show;
            ResumeLayout(true);
            ActiveControl = null;
        }

        void SyncHistory()
        {
            if (shownHistoryVersion == engine.HistoryVersion) return;
            shownHistoryVersion = engine.HistoryVersion;
            historyList.SetItems(engine.History);
            historyList.Visible = engine.History.Count > 0;
            clearHistoryButton.Enabled = engine.History.Count > 0;
            historyPanel.Invalidate();
        }

        void ApplyListTheme()
        {
            // Dark scrollbar for the history list.
            try { SetWindowTheme(historyList.Handle, theme.Dark ? "DarkMode_Explorer" : "Explorer", null); } catch { }
        }

        static string PickFont()
        {
            string result = "Segoe UI";
            using (var fonts = new InstalledFontCollection())
                foreach (FontFamily f in fonts.Families)
                {
                    if (f.Name == "Segoe UI Variable Text") result = f.Name;
                    if (f.Name == "Segoe Fluent Icons") CalcButton.IconFont = f.Name;
                }
            return result;
        }

        void OnPrefChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category != UserPreferenceCategory.General) return;
            BeginInvoke((MethodInvoker)ApplyTheme);
        }

        void ApplyTheme()
        {
            theme = Theme.ForMode(settings.ThemeMode);
            BackColor = theme.Back;
            display.Theme = theme;
            memStrip.Theme = sciPanel.Theme = mainGrid.Theme = theme;
            foreach (CalcButton b in buttons) b.Theme = theme;
            historyPanel.BackColor = theme.Back;
            historyHeader.BackColor = theme.Back;
            historyTitle.BackColor = theme.Back;
            historyTitle.ForeColor = theme.Text;
            historyList.Theme = theme;
            historyList.BackColor = theme.Back;
            ApplyMenuTheme(displayMenu);
            ApplyMenuTheme(historyMenu);
            navPanel.BackColor = theme.Back;
            navList.BackColor = theme.Back;
            navHeader.BackColor = theme.Back;
            navHeader.ForeColor = theme.Text;
            navSection.BackColor = theme.Back;
            navSection.ForeColor = theme.Dim;
            navOther.BackColor = theme.Back;
            navOther.ForeColor = theme.Dim;
            captionBar.BackColor = theme.Back;
            captionTitle.BackColor = theme.Back;
            captionTitle.ForeColor = theme.Text;
            navList.ThumbColor = theme.Dark ? Color.FromArgb(0x4D, 0x4D, 0x4D) : Color.FromArgb(0xC1, 0xC1, 0xC1);
            navList.Invalidate();
            if (historyList.IsHandleCreated) ApplyListTheme();
            historyList.Invalidate();
            if (IsHandleCreated) SetDarkTitle();
            Invalidate(true);
        }

        void SetDarkTitle()
        {
            int v = theme.Dark ? 1 : 0;
            try
            {
                if (DwmSetWindowAttribute(Handle, 20, ref v, 4) != 0)
                    DwmSetWindowAttribute(Handle, 19, ref v, 4);
            }
            catch { }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            SetDarkTitle();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            SystemEvents.UserPreferenceChanged -= OnPrefChanged;
            base.OnFormClosed(e);
            Updater.InstallOnExit();
        }

        void Refresh_()
        {
            if (scientific) display.SetText(sci.Expression, sci.DisplayText);
            else display.SetText(engine.Expression, engine.DisplayText);
            display.SetMemory(engine.HasMemory);
            memClear.Enabled = memRecall.Enabled = engine.HasMemory;
        }

        /// <summary>Key presses in scientific mode; the keys come from the keypad and from the function panel.</summary>
        void PressSci(string key)
        {
            switch (key)
            {
                case "0": case "1": case "2": case "3": case "4":
                case "5": case "6": case "7": case "8": case "9":
                    sci.Digit(key[0]); break;
                case ",": sci.Decimal(); break;
                case "+": sci.Operator("+"); break;
                case "−": sci.Operator("−"); break;
                case "×": sci.Operator("×"); break;
                case "÷": sci.Operator("÷"); break;
                case "%": sci.Operator(" mod "); break;   // the % key is labelled "mod" in this mode
                case "xʸ": sci.Operator("^"); break;
                case "=": sci.Evaluate(); break;
                case "±": sci.Negate(); break;
                case "√x": sci.Function("√"); break;
                case "x²": sci.Square(); break;
                case "1/x": sci.Reciprocal(); break;
                case "⌫": sci.Backspace(); break;
                case "CE": case "C": sci.Clear(); break;
                case "(": sci.OpenParen(); break;
                case ")": sci.CloseParen(); break;
                case "π": sci.Constant("π"); break;
                case "e": sci.Constant("e"); break;
                case "n!": sci.Factorial(); break;
                case "sin": case "cos": case "tan": sci.Function(second ? "a" + key : key); break;
                case "ln": if (second) sci.Function("exp"); else sci.Function("ln"); break;
                case "log": if (second) sci.TenPower(); else sci.Function("log"); break;
                case "|x|": sci.Function("abs"); break;
                case "∛x": sci.Function("∛"); break;
                case "2nd": second = !second; break;
                case "DEG": sci.CycleAngle(); break;
                case "MC": engine.MemoryClear(); break;
                case "MR":
                    if (engine.HasMemory)
                    {
                        engine.MemoryRecall();   // the memory itself lives in the standard engine
                        sci.Paste(engine.CopyText);
                    }
                    break;
                case "MS": case "M+": case "M−": SciMemory(key); break;
            }
        }

        /// <summary>MS, M+ and M− in scientific mode work on the value of what has been typed so far.</summary>
        void SciMemory(string key)
        {
            double value;
            if (!sci.TryGetValue(out value)) return;
            decimal number;
            try { number = (decimal)value; }
            catch (OverflowException) { return; }   // too large for the memory

            engine.Clear();
            engine.Paste(number.ToString(CultureInfo.InvariantCulture));
            if (key == "MS") engine.MemoryStore();
            else if (key == "M+") engine.MemoryAdd();
            else engine.MemorySubtract();
        }

        void Press(string key)
        {
            if (scientific)
            {
                PressSci(key);
                if (key == "2nd" || key == "DEG") UpdateSciLabels();
                Refresh_();
                SyncHistory();
                return;
            }
            switch (key)
            {
                case "0": case "1": case "2": case "3": case "4":
                case "5": case "6": case "7": case "8": case "9":
                    engine.Digit(key[0]); break;
                case ",": engine.Decimal(); break;
                case "+": engine.Operator('+'); break;
                case "−": engine.Operator('-'); break;
                case "×": engine.Operator('*'); break;
                case "÷": engine.Operator('/'); break;
                case "=": engine.Evaluate(); break;
                case "±": engine.Negate(); break;
                case "%": engine.Percent(); break;
                case "√x": engine.Sqrt(); break;
                case "x²": engine.Square(); break;
                case "1/x": engine.Reciprocal(); break;
                case "⌫": engine.Backspace(); break;
                case "CE": engine.ClearEntry(); break;
                case "C": engine.Clear(); break;
                case "MC": engine.MemoryClear(); break;
                case "MR": engine.MemoryRecall(); break;
                case "M+": engine.MemoryAdd(); break;
                case "M−": engine.MemorySubtract(); break;
                case "MS": engine.MemoryStore(); break;
            }
            Refresh_();
            SyncHistory();
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            string k = null;
            char c = e.KeyChar;
            if (c >= '0' && c <= '9') k = c.ToString();
            else if (c == '+') k = "+";
            else if (c == '-') k = "−";
            else if (c == '*') k = "×";
            else if (c == '/') k = "÷";
            else if (c == '=' || (c == '\r' && !buttons.Exists(b => b.Focused) && !memStrip.Focused && !sciPanel.Focused && !mainGrid.Focused)) k = "=";   // Enter presses a focused key instead
            else if (c == ',' || c == '.') k = ",";
            else if (c == '%') k = "%";
            else if (c == '\b') k = "⌫";
            else if (c == (char)27) k = "C";
            else if (scientific && c == '(') k = "(";
            else if (scientific && c == ')') k = ")";
            else if (scientific && c == '^') k = "xʸ";
            else if (scientific && c == '!') k = "n!";
            else if (scientific && (c == 'p' || c == 'P')) k = "π";
            else if (scientific && (c == 'e' || c == 'E')) k = "e";
            if (k != null) { e.Handled = true; Press(k); }
            base.OnKeyPress(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete) { Press("CE"); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.H) { ToggleHistory(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.T) { ToggleTopMost(); e.Handled = true; }
            else if (e.Control && (e.KeyCode == Keys.D1 || e.KeyCode == Keys.NumPad1)) { SetScientific(false); e.Handled = true; }
            else if (e.Control && (e.KeyCode == Keys.D2 || e.KeyCode == Keys.NumPad2)) { SetScientific(true); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.L) { Press("MC"); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.R) { Press("MR"); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.P) { Press("M+"); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.Q) { Press("M−"); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.M) { Press("MS"); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.C) { CopyDisplay(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.V) { PasteFromClipboard(); e.Handled = true; }
            base.OnKeyDown(e);
        }
    }
}

