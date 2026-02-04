using System.Text.Json.Serialization;

namespace PortKiller.Blazor.Models;

public class WatchedPort
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [JsonPropertyName("port")]
    public int Port { get; set; }

    [JsonPropertyName("notifyOnStart")]
    public bool NotifyOnStart { get; set; } = true;

    [JsonPropertyName("notifyOnStop")]
    public bool NotifyOnStop { get; set; } = true;
}
