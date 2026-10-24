// Главное окно и вся логика Милли: трей, глобальные клавиши, голосовой ввод,
// переписка с OpenCode. Поведение повторяет PowerShell-версию: закрытие окна
// прячет его в трей, запуск скрытым, клавиша окна показывает/убирает окно.
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;

namespace Milli;

class MainForm : Form
{
    internal AppConfig Cfg;
    internal ApiClient Api = new();
    string configPath;

    SttEngine stt;
    HotkeyWindow hotkeys;
    internal HotKeyDef HkVoice, HkWindow;
    internal bool HotkeyOk, WindowOk;
    internal bool HotkeyCapture;      // идёт назначение клавиши в диалоге

    bool exiting;
    internal string SessionId;
    Task cleanTask;          // фоновая уборка старых сессий Милли
    internal int CleanedSessions;
    // момент старта процесса: сессии новее него принадлежат текущему запуску
    readonly long startedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    bool pending, polling;
    bool holdMode, pendingVoice, voiceWarned;
    bool voiceHeld;      // клавиша/кнопка «говорить» нажата прямо сейчас
    bool voiceByKey;     // запись ведёт клавиша (авто-стоп по отпусканию), а не мышь

    NotifyIcon tray;
    internal ToolStripMenuItem MiShow, MiVoice, MiStop, MiAuto, MiOffline, MiKeys, MiModel, MiStt;
    internal ToolStripMenuItem MiLang, MiLangRu, MiLangEn, MiExit;
    RichTextBox chat;
    TextBox input;
    Label status;
    Button btnSend, btnStop, btnVoice, btnModel, btnStt;
    ToolTip tips;
    System.Windows.Forms.Timer pollTimer, holdTimer, remTimer;
    readonly List<Reminder> reminders = new();
    readonly List<TimerItem> timers = new();
    readonly List<string> notes = new();
    string lastAnswer;
    Font boldFont, regularFont;
    Icon appIcon;

    // папка с моделями распознавания речи (model рядом с программой)
    internal string ModelDir => Path.Combine(AppContext.BaseDirectory, "model");
    internal bool SttReady => stt != null && stt.Ready;

    public MainForm(string[] args)
    {
        configPath = Path.Combine(AppContext.BaseDirectory, "config.json");
        Cfg = AppConfig.Load(configPath);
        Lang.Code = Cfg.language;

        // персона и подпись не должны расходиться с языком: если их не правили
        // руками, берём текст на выбранном языке (пригодится после правки
        // config.json и когда язык выставил установщик)
        bool langFix = false;
        if (Lang.IsDefaultPersona(Cfg.persona) && Cfg.persona != Lang.PersonaFor(Lang.Code))
        {
            Cfg.persona = Lang.PersonaFor(Lang.Code);
            langFix = true;
        }
        if (Cfg.title == "Милли" || Cfg.title == "Milli")
        {
            var name = Lang.Tr("app.name");
            if (Cfg.title != name) { Cfg.title = name; langFix = true; }
        }

        // диагностика: --lang ru|en — разово переключить язык (как --keys/--model)
        var langIdx = Array.IndexOf(args, "--lang");
        if (langIdx >= 0 && langIdx + 1 < args.Length)
        {
            var code = args[langIdx + 1] == "en" ? "en" : "ru";
            if (code != Cfg.language)
            {
                Lang.Code = code;
                Cfg.language = code;
                if (Lang.IsDefaultPersona(Cfg.persona)) Cfg.persona = Lang.PersonaFor(code);
                if (Cfg.title == "Милли" || Cfg.title == "Milli") Cfg.title = Lang.Tr("app.name");
                langFix = true;
            }
        }
        if (langFix) SaveConfigQuiet();
        LoadNotes();

        appIcon = LoadIcon();
        Icon = appIcon;

        Text = Lang.Tr("app.name");
        Size = new Size(580, 640);
        MinimumSize = new Size(440, 420);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        KeyPreview = true;

        BuildUi();

        // клавиши из конфига; битую строку заменяем на значение по умолчанию
        HkVoice = KeyMap.Parse(Cfg.hotkeyVoice) ?? KeyMap.Parse("Num9");
        HkWindow = KeyMap.Parse(Cfg.hotkeyWindow) ?? KeyMap.Parse("Num8");
        bool needSave = Cfg.hotkeyVoice != HkVoice.Text || Cfg.hotkeyWindow != HkWindow.Text;
        Cfg.hotkeyVoice = HkVoice.Text;
        Cfg.hotkeyWindow = HkWindow.Text;
        if (needSave) SaveConfigQuiet();

        hotkeys = new HotkeyWindow();
        hotkeys.VoicePressed += () => VoiceDown(true);
        hotkeys.WindowPressed += () => ToggleWindow();
        ApplyHotkeyLabels();
        ApplyModelLabel();
        HotkeyOk = ApplyHotkey("voice");
        WindowOk = ApplyHotkey("window");

        // контекст трея
        tray = new NotifyIcon
        {
            Icon = appIcon,
            Text = Lang.Tr("app.name"),
            Visible = true,
            ContextMenuStrip = BuildTrayMenu(),
        };
        tray.DoubleClick += (s, e) => ShowWindow();
        tray.BalloonTipClicked += (s, e) => ShowWindow();
        // метки меню: трей создаётся после кнопок, поэтому обновляем их ещё раз —
        // иначе в меню не было бы текущих клавиш и названия модели
        ApplyHotkeyLabels();
        ApplyModelLabel();

        pollTimer = new System.Windows.Forms.Timer { Interval = 900 };
        pollTimer.Tick += (s, e) => PollReply();
        holdTimer = new System.Windows.Forms.Timer { Interval = 150 };
        holdTimer.Tick += (s, e) => HoldTick();
        holdTimer.Start();

        // напоминания проверяем раз в секунду
        remTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        remTimer.Tick += (s, e) => CheckReminders();
        remTimer.Start();

        // распознаватель: модель грузится заранее, чтобы работала с первого нажатия.
        // Если файл из config.json не найден, берём любую модель из папки model.
        var models = SttEngine.ListModels(ModelDir);
        var wanted = models.FirstOrDefault(m => m.File == Cfg.sttModel)?.File ?? models.FirstOrDefault()?.File;
        if (wanted != null)
        {
            if (wanted != Cfg.sttModel) { Cfg.sttModel = wanted; SaveConfigQuiet(); }
            stt = new SttEngine(ModelDir, Cfg.language);
            stt.Recognized += text => UI(() => OnRecognized(text));
            stt.Failed += msg => UI(() => OnVoiceFailed(msg));
            var file = wanted;
            Task.Run(() => stt.Load(file));
        }
        ApplySttLabel();
        ApplyLangUi();   // подписи и галочка выбранного языка в меню

        // уборка «мёртвых» сессий Милли — в фоне, чтобы не задерживать запуск.
        // Первый вопрос дожидается её (см. EnsureSession): иначе уборка могла бы
        // удалить только что созданную сессию.
        cleanTask = Task.Run(() => CleanSessions(false));

        // --keys / --model и стартовые подсказки — уже в цикле сообщений
        var _ = Handle;   // создать окно заранее: нужен хэндл для хоткеев и трея
        BeginInvoke(() =>
        {
            if (args.Contains("--keys")) ShowHotkeySettings();
            if (args.Contains("--model")) ShowModelSettings();
            if (args.Contains("--stt")) ShowSttSettings();
            // служебное: сразу открыть меню трея (там клавиши, модели, язык)
            if (args.Contains("--menu") && tray?.ContextMenuStrip != null)
                tray.ContextMenuStrip.Show(Cursor.Position);

            // служебное: --clean-sessions — разовая уборка старых сессий Милли с отчётом
            if (args.Contains("--clean-sessions"))
                Task.Run(() => CleanSessions(true));

            // диагностика: --say-file <wav> — распознать файл и обработать как голос
            var sayIdx = Array.IndexOf(args, "--say-file");
            if (sayIdx >= 0 && sayIdx + 1 < args.Length && stt != null)
            {
                var file = args[sayIdx + 1];
                Task.Run(() =>
                {
                    for (int i = 0; i < 300 && !stt.Ready; i++) Thread.Sleep(100);  // ждём загрузку модели
                    string text;
                    try { text = stt.RecognizeFile(file); }
                    catch (Exception ex) { UI(() => OnVoiceFailed(Lang.Tr("stt.recogFail", ex.Message))); return; }
                    UI(() => OnRecognized(text));
                });
            }

            if (!Cfg.startHidden) ShowWindow();

            // подсказка о клавишах — только при первом запуске, иначе она всплывала
            // бы при каждом входе в систему (Милли стоит в автозагрузке)
            if (AppConfig.FirstRun)
                Balloon(Lang.Tr("balloon.startHint", HkWindow.Text, HkVoice.Text), ToolTipIcon.Info, 4000);

            if (!HotkeyOk || !WindowOk)
            {
                var busy = new List<string>();
                if (!HotkeyOk) busy.Add(HkVoice.Text);
                if (!WindowOk) busy.Add(HkWindow.Text);
                Balloon(Lang.Tr("balloon.busyKeys", Lang.JoinAnd(busy)), ToolTipIcon.Warning, 7000);
            }
        });
    }

    // ------------------------------------------------------------------ иконка
    static Icon LoadIcon()
    {
        try
        {
            var p = Path.Combine(AppContext.BaseDirectory, "milli.ico");
            if (File.Exists(p)) return new Icon(p);
        }
        catch { }
        try
        {
            var exe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exe)) return Icon.ExtractAssociatedIcon(exe);
        }
        catch { }
        return SystemIcons.Application;
    }

    // ------------------------------------------------------------------ интерфейс
    void BuildUi()
    {
        var rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Theme.Bg,
        };
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        boldFont = new Font("Segoe UI", 10, FontStyle.Bold);
        regularFont = new Font("Segoe UI", 10);

        chat = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = Theme.Panel,
            ForeColor = Theme.Text,
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 10),
            HideSelection = false,
            Margin = new Padding(8, 8, 8, 4),
        };

        input = new TextBox
        {
            Multiline = true,
            Dock = DockStyle.Fill,
            BackColor = Theme.Panel,
            ForeColor = Theme.Text,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 10),
            Margin = new Padding(8, 4, 8, 0),
            ScrollBars = ScrollBars.Vertical,
        };
        input.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter && !e.Control && !e.Alt && !e.Shift)
            {
                e.SuppressKeyPress = true;
                SendCurrent();
            }
        };

        var btnRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            BackColor = Theme.Bg,
            Padding = new Padding(6),
        };

        btnSend = Theme.DarkButton(Lang.Tr("win.send"));
        btnStop = Theme.DarkButton(Lang.Tr("win.stop"));
        btnVoice = Theme.DarkButton(Lang.Tr("win.voice"));
        btnModel = Theme.DarkButton("🤖 …");
        btnStt = Theme.DarkButton("🎧 …");

        status = new Label
        {
            Text = Lang.Tr("status.ready"),
            AutoSize = false,
            Dock = DockStyle.Fill,
            ForeColor = Theme.Dim,
            BackColor = Theme.Bg,
            Padding = new Padding(10, 0, 8, 6),
            TextAlign = ContentAlignment.MiddleLeft,
            Height = 24,
        };

        tips = new ToolTip { AutoPopDelay = 8000, InitialDelay = 400, ReshowDelay = 200 };
        tips.SetToolTip(btnSend, Lang.Tr("tip.send"));
        tips.SetToolTip(btnStop, Lang.Tr("tip.stop"));
        tips.SetToolTip(btnVoice, Lang.Tr("tip.voice"));
        tips.SetToolTip(btnModel, Lang.Tr("tip.model"));
        tips.SetToolTip(btnStt, Lang.Tr("tip.stt"));

        btnSend.Click += (s, e) => SendCurrent();
        // кнопка «Стоп»: прервать текущий ответ Милли
        btnStop.Click += (s, e) => StopReply(Lang.Code);
        // кнопка работает по зажатию: держишь — пишет, отпустил — распознаёт.
        // Остановка по отпусканию клавиши проверяется таймером только для записи
        // с клавиатуры, поэтому здесь запись ведёт мышь (voiceByKey = false).
        btnVoice.MouseDown += (s, e) => VoiceDown(false);
        btnVoice.MouseUp += (s, e) => VoiceUp();
        // на кнопке — текущая модель ИИ; клик открывает выбор модели
        btnModel.Click += (s, e) => ShowModelSettings();
        // на кнопке — модель распознавания речи; клик открывает выбор
        btnStt.Click += (s, e) => ShowSttSettings();

        btnRow.Controls.Add(btnSend);
        btnRow.Controls.Add(btnStop);
        btnRow.Controls.Add(btnVoice);
        btnRow.Controls.Add(btnModel);
        btnRow.Controls.Add(btnStt);

        rootLayout.Controls.Add(chat, 0, 0);
        rootLayout.Controls.Add(input, 0, 1);
        rootLayout.Controls.Add(btnRow, 0, 2);
        rootLayout.Controls.Add(status, 0, 3);
        Controls.Add(rootLayout);
    }

    ContextMenuStrip BuildTrayMenu()
    {
        var menu = new ContextMenuStrip
        {
            Renderer = new DarkRenderer(),
            BackColor = Theme.Panel,
            ForeColor = Theme.Text,
        };
        MiShow = new ToolStripMenuItem(Lang.Tr("tray.show"));
        MiShow.Click += (s, e) => ShowWindow();
        MiVoice = new ToolStripMenuItem(Lang.Tr("tray.voice"));
        MiVoice.Click += (s, e) => TrayVoice();
        MiStop = new ToolStripMenuItem(Lang.Tr("tray.stop"));
        MiStop.Click += (s, e) => StopReply(Lang.Code);
        MiAuto = new ToolStripMenuItem(Lang.Tr("tray.auto"))
        {
            CheckOnClick = true,
            Checked = Cfg.autoSendVoice,
        };
        MiAuto.CheckedChanged += (s, e) => { Cfg.autoSendVoice = MiAuto.Checked; SaveConfigQuiet(); };
        MiOffline = new ToolStripMenuItem(Lang.Tr("tray.offline"))
        {
            CheckOnClick = true,
            Checked = Cfg.offlineMode,
        };
        MiOffline.CheckedChanged += (s, e) => { Cfg.offlineMode = MiOffline.Checked; SaveConfigQuiet(); UpdateOfflineStatus(); };
        MiKeys = new ToolStripMenuItem(Lang.Tr("tray.keys"));
        MiKeys.Click += (s, e) => ShowHotkeySettings();
        MiModel = new ToolStripMenuItem(Lang.Tr("tray.model"));
        MiModel.Click += (s, e) => ShowModelSettings();
        MiStt = new ToolStripMenuItem(Lang.Tr("tray.stt"));
        MiStt.Click += (s, e) => ShowSttSettings();

        // выбор языка: русский или английский (названия языков не переводим —
        // их должно быть видно даже в непонятном интерфейсе)
        MiLang = new ToolStripMenuItem(Lang.Tr("tray.lang"));
        MiLangRu = new ToolStripMenuItem("Русский");
        MiLangRu.Click += (s, e) => SetLanguage("ru");
        MiLangEn = new ToolStripMenuItem("English");
        MiLangEn.Click += (s, e) => SetLanguage("en");
        MiLang.DropDownItems.Add(MiLangRu);
        MiLang.DropDownItems.Add(MiLangEn);

        MiExit = new ToolStripMenuItem(Lang.Tr("tray.exit"));
        MiExit.Click += (s, e) => ExitApp();

        menu.Items.Add(MiShow);
        menu.Items.Add(MiStop);
        menu.Items.Add(MiVoice);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(MiAuto);
        menu.Items.Add(MiOffline);
        menu.Items.Add(MiKeys);
        menu.Items.Add(MiModel);
        menu.Items.Add(MiStt);
        menu.Items.Add(MiLang);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(MiExit);
        foreach (ToolStripItem it in menu.Items) it.ForeColor = Theme.Text;
        foreach (ToolStripItem it in MiLang.DropDownItems) it.ForeColor = Theme.Text;
        return menu;
    }

    // ------------------------------------------------------------------ показ окна
    // Application.Run с указанным MainForm сам показывает форму при старте, поэтому
    // первый показ гасим, если в настройках включён запуск в трее (startHidden).
    bool firstShowHandled;

    protected override void SetVisibleCore(bool value)
    {
        if (!firstShowHandled)
        {
            firstShowHandled = true;
            if (Cfg.startHidden) value = false;
        }
        base.SetVisibleCore(value);
    }

    internal void ShowWindow()
    {
        if (!Visible) Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Activate();
        try { input.Focus(); } catch { }
        SetDarkTitle();
    }

    internal void ToggleWindow()
    {
        // клавиша окна: показать окно из трея и убрать обратно при повторном нажатии
        if (Visible && WindowState == FormWindowState.Minimized) { ShowWindow(); return; }
        if (Visible && ContainsFocus) Hide(); else ShowWindow();
    }

    internal void Balloon(string text, ToolTipIcon icon = ToolTipIcon.Info, int ms = 5000)
    {
        try { tray.ShowBalloonTip(ms, Lang.Tr("app.name"), text, icon); } catch { }
    }

    void SetDarkTitle()
    {
        try { int v = 1; DwmSetWindowAttribute(Handle, 20, ref v, 4); } catch { }
    }

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        SetDarkTitle();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (WindowState == FormWindowState.Minimized) Hide();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!exiting) { e.Cancel = true; Hide(); return; }
        base.OnFormClosing(e);
    }

    // ------------------------------------------------------------------ клавиши
    internal string KeyText(string which) => which == "voice" ? HkVoice.Text : HkWindow.Text;

    internal void ApplyHotkeyLabels()
    {
        if (btnVoice != null) btnVoice.Text = Lang.Tr("win.voice.key", HkVoice.Text);
        if (MiVoice != null) MiVoice.Text = Lang.Tr("tray.voice.key", HkVoice.Text);
        if (MiShow != null) MiShow.Text = Lang.Tr("tray.show.key", HkWindow.Text);
    }

    internal bool ApplyHotkey(string which)
    {
        var hk = which == "voice" ? HkVoice : HkWindow;
        if (which == "voice") hotkeys.UnregisterVoice(); else hotkeys.UnregisterWindow();
        bool ok = which == "voice"
            ? hotkeys.RegisterVoice(hk.Mods, hk.Vk)
            : hotkeys.RegisterWindow(hk.Mods, hk.Vk);
        if (which == "voice") HotkeyOk = ok; else WindowOk = ok;
        return ok;
    }

    internal string TrySetHotkey(string which, HotKeyDef def)
    {
        var old = which == "voice" ? HkVoice : HkWindow;
        if (which == "voice") HkVoice = def; else HkWindow = def;
        if (ApplyHotkey(which))
        {
            if (which == "voice") Cfg.hotkeyVoice = def.Text; else Cfg.hotkeyWindow = def.Text;
            SaveConfigQuiet();
            ApplyHotkeyLabels();
            return "";
        }
        if (which == "voice") HkVoice = old; else HkWindow = old;
        ApplyHotkey(which);
        ApplyHotkeyLabels();
        return Lang.Tr("hotkey.busyStay", def.Text);
    }

    internal string ResetHotkeys()
    {
        hotkeys.UnregisterVoice();
        hotkeys.UnregisterWindow();
        HkVoice = KeyMap.Parse("Num9");
        HkWindow = KeyMap.Parse("Num8");
        var bad = new List<string>();
        if (!ApplyHotkey("voice")) bad.Add("Num9");
        if (!ApplyHotkey("window")) bad.Add("Num8");
        Cfg.hotkeyVoice = HkVoice.Text;
        Cfg.hotkeyWindow = HkWindow.Text;
        SaveConfigQuiet();
        ApplyHotkeyLabels();
        if (bad.Count > 0) return Lang.Tr("hotkey.resetBusy", Lang.JoinAnd(bad));
        return "";
    }

    // локальный перехват настроенных клавиш, пока окно открыто: резерв на случай,
    // если глобальную регистрацию не отдала система (клавиша занята)
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (HotkeyCapture) return;
        int mods = EventMods(e);
        if (mods == HkVoice.Mods && (int)e.KeyCode == HkVoice.Vk && !HotkeyOk) { e.SuppressKeyPress = true; VoiceDown(true); }
        else if (mods == HkWindow.Mods && (int)e.KeyCode == HkWindow.Vk && !WindowOk) { e.SuppressKeyPress = true; ToggleWindow(); }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (HotkeyCapture) return;
        if (EventMods(e) == HkVoice.Mods && (int)e.KeyCode == HkVoice.Vk) { e.SuppressKeyPress = true; VoiceUp(); }
    }

    internal static int EventMods(KeyEventArgs e)
    {
        int mods = 0;
        if (e.Control) mods |= KeyMap.MOD_CTRL;
        if (e.Alt) mods |= KeyMap.MOD_ALT;
        if (e.Shift) mods |= KeyMap.MOD_SHIFT;
        return mods;
    }

    // ------------------------------------------------------------------ голос
    // клавиша или кнопка «говорить» нажата
    internal void VoiceDown(bool byKey)
    {
        voiceHeld = true;
        voiceByKey = byKey;
        StartVoice(true, byKey);
    }

    // клавиша или кнопка «говорить» отпущена
    internal void VoiceUp()
    {
        voiceHeld = false;
        if (holdMode) StopVoice();
    }

    void HoldTick()
    {
        if (stt == null) return;
        if (pendingVoice && stt.Ready)
        {
            pendingVoice = false;
            // если клавишу уже отпустили, пока грузилась модель, запись не начинаем
            if (voiceHeld || !holdMode) StartVoice(holdMode, voiceByKey);
        }
        // авто-остановку по отпусканию клавиши проверяем только для записи,
        // начатой клавишей: запись с кнопки останавливает отпускание мыши
        if (stt.Recording && holdMode && voiceByKey && !KeyState.Down(HkVoice.Vk)) StopVoice();
    }

    internal void StartVoice(bool hold, bool byKey = true)
    {
        if (stt == null)
        {
            SetStatus(Lang.Tr("status.unavailable"));
            WarnVoiceUnavailable();
            return;
        }
        if (stt.Busy) { SetStatus(Lang.Tr("status.busy")); return; }
        if (!stt.Ready)
        {
            pendingVoice = true;
            holdMode = hold;
            voiceByKey = byKey;
            SetStatus(Lang.Tr("status.loading"));
            return;
        }
        holdMode = hold;
        voiceByKey = byKey;
        if (!stt.Start()) { SetStatus(Lang.Tr("status.noMic")); return; }
        SetStatus(Lang.Tr("status.listening"));
    }

    internal void StopVoice()
    {
        if (stt == null || !stt.Recording) { pendingVoice = false; return; }
        if (!holdMode) return;
        SetStatus(Lang.Tr("status.recognizing"));
        stt.Stop();
    }

    void TrayVoice()
    {
        // из трея — та же запись: первый клик начал, второй закончил
        if (stt != null && stt.Recording) { SetStatus(Lang.Tr("status.recognizing")); stt.Stop(); return; }
        StartVoice(false);
    }

    void WarnVoiceUnavailable()
    {
        if (voiceWarned) return;
        voiceWarned = true;
        var msg = stt != null && stt.LoadError != null
            ? Lang.Tr("balloon.voiceOff", stt.LoadError)
            : Lang.Tr("balloon.noModel");
        Balloon(msg, ToolTipIcon.Warning, 8000);
    }

    void OnRecognized(string text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            SetStatus(Lang.Tr("status.recognized"));
            if (Cfg.autoSendVoice) SendText(text);
            else { input.Text = text; input.SelectionStart = input.TextLength; }
        }
        else SetStatus(Lang.Tr("status.noSpeech"));
    }

    void OnVoiceFailed(string msg)
    {
        if (msg == "empty") { SetStatus(Lang.Tr("status.noSpeech")); return; }
        SetStatus(Lang.Tr("status.recogError"));
        AppendChat(Lang.Tr("chat.error"), msg, Theme.Error);
    }

    // ------------------------------------------------------------------ переписка
    internal void SetStatus(string s)
    {
        try { status.Text = s; status.Update(); } catch { }
    }

    void AppendChat(string who, string text, Color color)
    {
        chat.SelectionStart = chat.TextLength;
        chat.SelectionLength = 0;
        chat.SelectionColor = color;
        chat.SelectionFont = boldFont;
        chat.AppendText(who + "\r\n");
        chat.SelectionFont = regularFont;
        chat.SelectionColor = Theme.Text;
        chat.AppendText(text + "\r\n\r\n");
        chat.SelectionStart = chat.TextLength;
        chat.ScrollToCaret();
    }

    void SendCurrent()
    {
        var t = input.Text;
        if (string.IsNullOrWhiteSpace(t)) return;
        input.Clear();
        SendText(t);
    }

    internal void SendText(string text)
    {
        text = text.Trim();
        if (text.Length == 0) return;
        AppendChat(Lang.Tr("chat.you"), text, Theme.User);

        // локальная команда выполняется на месте, без обращения к ИИ
        var cmd = Commands.Parse(text);
        if (cmd.Kind != Commands.Kind.None) { RunCommand(cmd); return; }

        // голосовая команда "автономный режим"
        var tn = Commands.Norm(text);
        if (tn == "автономный режим" || tn == "автономный режим вкл" || tn == "включи автономный режим" || tn == "offline mode" || tn == "enable offline mode")
        {
            Cfg.offlineMode = true;
            SaveConfigQuiet();
            if (MiOffline != null) MiOffline.Checked = true;
            UpdateOfflineStatus();
            LocalReply(Lang.Code, Lang.Code == "en" ? "Offline mode enabled." : "Автономный режим включён.");
            return;
        }
        if (tn == "выключи автономный режим" || tn == "автономный режим выкл" || tn == "disable offline mode")
        {
            Cfg.offlineMode = false;
            SaveConfigQuiet();
            if (MiOffline != null) MiOffline.Checked = false;
            UpdateOfflineStatus();
            LocalReply(Lang.Code, Lang.Code == "en" ? "Offline mode disabled." : "Автономный режим выключен.");
            return;
        }

        if (Cfg.offlineMode)
        {
            LocalReply(Lang.Code, Lang.Code == "en" ? "Offline mode. I don't understand that. Try \"what can you do\"." : "Автономный режим. Не понял команду. Скажи «что ты умеешь».");
            return;
        }

        SetStatus(Lang.Tr("status.thinking"));
        pending = true;
        pollTimer.Start();
        Task.Run(() =>
        {
            try
            {
                var sid = EnsureSession();
                Api.SendPrompt(sid, text);
            }
            catch (Exception ex)
            {
                UI(() =>
                {
                    pending = false;
                    pollTimer.Stop();
                    Api.ResetServer();
                    if (Cfg.offlineMode)
                    {
                        LocalReply(Lang.Code, Lang.Code == "en" ? "OpenCode unavailable. Offline mode enabled. Try \"what can you do\"." : "OpenCode недоступен. Включён автономный режим. Скажи «что ты умеешь».");
                        if (MiOffline != null) MiOffline.Checked = true;
                        UpdateOfflineStatus();
                        return;
                    }
                    AppendChat(Lang.Tr("chat.error"), ex.Message, Theme.Error);
                    SetStatus(Lang.Tr("status.error"));
                });
            }
        });
    }

    string EnsureSession()
    {
        if (SessionId != null) return SessionId;
        // сначала уборка: новую сессию удалять нельзя, поэтому дожидаемся её
        try { cleanTask?.Wait(15000); } catch { }
        SessionId = Api.CreateSession(Cfg.title, Cfg.model.id, Cfg.model.providerID, Cfg.persona);
        return SessionId;
    }

    // ------------------------------------------------------------- уборка сессий
    // Каждый запуск Милли создаёт новую сессию, а прежние остаются в списке
    // OpenCode и больше никогда не используются. При старте такие «мёртвые»
    // сессии удаляются — отключается настройкой cleanSessions в config.json.
    int CleanSessions(bool report)
    {
        if (!Cfg.cleanSessions) return 0;
        var cleaned = 0;
        try
        {
            var active = Api.ActiveSessions();
            foreach (var s in Api.ListSessions())
            {
                if (s.created >= startedAt) continue;      // создана в этом запуске — это ещё живая
                if (s.id == SessionId || active.Contains(s.id)) continue;  // текущая или занятая
                if (!IsMilliSession(s)) continue;          // чужая сессия — не наше дело
                try { Api.DeleteSession(s.id); cleaned++; } catch { }
            }
        }
        catch { }   // сервер недоступен — уборка не должна мешать запуску

        CleanedSessions = cleaned;
        if (report) UI(() => SetStatus(Lang.Tr("status.cleaned", cleaned)));
        return cleaned;
    }

    // Сессия Милли: помеченная этой версией (metadata.milli) либо старая — с нашим
    // названием и служебным сообщением персоны. Название нужно только как дешёвый
    // отсев: не опрашиваем сервер по каждой чужой сессии.
    bool IsMilliSession(ApiSession s)
    {
        if (s.marked) return true;
        if (s.title != "Милли" && s.title != "Milli" && s.title != Cfg.title) return false;
        try { return Api.HasPersonaMarker(s.id); } catch { return false; }
    }

    void PollReply()
    {
        if (!pending) { pollTimer.Stop(); return; }
        if (polling) return;
        polling = true;
        var sid = SessionId;
        Task.Run(() =>
        {
            List<ChatMessage> msgs;
            try
            {
                if (sid == null) { UI(() => polling = false); return; }
                msgs = Api.GetMessages(sid);
            }
            catch
            {
                UI(() => polling = false);
                return;
            }
            UI(() => { polling = false; ProcessMessages(msgs); });
        });
    }

    void ProcessMessages(List<ChatMessage> msgs)
    {
        // API отдаёт сообщения от новых к старым — сортируем по времени
        var sorted = msgs.OrderBy(m => m.created).ToList();
        var lastUser = sorted.LastOrDefault(m => m.type == "user");
        var turn = lastUser == null ? sorted : sorted.Where(m => m.created >= lastUser.created).ToList();

        var asst = turn.Where(m => m.type == "assistant").ToList();
        if (asst.Count == 0) return;
        var lastAsst = asst[^1];
        var idle = turn.LastOrDefault(m => m.type == "idle");
        // ответ готов, когда после последнего сообщения ассистента пришёл idle
        if (idle == null || idle.created < lastAsst.created) return;

        pending = false;
        pollTimer.Stop();

        // финальный ответ — последнее сообщение хода, в котором есть текст
        var withText = asst.Where(m => m.texts.Any(t => !string.IsNullOrWhiteSpace(t))).ToList();
        var pick = withText.Count > 0 ? withText[^1] : null;
        var text = pick == null
            ? ""
            : string.Join("\r\n", pick.texts.Where(t => !string.IsNullOrWhiteSpace(t))).Trim();

        string err = null;
        if (!string.IsNullOrEmpty(lastAsst.error)) err = lastAsst.error;
        else if (idle.outcome == "failed")
            err = string.IsNullOrEmpty(idle.error) ? Lang.Tr("chat.unknownError") : idle.error;

        if (!string.IsNullOrEmpty(err) && text.Length == 0)
            AppendChat(Cfg.title, Lang.Tr("chat.errorPrefix", err), Theme.Error);
        else
        {
            AppendChat(Cfg.title, text, Theme.Accent);
            if (text.Length > 0) lastAnswer = text;   // для команды «повтори»
        }
        SetStatus(Lang.Tr("status.ready"));
    }

    // ------------------------------------------------------------------ настройки
    internal void ShowHotkeySettings()
    {
        using var dlg = new HotkeyDialog(this);
        hotkeys.UnregisterVoice();
        hotkeys.UnregisterWindow();
        try { dlg.ShowDialog(this); }
        finally
        {
            HotkeyCapture = false;
            ApplyHotkey("voice");
            ApplyHotkey("window");
            ApplyHotkeyLabels();
            if (!HotkeyOk || !WindowOk)
            {
                var busy = new List<string>();
                if (!HotkeyOk) busy.Add(HkVoice.Text);
                if (!WindowOk) busy.Add(HkWindow.Text);
                Balloon(Lang.Tr("balloon.busyKeysShort", Lang.JoinAnd(busy)), ToolTipIcon.Warning, 6000);
            }
        }
    }

    internal void ShowModelSettings()
    {
        using var dlg = new ModelDialog(this);
        dlg.ShowDialog(this);
    }

    internal void ShowSttSettings()
    {
        using var dlg = new SttModelDialog(this);
        dlg.ShowDialog(this);
    }

    // переключение модели распознавания: сохраняем в конфиг и грузим в фоне
    internal string ApplySttModel(string file)
    {
        if (stt == null) return Lang.Tr("stt.notConnected");
        if (stt.Busy) return Lang.Tr("stt.busy");
        if (!File.Exists(Path.Combine(ModelDir, file))) return Lang.Tr("stt.fileMissing", file);

        Cfg.sttModel = file;
        SaveConfigQuiet();
        ApplySttLabel();
        SetStatus(Lang.Tr("status.loadModel", SttEngine.ShortName(file)));
        Task.Run(() =>
        {
            stt.Load(file);
            UI(() =>
            {
                if (stt.Ready) SetStatus(Lang.Tr("status.sttModel", SttEngine.ShortName(file)));
                else
                {
                    SetStatus(Lang.Tr("status.modelFailed"));
                    Balloon(Lang.Tr("balloon.sttFailed", stt.LoadError ?? Lang.Tr("chat.unknownError")),
                        ToolTipIcon.Warning, 8000);
                }
                ApplySttLabel();
            });
        });
        return null;
    }

    internal void ApplySttLabel()
    {
        var short_ = SttEngine.ShortName(Cfg.sttModel);
        if (btnStt != null) btnStt.Text = "🎧 " + short_;
        if (MiStt != null) MiStt.Text = Lang.Tr("tray.stt.key", short_);
    }

    internal string ApplyModel(string id, string providerID)
    {
        Cfg.model = new ModelRef { id = id, providerID = providerID };
        SaveConfigQuiet();
        ApplyModelLabel();
        SetStatus(Lang.Tr("status.model", id));

        var note = "";
        if (!string.IsNullOrEmpty(SessionId))
        {
            try { Api.SetSessionModel(SessionId, id, providerID); }
            catch { note = Lang.Tr("model.switchFail"); }
        }
        return note;
    }

    internal void ApplyModelLabel()
    {
        if (btnModel != null) btnModel.Text = "🤖 " + Cfg.model.id;
        if (MiModel != null) MiModel.Text = Lang.Tr("tray.model.key", Cfg.model.id);
    }

    internal void SaveConfigQuiet()
    {
        try { Cfg.Save(configPath); }
        catch
        {
            // папка установки может быть только для чтения (Program Files) —
            // тогда храним настройки в профиле пользователя
            try
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Milli");
                Directory.CreateDirectory(dir);
                configPath = Path.Combine(dir, "config.json");
                Cfg.Save(configPath);
            }
            catch { }
        }
    }

    // ------------------------------------------------------------------ язык
    // смена языка: интерфейс, распознавание речи и персона — и всё сохраняется
    internal void SetLanguage(string code)
    {
        code = code == "en" ? "en" : "ru";
        // язык ставим до подстановки названия: иначе «app.name» вернётся на старом
        Lang.Code = code;
        if (code != Cfg.language)
        {
            Cfg.language = code;
            // персону пользователь не переписывал — подставляем текст нового языка
            if (Lang.IsDefaultPersona(Cfg.persona)) Cfg.persona = Lang.PersonaFor(code);
            // название помощницы на экране тоже на выбранном языке
            if (Cfg.title == "Милли" || Cfg.title == "Milli") Cfg.title = Lang.Tr("app.name");
        }

        if (stt != null) stt.Language = code;
        SaveConfigQuiet();
        ApplyLangUi();

        // в уже открытом разговоре меняем персону служебным сообщением, чтобы
        // не потерять контекст
        var sid = SessionId;
        if (sid != null)
        {
            var persona = Cfg.persona;
            Task.Run(() => { try { Api.SendSynthetic(sid, persona); } catch { } });
        }
    }

    internal void ApplyLangUi()
    {
        Text = Lang.Tr("app.name");
        if (tray != null) tray.Text = Lang.Tr("app.name");

        if (btnSend != null) btnSend.Text = Lang.Tr("win.send");
        if (btnStop != null) btnStop.Text = Lang.Tr("win.stop");
        if (tips != null)
        {
            tips.SetToolTip(btnSend, Lang.Tr("tip.send"));
            tips.SetToolTip(btnStop, Lang.Tr("tip.stop"));
            tips.SetToolTip(btnVoice, Lang.Tr("tip.voice"));
            tips.SetToolTip(btnModel, Lang.Tr("tip.model"));
            tips.SetToolTip(btnStt, Lang.Tr("tip.stt"));
        }

        if (MiStop != null) MiStop.Text = Lang.Tr("tray.stop");
        if (MiAuto != null) MiAuto.Text = Lang.Tr("tray.auto");
        if (MiKeys != null) MiKeys.Text = Lang.Tr("tray.keys");
        if (MiModel != null) MiModel.Text = Lang.Tr("tray.model");
        if (MiStt != null) MiStt.Text = Lang.Tr("tray.stt");
        if (MiOffline != null) MiOffline.Text = Lang.Tr("tray.offline");
        if (MiLang != null) MiLang.Text = Lang.Tr("tray.lang");
        if (MiLangRu != null) MiLangRu.Checked = !Lang.En;
        if (MiLangEn != null) MiLangEn.Checked = Lang.En;
        if (MiExit != null) MiExit.Text = Lang.Tr("tray.exit");

        ApplyHotkeyLabels();
        ApplyModelLabel();
        ApplySttLabel();

        // строку состояния трогаем, только если ничего не происходит
        if (!pending && (stt == null || !stt.Busy)) SetStatus(Lang.Tr("status.ready"));
    }

    // ------------------------------------------------------------------ выход
    void ExitApp()
    {
        exiting = true;
        try { pollTimer.Stop(); holdTimer.Stop(); remTimer.Stop(); } catch { }
        try { hotkeys.Dispose(); } catch { }
        try { stt?.Dispose(); } catch { }
        try { tray.Visible = false; tray.Dispose(); } catch { }
        try { tips?.Dispose(); } catch { }
        try { boldFont?.Dispose(); regularFont?.Dispose(); } catch { }
        try { Api.Dispose(); } catch { }
        Close();
    }

    // ------------------------------------------------------------------ команды
    // Локальные команды: выполняются на месте, без обращения к ИИ.
    void RunCommand(Commands.Result c)
    {
        switch (c.Kind)
        {
            case Commands.Kind.Stop: StopReply(c.Lang); break;
            case Commands.Kind.Repeat: RepeatLast(c.Lang); break;
            case Commands.Kind.NewChat: NewConversation(c.Lang); break;
            case Commands.Kind.Time:
                LocalReply(c.Lang, Lang.TrLang(c.Lang, "cmd.time", Commands.FormatTime(DateTime.Now)));
                break;
            case Commands.Kind.Date:
                LocalReply(c.Lang, Lang.TrLang(c.Lang, "cmd.date", Commands.FormatDate(c.Lang, DateTime.Now)));
                break;
            case Commands.Kind.Weekday:
                LocalReply(c.Lang, Lang.TrLang(c.Lang, "cmd.weekday", Commands.FormatWeekday(c.Lang, DateTime.Now)));
                break;
            case Commands.Kind.Help:
                LocalReply(c.Lang, Lang.TrLang(c.Lang, "cmd.help"));
                break;
            case Commands.Kind.OpenApp:
            case Commands.Kind.OpenFolder:
                OpenTarget(c);
                break;
            case Commands.Kind.Remind:
                if (c.Seconds <= 0) LocalReply(c.Lang, Lang.TrLang(c.Lang, "cmd.reminderBadTime"));
                else SetReminder(c.Lang, c.Seconds, c.Text);
                break;
            case Commands.Kind.Timer:
                if (c.Seconds <= 0) LocalReply(c.Lang, Lang.TrLang(c.Lang, "cmd.reminderBadTime"));
                else SetTimer(c.Lang, c.Seconds, c.Text);
                break;
            case Commands.Kind.TimerCancel:
                if (timers.Count == 0) LocalReply(c.Lang, Lang.TrLang(c.Lang, "cmd.timerCancelNone"));
                else { timers.Clear(); LocalReply(c.Lang, Lang.TrLang(c.Lang, "cmd.timerCancel")); }
                break;
            case Commands.Kind.RemindList:
                if (reminders.Count == 0) LocalReply(c.Lang, Lang.TrLang(c.Lang, "cmd.remindListEmpty"));
                else
                {
                    var list = string.Join("; ", reminders.OrderBy(r => r.Due).Select(r => r.Due.ToString("HH:mm") + (string.IsNullOrWhiteSpace(r.Text) ? "" : " " + r.Text)));
                    LocalReply(c.Lang, Lang.TrLang(c.Lang, "cmd.remindList", list));
                }
                break;
            case Commands.Kind.RemindClear:
                if (reminders.Count == 0) LocalReply(c.Lang, Lang.TrLang(c.Lang, "cmd.remindListEmpty"));
                else { reminders.Clear(); LocalReply(c.Lang, Lang.TrLang(c.Lang, "cmd.remindClear")); }
                break;
            case Commands.Kind.NoteAdd:
                if (string.IsNullOrWhiteSpace(c.Text)) { LocalReply(c.Lang, Lang.TrLang(c.Lang, "cmd.noteEmpty")); break; }
                notes.Add(c.Text.Trim());
                SaveNotes();
                LocalReply(c.Lang, Lang.TrLang(c.Lang, "cmd.noteAdded"));
                break;
            case Commands.Kind.NoteRead:
                if (notes.Count == 0) { LocalReply(c.Lang, Lang.TrLang(c.Lang, "cmd.noteNone")); break; }
                var nl = string.Join("\n", notes.Select((n,i) => (i+1) + ". " + n));
                LocalReply(c.Lang, Lang.TrLang(c.Lang, "cmd.noteList", nl));
                break;
            case Commands.Kind.NoteClear:
                if (notes.Count == 0) { LocalReply(c.Lang, Lang.TrLang(c.Lang, "cmd.noteNone")); break; }
                notes.Clear();
                SaveNotes();
                LocalReply(c.Lang, Lang.TrLang(c.Lang, "cmd.noteCleared"));
                break;
            case Commands.Kind.Calc:
                if (c.Seconds == 0 && double.IsNaN(c.Number)) { LocalReply(c.Lang, Lang.TrLang(c.Lang, "cmd.calcBad")); break; }
                var res = double.IsInfinity(c.Number) ? c.Text : c.Number.ToString("0.#####", System.Globalization.CultureInfo.InvariantCulture);
                LocalReply(c.Lang, Lang.TrLang(c.Lang, "cmd.calcResult", c.Text, res));
                break;
            case Commands.Kind.VolumeUp: TryVolume("+10", c.Lang); break;
            case Commands.Kind.VolumeDown: TryVolume("-10", c.Lang); break;
            case Commands.Kind.VolumeMute: TryVolume("mute", c.Lang); break;
            case Commands.Kind.VolumeUnmute: TryVolume("unmute", c.Lang); break;
            case Commands.Kind.VolumeGet: TryVolume("get", c.Lang); break;
            case Commands.Kind.VolumeSet: TryVolume("set:" + ((int)Math.Round(c.Number)), c.Lang); break;
            case Commands.Kind.Lock: TrySystem("lock", c.Lang); break;
            case Commands.Kind.Sleep: TrySystem("sleep", c.Lang); break;
            case Commands.Kind.Screenshot: DoScreenshot(c.Lang); break;
            case Commands.Kind.ShowDesktop: TrySystem("desktop", c.Lang); break;
            case Commands.Kind.Shutdown: TrySystem("shutdown", c.Lang); break;
            case Commands.Kind.Restart: TrySystem("restart", c.Lang); break;
            case Commands.Kind.ShutdownCancel: TrySystem("cancel", c.Lang); break;
        }
    }

    // ответ Милли на месте: в чат, а если окно спрятано — ещё и подсказкой в трее
    void LocalReply(string lang, string text)
    {
        AppendChat(Cfg.title, text, Theme.Accent);
        SetStatus(Lang.Tr("status.ready"));
        if (!Visible || !ContainsFocus) Balloon(Shorten(text), ToolTipIcon.Info, 6000);
    }

    static string Shorten(string s) => s.Length <= 250 ? s : s[..250] + "…";

    void StopReply(string lang)
    {
        var sid = SessionId;
        if (sid == null || !pending) { LocalReply(lang, Lang.TrLang(lang, "cmd.stopNothing")); return; }
        SetStatus(Lang.Tr("status.stopping"));
        LocalReply(lang, Lang.TrLang(lang, "cmd.stopping"));
        // опрос продолжается: сервер дошлёт частичный ответ с outcome=interrupted
        Task.Run(() =>
        {
            try { Api.Interrupt(sid); }
            catch (Exception ex) { UI(() => AppendChat(Lang.Tr("chat.error"), ex.Message, Theme.Error)); }
        });
    }

    void RepeatLast(string lang)
    {
        if (string.IsNullOrEmpty(lastAnswer)) { LocalReply(lang, Lang.TrLang(lang, "cmd.repeatNone")); return; }
        AppendChat(Cfg.title, lastAnswer, Theme.Accent);
        SetStatus(Lang.Tr("status.ready"));
        if (!Visible || !ContainsFocus) Balloon(Shorten(lastAnswer), ToolTipIcon.Info, 8000);
    }

    void NewConversation(string lang)
    {
        pending = false;
        try { pollTimer.Stop(); } catch { }
        SessionId = null;      // следующий вопрос создаст новую сессию
        lastAnswer = null;
        try { chat.Clear(); } catch { }
        LocalReply(lang, Lang.TrLang(lang, "cmd.newChat"));
    }

    void OpenTarget(Commands.Result c)
    {
        bool folder = c.Kind == Commands.Kind.OpenFolder;
        if (folder)
        {
            if (string.IsNullOrEmpty(c.Path) || !Directory.Exists(c.Path))
            {
                LocalReply(c.Lang, Lang.TrLang(c.Lang, "cmd.folderNotFound", c.Arg));
                return;
            }
        }
        else if (!string.IsNullOrEmpty(c.Path) && Path.IsPathRooted(c.Path) && !File.Exists(c.Path))
        {
            // системные команды (calc.exe) проверке не подлежат
            LocalReply(c.Lang, Lang.TrLang(c.Lang, "cmd.appNotFound", c.Arg));
            return;
        }

        try
        {
            Commands.Launch(c.Path);
            LocalReply(c.Lang, Lang.TrLang(c.Lang, folder ? "cmd.folderOpened" : "cmd.appOpened", c.Arg));
        }
        catch (Exception ex)
        {
            LocalReply(c.Lang, Lang.TrLang(c.Lang, "cmd.appFailed", c.Arg, ex.Message));
        }
    }

    string NotesPath => Path.Combine(Path.GetDirectoryName(configPath) ?? AppContext.BaseDirectory, "notes.json");
    void LoadNotes() { try { if (!File.Exists(NotesPath)) return; var j = File.ReadAllText(NotesPath, Encoding.UTF8); var arr = JsonSerializer.Deserialize<List<string>>(j); if (arr != null) { notes.Clear(); notes.AddRange(arr.Where(x => !string.IsNullOrWhiteSpace(x))); } } catch { } }
    void SaveNotes() { try { var j = JsonSerializer.Serialize(notes); File.WriteAllText(NotesPath, j, new UTF8Encoding(false)); } catch { } }

    // ------------------------------------------------------------------ напоминания/таймеры/заметки
    void SetReminder(string lang, int seconds, string text)
    {
        reminders.Add(new Reminder { Due = DateTime.Now.AddSeconds(seconds), Text = text ?? "" });
        var d = Commands.DurationText(lang, seconds);
        LocalReply(lang, string.IsNullOrWhiteSpace(text)
            ? Lang.TrLang(lang, "cmd.reminderSetShort", d)
            : Lang.TrLang(lang, "cmd.reminderSet", d, text));
    }

    void SetTimer(string lang, int seconds, string text)
    {
        timers.Add(new TimerItem { Due = DateTime.Now.AddSeconds(seconds), Text = string.IsNullOrWhiteSpace(text) ? (lang == "en" ? "Timer" : "Таймер") : text });
        var d = Commands.DurationText(lang, seconds);
        LocalReply(lang, string.IsNullOrWhiteSpace(text)
            ? Lang.TrLang(lang, "cmd.timerSetShort", d)
            : Lang.TrLang(lang, "cmd.timerSet", d, text));
    }

    void CheckReminders()
    {
        if (reminders.Count == 0 && timers.Count == 0) return;
        var now = DateTime.Now;
        for (int i = reminders.Count - 1; i >= 0; i--)
            if (reminders[i].Due <= now)
            {
                var r = reminders[i];
                reminders.RemoveAt(i);
                FireReminder(r);
            }
        for (int i = timers.Count - 1; i >= 0; i--)
            if (timers[i].Due <= now)
            {
                var r = timers[i];
                timers.RemoveAt(i);
                FireTimer(r);
            }
    }

    void FireReminder(Reminder r)
    {
        var text = Lang.Tr("cmd.reminderFired", string.IsNullOrWhiteSpace(r.Text) ? "" : r.Text);
        AppendChat(Cfg.title, text, Theme.Accent);
        Balloon(Shorten(text), ToolTipIcon.Info, 15000);
    }

    void FireTimer(TimerItem r)
    {
        var text = Lang.Tr("cmd.timerFired", string.IsNullOrWhiteSpace(r.Text) ? (Lang.Code == "en" ? "Timer" : "Таймер") : r.Text);
        AppendChat(Cfg.title, text, Theme.Accent);
        try { System.Media.SystemSounds.Exclamation.Play(); } catch { }
        Balloon(Shorten(text), ToolTipIcon.Info, 15000);
    }

    void TryVolume(string cmd, string lang)
    {
        try
        {
            if (cmd.StartsWith("set:") && int.TryParse(cmd.Substring(4), out var pset))
            {
                var v = Math.Max(0, Math.Min(100, pset));
                using var proc = new Process();
                proc.StartInfo.FileName = "powershell";
                proc.StartInfo.Arguments = "-WindowStyle Hidden -ExecutionPolicy Bypass -Command \"$wsh = New-Object -ComObject WScript.Shell; 1..50|%{ $wsh.SendKeys([char]174) }; 1.." + (Math.Round(v/2.0)).ToString() + "|%{ $wsh.SendKeys([char]175) }\"";
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.CreateNoWindow = true;
                proc.Start();
                LocalReply(lang, Lang.TrLang(lang, "cmd.volumeSet", v));
                return;
            }
            if (cmd == "+10")
            {
                using var proc = new Process();
                proc.StartInfo.FileName = "powershell";
                proc.StartInfo.Arguments = "-WindowStyle Hidden -ExecutionPolicy Bypass -Command \"$wsh = New-Object -ComObject WScript.Shell; 1..5|%{ $wsh.SendKeys([char]175) }\"";
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.CreateNoWindow = true;
                proc.Start();
                LocalReply(lang, Lang.TrLang(lang, "cmd.volumeUp", "10"));
                return;
            }
            if (cmd == "-10")
            {
                using var proc = new Process();
                proc.StartInfo.FileName = "powershell";
                proc.StartInfo.Arguments = "-WindowStyle Hidden -ExecutionPolicy Bypass -Command \"$wsh = New-Object -ComObject WScript.Shell; 1..5|%{ $wsh.SendKeys([char]174) }\"";
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.CreateNoWindow = true;
                proc.Start();
                LocalReply(lang, Lang.TrLang(lang, "cmd.volumeDown", "10"));
                return;
            }
            if (cmd == "mute")
            {
                using var proc = new Process();
                proc.StartInfo.FileName = "powershell";
                proc.StartInfo.Arguments = "-WindowStyle Hidden -ExecutionPolicy Bypass -Command \"$wsh = New-Object -ComObject WScript.Shell; $wsh.SendKeys([char]173)\"";
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.CreateNoWindow = true;
                proc.Start();
                LocalReply(lang, Lang.TrLang(lang, "cmd.volumeMute"));
                return;
            }
            if (cmd == "unmute")
            {
                using var proc = new Process();
                proc.StartInfo.FileName = "powershell";
                proc.StartInfo.Arguments = "-WindowStyle Hidden -ExecutionPolicy Bypass -Command \"$wsh = New-Object -ComObject WScript.Shell; $wsh.SendKeys([char]173)\"";
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.CreateNoWindow = true;
                proc.Start();
                LocalReply(lang, Lang.TrLang(lang, "cmd.volumeUnmute"));
                return;
            }
            LocalReply(lang, Lang.TrLang(lang, "cmd.volumeUp"));
        }
        catch { LocalReply(lang, Lang.TrLang(lang, "cmd.volumeUp")); }
    }

    void TrySystem(string cmd, string lang)
    {
        try
        {
            if (cmd == "lock") { Process.Start(new ProcessStartInfo { FileName = "rundll32.exe", Arguments = "user32.dll,LockWorkStation", UseShellExecute = true }); LocalReply(lang, Lang.TrLang(lang, "cmd.lock")); return; }
            if (cmd == "sleep") { Process.Start(new ProcessStartInfo { FileName = "rundll32.exe", Arguments = "powrprof.dll,SetSuspendState 0,1,0", UseShellExecute = true }); LocalReply(lang, Lang.TrLang(lang, "cmd.sleep")); return; }
            if (cmd == "desktop") { Process.Start(new ProcessStartInfo { FileName = "powershell", Arguments = "-WindowStyle Hidden -Command \"$s=New-Object -ComObject Shell.Application; $s.ToggleDesktop()\"", UseShellExecute = false, CreateNoWindow = true }); LocalReply(lang, Lang.TrLang(lang, "cmd.desktop")); return; }
            if (cmd == "shutdown") { Process.Start(new ProcessStartInfo { FileName = "shutdown", Arguments = "/s /t 30", UseShellExecute = false, CreateNoWindow = true }); LocalReply(lang, Lang.TrLang(lang, "cmd.shutdown")); return; }
            if (cmd == "restart") { Process.Start(new ProcessStartInfo { FileName = "shutdown", Arguments = "/r /t 30", UseShellExecute = false, CreateNoWindow = true }); LocalReply(lang, Lang.TrLang(lang, "cmd.restart")); return; }
            if (cmd == "cancel") { var p = Process.Start(new ProcessStartInfo { FileName = "shutdown", Arguments = "/a", UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true }); p?.WaitForExit(2000); LocalReply(lang, Lang.TrLang(lang, "cmd.shutdownCancel")); return; }
        }
        catch { }
    }

    void DoScreenshot(string lang)
    {
        try
        {
            var dir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var file = Path.Combine(dir, "Milli_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
            using var bmp = new System.Drawing.Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);
            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(Screen.PrimaryScreen.Bounds.X, Screen.PrimaryScreen.Bounds.Y, 0, 0, bmp.Size);
            }
            bmp.Save(file, System.Drawing.Imaging.ImageFormat.Png);
            LocalReply(lang, Lang.TrLang(lang, "cmd.screenshot", file));
        }
        catch { LocalReply(lang, Lang.TrLang(lang, "cmd.screenshotFail")); }
    }

    void UpdateOfflineStatus()
    {
        try
        {
            if (MiOffline != null) MiOffline.Checked = Cfg.offlineMode;
            SetStatus(Cfg.offlineMode ? (Lang.Code == "en" ? "offline" : "автономный режим") : (pending ? Lang.Tr("status.thinking") : Lang.Tr("status.ready")));
        }
        catch { }
    }

    void UI(Action action)
    {
        try { if (IsHandleCreated) BeginInvoke(action); } catch { }
    }
}

// отложенное напоминание/таймер
class Reminder
{
    public DateTime Due;
    public string Text;
}

class TimerItem
{
    public DateTime Due;
    public string Text;
}
