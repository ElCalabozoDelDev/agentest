using agentest.Forms.Theming;
namespace agentest.Controls.Agents;
public sealed partial class AgentsOverviewControl
{
    private Label summary = null!;
    private Button refresh = null!;
    private Button manage = null!;
    private FlowLayoutPanel canvas = null!;
    private void InitializeComponent()
    {
        summary = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
        refresh = new ThemeButton { Text = "Actualizar ahora", AutoSize = true, Dock = DockStyle.Right };
        manage = new ThemeButton { Text = "Administrar grupos", AutoSize = true, Dock = DockStyle.Right };
        canvas = new() { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        Dock = DockStyle.Fill;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 3 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var bar = new Panel { Dock = DockStyle.Fill };
        bar.Controls.Add(summary);
        bar.Controls.Add(refresh);
        bar.Controls.Add(manage);
        root.Controls.Add(bar, 0, 0);
        root.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "Las líneas indican pertenencia al sistema. Configura agentes desde el editor de perfiles en Chat.", AutoEllipsis = true }, 0, 1);
        root.Controls.Add(canvas, 0, 2);
        Controls.Add(root);
    }
}
