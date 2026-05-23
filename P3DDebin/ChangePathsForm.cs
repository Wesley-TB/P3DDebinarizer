using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using P3DDebin.Core;

namespace P3DDebin;

// Janela unificada de troca de caminhos:
//  - lista os .p3d carregados (com checkbox para incluir individualmente ou em conjunto);
//  - mostra abaixo a uniao dos caminhos internos dos arquivos marcados;
//  - permite editar caminho a caminho, trocar trechos (prefixo) e importar/exportar regras;
//  - duplo clique em um caminho copia para a area de transferencia (Ctrl+C tambem funciona).
public class ChangePathsForm : Form
{
    private static readonly Color BG       = Color.FromArgb(72, 72, 76);
    private static readonly Color BG_DARK  = Color.FromArgb(45, 45, 48);
    private static readonly Color BG_LIGHT = Color.FromArgb(100, 100, 104);
    private static readonly Color ACCENT   = Color.FromArgb(0, 160, 220);
    private static readonly Color TEXT     = Color.FromArgb(230, 230, 230);
    private static readonly Color TEXT_DIM = Color.FromArgb(150, 150, 150);
    private static readonly Color GREEN    = Color.FromArgb(100, 220, 100);

    private sealed record FileEntry(string FullPath, IReadOnlyList<string> Paths)
    {
        public override string ToString() => Path.GetFileName(FullPath);
    }

    private readonly List<FileEntry> _files;

    // Edicoes ativas: caminho original -> caminho novo (apenas quando difere).
    private readonly Dictionary<string, string> _edits =
        new(StringComparer.Ordinal);

    // Regras de prefixo aplicadas (para export/import reutilizavel).
    private readonly List<(string From, string To)> _prefixRules = new();

    private CheckedListBox _filesList = null!;
    private DataGridView   _grid      = null!;
    private TextBox        _txtFrom   = null!;
    private TextBox        _txtTo     = null!;
    private Label          _lblStatus = null!;
    private Label          _lblPaths  = null!;
    private bool           _suspend;

    // Resultados expostos para o MainForm:
    public List<(string From, string To)> Replacements { get; } = new();
    public List<string>                   SelectedFiles { get; } = new();

    public ChangePathsForm(IEnumerable<(string FilePath, IReadOnlyList<string> Paths)> files)
    {
        _files = files.Select(f => new FileEntry(f.FilePath, f.Paths)).ToList();
        BuildUI();
        PopulateFilesList();
        RebuildGrid();
    }

    // -------------------------------------------------------------------------
    // UI
    // -------------------------------------------------------------------------
    private void BuildUI()
    {
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        Text          = Strings.T("Cp.Title");
        Size          = new Size(880, 760);
        MinimumSize   = new Size(720, 580);
        BackColor     = BG;
        ForeColor     = TEXT;
        StartPosition = FormStartPosition.CenterParent;
        KeyPreview    = true;

        var lblInfo = new Label
        {
            Text      = Strings.T("Cp.Info"),
            Location  = new Point(12, 8),
            AutoSize  = true,
            ForeColor = TEXT_DIM,
            Font      = new Font("Segoe UI", 8.5f)
        };

        var lblFiles = new Label
        {
            Text      = Strings.T("Cp.LblFiles"),
            Location  = new Point(12, 30),
            AutoSize  = true,
            ForeColor = TEXT,
            Font      = new Font("Segoe UI", 9, FontStyle.Bold)
        };

        _filesList = new CheckedListBox
        {
            Location          = new Point(12, 52),
            Size              = new Size(708, 110),
            BackColor         = BG_DARK,
            ForeColor         = TEXT,
            BorderStyle       = BorderStyle.FixedSingle,
            Font              = new Font("Consolas", 8.5f),
            CheckOnClick      = true,
            IntegralHeight    = false,
            Anchor            = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
        };
        _filesList.ItemCheck += FilesList_ItemCheck;

        var btnAll = MakeButton(Strings.T("Cp.BtnAll"), 730, 52, 120);
        btnAll.Anchor = AnchorStyles.Right | AnchorStyles.Top;
        btnAll.Click += (_, _) => SetAllChecked(true);

        var btnNone = MakeButton(Strings.T("Cp.BtnNone"), 730, 84, 120);
        btnNone.Anchor = AnchorStyles.Right | AnchorStyles.Top;
        btnNone.Click += (_, _) => SetAllChecked(false);

        var lblFilesHint = new Label
        {
            Text      = Strings.T("Cp.LblFilesHint"),
            Location  = new Point(12, 164),
            AutoSize  = true,
            ForeColor = TEXT_DIM,
            Font      = new Font("Segoe UI", 8)
        };

        // ---- Lista de caminhos ----
        _lblPaths = new Label
        {
            Text      = Strings.T("Cp.LblPathsHeader"),
            Location  = new Point(12, 188),
            AutoSize  = true,
            ForeColor = TEXT,
            Font      = new Font("Segoe UI", 9, FontStyle.Bold)
        };

        _grid = new DataGridView
        {
            Location              = new Point(12, 210),
            Size                  = new Size(838, 360),
            BackgroundColor       = BG_DARK,
            BorderStyle           = BorderStyle.None,
            GridColor             = BG_LIGHT,
            AllowUserToAddRows    = false,
            AllowUserToDeleteRows = false,
            RowHeadersVisible     = false,
            SelectionMode         = DataGridViewSelectionMode.CellSelect,
            AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.Fill,
            ClipboardCopyMode     = DataGridViewClipboardCopyMode.EnableWithoutHeaderText,
            Font                  = new Font("Consolas", 8.5f),
            Anchor                = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom
        };
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersDefaultCellStyle.BackColor = BG_LIGHT;
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = TEXT;
        _grid.DefaultCellStyle.BackColor          = BG_DARK;
        _grid.DefaultCellStyle.ForeColor          = TEXT;
        _grid.DefaultCellStyle.SelectionBackColor = ACCENT;
        _grid.DefaultCellStyle.SelectionForeColor = Color.White;

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "atual", HeaderText = Strings.T("Cp.ColCurrent"), ReadOnly = true, FillWeight = 50
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "novo",  HeaderText = Strings.T("Cp.ColNew"),                    FillWeight = 50
        });

        _grid.CellDoubleClick += Grid_CellDoubleClick;
        _grid.CellValueChanged += Grid_CellValueChanged;

        // ---- Trecho ----
        var lblFrom = MakeLabel(Strings.T("Cp.LblFrom"), 12, 584);
        _txtFrom = MakeTextBox(112, 582, 280);
        var lblArrow = MakeLabel("->", 400, 584);
        _txtTo = MakeTextBox(428, 582, 280);

        var btnApplyPart = MakeButton(Strings.T("Cp.BtnApplyPart"), 720, 581, 130);
        btnApplyPart.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
        btnApplyPart.Click += BtnApplyPart_Click;

        var lblHint = new Label
        {
            Text      = Strings.T("Cp.Hint"),
            Location  = new Point(12, 610),
            Size      = new Size(840, 16),
            ForeColor = TEXT_DIM,
            Font      = new Font("Segoe UI", 8),
            Anchor    = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
        };

        _lblStatus = new Label
        {
            Text      = string.Empty,
            Location  = new Point(12, 632),
            Size      = new Size(560, 16),
            ForeColor = TEXT_DIM,
            Font      = new Font("Segoe UI", 8),
            Anchor    = AnchorStyles.Left | AnchorStyles.Bottom
        };

        var btnImport = MakeButton(Strings.T("Cp.BtnImport"), 12, 656, 130);
        btnImport.Click += BtnImport_Click;
        btnImport.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;

        var btnExport = MakeButton(Strings.T("Cp.BtnExport"), 148, 656, 130);
        btnExport.Click += BtnExport_Click;
        btnExport.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;

        var btnOk = MakeButton(Strings.T("Cp.BtnOk"), 648, 656, 140);
        btnOk.BackColor = ACCENT;
        btnOk.ForeColor = Color.White;
        btnOk.Click += BtnOk_Click;
        btnOk.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;

        var btnCancel = MakeButton(Strings.T("Cp.BtnCancel"), 792, 656, 70);
        btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        btnCancel.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;

        Controls.AddRange(new Control[]
        {
            lblInfo,
            lblFiles, _filesList, btnAll, btnNone, lblFilesHint,
            _lblPaths, _grid,
            lblFrom, _txtFrom, lblArrow, _txtTo, btnApplyPart,
            lblHint, _lblStatus,
            btnImport, btnExport, btnOk, btnCancel
        });
    }

    private void PopulateFilesList()
    {
        _suspend = true;
        foreach (FileEntry f in _files)
            _filesList.Items.Add(f, isChecked: true);
        _suspend = false;
    }

    // -------------------------------------------------------------------------
    // Eventos de lista de arquivos
    // -------------------------------------------------------------------------
    private void FilesList_ItemCheck(object? sender, ItemCheckEventArgs e)
    {
        if (_suspend) return;
        // ItemCheck dispara antes do estado mudar; reagenda para depois.
        BeginInvoke(new Action(RebuildGrid));
    }

    private void SetAllChecked(bool value)
    {
        _suspend = true;
        for (int i = 0; i < _filesList.Items.Count; i++)
            _filesList.SetItemChecked(i, value);
        _suspend = false;
        RebuildGrid();
    }

    private IEnumerable<FileEntry> CheckedEntries()
    {
        for (int i = 0; i < _filesList.Items.Count; i++)
            if (_filesList.GetItemChecked(i))
                yield return _files[i];
    }

    // -------------------------------------------------------------------------
    // Reconstrucao da grade
    // -------------------------------------------------------------------------
    private void RebuildGrid()
    {
        _grid.EndEdit();

        var union = new SortedSet<string>(StringComparer.Ordinal);
        foreach (FileEntry entry in CheckedEntries())
            foreach (string p in entry.Paths)
                union.Add(p);

        _grid.SuspendLayout();
        _grid.Rows.Clear();

        foreach (string path in union)
        {
            string novo = _edits.TryGetValue(path, out string? edited) ? edited : path;
            _grid.Rows.Add(path, novo);
        }
        _grid.ResumeLayout();

        _lblPaths.Text = Strings.T("Cp.LblPathsHeaderCount", union.Count);
    }

    // -------------------------------------------------------------------------
    // Eventos da grade
    // -------------------------------------------------------------------------
    private void Grid_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0)
            return;

        string? value = _grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value as string;
        if (string.IsNullOrEmpty(value))
            return;

        try
        {
            Clipboard.SetText(value);
            FlashStatus(Strings.T("Cp.MsgCopied", value));
        }
        catch
        {
            // Clipboard pode falhar momentaneamente; ignora.
        }
    }

    private void Grid_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex != 1)
            return;

        string atual = (string?)_grid.Rows[e.RowIndex].Cells[0].Value ?? string.Empty;
        string novo  = ((string?)_grid.Rows[e.RowIndex].Cells[1].Value ?? string.Empty).Trim();

        if (novo.Length == 0 || string.Equals(novo, atual, StringComparison.Ordinal))
            _edits.Remove(atual);
        else
            _edits[atual] = novo;
    }

    // -------------------------------------------------------------------------
    // Trecho / Import / Export
    // -------------------------------------------------------------------------
    private void BtnApplyPart_Click(object? sender, EventArgs e)
    {
        string from = _txtFrom.Text.Trim().TrimStart('\\');
        string to   = _txtTo.Text.Trim().TrimStart('\\');

        if (from.Length == 0)
        {
            MessageBox.Show(this, Strings.T("Cp.MsgNeedFrom"), Text);
            return;
        }

        int affected = ApplyPrefixRule(from, to);
        _prefixRules.Add((from, to));

        FlashStatus(Strings.T("Cp.MsgSegmentApplied", affected));
    }

    private int ApplyPrefixRule(string from, string to)
    {
        int affected = 0;
        foreach (DataGridViewRow row in _grid.Rows)
        {
            string atual = (string?)row.Cells[0].Value ?? string.Empty;
            string current = (string?)row.Cells[1].Value ?? string.Empty;

            if (current.StartsWith(from, StringComparison.OrdinalIgnoreCase))
            {
                string updated = to + current[from.Length..];
                row.Cells[1].Value = updated;
                _edits[atual] = updated; // CellValueChanged tambem dispara, mas garantimos aqui
                affected++;
            }
        }
        return affected;
    }

    private void BtnImport_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title  = Strings.T("Cp.DlgImportTitle"),
            Filter = Strings.T("Cp.DlgListFilter")
        };
        if (dlg.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            var rules = RepathRules.Load(dlg.FileName);
            int total = 0;
            foreach (var (from, to) in rules)
            {
                total += ApplyPrefixRule(from, to);
                _prefixRules.Add((from, to));
            }
            FlashStatus(Strings.T("Cp.MsgImported", rules.Count, total));
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, Strings.T("Cp.MsgImportFailed", ex.Message), Text);
        }
    }

    private void BtnExport_Click(object? sender, EventArgs e)
    {
        if (_prefixRules.Count == 0)
        {
            MessageBox.Show(this, Strings.T("Cp.MsgNoRulesToExport"), Text);
            return;
        }

        using var dlg = new SaveFileDialog
        {
            Title    = Strings.T("Cp.DlgExportTitle"),
            Filter   = Strings.T("Cp.DlgExportFilter"),
            FileName = Strings.T("Cp.DlgExportDefault")
        };
        if (dlg.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            RepathRules.Save(dlg.FileName, _prefixRules);
            FlashStatus(Strings.T("Cp.MsgExported", _prefixRules.Count));
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, Strings.T("Cp.MsgExportFailed", ex.Message), Text);
        }
    }

    // -------------------------------------------------------------------------
    // OK / Cancel
    // -------------------------------------------------------------------------
    private void BtnOk_Click(object? sender, EventArgs e)
    {
        _grid.EndEdit();

        SelectedFiles.Clear();
        foreach (FileEntry entry in CheckedEntries())
            SelectedFiles.Add(entry.FullPath);

        if (SelectedFiles.Count == 0)
        {
            MessageBox.Show(this, Strings.T("Cp.MsgNoFileChecked"), Text);
            return;
        }

        Replacements.Clear();
        foreach (DataGridViewRow row in _grid.Rows)
        {
            string atual = (string?)row.Cells[0].Value ?? string.Empty;
            string novo  = ((string?)row.Cells[1].Value ?? string.Empty).Trim();

            if (novo.Length == 0 || string.Equals(novo, atual, StringComparison.Ordinal))
                continue;

            Replacements.Add((atual, novo));
        }

        if (Replacements.Count == 0)
        {
            MessageBox.Show(this, Strings.T("Cp.MsgNoChange"), Text);
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    // -------------------------------------------------------------------------
    // Status temporario
    // -------------------------------------------------------------------------
    private void FlashStatus(string text)
    {
        _lblStatus.Text      = text;
        _lblStatus.ForeColor = GREEN;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------
    private Label MakeLabel(string text, int x, int y) => new()
    {
        Text      = text,
        Location  = new Point(x, y),
        AutoSize  = true,
        ForeColor = TEXT,
        Font      = new Font("Segoe UI", 9),
        Anchor    = AnchorStyles.Left | AnchorStyles.Bottom
    };

    private TextBox MakeTextBox(int x, int y, int width) => new()
    {
        Location    = new Point(x, y),
        Width       = width,
        BackColor   = BG_DARK,
        ForeColor   = TEXT,
        BorderStyle = BorderStyle.FixedSingle,
        Font        = new Font("Consolas", 9),
        Anchor      = AnchorStyles.Left | AnchorStyles.Bottom
    };

    private Button MakeButton(string text, int x, int y, int width) => new()
    {
        Text      = text,
        Location  = new Point(x, y),
        Size      = new Size(width, 28),
        BackColor = BG_LIGHT,
        ForeColor = TEXT,
        FlatStyle = FlatStyle.Flat,
        Font      = new Font("Segoe UI", 8.5f),
        Cursor    = Cursors.Hand
    };
}
