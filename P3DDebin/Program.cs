using System;
using System.Windows.Forms;
using P3DDebin.Core;

namespace P3DDebin;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        // Any argument? Run the headless CLI; otherwise launch the GUI.
        if (args.Length > 0)
            return Cli.Run(args);

        Strings.LoadPreference();
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
        return 0;
    }
}
