using agentest.Models;
using agentest.Services;
using agentest.Forms.Theming;

namespace agentest.Forms;

public sealed class GroupManagerForm : Form
{
    private readonly ListBox groups = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly TextBox name = new() { Dock = DockStyle.Fill, MaxLength = 100 };
    private readonly Label detail = new() { Dock = DockStyle.Fill, AutoEllipsis = true };
    private readonly Button rename = new ThemeButton { Text = "Renombrar", AutoSize = true };
    private readonly Button delete = new ThemeButton { Text = "Eliminar", AutoSize = true };
    public AgentCatalog Catalog { get; }

    public GroupManagerForm(AgentCatalog catalog, ThemePalette palette)
    {
        Catalog = AgentGroups.Copy(catalog);
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
        var add = new ThemeButton { Text = "Crear grupo", AutoSize = true };
        actions.Controls.AddRange([add, rename, delete]);
        root.Controls.Add(actions, 0, 4);
        var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var save = new ThemeButton { Text = "Guardar cambios", AutoSize = true, DialogResult = DialogResult.OK };
        var cancel = new ThemeButton { Text = "Cancelar", AutoSize = true, DialogResult = DialogResult.Cancel };
        footer.Controls.AddRange([save, cancel]);
        root.Controls.Add(footer, 0, 5);
        Controls.Add(root);
        CancelButton = cancel;
        groups.SelectedIndexChanged += (_, _) =>
        {
            var selected = groups.SelectedItem as string;
            name.Text = selected ?? "";
            rename.Enabled = delete.Enabled = selected is not null && !AgentGroups.IsDefault(selected);
            var count = Catalog.Profiles.Count(p => string.Equals(p.AgentSystem, selected, StringComparison.OrdinalIgnoreCase));
            detail.Text = selected is null ? "Selecciona un grupo o escribe un nombre para crearlo." : $"{count} agentes asignados" + (AgentGroups.IsDefault(selected) ? " · Grupo de respaldo protegido" : "");
        };
        add.Click += (_, _) => Change(() => AgentGroups.Add(Catalog, name.Text), name.Text.Trim());
        rename.Click += (_, _) => { if (groups.SelectedItem is string selected) Change(() => AgentGroups.Rename(Catalog, selected, name.Text), name.Text.Trim()); };
        delete.Click += (_, _) =>
        {
            if (groups.SelectedItem is not string selected) return;
            var count = Catalog.Profiles.Count(p => string.Equals(p.AgentSystem, selected, StringComparison.OrdinalIgnoreCase));
            if (MessageBox.Show(this, $"¿Eliminar el grupo '{selected}'? Sus {count} agentes pasarán a DEFAULT al guardar.", "Eliminar grupo", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                Change(() => AgentGroups.Delete(Catalog, selected), "DEFAULT");
        };
        Reload("DEFAULT");
        ThemeAppearance.Apply(this, palette);
    }

    private void Change(Action action, string selection)
    {
        try { action(); Reload(selection); }
        catch (ArgumentException ex) { MessageBox.Show(this, ex.Message, "Revisa el grupo", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    private void Reload(string selection)
    {
        groups.Items.Clear();
        groups.Items.AddRange(Catalog.Groups.OrderBy(g => g).Cast<object>().ToArray());
        groups.SelectedItem = selection;
    }
}
