using FishyFlip.Models;

namespace LabelerBot.Models;

#nullable disable

public class ImagePost
{
    public ATDid Did { get; set; }
    public string Cid { get; set; }
    public bool ValidAlt { get; set; }
    public DateTime Timestamp { get; set; }
}