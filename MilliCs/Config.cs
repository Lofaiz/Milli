// config.json — та же схема, что у PowerShell-версии (можно переносить файл
// между версиями). Ключи python/device здесь не используются: C#-версии Python
// не нужен, лишние ключи при чтении просто игнорируются.
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Milli;

class ModelRef
{
    public string providerID { get; set; } = "opencode";
    public string id { get; set; } = "big-pickle";
}

class AppConfig
{
    public string title { get; set; } = "Милли";
    public ModelRef model { get; set; } = new();
    public string language { get; set; } = "ru";
    // файл модели распознавания речи внутри папки model рядом с программой.
    // base-q5_1 — быстрая (около 4 с на слабом ноутбуке), small-q5_1 — точнее,
    // но заметно медленнее. Ключ есть только в C#-версии; PowerShell-версия
    // его игнорирует, а C# при отсутствии файла возьмёт любую другую модель.
    public string sttModel { get; set; } = "ggml-base-q5_1.bin";
    public bool autoSendVoice { get; set; } = true;
    public bool startHidden { get; set; } = true;
    // при запуске убирать «мёртвые» сессии Милли из списка OpenCode: каждая
    // Милли создаёт новую сессию, а прежние остаются висеть навсегда
    public bool cleanSessions { get; set; } = true;
    public bool offlineMode { get; set; } = false;
    public string hotkeyVoice { get; set; } = "Num9";
    public string hotkeyWindow { get; set; } = "Num8";
    public string persona { get; set; } = Lang.DefaultPersonaRu;

    // файла настроек не было — это самый первый запуск (подсказки показываем один раз)
    public static bool FirstRun { get; private set; }

    static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // кириллица как есть, не \uXXXX
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static AppConfig Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var cfg = JsonSerializer.Deserialize<AppConfig>(
                    File.ReadAllText(path, Encoding.UTF8), JsonOpts) ?? new AppConfig();
                cfg.model ??= new ModelRef();
                cfg.language = cfg.language == "en" ? "en" : "ru";
                if (string.IsNullOrWhiteSpace(cfg.persona)) cfg.persona = Lang.PersonaFor(cfg.language);
                return cfg;
            }
        }
        catch { }
        FirstRun = true;
        return new AppConfig();
    }

    public void Save(string path)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOpts) + Environment.NewLine,
            new UTF8Encoding(true));
    }
}
