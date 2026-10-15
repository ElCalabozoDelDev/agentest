using agentest.Forms.Theming;
using agentest.Models;

namespace agentest.Controls.Agents;

public sealed partial class AgentsOverviewControl : UserControl
{
    private readonly List<SystemDiagramControl> diagrams = [];
    private ThemePalette palette = ThemePalette.Light;
    private bool arranging;
    public event EventHandler? RefreshRequested;
    public event EventHandler? ManageGroupsRequested;

    public AgentsOverviewControl()
    {
        InitializeComponent();
        refresh.Enabled = false;
        refresh.Click += (_, _) => RefreshRequested?.Invoke(this, EventArgs.Empty);
        manage.Enabled = false;
        manage.Click += (_, _) => ManageGroupsRequested?.Invoke(this, EventArgs.Empty);
        canvas.SizeChanged += (_, _) =>
        {
            // Reflow after the parent layout finishes, so AutoScroll measures the new card bounds.
            if (IsHandleCreated) BeginInvoke((Action)(() => { if (!IsDisposed) ArrangeDiagrams(); }));
        };
    }

    public void SetRefreshing(bool busy, bool ready) => refresh.Enabled = ready && !busy;
    public void SetManagementEnabled(bool enabled) => manage.Enabled = enabled;

    public void SetPalette(ThemePalette value)
    {
        palette = value;
        ThemeAppearance.Apply(this, palette);
        foreach (var diagram in diagrams) diagram.SetPalette(palette);
    }

    public void Render(IReadOnlyList<AgentProfile> profiles, IReadOnlyDictionary<string, AgentHealth> results, IReadOnlyList<string>? systems = null)
    {
        var scroll = -canvas.AutoScrollPosition.Y;
        canvas.SuspendLayout();
        foreach (var diagram in diagrams) diagram.Dispose();
        diagrams.Clear();
        var groups = (systems ?? []).Concat(profiles.Select(p => p.AgentSystem)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(g => g).ToList();
        summary.Text = $"{groups.Count} sistemas · {profiles.Count} agentes · {profiles.Count(p => results.GetValueOrDefault(p.Id)?.State == "Saludable")} saludables";
        if (profiles.Count == 0) summary.Text += " · Agrega perfiles desde Chat.";
        foreach (var group in groups)
        {
            var diagram = new SystemDiagramControl(group, profiles.Where(p => string.Equals(p.AgentSystem, group, StringComparison.OrdinalIgnoreCase)).ToList(), results, palette);
            diagrams.Add(diagram);
            canvas.Controls.Add(diagram);
        }
        ArrangeDiagrams();
        canvas.ResumeLayout();
        canvas.AutoScrollPosition = new Point(0, scroll);
    }

    private void ArrangeDiagrams()
    {
        if (arranging) return;
        arranging = true;
        canvas.SuspendLayout();
        try
        {
            var width = Math.Max(250, canvas.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 4);
            foreach (var diagram in diagrams)
            {
                diagram.Margin = new Padding(0, 0, 0, 16);
                diagram.Size = new Size(width, diagram.Arrange(width));
            }
        }
        finally { canvas.ResumeLayout(); arranging = false; }
    }

}
