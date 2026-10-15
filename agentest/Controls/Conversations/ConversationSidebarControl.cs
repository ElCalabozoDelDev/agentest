using agentest.Models;
using agentest.Forms.Theming;

namespace agentest.Controls.Conversations;

public sealed partial class ConversationSidebarControl : UserControl
{
    private bool binding;
    private Font? headingFont;
    public ConversationSidebarControl() { InitializeComponent(); UpdateHeadingFont(); }
    public event EventHandler? NewRequested;
    public event EventHandler? DeleteRequested;
    public event EventHandler? OpenFolderRequested;
    public event EventHandler? ConversationSelected;
    internal Conversation? SelectedConversation => conversations.SelectedItem as Conversation;

    internal void Bind(IEnumerable<Conversation> items, Conversation? current, AgentProfile? agent)
    {
        binding = true;
        try
        {
            conversations.Items.Clear();
            conversations.Items.AddRange(items.Where(c => c.AgentProfileId == agent?.Id).OrderByDescending(c => c.UpdatedAt).Cast<object>().ToArray());
            conversations.SelectedItem = current;
            heading.Text = $"Conversaciones\n{agent?.Name ?? "Sin agente"} · {conversations.Items.Count}";
        }
        finally { binding = false; }
    }

    internal void RestoreSelection(Conversation? conversation)
    {
        binding = true;
        try { conversations.SelectedItem = conversation; }
        finally { binding = false; }
    }

    internal void SetAvailable(bool available)
    {
        newConversation.Enabled = conversations.Enabled = available;
        delete.Enabled = available && SelectedConversation is not null;
    }
    internal void SetPalette(ThemePalette palette) => conversations.SetPalette(palette);
    protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); UpdateHeadingFont(); }
    private void UpdateHeadingFont()
    {
        if (heading is null) return;
        var previous = headingFont;
        headingFont = new Font(Font, FontStyle.Bold);
        heading.Font = headingFont;
        previous?.Dispose();
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { headingFont?.Dispose(); headingFont = null; }
        base.Dispose(disposing);
    }
}
