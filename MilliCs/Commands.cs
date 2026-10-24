// Локальные команды Милли: короткие распоряжения и справки, которые
// выполняются на месте, без обращения к ИИ. Фразы понимаются на обоих языках
// независимо от языка интерфейса, сравнение — по всей фразе целиком, поэтому
// обычный текст с этими словами внутри командой не считается.
//
// Что понимается:
//   стоп / остановись                      — прервать текущий ответ;
//   повтори / ещё раз                      — повторить последний ответ;
//   новый разговор / начнём заново         — чистый чат;
//   сколько времени / какое число / какой день — ответ на месте;
//   что ты умеешь / помощь                 — список возможностей;
//   открой <приложение|папку>              — запуск по белому списку ниже;
//   напомни через <время> <текст>          — напоминание;
//   таймер на <время>                      — таймер со звуком;
//   мои напоминания / отмени напоминания   — список и отмена напоминаний;
//   запиши заметку <текст> / прочитай заметки / удали заметки;
//   посчитай <выражение>                   — арифметика без ИИ;
//   громче / тише / выключи звук / включи звук / громкость N;
//   заблокируй компьютер / спящий режим;
//   скриншот / сверни окна;
//   выключи компьютер / перезагрузи компьютер — с подтверждением.
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Milli;

static class Commands
{
    internal enum Kind
    {
        None, Stop, Repeat, NewChat, Time, Date, Weekday, OpenApp, OpenFolder, Remind,
        Help, Timer, TimerCancel, RemindList, RemindClear,
        NoteAdd, NoteRead, NoteClear, Calc,
        VolumeUp, VolumeDown, VolumeMute, VolumeUnmute, VolumeSet, VolumeGet,
        Lock, Sleep, Screenshot, ShowDesktop, Shutdown, Restart, ShutdownCancel
    }

    internal sealed class Result
    {
        public Kind Kind = Kind.None;
        public string Lang = "ru";
        public string Arg;
        public string Path;
        public string Text;
        public int Seconds;
        public double Number;
    }

    public static Result Parse(string raw)
    {
        var r = new Result();
        var t = Norm(raw);
        if (t.Length == 0) return r;

        if (In(t, StopRu)) { r.Kind = Kind.Stop; r.Lang = "ru"; return r; }
        if (In(t, StopEn)) { r.Kind = Kind.Stop; r.Lang = "en"; return r; }
        if (In(t, RepeatRu)) { r.Kind = Kind.Repeat; r.Lang = "ru"; return r; }
        if (In(t, RepeatEn)) { r.Kind = Kind.Repeat; r.Lang = "en"; return r; }
        if (In(t, NewChatRu)) { r.Kind = Kind.NewChat; r.Lang = "ru"; return r; }
        if (In(t, NewChatEn)) { r.Kind = Kind.NewChat; r.Lang = "en"; return r; }
        if (In(t, TimeRu)) { r.Kind = Kind.Time; r.Lang = "ru"; return r; }
        if (In(t, TimeEn)) { r.Kind = Kind.Time; r.Lang = "en"; return r; }
        if (In(t, DateRu)) { r.Kind = Kind.Date; r.Lang = "ru"; return r; }
        if (In(t, DateEn)) { r.Kind = Kind.Date; r.Lang = "en"; return r; }
        if (In(t, WeekdayRu)) { r.Kind = Kind.Weekday; r.Lang = "ru"; return r; }
        if (In(t, WeekdayEn)) { r.Kind = Kind.Weekday; r.Lang = "en"; return r; }
        if (In(t, HelpRu)) { r.Kind = Kind.Help; r.Lang = "ru"; return r; }
        if (In(t, HelpEn)) { r.Kind = Kind.Help; r.Lang = "en"; return r; }

        if (In(t, VolumeUpRu)) { r.Kind = Kind.VolumeUp; r.Lang = "ru"; return r; }
        if (In(t, VolumeUpEn)) { r.Kind = Kind.VolumeUp; r.Lang = "en"; return r; }
        if (In(t, VolumeDownRu)) { r.Kind = Kind.VolumeDown; r.Lang = "ru"; return r; }
        if (In(t, VolumeDownEn)) { r.Kind = Kind.VolumeDown; r.Lang = "en"; return r; }
        if (In(t, VolumeMuteRu)) { r.Kind = Kind.VolumeMute; r.Lang = "ru"; return r; }
        if (In(t, VolumeMuteEn)) { r.Kind = Kind.VolumeMute; r.Lang = "en"; return r; }
        if (In(t, VolumeUnmuteRu)) { r.Kind = Kind.VolumeUnmute; r.Lang = "ru"; return r; }
        if (In(t, VolumeUnmuteEn)) { r.Kind = Kind.VolumeUnmute; r.Lang = "en"; return r; }
        if (In(t, VolumeGetRu)) { r.Kind = Kind.VolumeGet; r.Lang = "ru"; return r; }
        if (In(t, VolumeGetEn)) { r.Kind = Kind.VolumeGet; r.Lang = "en"; return r; }
        if (TryParseVolume(t, out var vpct)) { r.Kind = Kind.VolumeSet; r.Lang = "ru"; r.Number = vpct; return r; }

        if (In(t, LockRu)) { r.Kind = Kind.Lock; r.Lang = "ru"; return r; }
        if (In(t, LockEn)) { r.Kind = Kind.Lock; r.Lang = "en"; return r; }
        if (In(t, SleepRu)) { r.Kind = Kind.Sleep; r.Lang = "ru"; return r; }
        if (In(t, SleepEn)) { r.Kind = Kind.Sleep; r.Lang = "en"; return r; }
        if (In(t, ScreenshotRu)) { r.Kind = Kind.Screenshot; r.Lang = "ru"; return r; }
        if (In(t, ScreenshotEn)) { r.Kind = Kind.Screenshot; r.Lang = "en"; return r; }
        if (In(t, DesktopRu)) { r.Kind = Kind.ShowDesktop; r.Lang = "ru"; return r; }
        if (In(t, DesktopEn)) { r.Kind = Kind.ShowDesktop; r.Lang = "en"; return r; }
        if (In(t, ShutdownRu)) { r.Kind = Kind.Shutdown; r.Lang = "ru"; return r; }
        if (In(t, ShutdownEn)) { r.Kind = Kind.Shutdown; r.Lang = "en"; return r; }
        if (In(t, RestartRu)) { r.Kind = Kind.Restart; r.Lang = "ru"; return r; }
        if (In(t, RestartEn)) { r.Kind = Kind.Restart; r.Lang = "en"; return r; }
        if (In(t, ShutdownCancelRu)) { r.Kind = Kind.ShutdownCancel; r.Lang = "ru"; return r; }
        if (In(t, ShutdownCancelEn)) { r.Kind = Kind.ShutdownCancel; r.Lang = "en"; return r; }

        if (In(t, TimerCancelRu)) { r.Kind = Kind.TimerCancel; r.Lang = "ru"; return r; }
        if (In(t, TimerCancelEn)) { r.Kind = Kind.TimerCancel; r.Lang = "en"; return r; }
        foreach (var p in TimerRu) if (t == p || t.StartsWith(p + " ")) return ParseTimer(t[p.Length..].Trim(), "ru", r);
        foreach (var p in TimerEn) if (t == p || t.StartsWith(p + " ")) return ParseTimer(t[p.Length..].Trim(), "en", r);

        if (In(t, RemindListRu)) { r.Kind = Kind.RemindList; r.Lang = "ru"; return r; }
        if (In(t, RemindListEn)) { r.Kind = Kind.RemindList; r.Lang = "en"; return r; }
        if (In(t, RemindClearRu)) { r.Kind = Kind.RemindClear; r.Lang = "ru"; return r; }
        if (In(t, RemindClearEn)) { r.Kind = Kind.RemindClear; r.Lang = "en"; return r; }

        if (In(t, NoteReadRu)) { r.Kind = Kind.NoteRead; r.Lang = "ru"; return r; }
        if (In(t, NoteReadEn)) { r.Kind = Kind.NoteRead; r.Lang = "en"; return r; }
        if (In(t, NoteClearRu)) { r.Kind = Kind.NoteClear; r.Lang = "ru"; return r; }
        if (In(t, NoteClearEn)) { r.Kind = Kind.NoteClear; r.Lang = "en"; return r; }
        foreach (var p in NoteAddRu) if (t == p || t.StartsWith(p + " ")) return ParseNote(t[p.Length..].Trim(), "ru", r);
        foreach (var p in NoteAddEn) if (t == p || t.StartsWith(p + " ")) return ParseNote(t[p.Length..].Trim(), "en", r);

        foreach (var p in CalcRu) if (t == p || t.StartsWith(p + " ")) return ParseCalc(t[p.Length..].Trim(), "ru", r);
        foreach (var p in CalcEn) if (t == p || t.StartsWith(p + " ")) return ParseCalc(t[p.Length..].Trim(), "en", r);

        foreach (var p in RemindRu) if (t == p || t.StartsWith(p + " ")) return ParseRemind(t[p.Length..].Trim(), "ru", r);
        foreach (var p in RemindEn) if (t == p || t.StartsWith(p + " ")) return ParseRemind(t[p.Length..].Trim(), "en", r);

        foreach (var p in OpenRu) if (t == p || t.StartsWith(p + " ")) return ParseOpen(t[p.Length..].Trim(), "ru", r);
        foreach (var p in OpenEn) if (t == p || t.StartsWith(p + " ")) return ParseOpen(t[p.Length..].Trim(), "en", r);

        return r;
    }

    static Result ParseOpen(string target, string lang, Result r)
    {
        if (target.Length == 0) return r;
        var words = target.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int from = 0;
        while (from < words.Length && words[from] is "папку" or "папка" or "папке" or "folder" or "the") from++;
        var name = string.Join(' ', words.Skip(from)).Trim();
        if (name.Length == 0) return r;

        foreach (var f in Folders)
        {
            if (!MatchKey(name, f.Keys)) continue;
            r.Kind = Kind.OpenFolder;
            r.Lang = lang;
            r.Arg = Lang.TrLang(lang, f.LangKey);
            r.Path = f.Explicit ?? Environment.GetFolderPath(f.Dir);
            return r;
        }

        foreach (var a in Apps)
        {
            if (!MatchKey(name, a.Keys)) continue;
            r.Kind = Kind.OpenApp;
            r.Lang = lang;
            r.Arg = lang == "en" ? (a.EnName ?? a.Name) : a.Name;
            r.Path = a.Path;
            if (!a.System && a.Glob != null && (a.Path == null || !File.Exists(a.Path)))
                r.Path = ResolveGlob(a.Glob) ?? a.Path;
            return r;
        }
        return r;
    }

    static bool MatchKey(string name, string[] keys)
    {
        foreach (var k in keys)
        {
            if (name == k) return true;
            if (k.Length >= 4 && name.Length > k.Length && name.Length - k.Length <= 2 && name.StartsWith(k, StringComparison.Ordinal)) return true;
            if (k.Length >= 5)
            {
                var stem = k[..^1];
                if (name.Length > stem.Length && name.Length - stem.Length <= 2 && name.StartsWith(stem, StringComparison.Ordinal)) return true;
            }
        }
        return false;
    }

    static Result ParseRemind(string rest, string lang, Result r)
    {
        r.Kind = Kind.Remind;
        r.Lang = lang;
        var marker = lang == "en" ? "in" : "через";
        if (rest.StartsWith(marker + " ", StringComparison.Ordinal)) rest = rest[(marker.Length + 1)..].Trim();
        else if (rest == marker) rest = "";
        if (!ParseDuration(lang, rest, out var seconds, out var text)) { r.Seconds = 0; return r; }
        r.Seconds = seconds;
        r.Text = text;
        return r;
    }

    static Result ParseTimer(string rest, string lang, Result r)
    {
        r.Kind = Kind.Timer;
        r.Lang = lang;
        if (lang == "ru") { if (rest.StartsWith("на ", StringComparison.Ordinal)) rest = rest[3..].Trim(); if (rest.StartsWith("через ", StringComparison.Ordinal)) rest = rest[6..].Trim(); }
        else { if (rest.StartsWith("for ", StringComparison.Ordinal)) rest = rest[4..].Trim(); if (rest.StartsWith("in ", StringComparison.Ordinal)) rest = rest[3..].Trim(); }
        if (!ParseDuration(lang, rest, out var seconds, out var text)) { r.Seconds = 0; return r; }
        r.Seconds = seconds;
        r.Text = string.IsNullOrWhiteSpace(text) ? (lang == "en" ? "Timer" : "Таймер") : text;
        return r;
    }

    static Result ParseNote(string rest, string lang, Result r)
    {
        r.Kind = Kind.NoteAdd;
        r.Lang = lang;
        r.Text = rest.Trim();
        if (r.Text.Length == 0) r.Seconds = 0;
        return r;
    }

    static Result ParseCalc(string rest, string lang, Result r)
    {
        r.Kind = Kind.Calc;
        r.Lang = lang;
        r.Text = rest.Trim();
        if (r.Text.Length == 0) { r.Seconds = 0; return r; }
        var val = CalcExpr(r.Text);
        if (double.IsNaN(val)) { r.Seconds = 0; return r; }
        r.Number = val;
        return r;
    }

    static double CalcExpr(string expr)
    {
        try
        {
            expr = expr.Replace('х', '*').Replace('X', '*').Replace(',', '.');
            var sb = new StringBuilder(expr.Length);
            foreach (var c in expr)
            {
                if (char.IsWhiteSpace(c)) continue;
                if (char.IsDigit(c) || c == '.' || c == '+' || c == '-' || c == '*' || c == '/' || c == '(' || c == ')' || c == '%')
                    sb.Append(c);
                else
                    return double.NaN;
            }
            var s = sb.ToString();
            if (s.Length == 0) return double.NaN;
            var data = new DataTable();
            var res = data.Compute(s, null);
            if (res == DBNull.Value) return double.NaN;
            return Convert.ToDouble(res, CultureInfo.InvariantCulture);
        }
        catch { return double.NaN; }
    }

    static bool TryParseVolume(string t, out int pct)
    {
        pct = -1;
        if (t.StartsWith("громкость "))
        {
            var x = t["громкость ".Length..].Trim();
            if (int.TryParse(x, out var p) && p >= 0 && p <= 100) { pct = p; return true; }
        }
        if (t.StartsWith("volume "))
        {
            var x = t["volume ".Length..].Trim();
            if (int.TryParse(x, out var p) && p >= 0 && p <= 100) { pct = p; return true; }
        }
        if (t == "громкость" || t == "volume") { pct = -2; return true; }
        return false;
    }

    static bool ParseDuration(string lang, string s, out int seconds, out string rest)
    {
        seconds = 0; rest = "";
        var words = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        double total = 0;
        int i = 0;
        while (i < words.Length)
        {
            if (lang == "ru" && words[i] is "полчаса" or "получаса") { total += 1800; i++; continue; }
            if (lang == "en" && i + 2 < words.Length && words[i] == "half" && words[i + 1] is "an" or "a" && words[i + 2].StartsWith("hour", StringComparison.Ordinal)) { total += 1800; i += 3; continue; }
            if (lang == "en" && i + 3 < words.Length && words[i + 1] == "and" && words[i + 2] is "a" or "an" && words[i + 3] == "half" && TryUnit(lang, words[i], out var hourA)) { total += hourA * 1.5; i += 4; continue; }
            if (TryUnit(lang, words[i], out var only)) { total += only; i++; continue; }
            if (!TryNumber(lang, words[i], out var num)) break;
            i++;
            if (i < words.Length && TryUnit(lang, words[i], out var unit)) { total += num * unit; i++; continue; }
            i--; break;
        }
        if (total <= 0 || total > 7 * 86400) return false;
        seconds = (int)Math.Round(total);
        rest = string.Join(' ', words.Skip(i)).Trim();
        if (rest.StartsWith("чтобы ", StringComparison.Ordinal)) rest = rest[6..].Trim();
        else if (rest == "чтобы") rest = "";
        if (rest.StartsWith("to ", StringComparison.Ordinal)) rest = rest[3..].Trim();
        else if (rest == "to") rest = "";
        return true;
    }

    static bool TryNumber(string lang, string w, out double value)
    {
        value = 0;
        if (double.TryParse(w, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) { value = d; return true; }
        if (lang == "en")
        {
            value = w switch { "a" or "an" => 1, "half" => 0.5, "one" => 1, "two" => 2, "three" => 3, "four" => 4, "five" => 5, "six" => 6, "seven" => 7, "eight" => 8, "nine" => 9, "ten" => 10, "eleven" => 11, "twelve" => 12, "fifteen" => 15, "twenty" => 20, "thirty" => 30, "forty" => 40, "fifty" => 50, "sixty" => 60, "ninety" => 90, _ => 0 };
            return value > 0;
        }
        value = w switch { "полтора" or "полторы" => 1.5, "пол" => 0.5, "один" or "одну" or "одна" => 1, "два" or "две" => 2, "три" => 3, "четыре" => 4, "пять" => 5, "шесть" => 6, "семь" => 7, "восемь" => 8, "девять" => 9, "десять" => 10, "одиннадцать" => 11, "двенадцать" => 12, "тринадцать" => 13, "четырнадцать" => 14, "пятнадцать" => 15, "шестнадцать" => 16, "семнадцать" => 17, "восемнадцать" => 18, "девятнадцать" => 19, "двадцать" => 20, "тридцать" => 30, "сорок" => 40, "пятьдесят" => 50, "шестьдесят" => 60, "девяносто" => 90, "сто" => 100, _ => 0 };
        return value > 0;
    }

    static bool TryUnit(string lang, string w, out int seconds)
    {
        seconds = 0;
        if (lang == "en")
        {
            seconds = w switch { "second" or "seconds" or "sec" => 1, "minute" or "minutes" or "min" => 60, "hour" or "hours" or "hr" => 3600, _ => 0 };
            return seconds > 0;
        }
        seconds = w switch { "секунда" or "секунды" or "секунд" or "секунду" or "секундой" or "сек" => 1, "минута" or "минуты" or "минут" or "минуту" or "минутой" or "минутку" or "мин" => 60, "час" or "часа" or "часов" or "часик" or "часика" or "часу" => 3600, _ => 0 };
        return seconds > 0;
    }

    public static void Launch(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("empty path");
        Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
    }

    static string ResolveGlob(string pattern)
    {
        try
        {
            var dir = Path.GetDirectoryName(pattern);
            var mask = Path.GetFileName(pattern);
            if (dir == null || !Directory.Exists(dir)) return null;
            return Directory.EnumerateDirectories(dir).Select(d => Path.Combine(d, mask)).Where(File.Exists).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
        }
        catch { return null; }
    }

    public static string DurationText(string lang, int seconds)
    {
        var h = seconds / 3600;
        var m = seconds % 3600 / 60;
        var s = seconds % 60;
        var parts = new List<string>();
        if (lang == "en")
        {
            if (h > 0) parts.Add(h == 1 ? "1 hour" : h + " hours");
            if (m > 0) parts.Add(m == 1 ? "1 minute" : m + " minutes");
            if (s > 0) parts.Add(s == 1 ? "1 second" : s + " seconds");
            return string.Join(' ', parts);
        }
        if (h > 0) parts.Add(h + " " + Plural(h, "час", "часа", "часов"));
        if (m > 0) parts.Add(m == 1 ? "минуту" : m + " " + Plural(m, "минута", "минуты", "минут"));
        if (s > 0) parts.Add(s == 1 ? "секунду" : s + " " + Plural(s, "секунда", "секунды", "секунд"));
        if (parts.Count == 1 && h == 1) return "час";
        return string.Join(' ', parts);
    }

    static string Plural(int n, string one, string few, string many)
    {
        var n10 = n % 10;
        var n100 = n % 100;
        if (n10 == 1 && n100 != 11) return one;
        if (n10 >= 2 && n10 <= 4 && (n100 < 12 || n100 > 14)) return few;
        return many;
    }

    public static CultureInfo Culture(string lang) => CultureInfo.GetCultureInfo(lang == "en" ? "en-US" : "ru-RU");
    public static string FormatTime(DateTime t) => t.ToString("HH:mm", CultureInfo.InvariantCulture);
    public static string FormatDate(string lang, DateTime t) => t.ToString(lang == "en" ? "MMMM d, yyyy" : "d MMMM yyyy", Culture(lang));
    public static string FormatWeekday(string lang, DateTime t) => t.ToString("dddd", Culture(lang));

    public static string Norm(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s.Trim().ToLowerInvariant())
        {
            if (ch == 'ё') { sb.Append('е'); continue; }
            if (ch is '\'' or '’' or '`') continue;
            sb.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
        }
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    static bool In(string norm, string[] phrases) => Array.IndexOf(phrases, norm) >= 0;

    static readonly string[] HelpRu = { "что ты умеешь", "что можешь", "помощь", "команды", "справка" };
    static readonly string[] HelpEn = { "what can you do", "help", "commands", "what are your commands" };

    static readonly string[] TimerRu = { "таймер", "таймер на" };
    static readonly string[] TimerEn = { "timer", "set timer", "timer for" };
    static readonly string[] TimerCancelRu = { "отмени таймер", "выключи таймер", "останови таймер" };
    static readonly string[] TimerCancelEn = { "cancel timer", "stop timer" };
    static readonly string[] RemindListRu = { "мои напоминания", "список напоминаний", "какие напоминания" };
    static readonly string[] RemindListEn = { "my reminders", "list reminders", "reminders list" };
    static readonly string[] RemindClearRu = { "отмени все напоминания", "удали все напоминания", "очисти напоминания" };
    static readonly string[] RemindClearEn = { "cancel all reminders", "clear reminders", "remove all reminders" };

    static readonly string[] NoteAddRu = { "запиши заметку", "сделай заметку", "заметка" };
    static readonly string[] NoteAddEn = { "take a note", "note down", "add note" };
    static readonly string[] NoteReadRu = { "прочитай заметки", "мои заметки", "что я записал" };
    static readonly string[] NoteReadEn = { "read notes", "my notes", "what did i note" };
    static readonly string[] NoteClearRu = { "удали заметки", "очисти заметки", "удали все заметки" };
    static readonly string[] NoteClearEn = { "clear notes", "delete notes", "remove all notes" };

    static readonly string[] CalcRu = { "посчитай", "вычисли", "сколько будет" };
    static readonly string[] CalcEn = { "calculate", "compute", "what is" };

    static readonly string[] VolumeUpRu = { "громче", "прибавь громкость", "увеличь громкость" };
    static readonly string[] VolumeUpEn = { "volume up", "louder" };
    static readonly string[] VolumeDownRu = { "тише", "уменьши громкость" };
    static readonly string[] VolumeDownEn = { "volume down", "quieter", "softer" };
    static readonly string[] VolumeMuteRu = { "выключи звук", "без звука", "убери звук" };
    static readonly string[] VolumeMuteEn = { "mute", "mute sound" };
    static readonly string[] VolumeUnmuteRu = { "включи звук" };
    static readonly string[] VolumeUnmuteEn = { "unmute" };
    static readonly string[] VolumeGetRu = { "какая громкость", "уровень громкости" };
    static readonly string[] VolumeGetEn = { "what is the volume", "volume level" };

    static readonly string[] LockRu = { "заблокируй компьютер", "заблокируй экран", "заблокируй" };
    static readonly string[] LockEn = { "lock computer", "lock screen", "lock" };
    static readonly string[] SleepRu = { "спящий режим", "перейди в спящий режим", "усыпи компьютер" };
    static readonly string[] SleepEn = { "sleep", "go to sleep" };
    static readonly string[] ScreenshotRu = { "скриншот", "сделай скриншот", "сделай снимок экрана" };
    static readonly string[] ScreenshotEn = { "screenshot", "take screenshot", "capture screen" };
    static readonly string[] DesktopRu = { "сверни окна", "покажи рабочий стол", "свернуть все окна" };
    static readonly string[] DesktopEn = { "show desktop", "minimize all", "go to desktop" };
    static readonly string[] ShutdownRu = { "выключи компьютер", "выключи пк", "завершение работы" };
    static readonly string[] ShutdownEn = { "shutdown", "turn off computer", "shut down" };
    static readonly string[] RestartRu = { "перезагрузи компьютер", "перезагрузка", "перезагрузи пк" };
    static readonly string[] RestartEn = { "restart", "reboot", "restart computer" };
    static readonly string[] ShutdownCancelRu = { "отмени выключение", "отмена выключения" };
    static readonly string[] ShutdownCancelEn = { "cancel shutdown", "abort shutdown" };

    static readonly string[] StopRu = { "стоп", "остановись", "останови", "стой", "хватит", "прекрати", "прерви", "не нужно", "не надо", "перестань", "замолчи", "отставить" };
    static readonly string[] StopEn = { "stop", "stop it", "halt", "cancel", "be quiet", "shut up", "enough" };
    static readonly string[] RepeatRu = { "повтори", "повтори еще раз", "еще раз", "скажи еще раз", "повтори ответ", "повтори пожалуйста", "что ты сказал", "повтори что сказал", "повтори последнее" };
    static readonly string[] RepeatEn = { "repeat", "repeat it", "say it again", "say that again", "once more", "repeat that", "repeat please" };
    static readonly string[] NewChatRu = { "новый разговор", "новый чат", "новый диалог", "начнем заново", "начать заново", "начни заново", "очисти чат", "забудь все", "сбрось разговор" };
    static readonly string[] NewChatEn = { "new conversation", "new chat", "new dialog", "start over", "fresh start", "clear chat", "reset conversation", "forget everything" };
    static readonly string[] TimeRu = { "сколько времени", "сколько сейчас времени", "который час", "время сейчас", "скажи время", "текущее время" };
    static readonly string[] TimeEn = { "what time is it", "whats the time", "what is the time", "tell me the time", "current time", "what time it is" };
    static readonly string[] DateRu = { "какое сегодня число", "какая сегодня дата", "сегодняшняя дата", "какое число" };
    static readonly string[] DateEn = { "whats the date", "what is the date", "what date is it", "todays date", "date today" };
    static readonly string[] WeekdayRu = { "какой сегодня день", "какой день недели", "какой сегодня день недели" };
    static readonly string[] WeekdayEn = { "what day is it", "what day is it today", "what day of the week is it" };

    static readonly string[] OpenRu = { "открой", "открыть", "открывай", "запусти", "запустить", "включи", "включить" };
    static readonly string[] OpenEn = { "open", "launch", "start", "run" };

    static readonly string[] RemindRu = { "напомни мне", "напомни пожалуйста", "напомни", "напомнить" };
    static readonly string[] RemindEn = { "remind me", "set a reminder", "set reminder", "remind" };

    sealed class App
    {
        public string Name;
        public string EnName;
        public string Path;
        public string[] Keys;
        public string Glob;
        public bool System;
    }

    sealed class FolderDef
    {
        public string[] Keys;
        public Environment.SpecialFolder Dir;
        public string Explicit;
        public string LangKey;
    }

    static readonly FolderDef[] Folders =
    {
        new() { Keys = new[] { "рабочий стол", "стол", "desktop" }, Dir = Environment.SpecialFolder.DesktopDirectory, LangKey = "folder.desktop" },
        new() { Keys = new[] { "документ", "documents", "my documents" }, Dir = Environment.SpecialFolder.MyDocuments, LangKey = "folder.documents" },
        new() { Keys = new[] { "загрузк", "скачан", "downloads", "download" }, Dir = Environment.SpecialFolder.UserProfile, Explicit = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"), LangKey = "folder.downloads" },
    };

    static readonly App[] Apps =
    {
        new() { Name = "Opera GX", Path = Path.Combine(Local, "Programs", "Opera GX", "opera.exe"), Keys = new[] { "opera", "opera gx", "опера", "оперу", "опере", "оперы" } },
        new() { Name = "Google Chrome", Path = Path.Combine(Pf, "Google", "Chrome", "Application", "chrome.exe"), Keys = new[] { "chrome", "google chrome", "хром", "гугл хром" } },
        new() { Name = "Microsoft Edge", Path = Path.Combine(Pf86, "Microsoft", "Edge", "Application", "msedge.exe"), Keys = new[] { "edge", "microsoft edge", "msedge", "эдж", "микрософт эдж" } },
        new() { Name = "Discord", Path = Path.Combine(Local, "Discord", "Discord.exe"), Keys = new[] { "discord", "дискорд" } },
        new() { Name = "Telegram", Path = Path.Combine(Roam, "Telegram Desktop", "Telegram.exe"), Keys = new[] { "telegram", "телеграм", "телега" } },
        new() { Name = "Steam", Path = Path.Combine(Pf86, "Steam", "steam.exe"), Keys = new[] { "steam", "стим" } },
        new() { Name = "Epic Games", Path = Path.Combine(Pf86, "Epic Games", "Launcher", "Portal", "Binaries", "Win32", "EpicGamesLauncher.exe"), Keys = new[] { "epic", "epic games", "epic launcher", "эпик", "эпик геймс", "эпик лаунчер" } },
        new() { Name = "Riot Client", Path = @"C:\Riot Games\Riot Client\RiotClientServices.exe", Keys = new[] { "riot", "риот", "league", "лига" } },
        new() { Name = "Albion Online", Path = Path.Combine(Local, "AlbionOnline", "launcher", "AlbionLauncher.exe"), Keys = new[] { "albion", "альбион" } },
        new() { Name = "Roblox", Glob = Path.Combine(Local, "Roblox", "Versions", "*", "RobloxPlayerBeta.exe"), Keys = new[] { "roblox", "роблокс" } },
        new() { Name = "Modrinth", Path = Path.Combine(Local, "Modrinth App", "Modrinth App.exe"), Keys = new[] { "modrinth", "модринт" } },
        new() { Name = "Minecraft", Path = Path.Combine(Roam, ".tlauncher", "legacy", "Minecraft", "TL.exe"), Keys = new[] { "minecraft", "майнкрафт", "майн", "tlauncher", "тлаунчер" } },
        new() { Name = "LM Studio", Path = Path.Combine(Local, "Programs", "lm-studio", "LM Studio.exe"), Keys = new[] { "lm studio", "лм студио", "лм студия", "элэм студия" } },
        new() { Name = "IntelliJ IDEA", Path = Path.Combine(Pf, "JetBrains", "IntelliJ IDEA Community Edition 2025.2", "bin", "idea64.exe"), Keys = new[] { "intellij", "intellij idea", "idea ce", "идея", "идею", "интелидж" } },
        new() { Name = "Sublime Text", Path = Path.Combine(Pf, "Sublime Text", "sublime_text.exe"), Keys = new[] { "sublime", "sublime text", "саблайм", "блокнот", "notepad" } },
        new() { Name = "OpenCode", Path = Path.Combine(Local, "Programs", "@opencodedesktop", "OpenCode.exe"), Keys = new[] { "opencode", "open code", "опенкод", "опен код" } },
        new() { Name = "qBittorrent", Path = Path.Combine(Pf, "qBittorrent", "qbittorrent.exe"), Keys = new[] { "qbittorrent", "кьюбитторрент", "кьюбит", "торрент", "torrent" } },
        new() { Name = "GIMP", Path = Path.Combine(Pf, "GIMP 3", "bin", "gimp-3.exe"), Keys = new[] { "gimp", "гимп" } },
        new() { Name = "Audacity", Path = Path.Combine(Pf, "Audacity", "Audacity.exe"), Keys = new[] { "audacity", "аудасити" } },
        new() { Name = "Parsec", Path = Path.Combine(Pf, "Parsec", "parsecd.exe"), Keys = new[] { "parsec", "парсек" } },
        new() { Name = "WinRAR", Path = Path.Combine(Pf, "WinRAR", "WinRAR.exe"), Keys = new[] { "winrar", "винрар" } },
        new() { Name = "FileZilla", Path = Path.Combine(Pf, "FileZilla FTP Client", "filezilla.exe"), Keys = new[] { "filezilla", "file zilla", "файлзилла" } },
        new() { Name = "7-Zip", Path = Path.Combine(Pf, "7-Zip", "7zFM.exe"), Keys = new[] { "7zip", "7 zip", "seven zip", "семерка", "семерку" } },
        new() { Name = "Калькулятор", EnName = "Calculator", Path = "calc.exe", Keys = new[] { "calc", "calculator", "калькулятор" }, System = true },
        new() { Name = "Проводник", EnName = "File Explorer", Path = "explorer.exe", Keys = new[] { "explorer", "проводник", "файлы", "files" }, System = true },
        new() { Name = "Диспетчер задач", EnName = "Task Manager", Path = "taskmgr.exe", Keys = new[] { "taskmgr", "диспетчер", "task manager" }, System = true },
        new() { Name = "Параметры", EnName = "Settings", Path = "ms-settings:", Keys = new[] { "settings", "настройки", "параметры" }, System = true },
        new() { Name = "Командная строка", EnName = "Command Prompt", Path = "cmd.exe", Keys = new[] { "cmd", "командная строка", "консоль", "command prompt" }, System = true },
        new() { Name = "Paint", Path = "mspaint.exe", Keys = new[] { "paint", "пейнт" }, System = true },
    };

    static string Local => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    static string Roam => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    static string Prof => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    static string Pf => Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
    static string Pf86 => Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
}
