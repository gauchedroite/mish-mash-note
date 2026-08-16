using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

var root = AppContext.BaseDirectory;
var dataPath = Path.Combine(root, "data.json");
var allowedRoots = new List<string>();

[DllImport("user32.dll")] static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, IntPtr dwExtraInfo);

static string Norm(string p) =>
    Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

bool SafePath(string p)
{
    var full = Norm(p);
    foreach (var r in allowedRoots.Append(Norm(root)))
        if (full == r || full.StartsWith(r + Path.DirectorySeparatorChar))
            return true;
    return false;
}

JsonDocument LoadData() { using var f = File.OpenRead(dataPath); return JsonDocument.Parse(f); }
void SaveData(JsonElement el) =>
    File.WriteAllText(dataPath, JsonSerializer.Serialize(el, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));

void RefreshRoots()
{
    allowedRoots.Clear();
    try
    {
        using var doc = LoadData();
        if (doc.RootElement.TryGetProperty("groups", out var groups))
            foreach (var g in groups.EnumerateObject())
                if (g.Value.TryGetProperty("dir", out var d) && d.GetString() is { Length: > 0 } s)
                    allowedRoots.Add(Norm(s));
    }
    catch { }
}

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:9341");
var app = builder.Build();

void Cors(HttpResponse r)
{
    r.Headers["Access-Control-Allow-Origin"] = "*";
    r.Headers["Cache-Control"] = "no-store";
}
void AllowOptions(IEndpointRouteBuilder e) =>
    e.MapMethods("/api/{**_}", new[] { "OPTIONS" }, async _ => Results.Ok());

app.MapGet("/", () => Results.File(Path.Combine(root, "index.html"), "text/html; charset=utf-8"));
app.MapGet("/index.html", () => Results.File(Path.Combine(root, "index.html"), "text/html; charset=utf-8"));

app.MapGet("/api/data", () => Results.File(dataPath, "application/json"));
app.MapGet("/api/root", () => Results.Text(root));
app.MapGet("/api/datafile", () => Results.File(dataPath, "text/plain; charset=utf-8"));

app.MapGet("/api/list", (string? path) =>
{
    if (path is null || !SafePath(path) || !Directory.Exists(path)) return Results.Json(Array.Empty<object>());
    var entries = Directory.GetFiles(path)
        .Select(p => new { name = Path.GetFileName(p), mtime = new DateTimeOffset(File.GetLastWriteTimeUtc(p)).ToUnixTimeSeconds() })
        .OrderBy(e => e.name, StringComparer.OrdinalIgnoreCase);
    return Results.Json(entries);
});

app.MapGet("/api/file", (string? path) =>
{
    if (path is null || !SafePath(path) || !File.Exists(path)) return Results.NotFound();
    return Results.File(path, "text/plain; charset=utf-8");
});

app.MapGet("/api/open", (string? path) =>
{
    if (path is null || !SafePath(path) || !(File.Exists(path) || Directory.Exists(path))) return Results.NotFound();
    // ponytail: ALT-tap to dodge Windows foreground-lock so Explorer opens in front.
    keybd_event(0x12, 0, 0, IntPtr.Zero);
    keybd_event(0x12, 0, 2, IntPtr.Zero);
    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    return Results.Json(new { ok = true });
});

app.MapGet("/api/delfile", (string? path) =>
{
    if (path is null || !SafePath(path) || !File.Exists(path)) return Results.NotFound();
    File.Delete(path);
    return Results.Json(new { ok = true });
});

app.MapPost("/api/data", async (HttpContext ctx) =>
{
    try
    {
        using var doc = await JsonDocument.ParseAsync(ctx.Request.Body);
        SaveData(doc.RootElement);
        RefreshRoots();
        return Results.Json(new { ok = true });
    }
    catch (Exception e) { return Results.Json(new { error = e.Message }, statusCode: 400); }
});

app.MapPost("/api/datafile", async (HttpContext ctx) =>
{
    try
    {
        using var ms = new MemoryStream();
        await ctx.Request.Body.CopyToAsync(ms);
        using (JsonDocument.Parse(ms.ToArray())) { } // validate before writing
        await File.WriteAllTextAsync(dataPath, System.Text.Encoding.UTF8.GetString(ms.ToArray()));
        RefreshRoots();
        return Results.Json(new { ok = true });
    }
    catch (Exception e) { return Results.Json(new { error = e.Message }, statusCode: 400); }
});

app.MapPost("/api/file", async (string? path, HttpContext ctx) =>
{
    if (path is null || !SafePath(path)) return Results.NotFound();
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    await using var f = File.Create(path);
    await ctx.Request.Body.CopyToAsync(f);
    return Results.Json(new { ok = true });
});

app.MapPost("/api/newfile", (string? dir, string? name) =>
{
    if (dir is null || !SafePath(dir) || string.IsNullOrWhiteSpace(name)) return Results.Json(new { error = "missing dir/name" }, statusCode: 400);
    if (!name.EndsWith(".md") && !name.EndsWith(".txt")) name += ".md";
    var p = Path.Combine(dir, name);
    if (File.Exists(p)) return Results.Json(new { error = "exists" }, statusCode: 409);
    Directory.CreateDirectory(dir);
    File.Create(p).Dispose();
    return Results.Json(new { path = p });
});

app.Use(async (ctx, next) => { Cors(ctx.Response); await next(); });
AllowOptions(app);
RefreshRoots();
app.Run();
