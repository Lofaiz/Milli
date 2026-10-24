// Диалог «Модель распознавания речи»: список ggml-моделей из папки model рядом
// с программой. Выбор сохраняется в config.json и применяется сразу — движок
// перезагружает модель в фоне. Быстрая base-q5_1 идёт по умолчанию, точная
// small-q5_1 лежит рядом и включается здесь. Тексты — из Lang.
using System.Drawing;
using System.Windows.Forms;

namespace Milli;

class SttModelDialog : Form
{
    readonly MainForm owner;
    ListBox list;
    Label status, detail, hintLabel;
    Button btnApply, btnClose;
    List<SttEngine.ModelChoice> shown = new();

    public SttModelDialog(MainForm owner)
    {
        this.owner = owner;

        Text = Lang.Tr("dialog.stt.title");
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        ClientSize = new Size(560, 372);
        Icon = owner.Icon;
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        KeyPreview = true;

        hintLabel = new Label
        {
            Text = Lang.Tr("dialog.stt.hint"),
            Location = new Point(20, 12),
            Size = new Size(520, 34),
            ForeColor = Theme.Dim,
        };
        Controls.Add(hintLabel);

        list = new ListBox
        {
            Location = new Point(20, 52),
            Size = new Size(520, 186),
            BackColor = Theme.Panel,
            ForeColor = Theme.Text,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 10),
            IntegralHeight = false,
        };
        list.SelectedIndexChanged += (s, e) => UpdateDetail();
        list.DoubleClick += (s, e) => ApplySelection();
        Controls.Add(list);

        detail = new Label
        {
            Location = new Point(20, 246),
            Size = new Size(520, 20),
            ForeColor = Theme.Dim,
        };
        Controls.Add(detail);

        status = new Label
        {
            Location = new Point(20, 268),
            Size = new Size(520, 40),
            ForeColor = Theme.Accent,
        };
        Controls.Add(status);

        btnApply = Theme.DarkButton(Lang.Tr("dialog.apply"));
        btnApply.Location = new Point(20, 320);
        btnApply.Size = new Size(150, 34);
        btnApply.Click += (s, e) => ApplySelection();
        Controls.Add(btnApply);

        btnClose = Theme.DarkButton(Lang.Tr("dialog.close"));
        btnClose.Location = new Point(390, 320);
        btnClose.Size = new Size(150, 34);
        btnClose.Click += (s, e) => Close();
        Controls.Add(btnClose);

        KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };
        Shown += (s, e) => { try { Activate(); } catch { } };

        Fill();
    }

    void Fill()
    {
        shown = SttEngine.ListModels(owner.ModelDir);
        list.Items.Clear();
        foreach (var m in shown)
        {
            var mark = m.File == owner.Cfg.sttModel ? "● " : "   ";
            var mb = (m.Size / 1024.0 / 1024.0).ToString("0") + " " + Lang.Tr("dialog.stt.mb");
            list.Items.Add($"{mark}{m.File}  —  {mb}, {m.Note}");
        }

        if (shown.Count == 0)
        {
            status.ForeColor = Theme.Error;
            status.Text = Lang.Tr("dialog.stt.none");
            return;
        }

        var idx = shown.FindIndex(m => m.File == owner.Cfg.sttModel);
        list.SelectedIndex = idx >= 0 ? idx : 0;
        status.ForeColor = Theme.Accent;
        status.Text = Lang.Tr("dialog.stt.current", SttEngine.ShortName(owner.Cfg.sttModel)) +
                      Lang.Tr(owner.SttReady ? "dialog.stt.loaded" : "dialog.stt.notLoaded");
    }

    void UpdateDetail()
    {
        var i = list.SelectedIndex;
        if (i < 0 || i >= shown.Count) { detail.Text = ""; return; }
        var m = shown[i];
        var speed = Lang.Tr(m.File.Contains("base") || m.File.Contains("tiny")
            ? "dialog.stt.speedFast"
            : "dialog.stt.speedSlow");
        detail.Text = $"{SttEngine.ShortName(m.File)}  ·  {m.Note}  ·  {speed}";
    }

    void ApplySelection()
    {
        var i = list.SelectedIndex;
        if (i < 0 || i >= shown.Count)
        {
            status.ForeColor = Theme.Error;
            status.Text = Lang.Tr("dialog.pickFirst");
            return;
        }
        var m = shown[i];
        if (m.File == owner.Cfg.sttModel && owner.SttReady)
        {
            status.ForeColor = Theme.Accent;
            status.Text = Lang.Tr("dialog.stt.inUse");
            return;
        }
        var err = owner.ApplySttModel(m.File);
        Fill();
        status.ForeColor = err == null ? Theme.Accent : Theme.Error;
        status.Text = err ?? Lang.Tr("dialog.stt.picked", SttEngine.ShortName(m.File));
    }
}
