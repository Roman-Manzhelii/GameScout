using GameScout.Domain.Enums;

namespace GameScout.Domain.Models;
public class SavedItem
{
    public int GameId { get; set; }
    public string Name { get; set; } = "";
    public BacklogStatus Status { get; set; } = BacklogStatus.Backlog;
    public string? Notes { get; set; }
}
