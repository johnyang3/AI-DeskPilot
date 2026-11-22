namespace DeskPilot.Models;

public sealed class AppSettings
{
    public string ApiEndpoint { get; set; } = "https://api.openai.com/v1/chat/completions";
    public string Model { get; set; } = "gpt-4o-mini";
    public string EncryptedApiKey { get; set; } = string.Empty;
    public string GitHubOwner { get; set; } = "johnyang3";
    public string GitHubRepo { get; set; } = "AI-DeskPilot";
    public string LastOutputDirectory { get; set; } = string.Empty;
    public List<string> ProtectedPaths { get; set; } = new();
}
