using System;
using System.Collections.Generic;

namespace GameScout.Domain.Models;
public class GameSummary
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int? Metacritic { get; set; }
    public DateOnly? Released { get; set; }
    public List<string> Platforms { get; set; } = new();
    public List<string> Genres { get; set; } = new();
}
