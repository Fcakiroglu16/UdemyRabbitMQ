namespace PatternExample.API.Models;

public class Discount
{
    public int Id { get; set; }
    public Guid UserId { get; set; }
    public decimal DiscountPercentage { get; set; }
    public DateTime CreatedAt { get; set; }
}
