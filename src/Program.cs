using System;
using System.Reflection;
using System.Windows.Forms;

[assembly: AssemblyTitle("Калькулятор")]
[assembly: AssemblyProduct("Калькулятор")]
[assembly: AssemblyDescription("Лёгкий калькулятор в стиле Windows 11")]
[assembly: AssemblyVersion("1.5.116.0")]
[assembly: AssemblyFileVersion("1.5.116.0")]

namespace Calculator
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
