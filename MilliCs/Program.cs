// Милли 2 — голосовой помощник поверх OpenCode.
// Нативная версия (C# / .NET): распознавание речи выполняется прямо в процессе
// (whisper.cpp через Whisper.net), Python не нужен. Поведение повторяет
// PowerShell-версию: трей, две глобальные клавиши, удержание для записи,
// выбор модели ИИ у локального сервера OpenCode.
using System.Threading;

namespace Milli;

static class Program
{
    const string MutexName = @"Local\Milli.SingleInstance";
    const string ShowEventName = @"Local\Milli.Show";

    [STAThread]
    static void Main(string[] args)
    {
        using var mutex = new Mutex(true, MutexName, out bool first);
        if (!first)
        {
            // второй экземпляр — попросить уже запущенный показать окно и выйти
            try { using var ev = EventWaitHandle.OpenExisting(ShowEventName); ev.Set(); } catch { }
            return;
        }

        ApplicationConfiguration.Initialize();

        var form = new MainForm(args);
        var ctx = new ApplicationContext();
        ctx.MainForm = form;

        // слушатель «показать окно» от второго экземпляра
        using var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        var listener = new Thread(() =>
        {
            while (true)
            {
                showEvent.WaitOne();
                try { form.BeginInvoke(form.ToggleWindow); } catch { break; }
            }
        }) { IsBackground = true };
        listener.Start();

        Application.Run(ctx);
    }
}
