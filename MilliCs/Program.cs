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
            try { using var ev = EventWaitHandle.OpenExisting(ShowEventName); ev.Set(); } catch { }
            return;
        }

        ApplicationConfiguration.Initialize();

        var form = new MainForm(args);
        var ctx = new ApplicationContext();
        ctx.MainForm = form;

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
