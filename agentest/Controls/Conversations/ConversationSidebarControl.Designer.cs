using agentest.Forms.Theming;

namespace agentest.Controls.Conversations;

public sealed partial class ConversationSidebarControl
{
    private ConversationListBox conversations = null!;
    private Label heading = null!;
    private Button newConversation = null!, delete = null!, openFolder = null!;

    private void InitializeComponent()
    {
        Dock = DockStyle.Fill;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(0, 0, 12, 0) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var height in new[] { 54, 48, 0, 48, 50 }) layout.RowStyles.Add(new RowStyle(height == 0 ? SizeType.Percent : SizeType.Absolute, height == 0 ? 100 : height));
        heading = new Label { Dock = DockStyle.Fill, AutoEllipsis = true };
        newConversation = new ThemeButton { Text = "Nueva conversación", Dock = DockStyle.Fill };
        conversations = new ConversationListBox();
        delete = new ThemeButton { Text = "Eliminar conversación", Dock = DockStyle.Fill };
        openFolder = new ThemeButton { Text = "Abrir carpeta local", Dock = DockStyle.Fill };
        layout.Controls.Add(heading, 0, 0);
        layout.Controls.Add(newConversation, 0, 1);
        layout.Controls.Add(conversations, 0, 2);
        layout.Controls.Add(delete, 0, 3);
        layout.Controls.Add(openFolder, 0, 4);
        Controls.Add(layout);
        newConversation.Click += (_, _) => NewRequested?.Invoke(this, EventArgs.Empty);
        delete.Click += (_, _) => DeleteRequested?.Invoke(this, EventArgs.Empty);
        openFolder.Click += (_, _) => OpenFolderRequested?.Invoke(this, EventArgs.Empty);
        conversations.SelectedIndexChanged += (_, _) => { if (!binding) ConversationSelected?.Invoke(this, EventArgs.Empty); };
    }
}
