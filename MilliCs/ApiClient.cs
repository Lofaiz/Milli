using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;

namespace Milli;

class ApiModel
{
    public string id { get; set; }
    public string providerID { get; set; }
    public string name { get; set; }
    public string status { get; set; }
    public bool enabled { get; set; } = true;
    public long context { get; set; }
    public bool hasCost { get; set; }
    public double costIn { get; set; }
    public double costOut { get; set; }
}

class ApiSession
{
    public string id { get; set; }
    public string title { get; set; }
    public long created { get; set; }
    public bool marked { get; set; }
}

class ChatMessage
{
    public string type { get; set; }
    public long created { get; set; }
    public List<string> texts { get; } = new();
    public string error { get; set; }
    public string outcome { get; set; }
}

class ApiClient : IDisposable
{
    readonly HttpClient http = new(new SocketsHttpHandler
    {
        ConnectTimeout = TimeSpan.FromSeconds(2),
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
    })
    { Timeout = TimeSpan.FromSeconds(180) };

    string serverUrl;
    string auth;

    static bool IsConnectionError(Exception ex) =>
        ex is HttpRequestException || ex is System.IO.IOException || ex is TaskCanceledException;

    string Auth
    {
        get
        {
            if (auth == null)
            {
                var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".config", "opencode", "service.json");
                using var doc = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
                var pass = doc.RootElement.GetProperty("password").GetString();
                auth = "Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes("opencode:" + pass));
            }
            return auth;
        }
    }

    public string EnsureServer()
    {
        if (serverUrl != null) return serverUrl;

        var ports = new List<int>();
        try
        {
            foreach (var ep in IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners())
            {
                if (ep.Address.Equals(IPAddress.Loopback) || ep.Address.Equals(IPAddress.Any) ||
                    ep.Address.Equals(IPAddress.IPv6Loopback) || ep.Address.Equals(IPAddress.IPv6Any))
                    ports.Add(ep.Port);
            }
        }
        catch { }

        ports = ports.Distinct().ToList();
        var found = new string[ports.Count];
        Parallel.For(0, ports.Count, i =>
        {
            var url = "http://127.0.0.1:" + ports[i];
            if (Probe(url)) found[i] = url;
        });
        serverUrl = found.FirstOrDefault(u => u != null);

        if (serverUrl == null)
            throw new InvalidOperationException(Lang.Tr("api.notFound"));
        return serverUrl;
    }

    bool Probe(string url)
    {
        try
        {
            using var cts = new CancellationTokenSource(1200);
            using var req = new HttpRequestMessage(HttpMethod.Get, url + "/api/info");
            req.Headers.TryAddWithoutValidation("Authorization", Auth);
            using var resp = http.Send(req, cts.Token);
            return resp.StatusCode == HttpStatusCode.OK;
        }
        catch { return false; }
    }

    JsonDocument Request(HttpMethod method, string path, object body = null)
    {
        try { return RequestOnce(method, path, body); }
        catch (Exception ex) when (IsConnectionError(ex) && serverUrl != null)
        {
            ResetServer();
            return RequestOnce(method, path, body);
        }
    }

    JsonDocument RequestOnce(HttpMethod method, string path, object body)
    {
        var url = EnsureServer() + path;
        using var req = new HttpRequestMessage(method, url);
        req.Headers.TryAddWithoutValidation("Authorization", Auth);
        if (body != null)
        {
            var json = JsonSerializer.Serialize(body);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }
        using var resp = http.Send(req);
        var text = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException(Lang.Tr("api.httpError", (int)resp.StatusCode, Trim(text)));
        if (string.IsNullOrWhiteSpace(text)) return JsonDocument.Parse("{}");
        return JsonDocument.Parse(text);
    }

    static string Trim(string s) => s == null ? "" : (s.Length > 300 ? s[..300] + "…" : s);

    public void ResetServer() => serverUrl = null;

    public string CreateSession(string title, string modelId, string providerId, string persona)
    {
        string id;
        using (var doc = Request(HttpMethod.Post, "/api/session",
            new
            {
                title,
                model = new { id = modelId, providerID = providerId },
                metadata = new { milli = true },
            }))
            id = doc.RootElement.GetProperty("data").GetProperty("id").GetString();

        if (!string.IsNullOrWhiteSpace(persona))
        {
            try { SendSynthetic(id, persona); }
            catch { }
        }
        return id;
    }

    public void SendSynthetic(string sessionId, string text, string description = "persona") =>
        Request(HttpMethod.Post, $"/api/session/{sessionId}/synthetic",
            new { text, description }).Dispose();

    public void SendPrompt(string sessionId, string text) =>
        Request(HttpMethod.Post, $"/api/session/{sessionId}/prompt", new { text }).Dispose();

    public bool Interrupt(string sessionId)
    {
        using var doc = Request(HttpMethod.Post, $"/api/session/{sessionId}/interrupt");
        return doc.RootElement.TryGetProperty("interrupted", out var v) &&
               v.ValueKind == JsonValueKind.True;
    }

    public void SetSessionModel(string sessionId, string modelId, string providerId) =>
        Request(HttpMethod.Post, $"/api/session/{sessionId}/model",
            new { model = new { id = modelId, providerID = providerId } }).Dispose();

    public List<ChatMessage> GetMessages(string sessionId)
    {
        var list = new List<ChatMessage>();
        using var doc = Request(HttpMethod.Get, $"/api/session/{sessionId}/message");
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var m in data.EnumerateArray())
        {
            var cm = new ChatMessage();
            if (m.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String) cm.type = t.GetString();
            if (m.TryGetProperty("time", out var time) && time.ValueKind == JsonValueKind.Object &&
                time.TryGetProperty("created", out var created) && created.ValueKind == JsonValueKind.Number)
                cm.created = created.GetInt64();
            if (m.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
            {
                foreach (var c in content.EnumerateArray())
                {
                    if (c.TryGetProperty("type", out var ct) && ct.ValueKind == JsonValueKind.String &&
                        ct.GetString() == "text" &&
                        c.TryGetProperty("text", out var tx) && tx.ValueKind == JsonValueKind.String)
                        cm.texts.Add(tx.GetString());
                }
            }
            if (m.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.Object &&
                err.TryGetProperty("message", out var em) && em.ValueKind == JsonValueKind.String)
                cm.error = em.GetString();
            if (m.TryGetProperty("outcome", out var oc) && oc.ValueKind == JsonValueKind.String)
                cm.outcome = oc.GetString();
            list.Add(cm);
        }
        return list;
    }

    public List<ApiSession> ListSessions(int limit = 200)
    {
        var list = new List<ApiSession>();
        using var doc = Request(HttpMethod.Get, $"/api/session?limit={limit}&order=desc");
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var s in data.EnumerateArray())
        {
            var it = new ApiSession();
            if (s.TryGetProperty("id", out var v) && v.ValueKind == JsonValueKind.String) it.id = v.GetString();
            if (s.TryGetProperty("title", out v) && v.ValueKind == JsonValueKind.String) it.title = v.GetString();
            if (s.TryGetProperty("time", out var t) && t.ValueKind == JsonValueKind.Object &&
                t.TryGetProperty("created", out var c) && c.ValueKind == JsonValueKind.Number)
                it.created = c.GetInt64();
            if (s.TryGetProperty("metadata", out var md) && md.ValueKind == JsonValueKind.Object &&
                md.TryGetProperty("milli", out var m) && m.ValueKind != JsonValueKind.Null && m.ValueKind != JsonValueKind.False)
                it.marked = true;
            if (!string.IsNullOrEmpty(it.id)) list.Add(it);
        }
        return list;
    }

    public HashSet<string> ActiveSessions()
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        using var doc = Request(HttpMethod.Get, "/api/session/active");
        if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
            foreach (var p in data.EnumerateObject())
                set.Add(p.Name);
        return set;
    }

    public void DeleteSession(string sessionId) =>
        Request(HttpMethod.Delete, $"/api/session/{sessionId}").Dispose();

    public bool HasPersonaMarker(string sessionId)
    {
        using var doc = Request(HttpMethod.Get, $"/api/session/{sessionId}/message?type=synthetic");
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return false;

        foreach (var m in data.EnumerateArray())
        {
            if (m.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String &&
                t.GetString() == "synthetic" &&
                m.TryGetProperty("description", out var d) && d.ValueKind == JsonValueKind.String &&
                d.GetString() == "persona")
                return true;
        }
        return false;
    }

    public List<ApiModel> GetModels()
    {
        var list = new List<ApiModel>();
        using var doc = Request(HttpMethod.Get, "/api/model");
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var m in data.EnumerateArray())
        {
            var am = new ApiModel();
            if (m.TryGetProperty("id", out var v) && v.ValueKind == JsonValueKind.String) am.id = v.GetString();
            if (m.TryGetProperty("providerID", out v) && v.ValueKind == JsonValueKind.String) am.providerID = v.GetString();
            if (m.TryGetProperty("name", out v) && v.ValueKind == JsonValueKind.String) am.name = v.GetString();
            if (m.TryGetProperty("status", out v) && v.ValueKind == JsonValueKind.String) am.status = v.GetString();
            if (m.TryGetProperty("enabled", out v) && v.ValueKind == JsonValueKind.False) am.enabled = false;
            if (m.TryGetProperty("limit", out var lim) && lim.ValueKind == JsonValueKind.Object &&
                lim.TryGetProperty("context", out var ctx) && ctx.ValueKind == JsonValueKind.Number)
                am.context = ctx.GetInt64();

            if (m.TryGetProperty("cost", out var cost))
            {
                JsonElement c = default;
                if (cost.ValueKind == JsonValueKind.Array && cost.GetArrayLength() > 0) c = cost[0];
                else if (cost.ValueKind == JsonValueKind.Object) c = cost;
                if (c.ValueKind == JsonValueKind.Object)
                {
                    am.hasCost = true;
                    if (c.TryGetProperty("input", out var i) && i.ValueKind == JsonValueKind.Number) am.costIn = i.GetDouble();
                    if (c.TryGetProperty("output", out var o) && o.ValueKind == JsonValueKind.Number) am.costOut = o.GetDouble();
                }
            }
            list.Add(am);
        }
        return list;
    }

    public string PriceText(ApiModel m)
    {
        if (!m.hasCost) return Lang.Tr("price.none");
        if (m.costIn <= 0 && m.costOut <= 0) return Lang.Tr("price.free");
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        return Lang.Tr("price.paid",
            m.costIn.ToString("0.###", ci),
            m.costOut.ToString("0.###", ci));
    }

    public void Dispose() => http.Dispose();
}
