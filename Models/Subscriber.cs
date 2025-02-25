using FishyFlip.Models;

namespace LabelerBot.Models;

#nullable disable

public class Subscriber
{
    public ATDid Did { get; set; }
    public DateTime Timestamp { get; set; }
    public bool Active { get; set; }
}