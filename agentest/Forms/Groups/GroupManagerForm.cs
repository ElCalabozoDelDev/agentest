using agentest.Models;
using agentest.Services;
using agentest.Forms.Theming;
namespace agentest.Forms.Groups;
public sealed partial class GroupManagerForm : Form
{
    public AgentCatalog Catalog { get; }
    public GroupManagerForm() : this(new AgentCatalog(), ThemePalette.Light) { }
    public GroupManagerForm(AgentCatalog catalog, ThemePalette palette)
    {
        Catalog = AgentGroups.Copy(catalog);
        InitializeComponent();
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
