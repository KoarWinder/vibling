using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;

namespace Vibling;

static class Updater
{
    const string Repo = "KoarWinder/vibling";
    const string AssetName = "Vibling.exe";

    public record Release(Version Version, string Url, string Page);

    static readonly HttpClient http = CreateClient();
    static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("Vibling-Updater");
        return c;
    }

    public static Version Current => Norm(typeof(Program).Assembly.GetName().Version);

    static Version Norm(Version v) => new(v.Major, v.Minor, Math.Max(0, v.Build));

    public static string Show(Version v) => $"{v.Major}.{v.Minor}.{v.Build}";

    static async Task<Release> Latest()
    {
        var json = await http.GetStringAsync($"https://api.github.com/repos/{Repo}/releases/latest");
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (!Version.TryParse(root.GetProperty("tag_name").GetString().TrimStart('v', 'V'), out var v)) return null;
        string url = null;
        foreach (var a in root.GetProperty("assets").EnumerateArray())
            if (string.Equals(a.GetProperty("name").GetString(), AssetName, StringComparison.OrdinalIgnoreCase))
                url = a.GetProperty("browser_download_url").GetString();
        return new Release(Norm(v), url, root.GetProperty("html_url").GetString());
    }

    public static Release Pending { get; private set; }

    public static void Dismiss() => Pending = null;

    public static async Task<string> Check(PetForm pet, bool manual)
    {
        try
        {
            var r = await Latest();
            if (r != null && r.Url != null && r.Version > Current)
            {
                Pending = r;
                pet.OnUpdateFound(manual);
                return null;
            }
            return string.Format(L.T("upd.latest"), Show(Current));
        }
        catch (Exception e)
        {
            return L.T("upd.error") + ": " + e.Message;
        }
    }

    public static async Task Install(Release r, IProgress<double> progress)
    {
        string exe = Environment.ProcessPath, fresh = exe + ".new", old = exe + ".old";
        using (var resp = await http.GetAsync(r.Url, HttpCompletionOption.ResponseHeadersRead))
        {
            resp.EnsureSuccessStatusCode();
            long total = resp.Content.Headers.ContentLength ?? 0;
            await using var src = await resp.Content.ReadAsStreamAsync();
            await using var dst = File.Create(fresh);
            var buf = new byte[81920];
            long done = 0;
            int n;
            while ((n = await src.ReadAsync(buf)) > 0)
            {
                await dst.WriteAsync(buf.AsMemory(0, n));
                done += n;
                if (total > 0) progress.Report((double)done / total);
            }
        }

        using (var f = File.OpenRead(fresh))
            if (f.Length < 100_000 || f.ReadByte() != 'M' || f.ReadByte() != 'Z')
                throw new InvalidDataException("downloaded file is not a program");

        if (File.Exists(old)) File.Delete(old);
        File.Move(exe, old);
        try { File.Move(fresh, exe); }
        catch { File.Move(old, exe); throw; }
        Process.Start(new ProcessStartInfo(exe, "--updated") { UseShellExecute = false });
    }

    public static void Cleanup()
    {
        try { File.Delete(Environment.ProcessPath + ".old"); } catch { }
        try { File.Delete(Environment.ProcessPath + ".new"); } catch { }
    }
}
