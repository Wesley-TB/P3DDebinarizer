using System;
using System.Windows.Forms;
using P3DDebin.Core;

namespace P3DDebin;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        // Carrega o idioma salvo em ambos os modos para que mensagens de log
        // (compartilhadas entre GUI e CLI) usem o idioma escolhido.
        Strings.LoadPreference();

        // Any argument? Run the headless CLI; otherwise launch the GUI.
        if (args.Length > 0)
            return Cli.Run(args);

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
        return 0;
    }
}
