using agentest.Models;

namespace agentest.Controls.Chat;

public sealed partial class AgentSelectorControl : UserControl
{
    public AgentSelectorControl() => InitializeComponent();
    public event EventHandler? SystemChanged;
    public event EventHandler? AgentChanged;
    internal string? SelectedSystem => systems.SelectedItem as string;
    internal AgentProfile? SelectedAgent => agents.SelectedItem as AgentProfile;

    internal void Bind(IReadOnlyList<string> groups, IReadOnlyList<AgentProfile> profiles, string system, string? agentId)
    {
        systems.Items.Clear();
        systems.Items.AddRange(groups.OrderBy(g => g).Cast<object>().ToArray());
        systems.SelectedItem = system;
        agents.Items.Clear();
        agents.Items.AddRange(profiles.Where(p => string.Equals(p.AgentSystem, system, StringComparison.OrdinalIgnoreCase)).Cast<object>().ToArray());
        agents.SelectedItem = profiles.FirstOrDefault(p => p.Id == agentId);
    }

    internal void SetAvailable(bool available)
    {
        systems.Enabled = available;
        agents.Enabled = available && agents.Items.Count > 0;
    }
}
