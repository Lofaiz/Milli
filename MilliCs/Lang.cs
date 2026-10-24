// Язык интерфейса: русский и английский. В config.json лежит код языка («ru»
// или «en»), он же уходит в распознавание речи (Whisper) и в персону — Милли
// отвечает на выбранном языке. Все видимые человеку строки берутся отсюда,
// в остальном коде русского текста быть не должно.
using System.Globalization;

namespace Milli;

static class Lang
{
    // «ru» или «en»; ставится из config.json до создания окон
    public static string Code { get; set; } = "ru";
    public static bool En => Code == "en";

    // Персона по умолчанию. Русский текст совпадает с тем, что был раньше,
    // поэтому у уже настроенных копий он считается «неизменённым» и при смене
    // языка заменяется на текст выбранного языка.
    public const string DefaultPersonaRu =
        "Тебя зовут Милли. Ты голосовая помощница. Общайся спокойно, нейтрально и по делу — как деловой собеседник. " +
        "Не используй ласковые обращения и подобные фразы («милый», «милая», «дорогой»), не сюсюкай и не добавляй комплиментов. " +
        "Отвечай коротко и точно, простыми предложениями. Не используй списки, таблицы, разметку и эмодзи. " +
        "Если спрашивают твоё имя — отвечай «Милли».";

    public const string DefaultPersonaEn =
        "Your name is Milli. You are a voice assistant. Speak calmly, neutrally and to the point, like a business partner. " +
        "Do not use pet names or endearments, do not baby-talk and do not add compliments. " +
        "Answer briefly and precisely, in simple sentences. Do not use lists, tables, markup or emoji. " +
        "Always answer in English. If you are asked your name, answer “Milli”.";

    public static string PersonaFor(string code) => code == "en" ? DefaultPersonaEn : DefaultPersonaRu;

    // персону не меняли руками — значит, при смене языка её можно подменить
    public static bool IsDefaultPersona(string p) =>
        string.IsNullOrWhiteSpace(p) || p == DefaultPersonaRu || p == DefaultPersonaEn;

    // ------------------------------------------------------------------ строки
    static readonly Dictionary<string, string[]> Table = new()
    {
        // названия
        ["app.name"] = new[] { "Милли", "Milli" },
        ["app.full"] = new[] { "Милли — голосовой помощник", "Milli — voice assistant" },
        ["join.and"] = new[] { " и ", " and " },

        // главное окно
        ["win.send"] = new[] { "Отправить  (Enter)", "Send  (Enter)" },
        ["win.voice"] = new[] { "🎤 Голос", "🎤 Voice" },
        ["win.voice.key"] = new[] { "🎤 Голос  (держать {0})", "🎤 Voice  (hold {0})" },
        ["tip.send"] = new[] { "Отправить сообщение в OpenCode (Enter)", "Send the message to OpenCode (Enter)" },
        ["tip.voice"] = new[] { "Держать, чтобы говорить. Отпустить — Милли распознает и ответит",
                                "Hold to talk. Release — Milli recognizes what you said and answers" },
        ["tip.model"] = new[] { "Модель ИИ, которой отвечает Милли", "The AI model Milli answers with" },
        ["tip.stt"] = new[] { "Модель распознавания речи: чем точнее, тем дольше думает",
                              "Speech recognition model: the more accurate, the longer it thinks" },

        // меню в трее
        ["tray.show"] = new[] { "Показать окно", "Show window" },
        ["tray.show.key"] = new[] { "Показать окно  ({0})", "Show window  ({0})" },
        ["tray.voice"] = new[] { "🎤 Голосовой ввод", "🎤 Voice input" },
        ["tray.voice.key"] = new[] { "🎤 Голосовой ввод  (держать {0})", "🎤 Voice input  (hold {0})" },
        ["tray.auto"] = new[] { "Голос → отправлять сразу", "Voice → send immediately" },
        ["tray.offline"] = new[] { "Автономный режим", "Offline mode" },
        ["tray.keys"] = new[] { "Настройки клавиш…", "Hotkeys…" },
        ["tray.model"] = new[] { "Модель ИИ…", "AI model…" },
        ["tray.model.key"] = new[] { "Модель ИИ…  ({0})", "AI model…  ({0})" },
        ["tray.stt"] = new[] { "🎧 Модель распознавания…", "🎧 Speech model…" },
        ["tray.stt.key"] = new[] { "🎧 Модель распознавания…  ({0})", "🎧 Speech model…  ({0})" },
        ["tray.lang"] = new[] { "Язык", "Language" },
        ["tray.exit"] = new[] { "Выход", "Exit" },

        // состояние (строка внизу окна)
        ["status.ready"] = new[] { "готов", "ready" },
        ["status.unavailable"] = new[] { "распознаватель недоступен", "speech engine unavailable" },
        ["status.busy"] = new[] { "уже слушаю...", "already listening..." },
        ["status.loading"] = new[] { "загружаю распознаватель...", "loading the speech engine..." },
        ["status.noMic"] = new[] { "микрофон недоступен", "microphone unavailable" },
        ["status.listening"] = new[] { "🎤 слушаю...", "🎤 listening..." },
        ["status.recognizing"] = new[] { "распознаю...", "recognizing..." },
        ["status.recognized"] = new[] { "распознано", "recognized" },
        ["status.noSpeech"] = new[] { "речь не обнаружена", "no speech detected" },
        ["status.recogError"] = new[] { "ошибка распознавания", "recognition error" },
        ["status.thinking"] = new[] { "думаю...", "thinking..." },
        ["status.error"] = new[] { "ошибка", "error" },
        ["status.model"] = new[] { "модель: {0}", "model: {0}" },
        ["status.loadModel"] = new[] { "загружаю модель {0}...", "loading model {0}..." },
        ["status.sttModel"] = new[] { "модель распознавания: {0}", "speech model: {0}" },
        ["status.modelFailed"] = new[] { "модель не загрузилась", "the model failed to load" },
        ["status.cleaned"] = new[] { "убрано старых сессий: {0}", "old sessions removed: {0}" },

        // переписка
        ["chat.you"] = new[] { "Вы", "You" },
        ["chat.error"] = new[] { "Ошибка", "Error" },
        ["chat.errorPrefix"] = new[] { "Ошибка: {0}", "Error: {0}" },
        ["chat.unknownError"] = new[] { "неизвестная ошибка", "unknown error" },

        // всплывающие подсказки в трее
        ["balloon.startHint"] = new[]
        {
            "{0} — показать или убрать окно, {1} — говорить по удержанию. Сменить клавиши: «Настройки клавиш…».",
            "{0} shows or hides the window, {1} talks while held. To change them: “Hotkeys…”.",
        },
        ["balloon.busyKeys"] = new[]
        {
            "{0} заняты другой программой — эти клавиши сработают только в открытом окне. Сменить их: трей → «Настройки клавиш…».",
            "{0} are taken by another program — they will work only while the window is open. To change them: tray → “Hotkeys…”.",
        },
        ["balloon.busyKeysShort"] = new[]
        {
            "{0} заняты другой программой — эти клавиши сработают только в открытом окне.",
            "{0} are taken by another program — they will work only while the window is open.",
        },
        ["balloon.voiceOff"] = new[] { "Распознавание речи не работает: {0}", "Speech recognition is not working: {0}" },
        ["balloon.noModel"] = new[]
        {
            "Модель распознавания не найдена: положите файл ggml-*.bin в папку model рядом с программой.",
            "No speech model found: put a ggml-*.bin file into the model folder next to the program.",
        },
        ["balloon.sttFailed"] = new[]
        {
            "Не удалось загрузить модель распознавания: {0}",
            "Could not load the speech model: {0}",
        },

        // распознавание речи
        ["stt.notConnected"] = new[]
        {
            "Распознаватель не подключён: в папке model нет моделей.",
            "The speech engine is not connected: there are no models in the model folder.",
        },
        ["stt.busy"] = new[]
        {
            "Сейчас идёт запись или распознавание — повторите через пару секунд.",
            "Recording or recognition is in progress — try again in a couple of seconds.",
        },
        ["stt.fileMissing"] = new[] { "Файл модели не найден: {0}", "Model file not found: {0}" },
        ["stt.micFail"] = new[] { "нет доступа к микрофону: {0}", "no access to the microphone: {0}" },
        ["stt.notLoaded"] = new[] { "модель распознавания не загружена", "the speech model is not loaded" },
        ["stt.recogFail"] = new[] { "ошибка распознавания: {0}", "recognition error: {0}" },
        ["api.notFound"] = new[]
        {
            "Локальный сервер OpenCode не найден. Открыто ли приложение OpenCode?",
            "The local OpenCode server was not found. Is the OpenCode app running?",
        },
        ["api.httpError"] = new[]
        {
            "OpenCode вернул {0}: {1}",
            "OpenCode returned {0}: {1}",
        },
        ["stt.desc.tiny"] = new[] { "самая быстрая, точность низкая", "the fastest, low accuracy" },
        ["stt.desc.base"] = new[] { "быстрая, точность базовая", "fast, basic accuracy" },
        ["stt.desc.small"] = new[] { "точная, но на слабом процессоре заметно медленнее",
                                     "accurate, but noticeably slower on a weak processor" },
        ["stt.desc.medium"] = new[] { "очень точная, требует сильного процессора",
                                      "very accurate, needs a powerful processor" },
        ["stt.desc.large"] = new[] { "наиболее точная, на слабом процессоре не подходит",
                                     "the most accurate, not suitable for a weak processor" },

        // клавиши
        ["hotkey.busyStay"] = new[]
        {
            "«{0}» занята другой программой или системой — оставил прежнюю клавишу",
            "“{0}” is taken by another program or the system — the previous key was kept",
        },
        ["hotkey.resetBusy"] = new[]
        {
            "{0} заняты другой программой — выберите другие клавиши.",
            "{0} are taken by another program — please choose other keys.",
        },

        // модель ИИ
        ["model.switchFail"] = new[]
        {
            " (в текущем разговоре переключить не удалось — применится в новом)",
            " (could not switch it in the current conversation — it will apply in a new one)",
        },
        ["price.none"] = new[] { "цена не указана", "price not specified" },
        ["price.free"] = new[] { "бесплатно", "free" },
        ["price.paid"] = new[]
        {
            "платно: вход ${0} / выход ${1} за 1 млн токенов",
            "paid: input ${0} / output ${1} per 1M tokens",
        },

        // диалог клавиш
        ["dialog.keys.title"] = new[] { "Клавиши Милли", "Milli hotkeys" },
        ["dialog.keys.hint"] = new[]
        {
            "Клавиши работают глобально, пока Милли запущена — даже когда окно спрятано в трей.",
            "The keys work globally while Milli is running — even when the window is hidden in the tray.",
        },
        ["dialog.keys.voice"] = new[] { "Голосовой ввод (держать клавишу):", "Voice input (hold the key):" },
        ["dialog.keys.window"] = new[] { "Показать / убрать окно:", "Show / hide window:" },
        ["dialog.keys.help"] = new[]
        {
            "Подойдут функциональные клавиши (F1–F24), клавиши цифрового блока (Num0–Num9), Pause, ScrollLock,\n" +
            "а также любая клавиша вместе с Ctrl, Alt или Shift. Буквы и цифры без модификатора нельзя:\n" +
            "такая клавиша перестала бы печататься во всех остальных программах.",
            "You can use the function keys (F1–F24), the numeric keypad (Num0–Num9), Pause, ScrollLock,\n" +
            "or any key together with Ctrl, Alt or Shift. Letters and digits without a modifier are not\n" +
            "allowed: the key would stop being typed in every other program.",
        },
        ["dialog.keys.prompt"] = new[] { "Нажмите «Изменить», затем — нужную клавишу.", "Press “Change”, then the key you want." },
        ["dialog.keys.change"] = new[] { "Изменить", "Change" },
        ["dialog.keys.defaults"] = new[] { "Клавиши по умолчанию", "Default keys" },
        ["dialog.keys.capture"] = new[]
        {
            "Нажмите клавишу для {0} — можно с Ctrl, Alt или Shift. Esc — отмена.",
            "Press a key for {0} — you may use Ctrl, Alt or Shift. Escape cancels.",
        },
        ["dialog.keys.forWindow"] = new[] { "окна", "the window" },
        ["dialog.keys.forVoice"] = new[] { "голосового ввода", "voice input" },
        ["dialog.keys.resetOk"] = new[]
        {
            "Клавиши по умолчанию: Num9 — голос, Num8 — окно.",
            "Default keys: Num9 for voice, Num8 for the window.",
        },
        ["dialog.keys.cancelled"] = new[] { "Отменено.", "Cancelled." },
        ["dialog.keys.notAllowed"] = new[]
        {
            "Эту клавишу назначить нельзя. Подойдут F1–F24, цифровой блок, Pause, ScrollLock " +
            "или клавиша с Ctrl/Alt/Shift.",
            "This key cannot be assigned. Use F1–F24, the numeric keypad, Pause, ScrollLock " +
            "or a key with Ctrl/Alt/Shift.",
        },
        ["dialog.keys.needMod"] = new[]
        {
            "«{0}» без Ctrl, Alt или Shift нельзя — клавиша перехватывалась бы во всех программах.",
            "“{0}” needs Ctrl, Alt or Shift — otherwise the key would be captured in every program.",
        },
        ["dialog.keys.taken"] = new[] { "«{0}» уже назначена другому действию.", "“{0}” is already assigned to another action." },
        ["dialog.keys.assigned"] = new[] { "Назначено: {0}", "Assigned: {0}" },

        // диалог модели ИИ
        ["dialog.model.title"] = new[] { "Модель Милли", "Milli model" },
        ["dialog.model.hint"] = new[]
        {
            "Какой моделью ИИ отвечает Милли. Список берётся у OpenCode, выбор сохраняется.",
            "Which AI model Milli answers with. The list comes from OpenCode; the choice is saved.",
        },
        ["dialog.search"] = new[] { "Поиск:", "Search:" },
        ["dialog.model.refresh"] = new[] { "Обновить список", "Refresh list" },
        ["dialog.model.loading"] = new[] { "Загружаю список моделей…", "Loading the model list…" },
        ["dialog.model.loadFail"] = new[]
        {
            "Не удалось получить список моделей от OpenCode: {0}",
            "Could not get the model list from OpenCode: {0}",
        },
        ["dialog.model.empty"] = new[] { "OpenCode не вернул ни одной доступной модели.", "OpenCode returned no available models." },
        ["dialog.model.count"] = new[]
        {
            "Доступно моделей: {0}. Текущая: {1}",
            "Models available: {0}. Current: {1}",
        },
        ["dialog.model.context"] = new[] { "  ·  контекст: {0} токенов", "  ·  context: {0} tokens" },
        ["dialog.model.already"] = new[] { "Эта модель уже выбрана: {0}", "This model is already selected: {0}" },
        ["dialog.model.selected"] = new[] { "Выбрана модель: {0}", "Model selected: {0}" },
        ["dialog.pickFirst"] = new[] { "Сначала выберите модель в списке.", "Select a model in the list first." },
        ["dialog.apply"] = new[] { "Применить", "Apply" },
        ["dialog.close"] = new[] { "Закрыть", "Close" },

        // диалог модели распознавания
        ["dialog.stt.title"] = new[] { "Модель распознавания речи", "Speech recognition model" },
        ["dialog.stt.hint"] = new[]
        {
            "Чем точнее модель, тем дольше она думает. Файлы лежат в папке model рядом с программой.",
            "The more accurate the model, the longer it thinks. The files live in the model folder next to the program.",
        },
        ["dialog.stt.none"] = new[] { "В папке model нет ни одного файла ggml-*.bin.", "There is no ggml-*.bin file in the model folder." },
        ["dialog.stt.mb"] = new[] { "МБ", "MB" },
        ["dialog.stt.current"] = new[] { "Сейчас: {0}", "Current: {0}" },
        ["dialog.stt.loaded"] = new[] { " (загружена)", " (loaded)" },
        ["dialog.stt.notLoaded"] = new[] { " (не загружена)", " (not loaded)" },
        ["dialog.stt.speedFast"] = new[] { "можно использовать постоянно", "good for everyday use" },
        ["dialog.stt.speedSlow"] = new[] { "заметная пауза после каждой фразы", "a noticeable pause after every phrase" },
        ["dialog.stt.inUse"] = new[] { "Эта модель уже используется.", "This model is already in use." },
        ["dialog.stt.picked"] = new[]
        {
            "Выбрана модель распознавания: {0} — загружаю в фоне.",
            "Speech model selected: {0} — loading it in the background.",
        },

        // кнопка «Стоп»: прервать текущий ответ
        ["win.stop"] = new[] { "⏹ Стоп", "⏹ Stop" },
        ["tray.stop"] = new[] { "⏹ Остановить ответ", "⏹ Stop the answer" },
        ["tip.stop"] = new[] { "Прервать ответ Милли", "Interrupt Milli's answer" },
        ["status.stopping"] = new[] { "останавливаю...", "stopping..." },

        // локальные команды: ответы Милли. Берутся на языке распознанной фразы
        // (см. Lang.TrLang(код, ключ)) — русская фраза отвечает по-русски, даже если
        // интерфейс переключён на английский.
        ["cmd.stopNothing"] = new[] { "Сейчас нечего останавливать.", "There is nothing to stop right now." },
        ["cmd.stopping"] = new[] { "Останавливаю.", "Stopping." },
        ["cmd.repeatNone"] = new[] { "Мне пока нечего повторять.", "I have nothing to repeat yet." },
        ["cmd.newChat"] = new[] { "Начинаю новый разговор.", "Starting a new conversation." },
        ["cmd.time"] = new[] { "Сейчас {0}.", "It is {0} now." },
        ["cmd.date"] = new[] { "Сегодня {0}.", "Today is {0}." },
        ["cmd.weekday"] = new[] { "Сегодня {0}.", "Today is {0}." },
        ["cmd.appOpened"] = new[] { "Открываю: {0}.", "Opening {0}." },
        ["cmd.appNotFound"] = new[] { "Не нашёл приложение «{0}».", "I could not find the app “{0}”." },
        ["cmd.appFailed"] = new[] { "Не удалось открыть «{0}»: {1}", "Could not open “{0}”: {1}" },
        ["cmd.folderOpened"] = new[] { "Открываю: {0}.", "Opening {0}." },
        ["cmd.folderNotFound"] = new[] { "Не нашёл папку «{0}».", "I could not find the folder “{0}”." },
        ["cmd.reminderSet"] = new[] { "Напомню через {0}: {1}", "I will remind you in {0}: {1}" },
        ["cmd.reminderSetShort"] = new[] { "Напомню через {0}.", "I will remind you in {0}." },
        ["cmd.reminderFired"] = new[] { "Напоминание: {0}", "Reminder: {0}" },
        ["cmd.reminderBadTime"] = new[]
        {
            "Не понял время напоминания. Скажи, например: «напомни через 10 минут позвонить».",
            "I did not understand the reminder time. Say, for example: “remind me in 10 minutes to call”.",
        },
        ["cmd.help"] = new[]
        {
            "Могу: время/дата/день недели, открыть приложение/папку, напомнить/таймер, мои напоминания/отменить напоминания, записать/прочитать/удалить заметки, посчитать, громче/тише/громкость/звук, заблокировать/спящий режим, скриншот/свернуть окна, выключить/перезагрузить/отменить выключение, стоп/повтори/новый разговор.",
            "I can: time/date/weekday, open app/folder, remind/timer, my reminders/cancel reminders, note/read/clear notes, calculate, volume/mute, lock/sleep, screenshot/show desktop, shutdown/restart/cancel shutdown, stop/repeat/new chat.",
        },
        ["cmd.timerSet"] = new[] { "Таймер через {0}: {1}", "Timer set for {0}: {1}" },
        ["cmd.timerSetShort"] = new[] { "Таймер через {0}.", "Timer set for {0}." },
        ["cmd.timerFired"] = new[] { "Таймер: {0}", "Timer: {0}" },
        ["cmd.timerCancel"] = new[] { "Таймер отменён.", "Timer cancelled." },
        ["cmd.timerCancelNone"] = new[] { "Таймеров нет.", "No timers running." },
        ["cmd.remindListEmpty"] = new[] { "Напоминаний нет.", "No reminders." },
        ["cmd.remindList"] = new[] { "Напоминания: {0}", "Reminders: {0}" },
        ["cmd.remindClear"] = new[] { "Все напоминания отменены.", "All reminders cancelled." },
        ["cmd.noteEmpty"] = new[] { "Указать текст заметки.", "Say what to note." },
        ["cmd.noteAdded"] = new[] { "Заметка сохранена.", "Note saved." },
        ["cmd.noteNone"] = new[] { "Заметок нет.", "No notes." },
        ["cmd.noteList"] = new[] { "Заметки:\n{0}", "Notes:\n{0}" },
        ["cmd.noteCleared"] = new[] { "Заметки удалены.", "Notes cleared." },
        ["cmd.calcBad"] = new[] { "Не могу посчитать.", "Cannot calculate." },
        ["cmd.calcResult"] = new[] { "{0} = {1}", "{0} = {1}" },
        ["cmd.volumeSet"] = new[] { "Громкость: {0}%.", "Volume: {0}%." },
        ["cmd.volumeGet"] = new[] { "Громкость: {0}%.", "Volume: {0}%." },
        ["cmd.volumeMute"] = new[] { "Звук выключен.", "Sound muted." },
        ["cmd.volumeUnmute"] = new[] { "Звук включён.", "Sound unmuted." },
        ["cmd.lock"] = new[] { "Блокирую компьютер.", "Locking computer." },
        ["cmd.sleep"] = new[] { "Перевожу в спящий режим.", "Going to sleep." },
        ["cmd.screenshot"] = new[] { "Сделал скриншот: {0}", "Screenshot saved: {0}" },
        ["cmd.screenshotFail"] = new[] { "Не удалось сделать скриншот.", "Failed to take screenshot." },
        ["cmd.desktop"] = new[] { "Свернул окна.", "Minimized windows." },
        ["cmd.shutdown"] = new[] { "Выключение через 30 секунд. Скажи «отмени выключение», чтобы отменить.", "Shutting down in 30 seconds. Say \"cancel shutdown\" to cancel." },
        ["cmd.restart"] = new[] { "Перезагрузка через 30 секунд. Скажи «отмени выключение», чтобы отменить.", "Restarting in 30 seconds. Say \"cancel shutdown\" to cancel." },
        ["cmd.shutdownCancel"] = new[] { "Выключение/перезагрузка отменены.", "Shutdown/restart cancelled." },
        ["cmd.shutdownCancelNone"] = new[] { "Нечего отменять.", "Nothing to cancel." },

        // названия папок (для команды «открой документы»)
        ["folder.desktop"] = new[] { "Рабочий стол", "Desktop" },
        ["folder.documents"] = new[] { "Документы", "Documents" },
        ["folder.downloads"] = new[] { "Загрузки", "Downloads" },
    };

    public static string Tr(string key, params object[] args) => Pick(key, En ? 1 : 0, args);

    // Строка на заданном языке («ru»/«en») независимо от языка интерфейса:
    // нужно для ответов на локальные команды — они понимаются на обоих языках.
    // Имя отдельное от Tr обязательно: перегрузка Tr(код, ключ) перехватывала
    // обычные вызовы Tr(ключ, строка) — вторая строка читалась как ключ и
    // форматирование пропадало (проверено вживую: подпись клавиши «Num9»).
    public static string TrLang(string langCode, string key, params object[] args) =>
        Pick(key, langCode == "en" ? 1 : 0, args);

    static string Pick(string key, int idx, object[] args)
    {
        var row = Table.TryGetValue(key, out var v) ? v : null;
        var s = row == null ? key : row[idx];
        if (args != null && args.Length > 0)
        {
            try { s = string.Format(CultureInfo.InvariantCulture, s, args); } catch { }
        }
        return s;
    }

    // «Num9 и Num8» на нужном языке
    public static string JoinAnd(IEnumerable<string> parts) =>
        string.Join(Tr("join.and"), parts);
}
