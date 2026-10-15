using agentest.Forms.Theming;
namespace agentest.Forms.Conversations;
public sealed partial class DeleteConversationForm
{
    private Label message = null!;
    private void InitializeComponent()
    {
        Text = "Eliminar conversación";
        Font = new Font("Segoe UI", 10);
        Size = new Size(500, 270);
        MinimumSize = new Size(440, 250);
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = MaximizeBox = false;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 2 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        message = new Label { Dock = DockStyle.Fill };
        root.Controls.Add(message, 0, 0);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var cancel = new ThemeButton { Text = "Cancelar", AutoSize = true, DialogResult = DialogResult.Cancel };
        var delete = new ThemeButton { Text = "Eliminar", AutoSize = true, DialogResult = DialogResult.OK };
        actions.Controls.AddRange([cancel, delete]);
        root.Controls.Add(actions, 0, 1);
        Controls.Add(root);
        CancelButton = cancel;
        AcceptButton = cancel;
    }
}
