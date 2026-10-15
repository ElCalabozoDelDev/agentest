using agentest.Models;
using agentest.Persistence;

namespace agentest.Services;

public sealed class ChatSessionService(LocalStore store, OrchestratorClient? transport = null) : IDisposable
{
    private readonly OrchestratorClient client = transport ?? new();
    private CancellationTokenSource? sending;
    private bool disposed;
    public bool IsSending => sending is not null;
    public bool CanCancel => sending is { IsCancellationRequested: false };
    public string Status { get; private set; } = "";
    public event EventHandler? Changed;
    public void Cancel() { sending?.Cancel(); Status = "Cancelando solicitud…"; Changed?.Invoke(this, EventArgs.Empty); }

    public async Task<string?> SendAsync(ChatSession session, AgentProfile profile, string credential)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (IsSending) throw new InvalidOperationException("Ya hay un mensaje en curso.");
        var conversation = session.Current ?? throw new InvalidOperationException("No hay una conversación seleccionada.");
        if (conversation.AgentProfileId != profile.Id) throw new InvalidOperationException("La conversación pertenece a otro agente.");
        if (!session.HasDraft) throw new InvalidOperationException("El borrador está vacío.");
        OrchestratorClient.ValidateProfile(profile);
        var message = new ChatMessage { Text = session.Draft.Trim(), Attachments = session.PendingAttachments.ToList(), DeliveryStatus = "pending" };
        var previousTitle = conversation.Title;
        var previousUpdated = conversation.UpdatedAt;
        conversation.Messages.Add(message);
        conversation.UpdatedAt = DateTimeOffset.Now;
        if (conversation.Messages.Count == 1)
        {
            var title = string.IsNullOrWhiteSpace(message.Text) ? message.Attachments[0].Name : message.Text.Replace('\r', ' ').Replace('\n', ' ');
            conversation.Title = title.Length > 50 ? title[..50] + "…" : title;
        }
        sending = new CancellationTokenSource();
        Changed?.Invoke(this, EventArgs.Empty);
        string? warning = null;
        try
        {
            try { await store.SaveConversationAsync(conversation); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                conversation.Messages.Remove(message);
                conversation.Title = previousTitle;
                conversation.UpdatedAt = previousUpdated;
                Status = "No se envió el mensaje porque no pudo guardarse.";
                throw new IOException("No se envió el mensaje porque no pudo guardarse: " + ex.Message, ex);
            }
            if (!session.Conversations.Contains(conversation)) session.Conversations.Add(conversation);
            session.ClearDraft();
            Status = "Esperando respuesta del orquestador…";
            Changed?.Invoke(this, EventArgs.Empty);
            try
            {
                var reply = await client.SendAsync(profile, conversation, message, credential, sending.Token);
                message.DeliveryStatus = "completed";
                if (reply.ConversationId is not null) conversation.RemoteConversationId = reply.ConversationId;
                conversation.Messages.Add(new ChatMessage { Role = "assistant", Text = reply.Text, RawResponse = reply.RawResponse });
                foreach (var attachment in message.Attachments) attachment.Base64 = "";
                Status = "Respuesta recibida.";
            }
            catch (Exception ex)
            {
                message.DeliveryStatus = ex is OperationCanceledException ? "cancelled" : "failed";
                var error = ex is OperationCanceledException ? "Solicitud cancelada. El backend podría haberla procesado; verifica antes de reenviar." : ex.Message;
                conversation.Messages.Add(new ChatMessage { Role = "system", Text = error, DeliveryStatus = "failed" });
                session.Draft = message.Text;
                session.PendingAttachments.AddRange(message.Attachments);
                Status = "El mensaje no se completó. Se recuperó el borrador.";
            }
            conversation.UpdatedAt = DateTimeOffset.Now;
            try { await store.SaveConversationAsync(conversation); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Status = "No se pudo guardar el resultado en disco.";
                warning = Status + " " + ex.Message;
            }
            return warning;
        }
        finally
        {
            sending.Dispose();
            sending = null;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        sending?.Cancel();
        client.Dispose();
    }
}
