using FishyFlip.Models;

namespace LabelerBot.Bot.Models;

#nullable disable

public class Label
{
    public ATDid Did { get; set; }
    public LabelLevel Level { get; set; }
    public DateTime Timestamp { get; set; }
}