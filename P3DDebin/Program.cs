using System;
using System.Windows.Forms;
using P3DDebin.Core;

namespace P3DDebin;

static class Program
{
    [STAThread]
    static void Main()
    {
        Strings.LoadPreference();
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
