using System.Text.Json.Serialization;

namespace agentest.Models;

public sealed class Conversation
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string AgentProfileId { get; set; } = "";
    public string? RemoteConversationId { get; set; }
    public string Title { get; set; } = "Nueva conversación";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
    public List<ChatMessage> Messages { get; set; } = [];
    public override string ToString() => Title;
}

public sealed class ChatMessage
{
    public string Role { get; set; } = "user";
    public string Text { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public List<ChatAttachment> Attachments { get; set; } = [];
    public string? RawResponse { get; set; }
    public string DeliveryStatus { get; set; } = "completed";
}

public sealed class ChatAttachment
{
    public string Name { get; set; } = "";
    public string ContentType { get; set; } = "application/octet-stream";
    public long Size { get; set; }
    // Binary contents are sent with the current request only; local history stores metadata.
    [JsonIgnore]
    public string Base64 { get; set; } = "";
}
