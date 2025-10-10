namespace GameScout.Domain.Models;
public class GameDetails
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<string> Screenshots { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public List<string> Platforms { get; set; } = new();
    public List<string> Genres { get; set; } = new();
    public int? Metacritic { get; set; }
}
