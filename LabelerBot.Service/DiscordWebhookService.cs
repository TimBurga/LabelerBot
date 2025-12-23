using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LabelerBot.Service;

public interface INotificationClient
{
    Task<HttpResponseMessage> SendAsync(string contentBody);
}

public class DiscordWebhookClient(HttpClient httpClient, IConfiguration config) : INotificationClient
{
    public async Task<HttpResponseMessage> SendAsync(string contentBody)
    {
        var request = new WebHookRequest { Username = "LabelerBot", Content = contentBody };
        var json = JsonSerializer.Serialize(request);

        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(config.GetValue<string>("Labeler:WebhookUrl"), content);
        response.EnsureSuccessStatusCode();

        return response;
    }
}


public class WebHookRequest
{
    [JsonPropertyName("content")]
    public string Content { get; set; }
    [JsonPropertyName("username")]
    public string? Username { get; set; }
}