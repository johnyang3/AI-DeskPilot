namespace DeskPilot.Models;

public sealed class DesktopMove
{
    public string Source { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
}

public sealed class MoveJournal
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public List<DesktopMove> Moves { get; set; } = new();
}
