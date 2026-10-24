// Распознавание речи целиком внутри приложения: запись микрофона (NAudio,
// WASAPI) и Whisper (whisper.cpp через Whisper.net). Python не нужен.
// Модель загружается один раз при старте — как воркер в PowerShell-версии, но
// без отдельного процесса и без файлов-сигналов: всё в одном процессе.
using System.Text;
using NAudio.CoreAudioApi;        // NAudio 3.x: запись микрофона живёт здесь
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Whisper.net;

namespace Milli;

class SttEngine : IDisposable
{
    readonly string modelDir;

    WhisperFactory factory;
    WasapiCapture capture;
    MemoryStream pcm;
    WaveFormat deviceFormat;
    DateTime startedAt;
    int stopping;
    int autoStop;                     // запись уже просили остановить по лимиту времени
    readonly object loadLock = new();

    // язык распознавания («ru» / «en»): меняется вместе с языком интерфейса
    public string Language { get; set; }

    public bool Ready { get; private set; }
    public string LoadError { get; private set; }
    public bool Recording => capture != null;
    public bool Busy { get; private set; }        // запись или распознавание
    public string ModelFile { get; private set; } // имя файла модели в папке model

    public event Action<string> Recognized;       // текст (может быть пустым)
    public event Action<string> Failed;           // текст ошибки

    public SttEngine(string modelDir, string language)
    {
        this.modelDir = modelDir;
        Language = language;
    }

    // модели распознавания, лежащие в папке model
    public class ModelChoice
    {
        public string File;
        public long Size;
        public string Note;
    }

    public static List<ModelChoice> ListModels(string dir)
    {
        var list = new List<ModelChoice>();
        try
        {
            if (!Directory.Exists(dir)) return list;
            foreach (var f in Directory.GetFiles(dir, "ggml-*.bin"))
            {
                var name = Path.GetFileName(f);
                list.Add(new ModelChoice
                {
                    File = name,
                    Size = new FileInfo(f).Length,
                    Note = Describe(name),
                });
            }
        }
        catch { }
        // быстрые сначала: по возрастанию размера
        list.Sort((a, b) => a.Size.CompareTo(b.Size));
        return list;
    }

    // человеческое описание модели: скорость против точности
    public static string Describe(string file)
    {
        var n = (file ?? "").ToLowerInvariant();
        if (n.Contains("tiny")) return Lang.Tr("stt.desc.tiny");
        if (n.Contains("base")) return Lang.Tr("stt.desc.base");
        if (n.Contains("small")) return Lang.Tr("stt.desc.small");
        if (n.Contains("medium")) return Lang.Tr("stt.desc.medium");
        if (n.Contains("large")) return Lang.Tr("stt.desc.large");
        return "";
    }

    // короткое имя для кнопок: ggml-base-q5_1.bin → base
    public static string ShortName(string file)
    {
        var n = (file ?? "").Replace("ggml-", "").Replace(".bin", "");
        var i = n.IndexOf('-');
        return i > 0 ? n.Substring(0, i) : n;
    }

    public void Load(string fileName = null)
    {
        lock (loadLock)
        {
            if (!string.IsNullOrEmpty(fileName)) ModelFile = fileName;
            try
            {
                var path = Path.Combine(modelDir, ModelFile ?? "");
                if (!File.Exists(path))
                {
                    Ready = false;
                    LoadError = Lang.Tr("stt.fileMissing", ModelFile ?? "");
                    return;
                }
                var fresh = WhisperFactory.FromPath(path);
                var old = factory;
                factory = fresh;
                Ready = true;
                LoadError = null;
                // старый контекст освобождаем только когда распознавание не идёт
                try { if (!Busy) old?.Dispose(); } catch { }
            }
            catch (Exception ex)
            {
                Ready = false;
                LoadError = ex.Message;
            }
        }
    }

    public bool Start()
    {
        if (capture != null || !Ready) return false;
        try
        {
            var cap = new WasapiCapture();
            pcm = new MemoryStream();
            deviceFormat = cap.WaveFormat;
            startedAt = DateTime.Now;
            stopping = 0;
            Interlocked.Exchange(ref autoStop, 0);
            cap.DataAvailable += OnData;
            cap.RecordingStopped += OnStopped;
            cap.StartRecording();
            capture = cap;
            Busy = true;
            return true;
        }
        catch (Exception ex)
        {
            capture = null;
            pcm = null;
            Failed?.Invoke(Lang.Tr("stt.micFail", ex.Message));
            return false;
        }
    }

    // остановить запись и распознать записанное
    public void Stop()
    {
        var cap = capture;
        if (cap == null) return;
        if (Interlocked.Exchange(ref stopping, 1) != 0) return;
        try { cap.StopRecording(); } catch { }
    }

    // отменить без распознавания (сбой таймера)
    public void Abort()
    {
        var cap = capture;
        capture = null;
        if (cap != null)
        {
            try { cap.DataAvailable -= OnData; cap.RecordingStopped -= OnStopped; } catch { }
            try { cap.StopRecording(); } catch { }
            try { cap.Dispose(); } catch { }
        }
        try { pcm?.Dispose(); } catch { }
        pcm = null;
        Busy = false;
        Interlocked.Exchange(ref stopping, 0);
    }

    void OnData(object sender, WaveInEventArgs e)
    {
        var buf = pcm;
        if (buf == null) return;
        try { buf.Write(e.Buffer, 0, e.BytesRecorded); } catch { }
        // записи дольше 90 с быть не может: останавливаемся сами. Stop() уводим
        // в отдельный поток — из потока захвата NAudio можно подвесить запись.
        if ((DateTime.Now - startedAt).TotalSeconds > 90 && Interlocked.Exchange(ref autoStop, 1) == 0)
            Task.Run(() => Stop());
    }

    void OnStopped(object sender, StoppedEventArgs e)
    {
        var cap = capture;
        capture = null;
        if (cap != null)
        {
            try { cap.DataAvailable -= OnData; cap.RecordingStopped -= OnStopped; } catch { }
            try { cap.Dispose(); } catch { }
        }

        var bytes = Array.Empty<byte>();
        var fmt = deviceFormat;
        try { if (pcm != null) bytes = pcm.ToArray(); } catch { }
        try { pcm?.Dispose(); } catch { }
        pcm = null;
        Interlocked.Exchange(ref stopping, 0);

        var stopError = e?.Exception;
        Task.Run(() =>
        {
            try
            {
                if (stopError != null) throw stopError;
                var text = Recognize(bytes, fmt);
                Busy = false;
                Recognized?.Invoke(text);
            }
            catch (Exception ex)
            {
                Busy = false;
                Failed?.Invoke(Lang.Tr("stt.recogFail", ex.Message));
            }
        });
    }

    // устройство может писать 44-48 кГц стерео во float — приводим к 16 кГц моно
    string Recognize(byte[] bytes, WaveFormat fmt)
    {
        if (bytes.Length == 0) return "";
        float[] samples;
        using (var ms = new MemoryStream(bytes))
        using (var src = new RawSourceWaveStream(ms, fmt))
        using (var resampler = new MediaFoundationResampler(src, new WaveFormat(16000, 1)))
        {
            // передискретизатор отдаёт PCM 16 бит, 16 кГц, моно — переводим в float
            var list = new List<float>(bytes.Length / 4 + 1024);
            var buf = new byte[16384];
            int n;
            while ((n = resampler.Read(buf.AsSpan())) > 0)
            {
                for (int i = 0; i + 1 < n; i += 2)
                {
                    short s = (short)(buf[i] | (buf[i + 1] << 8));
                    list.Add(s / 32768f);
                }
            }
            samples = list.ToArray();
        }
        // короче 0.25 с — это щелчок клавиши, а не речь
        if (samples.Length < 4000) return "";

        var f = factory;
        if (f == null) throw new InvalidOperationException(Lang.Tr("stt.notLoaded"));

        // все ядра: на слабых процессорах это единственный способ ускориться
        var threads = Math.Max(1, Math.Min(Environment.ProcessorCount, 8));

        // Whisper.net отдаёт расшифровку через обработчик сегментов
        var sb = new StringBuilder();
        using var processor = f.CreateBuilder()
            .WithLanguage(Language)
            .WithThreads(threads)
            .WithSegmentEventHandler(seg => sb.Append(seg.Text))
            .Build();
        processor.Process(samples);
        return sb.ToString().Trim();
    }

    // диагностика: распознать готовый wav-файл (как stt.py --file в PowerShell-версии)
    public string RecognizeFile(string path)
    {
        using var reader = new WaveFileReader(path);
        var fmt = reader.WaveFormat;
        using var ms = new MemoryStream();
        reader.CopyTo(ms);
        return Recognize(ms.ToArray(), fmt);
    }

    public void Dispose()
    {
        Abort();
        try { factory?.Dispose(); } catch { }
        factory = null;
        Ready = false;
    }
}
