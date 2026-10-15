namespace agentest.Controls.Chat;

public sealed partial class AgentSelectorControl
{
    private ComboBox systems = null!, agents = null!;
    private void InitializeComponent()
    {
        Dock = DockStyle.Fill;
        systems = new ComboBox { Name = "ChatSystem", AccessibleName = "Sistema", DropDownStyle = ComboBoxStyle.DropDownList, Anchor = AnchorStyles.Left | AnchorStyles.Right };
        agents = new ComboBox { Name = "ChatAgent", AccessibleName = "Agente", DropDownStyle = ComboBoxStyle.DropDownList, Anchor = AnchorStyles.Left | AnchorStyles.Right };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.Controls.Add(new Label { Text = "Sistema", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, 8, 0) }, 0, 0);
        layout.Controls.Add(systems, 1, 0);
        layout.Controls.Add(new Label { Text = "Agente", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(12, 0, 8, 0) }, 2, 0);
        layout.Controls.Add(agents, 3, 0);
        Controls.Add(layout);
        systems.SelectedIndexChanged += (_, _) => SystemChanged?.Invoke(this, EventArgs.Empty);
        agents.SelectedIndexChanged += (_, _) => AgentChanged?.Invoke(this, EventArgs.Empty);
    }
}
