using agentest.Forms.Theming;
namespace agentest.Forms.Groups;
public sealed partial class GroupManagerForm
{
    private ListBox groups = null!;
    private TextBox name = null!;
    private Label detail = null!;
    private Button rename = null!;
    private Button delete = null!;
    private Button add = null!;
    private void InitializeComponent()
    {
        groups = new() { Dock = DockStyle.Fill, IntegralHeight = false };
        name = new() { Dock = DockStyle.Fill, MaxLength = 100 };
        detail = new() { Dock = DockStyle.Fill, AutoEllipsis = true };
        rename = new ThemeButton { Text = "Renombrar", AutoSize = true };
        delete = new ThemeButton { Text = "Eliminar", AutoSize = true };
        Text = "Administrar grupos · Sistemas agénticos";
        Font = new Font("Segoe UI", 10);
        Size = new Size(650, 510);
        MinimumSize = new Size(560, 450);
        StartPosition = FormStartPosition.CenterParent;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 6 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var height in new[] { 48, 0, 36, 36, 48, 48 })
            root.RowStyles.Add(new RowStyle(height == 0 ? SizeType.Percent : SizeType.Absolute, height == 0 ? 100 : height));
        root.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "Cada grupo representa un sistema agéntico. Puedes conservar grupos vacíos. DEFAULT recibe los agentes de los grupos eliminados." }, 0, 0);
        root.Controls.Add(groups, 0, 1);
        root.Controls.Add(detail, 0, 2);
        root.Controls.Add(name, 0, 3);
        name.PlaceholderText = "Nombre del grupo";
        name.AccessibleName = "Nombre del grupo";
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        add = new ThemeButton { Text = "Crear grupo", AutoSize = true };
        actions.Controls.AddRange([add, rename, delete]);
        root.Controls.Add(actions, 0, 4);
        var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var save = new ThemeButton { Text = "Guardar cambios", AutoSize = true, DialogResult = DialogResult.OK };
        var cancel = new ThemeButton { Text = "Cancelar", AutoSize = true, DialogResult = DialogResult.Cancel };
        footer.Controls.AddRange([save, cancel]);
        root.Controls.Add(footer, 0, 5);
        Controls.Add(root);
        CancelButton = cancel;
    }
}
