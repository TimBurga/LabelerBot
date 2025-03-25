using FishyFlip.Models;

namespace LabelerBot.Data.Entities;

#nullable disable

public class Label
{
    public ATDid Did { get; set; }
    public LabelLevel Level { get; set; }
    public DateTime Timestamp { get; set; }
    public virtual Subscriber SubscriberNavigation { get; set; }
}