using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using P3DDebin.Core;

namespace P3DDebin;

// Visualizador de texto para RVMATs e model.cfg reconstruidos, com a opcao
// de salvar o conteudo na pasta de saida.
public class TextPreviewForm : Form
{
    private static readonly Color BG       = Color.FromArgb(72, 72, 76);
    private static readonly Color BG_DARK  = Color.FromArgb(45, 45, 48);
    private static readonly Color BG_LIGHT = Color.FromArgb(100, 100, 104);
    private static readonly Color ACCENT   = Color.FromArgb(0, 160, 220);
    private static readonly Color TEXT     = Color.FromArgb(230, 230, 230);
    private static readonly Color TEXT_DIM = Color.FromArgb(150, 150, 150);

    private readonly List<OdolExtract.TextItem> _items;
    private readonly string _outputFolder;

    private ListBox  _list    = null!;
    private TextBox  _content = null!;

    public TextPreviewForm(string title, List<OdolExtract.TextItem> items, string outputFolder)
    {
        _items = items;
        _outputFolder = outputFolder;
        BuildUI(title);
    }

    private void BuildUI(string title)
    {
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        Text          = title;
        Size          = new Size(860, 600);
        MinimumSize   = new Size(560, 360);
        BackColor     = BG;
        ForeColor     = TEXT;
        StartPosition = FormStartPosition.CenterParent;

        bool multi = _items.Count > 1;

        var lblInfo = new Label
        {
            Text      = Strings.T("Tp.Info", _items.Count),
            Location  = new Point(12, 10),
            AutoSize  = true,
            ForeColor = TEXT_DIM,
            Font      = new Font("Segoe UI", 8.5f)
        };

        int listWidth = multi ? 240 : 0;

        _list = new ListBox
        {
            Location    = new Point(12, 36),
            Size        = new Size(listWidth, 480),
            BackColor   = BG_DARK,
            ForeColor   = TEXT,
            BorderStyle = BorderStyle.FixedSingle,
            Font        = new Font("Consolas", 8.5f),
            Visible     = multi,
            Anchor      = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Bottom
        };
        foreach (var item in _items)
            _list.Items.Add(item.Name);
        _list.SelectedIndexChanged += (_, _) => ShowSelected();

        int contentLeft = multi ? 12 + listWidth + 8 : 12;

        _content = new TextBox
        {
            Location    = new Point(contentLeft, 36),
            Size        = new Size(836 - contentLeft, 480),
            BackColor   = BG_DARK,
            ForeColor   = TEXT,
            BorderStyle = BorderStyle.FixedSingle,
            Font        = new Font("Consolas", 9),
            Multiline   = true,
            ReadOnly    = true,
            ScrollBars  = ScrollBars.Both,
            WordWrap    = false,
            Anchor      = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom
        };

        var btnSave = new Button
        {
            Text      = Strings.T("Tp.BtnSave"),
            Location  = new Point(560, 526),
            Size      = new Size(190, 32),
            BackColor = ACCENT,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 9, FontStyle.Bold),
            Cursor    = Cursors.Hand,
            Anchor    = AnchorStyles.Right | AnchorStyles.Bottom
        };
        btnSave.FlatAppearance.BorderSize = 0;
        btnSave.Click += BtnSave_Click;

        var btnClose = new Button
        {
            Text      = Strings.T("Tp.BtnClose"),
            Location  = new Point(756, 526),
            Size      = new Size(80, 32),
            BackColor = BG_LIGHT,
            ForeColor = TEXT,
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 8.5f),
            Cursor    = Cursors.Hand,
            Anchor    = AnchorStyles.Right | AnchorStyles.Bottom
        };
        btnClose.Click += (_, _) => Close();

        Controls.AddRange(new Control[] { lblInfo, _list, _content, btnSave, btnClose });

        if (_items.Count > 0)
        {
            if (multi)
                _list.SelectedIndex = 0;
            else
                ShowContent(_items[0]);
        }
    }

    private void ShowSelected()
    {
        if (_list.SelectedIndex >= 0)
            ShowContent(_items[_list.SelectedIndex]);
    }

    private void ShowContent(OdolExtract.TextItem item)
        => _content.Text = item.Content.Replace("\n", Environment.NewLine);

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        if (!Directory.Exists(_outputFolder))
        {
            MessageBox.Show(this, Strings.T("Tp.MsgInvalidOutput"), Text);
            return;
        }

        try
        {
            foreach (var item in _items)
            {
                string target = Path.Combine(_outputFolder, item.Name);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.WriteAllText(target, item.Content);
            }

            MessageBox.Show(this, Strings.T("Tp.MsgSaved", _items.Count, _outputFolder), Text);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, Strings.T("Tp.MsgSaveFailed", ex.Message), Text);
        }
    }
}
