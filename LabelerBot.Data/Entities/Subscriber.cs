using FishyFlip.Models;

namespace LabelerBot.Data.Entities;

public class Subscriber
{
    public ATDid Did { get; set; }
    public DateTime Timestamp { get; set; }
    public bool Active { get; set; }
    public string Handle { get; set; }
    public string Rkey { get; set; }
    public ICollection<ImagePost> Posts { get; set; } = new List<ImagePost>();
    public Label? Label { get; set; }
}