using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace Calculator
{
    /// <summary>User state kept between runs: window bounds, "always on top" and the history panel.
    /// Stored as simple key=value lines in %AppData%\Calculator\settings.ini.</summary>
    public sealed class Settings
    {
        /// <summary>Window size is stored in 96-dpi units so it survives a change of display scaling.</summary>
        public bool HasBounds;
        public int Left, Top, Width, Height;
        public bool TopMost;
        public bool HistoryOpen;
        public bool Scientific;
        public AngleMode Angle = AngleMode.Degrees;

        public static string FilePath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Calculator", "settings.ini");
            }
        }

        public static Settings Parse(string text)
        {
            var s = new Settings();
            int left = 0, top = 0, width = 0, height = 0;
            bool gotLeft = false, gotTop = false, gotWidth = false, gotHeight = false;
            if (text == null) return s;

            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                string val = line.Substring(eq + 1).Trim();
                int n;
                switch (key)
                {
                    case "left": gotLeft = TryInt(val, out left); break;
                    case "top": gotTop = TryInt(val, out top); break;
                    case "width": gotWidth = TryInt(val, out width); break;
                    case "height": gotHeight = TryInt(val, out height); break;
                    case "topmost": s.TopMost = TryInt(val, out n) && n != 0; break;
                    case "historyopen": s.HistoryOpen = TryInt(val, out n) && n != 0; break;
                    case "scientific": s.Scientific = TryInt(val, out n) && n != 0; break;
                    case "angle":
                        if (TryInt(val, out n) && Enum.IsDefined(typeof(AngleMode), n)) s.Angle = (AngleMode)n;
                        break;
                }
            }

            if (gotLeft && gotTop && gotWidth && gotHeight &&
                width >= 200 && width <= 10000 && height >= 200 && height <= 10000 &&
                Math.Abs(left) <= 100000 && Math.Abs(top) <= 100000)
            {
                s.HasBounds = true;
                s.Left = left; s.Top = top; s.Width = width; s.Height = height;
            }
            return s;
        }

        public string Serialize()
        {
            var sb = new StringBuilder();
            if (HasBounds)
            {
                sb.Append("left=").Append(Left.ToString(CultureInfo.InvariantCulture)).Append('\n');
                sb.Append("top=").Append(Top.ToString(CultureInfo.InvariantCulture)).Append('\n');
                sb.Append("width=").Append(Width.ToString(CultureInfo.InvariantCulture)).Append('\n');
                sb.Append("height=").Append(Height.ToString(CultureInfo.InvariantCulture)).Append('\n');
            }
            sb.Append("topmost=").Append(TopMost ? '1' : '0').Append('\n');
            sb.Append("historyopen=").Append(HistoryOpen ? '1' : '0').Append('\n');
            sb.Append("scientific=").Append(Scientific ? '1' : '0').Append('\n');
            sb.Append("angle=").Append((int)Angle).Append('\n');
            return sb.ToString();
        }

        public static Settings Load()
        {
            try
            {
                return File.Exists(FilePath) ? Parse(File.ReadAllText(FilePath)) : new Settings();
            }
            catch { return new Settings(); }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath, Serialize());
            }
            catch { /* settings are a convenience; never fail the app because of them */ }
        }

        static bool TryInt(string s, out int value)
        {
            return int.TryParse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
        }
    }
}
