// Диалог «Модель Милли»: список моделей от OpenCode с ценой, поиск, обновление
// и применение. Выбор сохраняется в config.json и сразу переключается в
// открытом разговоре (POST /api/session/<id>/model), чтобы не терять контекст.
// Тексты — из Lang (язык выбирается в меню трея).
using System.Drawing;
using System.Windows.Forms;

namespace Milli;

class ModelDialog : Form
{
    readonly MainForm owner;
    ListBox list;
    TextBox filter;
    Label status, detail, hintLabel, searchLabel;
    Button btnRefresh, btnApply, btnClose;
    List<ApiModel> all = new();
    List<ApiModel> shown = new();
    bool loading;

    public ModelDialog(MainForm owner)
    {
        this.owner = owner;

        Text = Lang.Tr("dialog.model.title");
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = true;
        ClientSize = new Size(600, 442);
        Icon = owner.Icon;
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        KeyPreview = true;

        hintLabel = new Label
        {
            Text = Lang.Tr("dialog.model.hint"),
            Location = new Point(20, 12),
            Size = new Size(560, 20),
            ForeColor = Theme.Dim,
        };
        Controls.Add(hintLabel);

        searchLabel = new Label
        {
            Text = Lang.Tr("dialog.search"),
            Location = new Point(20, 46),
            Size = new Size(52, 22),
            ForeColor = Theme.Text,
        };
        Controls.Add(searchLabel);

        filter = new TextBox
        {
            Location = new Point(76, 43),
            Size = new Size(300, 24),
            BackColor = Theme.Panel,
            ForeColor = Theme.Text,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 10),
        };
        filter.TextChanged += (s, e) => ResetList();
        Controls.Add(filter);

        btnRefresh = Theme.DarkButton(Lang.Tr("dialog.model.refresh"));
        btnRefresh.Location = new Point(392, 41);
        btnRefresh.Size = new Size(188, 30);
        btnRefresh.Click += (s, e) => RefreshList();
        Controls.Add(btnRefresh);

        list = new ListBox
        {
            Location = new Point(20, 84),
            Size = new Size(560, 226),
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
            Location = new Point(20, 318),
            Size = new Size(560, 20),
            ForeColor = Theme.Dim,
        };
        Controls.Add(detail);

        status = new Label
        {
            Location = new Point(20, 340),
            Size = new Size(560, 42),
            ForeColor = Theme.Accent,
        };
        Controls.Add(status);

        btnApply = Theme.DarkButton(Lang.Tr("dialog.apply"));
        btnApply.Location = new Point(20, 392);
        btnApply.Size = new Size(150, 34);
        btnApply.Click += (s, e) => ApplySelection();
        Controls.Add(btnApply);

        btnClose = Theme.DarkButton(Lang.Tr("dialog.close"));
        btnClose.Location = new Point(430, 392);
        btnClose.Size = new Size(150, 34);
        btnClose.Click += (s, e) => Close();
        Controls.Add(btnClose);

        // двойной клик по модели тоже применяет выбор; Esc закрывает окно
        KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };
        Shown += (s, e) => { try { Activate(); } catch { } };

        RefreshList();
    }

    void RefreshList()
    {
        if (loading) return;
        loading = true;
        status.ForeColor = Theme.Dim;
        status.Text = Lang.Tr("dialog.model.loading");
        try { status.Update(); } catch { }

        Task.Run(() =>
        {
            List<ApiModel> models = null;
            string error = null;
            try { models = owner.Api.GetModels(); }
            catch (Exception ex) { error = ex.Message; }

            try
            {
                if (!IsHandleCreated) return;
                BeginInvoke(() =>
                {
                    loading = false;
                    if (error != null)
                    {
                        all = new List<ApiModel>();
                        shown = new List<ApiModel>();
                        list.Items.Clear();
                        status.ForeColor = Theme.Error;
                        status.Text = Lang.Tr("dialog.model.loadFail", error);
                        return;
                    }
                    all = models.Where(m => m.enabled).ToList();
                    ResetList();
                    if (all.Count == 0)
                    {
                        status.ForeColor = Theme.Error;
                        status.Text = Lang.Tr("dialog.model.empty");
                        return;
                    }
                    status.ForeColor = Theme.Accent;
                    status.Text = Lang.Tr("dialog.model.count", all.Count, owner.Cfg.model.id);
                });
            }
            catch { }
        });
    }

    bool IsCurrent(ApiModel m) =>
        m.id == owner.Cfg.model.id && m.providerID == owner.Cfg.model.providerID;

    string FormatLine(ApiModel m)
    {
        var mark = IsCurrent(m) ? "● " : "   ";
        var st = string.IsNullOrEmpty(m.status) || m.status == "active" ? "" : $"  [{m.status}]";
        return mark + m.name + " — " + owner.Api.PriceText(m) + st;
    }

    void ResetList()
    {
        // перерисовать список с учётом строки поиска, сохранив выбор на текущей модели
        var f = filter.Text.Trim().ToLowerInvariant();
        list.Items.Clear();
        shown = new List<ApiModel>();
        foreach (var m in all)
        {
            if (f != "")
            {
                var hay = ((m.name ?? "") + " " + (m.id ?? "") + " " + (m.providerID ?? "")).ToLowerInvariant();
                if (!hay.Contains(f)) continue;
            }
            shown.Add(m);
            list.Items.Add(FormatLine(m));
        }
        int idx = shown.FindIndex(IsCurrent);
        if (idx < 0 && shown.Count > 0) idx = 0;
        if (idx >= 0) list.SelectedIndex = idx;
        UpdateDetail();
    }

    void UpdateDetail()
    {
        var i = list.SelectedIndex;
        if (i < 0 || i >= shown.Count) { detail.Text = ""; return; }
        var m = shown[i];
        var lim = m.context > 0 ? Lang.Tr("dialog.model.context", m.context) : "";
        detail.Text = $"{m.name}  ·  {m.providerID} / {m.id}{lim}";
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
        if (IsCurrent(m))
        {
            status.ForeColor = Theme.Accent;
            status.Text = Lang.Tr("dialog.model.already", m.name);
            return;
        }
        var note = owner.ApplyModel(m.id, m.providerID);
        ResetList();   // перерисовать метку ● у текущей модели
        status.ForeColor = Theme.Accent;
        status.Text = Lang.Tr("dialog.model.selected", m.name) + note;
    }
}
