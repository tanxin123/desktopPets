using System;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace Spider;

// 通过 GitHub Releases 的 latest 接口检查新版本；任何网络 / 解析错误都返回 null，不影响程序运行
public static class UpdateChecker
{
    public const string Repo = "tanxin123/desktopPets";
    public static string PageUrl => $"https://github.com/{Repo}/releases/latest";

    public static Version Current => Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);

    public static async Task<(Version Latest, string Tag, string Url)?> CheckAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("DesktopPet");
            var json = await http.GetStringAsync($"https://api.github.com/repos/{Repo}/releases/latest");
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            string tag = root.GetProperty("tag_name").GetString() ?? "";
            string url = root.TryGetProperty("html_url", out var u) ? (u.GetString() ?? PageUrl) : PageUrl;
            if (!Version.TryParse(tag.TrimStart('v', 'V'), out var v)) return null;
            return (v, tag, url);
        }
        catch { return null; }
    }
}
