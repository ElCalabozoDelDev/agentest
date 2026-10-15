using agentest.Forms.Theming;
using agentest.Models;
namespace agentest.Forms.Conversations;
public sealed partial class DeleteConversationForm : Form
{
    public DeleteConversationForm() : this(new Conversation(), false, ThemePalette.Light) { }
    public DeleteConversationForm(Conversation conversation, bool hasDraft, ThemePalette palette)
    {
        InitializeComponent();
        message.Text = $"¿Eliminar «{conversation.Title}» del historial local?\n\nEsta acción no elimina la conversación del servidor." + (hasDraft ? "\nTambién se descartará el borrador actual." : "");
        ThemeAppearance.Apply(this, palette);
    }
}
