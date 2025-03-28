﻿using FishyFlip.Models;

namespace LabelerBot.Data.Entities;

#nullable disable

public class ImagePost
{
    public ATDid Did { get; set; }
    public string Cid { get; set; }
    public bool ValidAlt { get; set; }
    public DateTime Timestamp { get; set; }
    public virtual Subscriber Subscriber { get; set; }
}