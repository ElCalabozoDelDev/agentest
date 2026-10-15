namespace agentest.Models;

// Estado en memoria de la sesión. Solo el catálogo y las conversaciones se persisten explícitamente.
public sealed class ChatSession
{
    public AgentCatalog Catalog { get; set; } = new();
    public List<Conversation> Conversations { get; set; } = [];
    public Conversation? Current { get; set; }
    public string Draft { get; set; } = "";
    public List<ChatAttachment> PendingAttachments { get; } = [];
    public bool HasDraft => !string.IsNullOrWhiteSpace(Draft) || PendingAttachments.Count > 0;

    public void ClearDraft() { Draft = ""; PendingAttachments.Clear(); }
}
