using agentest.Models;
using agentest.Forms.Theming;
namespace agentest.Controls.Chat;

public sealed partial class ChatView : UserControl
{
    public ChatView() => InitializeComponent();
    public event EventHandler? NewAgentRequested, EditAgentRequested, DeleteAgentRequested, ManageGroupsRequested;
    public event Action<ThemeMode>? ThemeRequested;
    internal AgentSelectorControl Selector => selector;
    internal Conversations.ConversationSidebarControl Sidebar => sidebar;
    internal MessageComposerControl Composer => composer;
    internal TranscriptControl Transcript => transcript;
    internal void SetStatus(string text) => status.Text = text;
    internal void SetAttachments(IReadOnlyList<ChatAttachment> attachments)
    {
        composer.BindAttachments(attachments);
        chatLayout.RowStyles[4].Height = attachments.Count > 0 ? 232 : 156;
    }
    internal void SetState(bool ready, bool busy, bool sending, bool canCancel, AgentProfile? profile, bool hasDraft, bool changingTheme = false)
    {
        selector.SetAvailable(ready && !busy);
        sidebar.SetAvailable(ready && !busy);
        manageAgents.Enabled = manageGroups.Enabled = ready && !busy;
        theme.Enabled = ready && !changingTheme;
        newAgent.Enabled = ready && !busy;
        editAgent.Enabled = deleteAgent.Enabled = ready && !busy && profile is not null;
        composer.SetState(ready && !busy, ready && !busy && profile is not null && hasDraft, sending && canCancel, ready && !busy && profile?.AcceptsAttachments == true);
        capabilities.Text = profile is null ? "Selecciona un agente o usa Administrar agentes para crear uno en este sistema." : profile.AcceptsAttachments
            ? $"Adjuntos: {profile.AllowedExtensions} · hasta {profile.MaxAttachmentMegabytes} MB por archivo" : "Este agente admite mensajes de texto.";
    }
    internal void SetTheme(ThemeMode mode, ThemePalette palette)
    {
        ThemeAppearance.Apply(this, palette);
        ThemeAppearance.Apply(agentsMenu, palette);
        ThemeAppearance.Apply(themeMenu, palette);
        sidebar.SetPalette(palette);
        theme.Text = mode switch
        {
            ThemeMode.Light => "Tema: Claro ▾", ThemeMode.Dark => "Tema: Oscuro ▾",
            _ => $"Tema: Sistema ({(palette.IsDark ? "oscuro" : "claro")}) ▾"
        };
        foreach (ToolStripMenuItem item in themeMenu.Items) item.Checked = Equals(item.Tag, mode);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) components?.Dispose();
        base.Dispose(disposing);
    }
}
