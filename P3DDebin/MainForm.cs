using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using P3DDebin.Core;

namespace P3DDebin;

public class MainForm : Form
{
    // -------------------------------------------------------------------------
    // Controles
    // -------------------------------------------------------------------------
    private TextBox     _txtInput       = null!;
    private TextBox     _txtOutput      = null!;
    private Button      _btnInputFile   = null!;
    private Button      _btnInputFolder = null!;
    private Button      _btnOutput      = null!;
    private Button      _btnChangePaths = null!;
    private Button      _btnDebin       = null!;
    private Button      _btnRvmat       = null!;
    private Button      _btnCfg         = null!;
    private Label       _lblTitle       = null!;
    private Label       _lblSub         = null!;
    private Label       _lblInput       = null!;
    private Label       _lblOutput      = null!;
    private Label       _lblLog         = null!;
    private Label       _lblLanguage    = null!;
    private ComboBox    _cboLanguage    = null!;
    private Label       _lblFormat      = null!;
    private ProgressBar _progress       = null!;
    private RichTextBox _log            = null!;
    private Label       _lblStatus      = null!;

    private P3DFormat _format = P3DFormat.Unknown;
    private bool      _batchMode;

    private readonly Prefs _prefs = Prefs.Load();

    // -------------------------------------------------------------------------
    // Constantes de cor
    // -------------------------------------------------------------------------
    private static readonly Color BG       = Color.FromArgb(72, 72, 76);
    private static readonly Color BG_DARK  = Color.FromArgb(45, 45, 48);
    private static readonly Color BG_LIGHT = Color.FromArgb(100, 100, 104);
    private static readonly Color ACCENT   = Color.FromArgb(0, 160, 220);
    private static readonly Color TEXT     = Color.FromArgb(230, 230, 230);
    private static readonly Color TEXT_DIM = Color.FromArgb(150, 150, 150);
    private static readonly Color GREEN    = Color.FromArgb(100, 220, 100);

    public MainForm()
    {
        BuildUI();
        ApplyPrefs();
        FormClosing += (_, _) => CapturePrefs();
    }

    // -------------------------------------------------------------------------
    // Preferencias persistentes
    // -------------------------------------------------------------------------
    private void ApplyPrefs()
    {
        // Restaura ultima pasta de input/output, se ainda existem.
        // Output primeiro: SetInput so preenche output se estiver vazio.
        if (!string.IsNullOrWhiteSpace(_prefs.LastOutput) && Directory.Exists(_prefs.LastOutput))
            _txtOutput.Text = _prefs.LastOutput;

        if (!string.IsNullOrWhiteSpace(_prefs.LastInput) && PathStillUsable(_prefs.LastInput))
            SetInput(_prefs.LastInput);

        // Restaura geometria da janela, validando que ainda cabe na area visivel.
        if (_prefs.WindowWidth > 200 && _prefs.WindowHeight > 200)
        {
            var bounds = new Rectangle(
                _prefs.WindowX, _prefs.WindowY, _prefs.WindowWidth, _prefs.WindowHeight);
            if (IsBoundsVisible(bounds))
            {
                StartPosition = FormStartPosition.Manual;
                Bounds        = bounds;
            }
        }

        if (_prefs.WindowMaximized)
            WindowState = FormWindowState.Maximized;
    }

    private void CapturePrefs()
    {
        _prefs.LastInput  = _txtInput.Text.Trim();
        _prefs.LastOutput = _txtOutput.Text.Trim();

        if (WindowState == FormWindowState.Normal)
        {
            _prefs.WindowX      = Bounds.X;
            _prefs.WindowY      = Bounds.Y;
            _prefs.WindowWidth  = Bounds.Width;
            _prefs.WindowHeight = Bounds.Height;
        }
        else
        {
            _prefs.WindowX      = RestoreBounds.X;
            _prefs.WindowY      = RestoreBounds.Y;
            _prefs.WindowWidth  = RestoreBounds.Width;
            _prefs.WindowHeight = RestoreBounds.Height;
        }
        _prefs.WindowMaximized = WindowState == FormWindowState.Maximized;

        _prefs.Save();
    }

    private static bool PathStillUsable(string path)
        => File.Exists(path) || Directory.Exists(path);

    private static bool IsBoundsVisible(Rectangle bounds)
    {
        foreach (Screen s in Screen.AllScreens)
            if (s.WorkingArea.IntersectsWith(bounds))
                return true;
        return false;
    }

    // -------------------------------------------------------------------------
    // Construção da UI
    // -------------------------------------------------------------------------
    private void BuildUI()
    {
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        Size            = new Size(680, 540);
        MinimumSize     = new Size(620, 500);
        BackColor       = BG;
        ForeColor       = TEXT;
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition   = FormStartPosition.CenterScreen;
        AllowDrop       = true;
        DragEnter      += Form_DragEnter;
        DragDrop       += Form_DragDrop;

        _lblTitle = MakeLabel("", 16, bold: true);
        _lblTitle.Location  = new Point(18, 16);
        _lblTitle.ForeColor = ACCENT;

        _lblSub = MakeLabel("", 9);
        _lblSub.Location  = new Point(18, 44);
        _lblSub.ForeColor = TEXT_DIM;

        _lblLanguage = MakeLabel("", 8.5f);
        _lblLanguage.Location  = new Point(420, 20);
        _lblLanguage.ForeColor = TEXT_DIM;
        _lblLanguage.Anchor    = AnchorStyles.Right | AnchorStyles.Top;

        _cboLanguage = new ComboBox
        {
            Location      = new Point(480, 18),
            Width         = 170,
            BackColor     = BG_DARK,
            ForeColor     = TEXT,
            FlatStyle     = FlatStyle.Flat,
            Font          = new Font("Segoe UI", 8.5f),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Anchor        = AnchorStyles.Right | AnchorStyles.Top
        };
        foreach (var (lang, name) in Strings.Available)
            _cboLanguage.Items.Add(new LanguageItem(lang, name));
        _cboLanguage.SelectedIndex = Array.FindIndex(Strings.Available, x => x.Lang == Strings.Current);
        if (_cboLanguage.SelectedIndex < 0) _cboLanguage.SelectedIndex = 0;
        _cboLanguage.SelectedIndexChanged += (_, _) =>
        {
            if (_cboLanguage.SelectedItem is LanguageItem item)
                Strings.Current = item.Lang;
        };
        Strings.LanguageChanged += ApplyLanguage;

        var sep1 = new Panel { BackColor = BG_LIGHT, Height = 1, Left = 18, Top = 66, Width = 624, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };

        _lblInput = MakeLabel("", 9);
        _lblInput.Location = new Point(18, 82);

        _txtInput = MakeReadOnlyBox(18, 102, 436);

        _btnInputFile = MakeButton("", 460, 100, 90);
        _btnInputFile.Anchor = AnchorStyles.Right | AnchorStyles.Top;
        _btnInputFile.Click += BtnInputFile_Click;

        _btnInputFolder = MakeButton("", 556, 100, 94);
        _btnInputFolder.Anchor = AnchorStyles.Right | AnchorStyles.Top;
        _btnInputFolder.Click += BtnInputFolder_Click;

        _lblOutput = MakeLabel("", 9);
        _lblOutput.Location = new Point(18, 138);

        _txtOutput = MakeReadOnlyBox(18, 158, 530);
        _btnOutput = MakeButton("", 560, 156, 90);
        _btnOutput.Anchor = AnchorStyles.Right | AnchorStyles.Top;
        _btnOutput.Click += BtnOutput_Click;

        _lblFormat = MakeLabel("", 8.5f);
        _lblFormat.Location  = new Point(18, 186);
        _lblFormat.ForeColor = TEXT_DIM;

        var sep2 = new Panel { BackColor = BG_LIGHT, Height = 1, Left = 18, Top = 208, Width = 624, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };

        _btnChangePaths = MakeActionButton("", 18);
        _btnChangePaths.Click += BtnChangePaths_Click;

        _btnDebin = MakeActionButton("", 178);
        _btnDebin.Click += BtnDebin_Click;

        _btnRvmat = MakeActionButton("", 338);
        _btnRvmat.Click += BtnRvmat_Click;

        _btnCfg = MakeActionButton("", 498);
        _btnCfg.Click += BtnCfg_Click;

        // ---- Progresso ----
        _progress = new ProgressBar
        {
            Location = new Point(18, 274),
            Width    = 632,
            Height   = 6,
            Style    = ProgressBarStyle.Continuous,
            Anchor   = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
            Visible  = false
        };

        _lblLog = MakeLabel("", 9);
        _lblLog.Location = new Point(18, 288);

        _log = new RichTextBox
        {
            Location    = new Point(18, 308),
            Size        = new Size(632, 140),
            BackColor   = BG_DARK,
            ForeColor   = TEXT_DIM,
            Font        = new Font("Consolas", 8.5f),
            ReadOnly    = true,
            BorderStyle = BorderStyle.None,
            ScrollBars  = RichTextBoxScrollBars.Vertical,
            Anchor      = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom
        };

        _lblStatus = new Label
        {
            Text      = string.Empty,
            Location  = new Point(18, 462),
            AutoSize  = false,
            Width     = 632,
            Height    = 18,
            ForeColor = TEXT_DIM,
            Font      = new Font("Segoe UI", 8),
            Anchor    = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
        };

        Controls.AddRange(new Control[]
        {
            _lblTitle, _lblSub, _lblLanguage, _cboLanguage, sep1,
            _lblInput,  _txtInput,  _btnInputFile, _btnInputFolder,
            _lblOutput, _txtOutput, _btnOutput,
            _lblFormat, sep2,
            _btnChangePaths, _btnDebin, _btnRvmat, _btnCfg,
            _progress, _lblLog, _log,
            _lblStatus
        });

        ApplyLanguage();
    }

    private sealed record LanguageItem(Language Lang, string DisplayName)
    {
        public override string ToString() => DisplayName;
    }

    // Aplica os textos do idioma corrente em todos os controles estaticos.
    private void ApplyLanguage()
    {
        Text                = Strings.T("Main.Title");
        _lblTitle.Text      = "P3D Debinarizer";
        _lblSub.Text        = Strings.T("Main.Subtitle");
        _lblLanguage.Text   = Strings.T("Main.LblLanguage");
        _lblInput.Text      = Strings.T("Main.LblInput");
        _lblOutput.Text     = Strings.T("Main.LblOutput");
        _lblLog.Text        = Strings.T("Main.LblLog");

        _btnInputFile.Text   = Strings.T("Main.BtnFile");
        _btnInputFolder.Text = Strings.T("Main.BtnFolder");
        _btnOutput.Text      = Strings.T("Main.BtnSelect");

        _btnChangePaths.Text = Strings.T("Main.BtnChangePaths");
        _btnDebin.Text       = Strings.T("Main.BtnDebin");
        _btnRvmat.Text       = Strings.T("Main.BtnExtractRvmats");
        _btnCfg.Text         = Strings.T("Main.BtnExtractCfg");

        if (string.IsNullOrEmpty(_lblStatus.Text))
            _lblStatus.Text = Strings.T("Main.StatusReady");

        RefreshActions();
    }

    // -------------------------------------------------------------------------
    // Seleção de entrada / saída
    // -------------------------------------------------------------------------

    private void BtnInputFile_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title       = Strings.T("Main.DlgFileTitle"),
            Filter      = Strings.T("Main.DlgFileFilter"),
            Multiselect = false
        };

        if (dlg.ShowDialog() == DialogResult.OK)
            SetInput(dlg.FileName);
    }

    private void BtnInputFolder_Click(object? sender, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog
        {
            Description            = Strings.T("Main.DlgFolderInTitle"),
            UseDescriptionForTitle = true
        };

        if (dlg.ShowDialog() == DialogResult.OK)
            SetInput(dlg.SelectedPath);
    }

    private void BtnOutput_Click(object? sender, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog
        {
            Description            = Strings.T("Main.DlgFolderOutTitle"),
            UseDescriptionForTitle = true
        };

        if (!string.IsNullOrWhiteSpace(_txtOutput.Text))
            dlg.InitialDirectory = _txtOutput.Text;

        if (dlg.ShowDialog() != DialogResult.OK)
            return;

        _txtOutput.Text = dlg.SelectedPath;
        RefreshActions();
    }

    private void Form_DragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            e.Effect = DragDropEffects.Copy;
    }

    private void Form_DragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] paths && paths.Length > 0)
            SetInput(paths[0]);
    }

    private void SetInput(string path)
    {
        _txtInput.Text = path;

        if (Directory.Exists(path))
        {
            _batchMode = true;
            if (string.IsNullOrWhiteSpace(_txtOutput.Text))
                _txtOutput.Text = path;
        }
        else if (File.Exists(path))
        {
            _batchMode = false;
            if (string.IsNullOrWhiteSpace(_txtOutput.Text))
                _txtOutput.Text = Path.GetDirectoryName(path) ?? string.Empty;
        }

        RefreshActions();
    }

    // Detecta o formato / modo e habilita os botões correspondentes.
    private void RefreshActions()
    {
        string input = _txtInput.Text.Trim();

        if (_batchMode && Directory.Exists(input))
        {
            int count = BatchFiles().Length;
            _lblFormat.Text      = Strings.T("Main.FmtBatch", count);
            _lblFormat.ForeColor = count > 0 ? ACCENT : TEXT_DIM;

            _btnChangePaths.Enabled = count > 0;
            _btnDebin.Enabled       = count > 0;
            _btnRvmat.Enabled       = count > 0;
            _btnCfg.Enabled         = count > 0;
            return;
        }

        _format = File.Exists(input) ? Converter.DetectFormat(input) : P3DFormat.Unknown;

        switch (_format)
        {
            case P3DFormat.Odol:
                _lblFormat.Text      = Strings.T("Main.FmtOdol");
                _lblFormat.ForeColor = ACCENT;
                break;
            case P3DFormat.Mlod:
                _lblFormat.Text      = Strings.T("Main.FmtMlod");
                _lblFormat.ForeColor = GREEN;
                break;
            default:
                _lblFormat.Text      = File.Exists(input)
                    ? Strings.T("Main.FmtUnknown")
                    : Strings.T("Main.FmtNone");
                _lblFormat.ForeColor = TEXT_DIM;
                break;
        }

        bool isOdol = _format == P3DFormat.Odol;
        bool isP3d  = _format is P3DFormat.Odol or P3DFormat.Mlod;

        _btnChangePaths.Enabled = isP3d;
        _btnDebin.Enabled       = isOdol;
        _btnRvmat.Enabled       = isOdol;
        _btnCfg.Enabled         = isOdol;
    }

    private string[] BatchFiles()
    {
        try
        {
            return Directory.GetFiles(_txtInput.Text.Trim(), "*.p3d", SearchOption.AllDirectories);
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    // -------------------------------------------------------------------------
    // Ações
    // -------------------------------------------------------------------------

    private async void BtnDebin_Click(object? sender, EventArgs e)
    {
        if (!Validate(out string input, out string output))
            return;

        SetBusy(true);

        if (_batchMode)
        {
            await BatchOdol(Strings.T("Main.OpDebinarize"), input, f =>
            {
                var r = Converter.Convert(f, output, m => Log(m));
                if (!r.Success)
                    throw new InvalidOperationException(r.Message);
                Log($"  ✓ {Path.GetFileName(r.OutputPath)}");
            });
        }
        else
        {
            Log(Strings.T("Main.LogDebinarizing", Path.GetFileName(input)));
            var result = await Task.Run(() => Converter.Convert(input, output, m => Log(m)));

            if (result.Success)
            {
                Log($"✓  {Path.GetFileName(result.OutputPath)}", isSuccess: true);
                Log(Strings.T("Main.LogSavedTo", result.OutputPath));
                _lblStatus.Text = Strings.T("Main.LogConcluded", Path.GetFileName(result.OutputPath));
            }
            else
            {
                Log($"✗  {result.Message}", isError: true);
                _lblStatus.Text = Strings.T("Main.LogFailureDebin");
            }
        }

        SetBusy(false);
    }

    private async void BtnRvmat_Click(object? sender, EventArgs e)
    {
        if (!Validate(out string input, out string output))
            return;

        SetBusy(true);

        if (_batchMode)
        {
            await BatchOdol(Strings.T("Main.OpExtractRvmats"), input, f =>
            {
                int n = OdolExtract.ExtractRvmats(f, output, m => Log(m));
                Log($"  ✓ {Path.GetFileName(f)}: {n} RVMAT(s)");
            });
        }
        else
        {
            Log(Strings.T("Main.LogExtractingRvmats", Path.GetFileName(input)));
            try
            {
                var items = await Task.Run(() => OdolExtract.GetRvmats(input, m => Log(m)));
                if (items.Count == 0)
                {
                    Log(Strings.T("Main.LogNoRvmats"));
                    _lblStatus.Text = Strings.T("Main.LogStatusNoRvmats");
                }
                else
                {
                    Log(Strings.T("Main.LogRvmatsReconstructed", items.Count), isSuccess: true);
                    _lblStatus.Text = Strings.T("Main.LogStatusRvmatsPreview", items.Count);
                    using var pv = new TextPreviewForm(
                        Strings.T("Tp.TitleRvmats", Path.GetFileName(input)), items, output);
                    pv.ShowDialog(this);
                }
            }
            catch (Exception ex)
            {
                Log($"✗  {ex.Message}", isError: true);
                _lblStatus.Text = Strings.T("Main.LogFailureRvmats");
            }
        }

        SetBusy(false);
    }

    private async void BtnCfg_Click(object? sender, EventArgs e)
    {
        if (!Validate(out string input, out string output))
            return;

        SetBusy(true);

        if (_batchMode)
        {
            await BatchOdol(Strings.T("Main.OpExtractCfg"), input, f =>
            {
                string target = OdolExtract.ExtractModelCfg(f, output, m => Log(m));
                Log($"  ✓ {Path.GetFileName(target)}");
            });
        }
        else
        {
            Log(Strings.T("Main.LogExtractingCfg", Path.GetFileName(input)));
            try
            {
                var item = await Task.Run(() => OdolExtract.GetModelCfg(input, m => Log(m)));
                Log(Strings.T("Main.LogCfgReconstructed", item.Name), isSuccess: true);
                _lblStatus.Text = Strings.T("Main.LogStatusCfgPreview");
                using var pv = new TextPreviewForm(
                    Strings.T("Tp.TitleCfg", Path.GetFileName(input)),
                    new List<OdolExtract.TextItem> { item }, output);
                pv.ShowDialog(this);
            }
            catch (Exception ex)
            {
                Log($"✗  {ex.Message}", isError: true);
                _lblStatus.Text = Strings.T("Main.LogFailureCfg");
            }
        }

        SetBusy(false);
    }

    private async void BtnChangePaths_Click(object? sender, EventArgs e)
    {
        if (!Validate(out string input, out string output))
            return;

        // Lista de arquivos a oferecer na janela de troca de caminhos.
        string[] files = _batchMode ? BatchFiles() : new[] { input };
        if (files.Length == 0)
        {
            Log(Strings.T("Main.LogNoP3dFound"), isError: true);
            return;
        }

        SetBusy(true);
        Log(Strings.T("Main.LogChangePathsReading", files.Length));

        List<(string FilePath, IReadOnlyList<string> Paths)> loaded;
        try
        {
            loaded = await Task.Run(() =>
            {
                var list = new List<(string, IReadOnlyList<string>)>();
                foreach (string f in files)
                {
                    try
                    {
                        var paths = P3DRepath.ListPaths(f);
                        list.Add((f, paths));
                        Log(Strings.T("Main.LogChangePathsItem", Path.GetFileName(f), paths.Count));
                    }
                    catch (Exception ex)
                    {
                        Log($"  ✗ {Path.GetFileName(f)}: {ex.Message}", isError: true);
                    }
                }
                return list;
            });
        }
        catch (Exception ex)
        {
            Log($"✗  {ex.Message}", isError: true);
            SetBusy(false);
            return;
        }

        if (loaded.Count == 0)
        {
            Log(Strings.T("Main.LogChangePathsNoneRead"), isError: true);
            SetBusy(false);
            return;
        }

        using var dlg = new ChangePathsForm(loaded);
        if (_prefs.RecentRules.Length > 0)
        {
            var preload = new List<RepathRules.Rule>(_prefs.RecentRules.Length);
            foreach (var r in _prefs.RecentRules)
                preload.Add(new RepathRules.Rule(r.From, r.To, r.Regex));
            dlg.PreloadRules(preload);
        }

        if (dlg.ShowDialog(this) != DialogResult.OK)
        {
            Log(Strings.T("Main.LogChangePathsCancelled"));
            _lblStatus.Text = Strings.T("Main.StatusCancelled");
            SetBusy(false);
            return;
        }

        // Persiste as regras aplicadas como "recentes" para a proxima sessao.
        if (dlg.AppliedPrefixRules.Count > 0)
        {
            var arr = new Prefs.PrefRule[dlg.AppliedPrefixRules.Count];
            for (int i = 0; i < arr.Length; i++)
            {
                var r = dlg.AppliedPrefixRules[i];
                arr[i] = new Prefs.PrefRule { From = r.From, To = r.To, Regex = r.IsRegex };
            }
            _prefs.RecentRules = arr;
            _prefs.Save();
        }

        var replacements = dlg.Replacements;
        var targets = dlg.SelectedFiles;
        Log(Strings.T("Main.LogChangePathsApplying", replacements.Count, targets.Count));

        bool batch = _batchMode;
        try
        {
            await Task.Run(() =>
            {
                foreach (string file in targets)
                {
                    try
                    {
                        if (batch)
                        {
                            // Lote: gera novo arquivo com sufixo _repath ao lado do original.
                            string dir = Path.GetDirectoryName(file) ?? string.Empty;
                            string newName = Path.GetFileNameWithoutExtension(file) + "_repath.p3d";
                            string newPath = Path.Combine(dir, newName);
                            var r = P3DRepath.Apply(file, newPath, replacements);
                            Log(Strings.T("Main.LogChangePathsBatchOk", Path.GetFileName(newPath), r.Sites));
                        }
                        else
                        {
                            // Único: grava como <nome>_repath.p3d na pasta de saída.
                            string copyName = Path.GetFileNameWithoutExtension(file) + "_repath.p3d";
                            string copyPath = Path.Combine(output, copyName);
                            var r = P3DRepath.Apply(file, copyPath, replacements);
                            Log(Strings.T("Main.LogChangePathsSingleOk", copyName, r.Sites));
                            Log(Strings.T("Main.LogSavedTo", r.OutputPath));
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"  ✗ {Path.GetFileName(file)}: {ex.Message}", isError: true);
                    }
                }
            });

            Log(Strings.T("Main.LogChangePathsDone", targets.Count), isSuccess: true);
            _lblStatus.Text = Strings.T("Main.StatusChangePathsDone", targets.Count);
        }
        catch (Exception ex)
        {
            Log($"✗  {ex.Message}", isError: true);
            _lblStatus.Text = Strings.T("Main.LogFailureChangePaths");
        }

        SetBusy(false);
    }

    // -------------------------------------------------------------------------
    // Lote
    // -------------------------------------------------------------------------

    // Executa uma operação restrita a arquivos ODOL sobre todos os .p3d da pasta.
    private async Task BatchOdol(string opName, string folder, Action<string> perFile)
    {
        string[] files = BatchFiles();
        Log(Strings.T("Main.LogBatchHeader", opName, files.Length, folder));

        int ok = 0, skipped = 0, failed = 0;

        await Task.Run(() =>
        {
            foreach (string file in files)
            {
                if (Converter.DetectFormat(file) != P3DFormat.Odol)
                {
                    skipped++;
                    Log(Strings.T("Main.LogBatchSkipNonOdol", Path.GetFileName(file)));
                    continue;
                }

                try
                {
                    perFile(file);
                    ok++;
                }
                catch (Exception ex)
                {
                    failed++;
                    Log($"  ✗ {Path.GetFileName(file)}: {ex.Message}", isError: true);
                }
            }
        });

        Log(Strings.T("Main.LogBatchSummary", opName, ok, skipped, failed),
            isError: failed > 0, isSuccess: failed == 0);
        _lblStatus.Text = Strings.T("Main.StatusBatchSummary", opName, ok, skipped, failed);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private bool Validate(out string input, out string output)
    {
        input  = _txtInput.Text.Trim();
        output = _txtOutput.Text.Trim();

        bool inputOk = _batchMode ? Directory.Exists(input) : File.Exists(input);
        if (!inputOk)
        {
            Log(Strings.T("Main.LogInputNotFound", input), isError: true);
            return false;
        }
        if (!Directory.Exists(output))
        {
            Log(Strings.T("Main.LogOutputNotFound", output), isError: true);
            return false;
        }
        return true;
    }

    private void SetBusy(bool busy)
    {
        _btnInputFile.Enabled   = !busy;
        _btnInputFolder.Enabled = !busy;
        _btnOutput.Enabled      = !busy;
        _progress.Visible       = busy;
        _progress.Style         = busy ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;

        if (busy)
        {
            _btnChangePaths.Enabled = false;
            _btnDebin.Enabled       = false;
            _btnRvmat.Enabled       = false;
            _btnCfg.Enabled         = false;
            _lblStatus.Text         = Strings.T("Main.StatusBusy");
        }
        else
        {
            RefreshActions();
        }
    }

    private void Log(string message, bool isError = false, bool isSuccess = false)
    {
        if (InvokeRequired) { Invoke(() => Log(message, isError, isSuccess)); return; }

        string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        _log.SelectionStart  = _log.TextLength;
        _log.SelectionLength = 0;
        _log.SelectionColor  = isError   ? Color.FromArgb(255, 100, 100)
                             : isSuccess ? GREEN
                             : TEXT_DIM;
        _log.AppendText(line + Environment.NewLine);
        _log.ScrollToCaret();
    }

    private Label MakeLabel(string text, float fontSize, bool bold = false) => new()
    {
        Text      = text,
        ForeColor = TEXT,
        BackColor = Color.Transparent,
        Font      = new Font("Segoe UI", fontSize, bold ? FontStyle.Bold : FontStyle.Regular),
        AutoSize  = true
    };

    private TextBox MakeReadOnlyBox(int x, int y, int width) => new()
    {
        Location    = new Point(x, y),
        Width       = width,
        BackColor   = BG_DARK,
        ForeColor   = TEXT,
        BorderStyle = BorderStyle.FixedSingle,
        Font        = new Font("Segoe UI", 9),
        ReadOnly    = true,
        Cursor      = Cursors.Arrow,
        Anchor      = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
    };

    private Button MakeButton(string text, int x, int y, int width) => new()
    {
        Text      = text,
        Location  = new Point(x, y),
        Size      = new Size(width, 26),
        BackColor = BG_LIGHT,
        ForeColor = TEXT,
        FlatStyle = FlatStyle.Flat,
        Font      = new Font("Segoe UI", 8.5f),
        Cursor    = Cursors.Hand
    };

    private Button MakeActionButton(string text, int x)
    {
        var btn = new Button
        {
            Text      = text,
            Location  = new Point(x, 222),
            Size      = new Size(152, 40),
            BackColor = ACCENT,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Cursor    = Cursors.Hand,
            Anchor    = AnchorStyles.Left | AnchorStyles.Top
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.EnabledChanged += (_, _) => btn.BackColor = btn.Enabled ? ACCENT : BG_LIGHT;
        btn.MouseEnter += (_, _) => { if (btn.Enabled) btn.BackColor = Color.FromArgb(0, 140, 200); };
        btn.MouseLeave += (_, _) => { if (btn.Enabled) btn.BackColor = ACCENT; };
        return btn;
    }
}
