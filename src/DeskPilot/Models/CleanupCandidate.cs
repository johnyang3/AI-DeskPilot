namespace DeskPilot.Models;

public sealed class CleanupCandidate
{
    public bool IsSelected { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string UninstallString { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string SizeText => SizeBytes <= 0 ? "—" : $"{SizeBytes / 1024d / 1024d:F1} MB";
}
