namespace agentest.Models;

public sealed class AgentProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Orquestador (ejemplo)";
    public string Endpoint { get; set; } = "http://localhost:8000/chat";
    public string Method { get; set; } = "POST";
    public string AuthenticationHeader { get; set; } = "Authorization";
    public string AuthenticationPrefix { get; set; } = "Bearer";
    public int TimeoutSeconds { get; set; } = 120;
    public bool AcceptsAttachments { get; set; }
    public string AllowedExtensions { get; set; } = ".txt,.pdf";
    public int MaxAttachmentMegabytes { get; set; } = 10;
    public string ResponseTextPath { get; set; } = "answer";
    public string ResponseConversationIdPath { get; set; } = "conversation_id";
    public string RequestTemplate { get; set; } = """
        {
          "query": "{{message}}",
          "conversation_id": "{{conversationId}}"
        }
        """;

    public override string ToString() => Name;
}
