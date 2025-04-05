using FishyFlip.Models;

namespace LabelerBot.Data.Entities;

public class ImagePost
{
    public required ATDid Did { get; set; }
    public required string Cid { get; set; }
    public string? Rkey { get; set; }
    public bool ValidAlt { get; set; }
    public DateTime Timestamp { get; set; }
    public virtual Subscriber? Subscriber { get; set; }
}