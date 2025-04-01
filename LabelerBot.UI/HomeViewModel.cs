namespace LabelerBot.UI;

public class HomeViewModel
{
    public required string Did { get; set; }
    public required DateTimeOffset Timestamp { get; set; }
    public required string Active { get; set; }
    public required string Handle { get; set; }
    public required int Posts { get; set; }
    public required int Score { get; set; }
    public required string Label { get; set; }
}
