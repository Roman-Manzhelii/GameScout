namespace GameScout.Domain.Models;
public class Deal
{
    public string Store { get; set; } = "";
    public decimal Price { get; set; }
    public decimal NormalPrice { get; set; }
    public decimal Savings { get; set; }
    public string Url { get; set; } = "";
    public string? Image { get; set; }
}
