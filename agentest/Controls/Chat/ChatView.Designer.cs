#nullable enable
using agentest.Models;
using agentest.Forms.Theming;
using agentest.Controls.Conversations;
namespace agentest.Controls.Chat;
public sealed partial class ChatView
{
    private System.ComponentModel.IContainer? components;
    private AgentSelectorControl selector = null!;
    private ConversationSidebarControl sidebar = null!;
    private TranscriptControl transcript = null!;
    private MessageComposerControl composer = null!;
    private Button manageAgents = null!, manageGroups = null!, theme = null!;
    private ToolStripMenuItem newAgent = null!, editAgent = null!, deleteAgent = null!;
    private ContextMenuStrip agentsMenu = null!, themeMenu = null!;
    private Label status = null!, capabilities = null!;
    private TableLayoutPanel chatLayout = null!;
    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        Dock = DockStyle.Fill;
        Font = new Font("Segoe UI", 10);
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 2, RowCount = 1 };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240)); root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        sidebar = new ConversationSidebarControl();
        selector = new AgentSelectorControl();
        transcript = new TranscriptControl();
        composer = new MessageComposerControl();
        chatLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6 };
        chatLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        chatLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        chatLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        chatLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        chatLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        chatLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 156));
        chatLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Margin = new Padding(0, 0, 0, 6) };
        manageAgents = MakeAction("Administrar agentes"); manageGroups = MakeAction("Administrar grupos"); theme = MakeAction("Tema: Sistema ▾");
        actions.Controls.AddRange([manageAgents, manageGroups, theme]);
        capabilities = new Label { Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
        status = new Label { Dock = DockStyle.Fill, Text = "Cargando…", AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
        chatLayout.Controls.Add(selector, 0, 0); chatLayout.Controls.Add(actions, 0, 1); chatLayout.Controls.Add(capabilities, 0, 2);
        chatLayout.Controls.Add(transcript, 0, 3); chatLayout.Controls.Add(composer, 0, 4); chatLayout.Controls.Add(status, 0, 5);
        root.Controls.Add(sidebar, 0, 0); root.Controls.Add(chatLayout, 1, 0); Controls.Add(root);
        agentsMenu = new ContextMenuStrip(components); themeMenu = new ContextMenuStrip(components);
        newAgent = new ToolStripMenuItem("Nuevo agente"); editAgent = new ToolStripMenuItem("Editar agente"); deleteAgent = new ToolStripMenuItem("Eliminar agente");
        agentsMenu.Items.AddRange([newAgent, editAgent, deleteAgent]);
        manageAgents.ContextMenuStrip = agentsMenu; theme.ContextMenuStrip = themeMenu;
        manageAgents.Click += (_, _) => agentsMenu.Show(manageAgents, new Point(0, manageAgents.Height));
        theme.Click += (_, _) => themeMenu.Show(theme, new Point(0, theme.Height));
        newAgent.Click += (_, _) => NewAgentRequested?.Invoke(this, EventArgs.Empty);
        editAgent.Click += (_, _) => EditAgentRequested?.Invoke(this, EventArgs.Empty);
        deleteAgent.Click += (_, _) => DeleteAgentRequested?.Invoke(this, EventArgs.Empty);
        manageGroups.Click += (_, _) => ManageGroupsRequested?.Invoke(this, EventArgs.Empty);
        foreach (var (mode, label) in new[] { (ThemeMode.System, "Usar tema del sistema"), (ThemeMode.Light, "Interfaz clara"), (ThemeMode.Dark, "Interfaz oscura") })
        {
            var item = new ToolStripMenuItem(label) { Tag = mode };
            item.Click += (_, _) => ThemeRequested?.Invoke(mode);
            themeMenu.Items.Add(item);
        }
        SetState(false, false, false, false, null, false);
    }
    private static Button MakeAction(string text) => new ThemeButton { Text = text, AutoSize = true, Padding = new Padding(5), Margin = new Padding(3) };
}
