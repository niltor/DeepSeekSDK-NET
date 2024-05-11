using System.Text.Json.Serialization;

namespace DeepSeek.Core.Models;

public class ModelResponse
{
    public string? Object { get; set; }
    public List<Model> Data { get; set; } = [];
}

public record Model
{
    public string? Id { get; set; }
    public string? Object { get; set; }
    [JsonPropertyName("owned_by")]
    public string? OwnedBy { get; set; }
}

