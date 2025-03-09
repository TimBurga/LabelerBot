using FishyFlip.Models;

namespace LabelerBot.Bot.Models;

#nullable disable

public class Subscriber
{
    public ATDid Did { get; set; }
    public DateTime Timestamp { get; set; }
    public bool Active { get; set; }
    public string Handle { get; set; }
    public string Rkey { get; set; }
}