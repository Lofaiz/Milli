// Диалог «Настройки клавиш»: две строки (голос, окно), захват комбинации, запрет клавиш без модификатора (перехватывались бы во всех программах),
// проверка занятости и кнопка возврата клавиш по умолчанию. Тексты — из Lang: язык интерфейса выбирается в меню трея.
using System.Drawing;
using System.Windows.Forms;

namespace Milli;

class HotkeyDialog : Form
{
    readonly MainForm owner;
    Label voiceVal, windowVal, status;
    string capture = "";

    public HotkeyDialog(MainForm owner)
    {
        this.owner = owner;
        owner.HotkeyCapture = false;

        Text = Lang.Tr("dialog.keys.title");
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = true;
        ClientSize = new Size(502, 306);
        Icon = owner.Icon;
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        KeyPreview = true;

        var hint = new Label
        {
            Text = Lang.Tr("dialog.keys.hint"),
            Location = new Point(20, 12),
            Size = new Size(464, 20),
            ForeColor = Theme.Dim,
        };
        Controls.Add(hint);

        voiceVal = AddRow(Lang.Tr("dialog.keys.voice"), "voice", 50);
        windowVal = AddRow(Lang.Tr("dialog.keys.window"), "window", 92);
        voiceVal.Text = owner.KeyText("voice");
        windowVal.Text = owner.KeyText("window");

        var help = new Label
        {
            Text = Lang.Tr("dialog.keys.help"),
            Location = new Point(20, 132),
            Size = new Size(464, 58),
            ForeColor = Theme.Dim,
        };
        Controls.Add(help);

        status = new Label
        {
            Text = Lang.Tr("dialog.keys.prompt"),
            Location = new Point(20, 196),
            Size = new Size(464, 34),
            ForeColor = Theme.Accent,
        };
        Controls.Add(status);

        var btnReset = Theme.DarkButton(Lang.Tr("dialog.keys.defaults"));
        btnReset.Location = new Point(20, 252);
        btnReset.Size = new Size(186, 32);
        btnReset.Click += (s, e) => ResetKeys();
        Controls.Add(btnReset);

        var btnClose = Theme.DarkButton(Lang.Tr("dialog.close"));
        btnClose.Location = new Point(390, 252);
        btnClose.Size = new Size(94, 32);
        btnClose.Click += (s, e) => Close();
        Controls.Add(btnClose);

        KeyDown += DialogKeyDown;
        Shown += (s, e) => { try { Activate(); } catch { } };
    }

    Label AddRow(string caption, string which, int y)
    {
        var lbl = new Label
        {
            Text = caption,
            Location = new Point(20, y),
            Size = new Size(266, 22),
            ForeColor = Theme.Text,
        };
        Controls.Add(lbl);

        var val = new Label
        {
            Location = new Point(292, y - 3),
            Size = new Size(104, 28),
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            ForeColor = Theme.Accent,
            TextAlign = ContentAlignment.MiddleCenter,
        };
        Controls.Add(val);

        var btn = Theme.DarkButton(Lang.Tr("dialog.keys.change"));
        btn.Location = new Point(404, y - 5);
        btn.Size = new Size(80, 30);
        btn.Click += (s, e) => StartCapture(which);
        Controls.Add(btn);
        return val;
    }

    void StartCapture(string which)
    {
        capture = which;
        owner.HotkeyCapture = true;
        status.ForeColor = Theme.Accent;
        status.Text = Lang.Tr("dialog.keys.capture",
            Lang.Tr(which == "window" ? "dialog.keys.forWindow" : "dialog.keys.forVoice"));
        try { Activate(); Focus(); } catch { }
    }

    void ResetKeys()
    {
        owner.HotkeyCapture = false;
        capture = "";
        var bad = owner.ResetHotkeys();
        voiceVal.Text = owner.KeyText("voice");
        windowVal.Text = owner.KeyText("window");
        if (bad != "")
        {
            status.ForeColor = Theme.Error;
            status.Text = bad;
        }
        else
        {
            status.ForeColor = Theme.Accent;
            status.Text = Lang.Tr("dialog.keys.resetOk");
        }
    }

    static readonly int[] ModifierVks =
    {
        0x10, 0x11, 0x12,             // Shift, Ctrl, Alt
        0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5, // L/R Shift, Ctrl, Alt
        0x5B, 0x5C,                   // Win
    };

    void DialogKeyDown(object sender, KeyEventArgs e)
    {
        if (capture == "") return;
        if (e.KeyCode == Keys.Escape)
        {
            capture = "";
            owner.HotkeyCapture = false;
            status.ForeColor = Theme.Dim;
            status.Text = Lang.Tr("dialog.keys.cancelled");
            return;
        }

        int vk = (int)e.KeyCode;
        // нажатие самих модификаторов клавишей не считаем — ждём обычную клавишу
        if (ModifierVks.Contains(vk)) return;
        e.SuppressKeyPress = true;

        if (!KeyMap.Known(vk))
        {
            status.ForeColor = Theme.Error;
            status.Text = Lang.Tr("dialog.keys.notAllowed");
            return;
        }

        int mods = MainForm.EventMods(e);
        var text = KeyMap.Format(mods, vk);

        if (!KeyMap.IsAllowed(mods, vk))
        {
            status.ForeColor = Theme.Error;
            status.Text = Lang.Tr("dialog.keys.needMod", text);
            return;
        }

        var other = capture == "voice" ? owner.HkWindow : owner.HkVoice;
        if (mods == other.Mods && vk == other.Vk)
        {
            status.ForeColor = Theme.Error;
            status.Text = Lang.Tr("dialog.keys.taken", text);
            return;
        }

        var err = owner.TrySetHotkey(capture, new HotKeyDef(mods, vk, text));
        if (err != "")
        {
            status.ForeColor = Theme.Error;
            status.Text = err;
            return;
        }

        (capture == "voice" ? voiceVal : windowVal).Text = text;
        status.ForeColor = Theme.Accent;
        status.Text = Lang.Tr("dialog.keys.assigned", text);
        capture = "";
        owner.HotkeyCapture = false;
    }
}
