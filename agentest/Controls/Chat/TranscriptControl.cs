using agentest.Models;
using agentest.Forms.Theming;
namespace agentest.Controls.Chat;
public sealed partial class TranscriptControl : UserControl
{
    public TranscriptControl() => InitializeComponent();
    internal void Render(Conversation? current, ThemePalette palette)
    {
        transcript.Clear();
        if (current is null || current.Messages.Count == 0)
        {
            transcript.Text = "Inicia una conversación\n\nSelecciona un sistema y un agente. Usa Administrar agentes para configurar la conexión de tu orquestador.\n\nLos mensajes enviados y las respuestas se guardan en la carpeta local de Agentest.";
            return;
        }
        using var bold = new Font(transcript.Font, FontStyle.Bold);
        foreach (var message in current.Messages)
        {
            transcript.SelectionColor = message.Role == "user" ? palette.User : message.Role == "assistant" ? palette.Assistant : palette.Notice;
            transcript.SelectionFont = bold;
            var role = message.Role == "user" ? "Tú" : message.Role == "assistant" ? "Agente" : "Sistema";
            var delivery = message.DeliveryStatus switch { "pending" => " · enviando", "failed" => " · fallido", "cancelled" => " · cancelado", "interrupted" => " · interrumpido", _ => "" };
            transcript.AppendText($"{role} · {message.CreatedAt.LocalDateTime:dd/MM HH:mm}{delivery}\n");
            transcript.SelectionColor = palette.Text;
            transcript.SelectionFont = transcript.Font;
            transcript.AppendText(message.Text + "\n");
            foreach (var attachment in message.Attachments)
                transcript.AppendText($"  📎 {attachment.Name} ({attachment.Size / 1024.0:N1} KB)\n");
            transcript.AppendText("\n");
        }
        transcript.SelectionStart = transcript.TextLength;
        transcript.ScrollToCaret();
    }

}
