using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Calculator
{
    enum Kind { Number, Function, Equals }

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

    /// <summary>Flat rounded button that never takes keyboard focus.</summary>
    sealed class CalcButton : Control
    {
        public Kind Kind;
        public Theme Theme;
        public string FontFamilyName = "Segoe UI";
        bool hover, pressed;

        public CalcButton()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.StandardClick | ControlStyles.StandardDoubleClick, true);
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
            Cursor = Cursors.Default;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { pressed = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Back);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            Color fill, fore = Theme.Text;
            switch (Kind)
            {
                case Kind.Equals:
                    fill = pressed ? Theme.EqPress : hover ? Theme.EqHover : Theme.EqNormal;
                    fore = Theme.EqText;
                    break;
                case Kind.Number:
                    fill = pressed ? Theme.NumPress : hover ? Theme.NumHover : Theme.NumNormal;
                    break;
                default:
                    fill = pressed ? Theme.FnPress : hover ? Theme.FnHover : Theme.FnNormal;
                    break;
            }
            if (pressed && Kind != Kind.Equals) fore = Theme.Dim;

            int r = (int)(Math.Min(Width, Height) * 0.11f) + 2;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Round(rect, r))
            using (var br = new SolidBrush(fill))
            {
                g.FillPath(br, path);
                if (!Theme.Dark && Kind != Kind.Equals)
                    using (var pen = new Pen(Theme.Border)) g.DrawPath(pen, path);
            }

            float px = Math.Max(10f, Math.Min(Height * 0.36f, Width * 0.30f));
            FontStyle style = Kind == Kind.Number ? FontStyle.Bold : FontStyle.Regular;
            string family = Text == "⌫" ? "Segoe UI Symbol" : FontFamilyName;
            using (var f = new Font(family, px, style, GraphicsUnit.Pixel))
                TextRenderer.DrawText(g, Text, f, ClientRectangle, fore,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
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

        public DisplayBox()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
        }

        public void SetText(string expression, string val)
        {
            expr = expression;
            value = val;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Back);
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            const TextFormatFlags flags = TextFormatFlags.Right | TextFormatFlags.NoPadding |
                                          TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
            int pad = Math.Max(8, Width / 28);
            int avail = Width - pad * 2;

            float small = Math.Max(11f, Height * 0.13f);
            using (var f = new Font(FontFamilyName, small, FontStyle.Regular, GraphicsUnit.Pixel))
                TextRenderer.DrawText(g, expr, f, new Rectangle(pad, Height / 10, avail, (int)(small * 1.5f)),
                    Theme.Dim, flags | TextFormatFlags.Top);

            float big = Math.Max(14f, Height * 0.42f);
            Font fv = new Font(FontFamilyName, big, FontStyle.Bold, GraphicsUnit.Pixel);
            while (big > 14f && TextRenderer.MeasureText(g, value, fv, new Size(int.MaxValue, int.MaxValue),
                       TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width > avail)
            {
                big -= 2f;
                fv.Dispose();
                fv = new Font(FontFamilyName, big, FontStyle.Bold, GraphicsUnit.Pixel);
            }
            using (fv)
                TextRenderer.DrawText(g, value, fv, new Rectangle(pad, 0, avail, Height - Height / 12),
                    Theme.Text, flags | TextFormatFlags.Bottom);
        }
    }

    sealed class MainForm : Form
    {
        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        readonly CalcEngine engine = new CalcEngine();
        readonly DisplayBox display = new DisplayBox();
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

        public MainForm()
        {
            family = PickFont();
            theme = Theme.FromSystem();

            Text = "Калькулятор";
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(340, 520);
            MinimumSize = new Size(300, 430);
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;
            DoubleBuffered = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(4, 0, 4, 4)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 21f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 79f));

            display.Dock = DockStyle.Fill;
            display.FontFamilyName = family;
            root.Controls.Add(display, 0, 0);

            var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 6, Margin = new Padding(0) };
            for (int c = 0; c < 4; c++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            for (int r = 0; r < 6; r++) grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / 6f));

            for (int r = 0; r < 6; r++)
                for (int c = 0; c < 4; c++)
                {
                    string label = KeyLayout[r, c];
                    if (label == ",") label = CalcEngine.DecimalSeparator;
                    var b = new CalcButton
                    {
                        Text = label,
                        Dock = DockStyle.Fill,
                        Margin = new Padding(2),
                        FontFamilyName = family,
                        Kind = label == "=" ? Kind.Equals
                             : (r >= 2 && c < 3 && r < 5) || (r == 5 && c == 1) || (r == 5 && c == 2) ? Kind.Number
                             : Kind.Function
                    };
                    string key = KeyLayout[r, c];
                    b.Click += delegate { Press(key); };
                    buttons.Add(b);
                    grid.Controls.Add(b, c, r);
                }
            root.Controls.Add(grid, 0, 1);
            Controls.Add(root);

            ApplyTheme();
            Refresh_();
            SystemEvents.UserPreferenceChanged += OnPrefChanged;
        }

        static string PickFont()
        {
            using (var fonts = new InstalledFontCollection())
                foreach (FontFamily f in fonts.Families)
                    if (f.Name == "Segoe UI Variable Text") return f.Name;
            return "Segoe UI";
        }

        void OnPrefChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category != UserPreferenceCategory.General) return;
            BeginInvoke((MethodInvoker)ApplyTheme);
        }

        void ApplyTheme()
        {
            theme = Theme.FromSystem();
            BackColor = theme.Back;
            display.Theme = theme;
            foreach (CalcButton b in buttons) b.Theme = theme;
            if (IsHandleCreated) SetDarkTitle();
            Invalidate(true);
        }

        void SetDarkTitle()
        {
            int v = theme.Dark ? 1 : 0;
            try { DwmSetWindowAttribute(Handle, 20, ref v, 4); } catch { }
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
        }

        void Refresh_() { display.SetText(engine.Expression, engine.DisplayText); }

        void Press(string key)
        {
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
            }
            Refresh_();
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
            else if (c == '=' || c == '\r') k = "=";
            else if (c == ',' || c == '.') k = ",";
            else if (c == '%') k = "%";
            else if (c == '\b') k = "⌫";
            else if (c == (char)27) k = "C";
            if (k != null) { e.Handled = true; Press(k); }
            base.OnKeyPress(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete) { Press("CE"); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.C)
            {
                try { Clipboard.SetText(engine.DisplayText.Replace(" ", "")); } catch { }
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }
    }
}

