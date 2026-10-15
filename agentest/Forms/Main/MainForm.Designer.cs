using agentest.Controls.Chat;
using agentest.Controls.Agents;

namespace agentest.Forms.Main;

public sealed partial class MainForm
{
    private ChatView chat = null!;
    private AgentsOverviewControl overview = null!;
    private void InitializeComponent()
    {
        Text = "Agentest · Chat con agentes";
        Size = new Size(1180, 820);
        MinimumSize = new Size(960, 700);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10);
        var tabs = new TabControl { Dock = DockStyle.Fill };
        var chatTab = new TabPage("Chat") { UseVisualStyleBackColor = false };
        var agentsTab = new TabPage("Agentes") { UseVisualStyleBackColor = false };
        chat = new ChatView(); overview = new AgentsOverviewControl();
        chatTab.Controls.Add(chat); agentsTab.Controls.Add(overview);
        tabs.TabPages.AddRange([chatTab, agentsTab]); Controls.Add(tabs);
    }
}
