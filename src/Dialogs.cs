using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace Calculator
{
    /// <summary>Program name, version, license and a link to the project on GitHub.</summary>
    sealed class AboutForm : Form
    {
        const string ProjectUrl = "https://github.com/danilneibet0-oss/calculator";

        public AboutForm(Theme theme, string family)
        {
            Text = "О программе";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(320, 190);
            BackColor = theme.Back;
            ForeColor = theme.Text;
            Font = new Font(family, 10f, FontStyle.Regular, GraphicsUnit.Point);

            var version = typeof(AboutForm).Assembly.GetName().Version;
            var title = new Label { Text = "Калькулятор", Font = new Font(family, 16f, FontStyle.Bold, GraphicsUnit.Point), Location = new Point(20, 16), AutoSize = true };
            var versionLabel = new Label { Text = string.Format("Версия {0}.{1}.{2}", version.Major, version.Minor, version.Build), Location = new Point(22, 58), AutoSize = true };
            var license = new Label { Text = "Лицензия: MIT", Location = new Point(22, 82), AutoSize = true };

            var link = new LinkLabel
            {
                Text = "Исходный код на GitHub",
                Location = new Point(22, 110),
                AutoSize = true,
                LinkColor = theme.EqNormal,
                ActiveLinkColor = theme.EqHover,
                VisitedLinkColor = theme.EqNormal
            };
            link.LinkClicked += delegate { OpenProject(); };

            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(206, 146), Size = new Size(90, 30) };
            AcceptButton = ok;

            Controls.AddRange(new Control[] { title, versionLabel, license, link, ok });
        }

        static void OpenProject()
        {
            try { Process.Start(ProjectUrl); }
            catch { }
        }
    }

    /// <summary>Theme and update settings. The caller applies the values from the properties after OK.</summary>
    sealed class SettingsForm : Form
    {
        readonly ComboBox themeBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(20, 46), Width = 280 };
        readonly CheckBox autoUpdate = new CheckBox { Text = "Проверять обновления при запуске", Location = new Point(20, 100), AutoSize = true };

        /// <summary>0 follows Windows, 1 light, 2 dark (the order of the list).</summary>
        public int ThemeMode { get { return themeBox.SelectedIndex; } }
        public bool AutoUpdate { get { return autoUpdate.Checked; } }

        public SettingsForm(Settings current, Theme theme, string family)
        {
            Text = "Настройки";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(320, 170);
            BackColor = theme.Back;
            ForeColor = theme.Text;
            Font = new Font(family, 10f, FontStyle.Regular, GraphicsUnit.Point);

            var themeLabel = new Label { Text = "Тема оформления", Location = new Point(20, 20), AutoSize = true };
            themeBox.Items.AddRange(new object[] { "Как в Windows", "Светлая", "Тёмная" });
            themeBox.SelectedIndex = current.ThemeMode;
            autoUpdate.Checked = current.AutoUpdate;

            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(126, 130), Size = new Size(84, 30) };
            var cancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel, Location = new Point(216, 130), Size = new Size(84, 30) };
            AcceptButton = ok;
            CancelButton = cancel;

            Controls.AddRange(new Control[] { themeLabel, themeBox, autoUpdate, ok, cancel });
        }
    }

    /// <summary>Converts a number between units of one category. Recalculates on every change.</summary>
    sealed class ConverterForm : Form
    {
        readonly ComboBox categoryBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(16, 16), Width = 288 };
        readonly TextBox input = new TextBox { Location = new Point(16, 56), Width = 150, Text = "1" };
        readonly ComboBox fromBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(172, 56), Width = 132 };
        readonly Label result = new Label { Location = new Point(16, 112), Width = 150, Height = 34 };
        readonly ComboBox toBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(172, 116), Width = 132 };
        bool loading;

        public ConverterForm(Theme theme, string family)
        {
            Text = "Конвертер единиц";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(320, 170);
            BackColor = theme.Back;
            ForeColor = theme.Text;
            Font = new Font(family, 10f, FontStyle.Regular, GraphicsUnit.Point);

            result.Font = new Font(family, 16f, FontStyle.Bold, GraphicsUnit.Point);
            result.TextAlign = ContentAlignment.MiddleLeft;

            foreach (UnitCategory c in Units.Categories) categoryBox.Items.Add(c);
            categoryBox.SelectedIndexChanged += delegate { LoadUnits(); };
            fromBox.SelectedIndexChanged += delegate { Recalculate(); };
            toBox.SelectedIndexChanged += delegate { Recalculate(); };
            input.TextChanged += delegate { Recalculate(); };

            Controls.AddRange(new Control[] { categoryBox, input, fromBox, result, toBox });
            categoryBox.SelectedIndex = 0;
        }

        public void SelectCategory(UnitCategory category)
        {
            categoryBox.SelectedItem = category;
        }

        void LoadUnits()
        {
            var category = categoryBox.SelectedItem as UnitCategory;
            if (category == null) return;
            loading = true;
            fromBox.Items.Clear();
            toBox.Items.Clear();
            foreach (Unit u in category.Units)
            {
                fromBox.Items.Add(u);
                toBox.Items.Add(u);
            }
            fromBox.SelectedIndex = 0;
            toBox.SelectedIndex = Math.Min(1, category.Units.Length - 1);
            loading = false;
            Recalculate();
        }

        void Recalculate()
        {
            if (loading) return;
            var from = fromBox.SelectedItem as Unit;
            var to = toBox.SelectedItem as Unit;
            decimal typed;
            if (from == null || to == null || !CalcEngine.TryParseNumber(input.Text, out typed))
            {
                result.Text = "";
                return;
            }
            double value = Units.Convert((double)typed, from, to);
            result.Text = Expr.Format(value).Replace(".", CalcEngine.DecimalSeparator);
        }
    }
}
