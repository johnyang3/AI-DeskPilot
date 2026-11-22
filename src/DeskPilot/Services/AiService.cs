using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DeskPilot.Models;

namespace DeskPilot.Services;

public sealed class AiService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(120) };

    public async Task<string> SendAsync(AppSettings settings, string apiKey, string userText, Func<string, JsonElement, Task<string>> toolHandler, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(settings.ApiEndpoint, UriKind.Absolute, out var endpoint) ||
            (endpoint.Scheme != Uri.UriSchemeHttps && !(endpoint.Scheme == Uri.UriSchemeHttp && endpoint.IsLoopback)))
            throw new InvalidOperationException("The API endpoint must use HTTPS (HTTP is allowed only on localhost).");
        var messages = new List<object>
        {
            new { role = "system", content = Loc.IsChinese
                ? "你是 DeskPilot 的 Windows 桌面助手。只能使用提供的白名单工具，不得声称执行了未返回成功的操作。清理候选不等于恶意软件，必须由用户判断。涉及文件或软件改动时，工具会请求用户确认；尊重取消结果。回答使用用户所用语言。"
                : "You are DeskPilot's Windows desktop assistant. Use only the allow-listed tools. Never claim an action succeeded unless its tool result says so. Cleanup candidates are not malware verdicts. File and software changes require user confirmation through tools; respect cancellation. Reply in the user's language." },
            new { role = "user", content = userText }
        };

        for (var round = 0; round < 8; round++)
        {
            var payload = new { model = settings.Model, messages, tools = Tools(), tool_choice = "auto", temperature = 0.2 };
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var response = await Http.SendAsync(request, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"API {(int)response.StatusCode}: {responseText[..Math.Min(responseText.Length, 1200)]}");
            using var document = JsonDocument.Parse(responseText);
            var message = document.RootElement.GetProperty("choices")[0].GetProperty("message");
            if (!message.TryGetProperty("tool_calls", out var toolCalls) || toolCalls.GetArrayLength() == 0)
                return message.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String ? content.GetString() ?? string.Empty : string.Empty;

            messages.Add(JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(message.GetRawText())!);
            foreach (var call in toolCalls.EnumerateArray())
            {
                var id = call.GetProperty("id").GetString() ?? string.Empty;
                var function = call.GetProperty("function");
                var name = function.GetProperty("name").GetString() ?? string.Empty;
                string result;
                try
                {
                    using var arguments = JsonDocument.Parse(function.GetProperty("arguments").GetString() ?? "{}");
                    result = await toolHandler(name, arguments.RootElement.Clone());
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    result = $"ERROR: {ex.Message}";
                }
                messages.Add(new { role = "tool", tool_call_id = id, content = result });
            }
        }
        throw new InvalidOperationException("The AI tool-call limit (8 rounds) was reached.");
    }

    private static object[] Tools() =>
    [
        Tool("get_system_info", "Read basic Windows and app information.", new { type = "object", properties = new { } }),
        Tool("list_desktop_files", "List files and folders on the current user's desktop.", new { type = "object", properties = new { } }),
        Tool("scan_cleanup_candidates", "Scan temporary files, rule-matched programs and 360-related browser extension candidates. Read-only; candidates are not malware verdicts.", new { type = "object", properties = new { } }),
        Tool("organize_desktop", "Preview and organize desktop files after user confirmation.", new { type = "object", properties = new { } }),
        Tool("extract_archives", "Extract archives after user confirmation.", new { type = "object", properties = new { paths = new { type = "array", items = new { type = "string" }, description = "Existing archive file paths" }, output_directory = new { type = "string" } }, required = new[] { "paths" } }),
        Tool("recycle_files", "Send files or directories to Recycle Bin after user confirmation. Protected paths and critical roots are rejected.", new { type = "object", properties = new { paths = new { type = "array", items = new { type = "string" } } }, required = new[] { "paths" } }),
        Tool("uninstall_program", "Launch the registered uninstaller for an exact cleanup-candidate name after user confirmation.", new { type = "object", properties = new { name = new { type = "string", description = "Exact DisplayName returned by scan_cleanup_candidates" } }, required = new[] { "name" } })
    ];

    private static object Tool(string name, string description, object parameters) => new
    {
        type = "function",
        function = new { name, description, parameters }
    };
}
