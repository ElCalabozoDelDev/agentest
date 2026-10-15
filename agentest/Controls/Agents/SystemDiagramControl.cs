using agentest.Forms.Theming;
using agentest.Models;
namespace agentest.Controls.Agents;
public sealed class SystemDiagramControl : Panel
{
    private readonly Label title;
    private readonly List<Panel> cards = [];
    private readonly List<Label> states = [];
    private readonly Label? empty;
    private ThemePalette palette;
    private readonly List<(Point Start, Point End)> branches = [];

    public SystemDiagramControl() : this("Sistema", [], new Dictionary<string, AgentHealth>(), ThemePalette.Light) { }

    public SystemDiagramControl(string system, List<AgentProfile> profiles, IReadOnlyDictionary<string, AgentHealth> results, ThemePalette palette)
    {
        this.palette = palette;
        DoubleBuffered = true;
        var healthy = profiles.Count(p => results.GetValueOrDefault(p.Id)?.State == "Saludable");
        title = new Label { Text = $"{system} · {profiles.Count} agentes · {healthy} saludables", AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
        Controls.Add(title);
        if (profiles.Count == 0)
        {
            empty = new Label { Text = "Sin agentes. Asigna perfiles a este grupo desde Chat.", AutoEllipsis = true };
            Controls.Add(empty);
        }
        foreach (var profile in profiles)
        {
            var result = results.GetValueOrDefault(profile.Id);
            var state = result?.State ?? (string.IsNullOrWhiteSpace(profile.Health.Endpoint) ? "Sin configurar" : "Sin consultar");
            var card = new Panel { AccessibleName = profile.Name };
            var name = new Label { Text = profile.Name, Dock = DockStyle.Top, Height = 28, AutoEllipsis = true };
            var status = new Label { Text = state, Dock = DockStyle.Top, Height = 26, AutoEllipsis = true, Tag = state };
            var detail = new Label { Dock = DockStyle.Fill, AutoEllipsis = true, Text = result is null
                ? (state == "Sin configurar" ? "Configura el endpoint de health." : "Esperando la primera consulta…")
                : $"{result.LatencyMilliseconds} ms · {result.CheckedAt.LocalDateTime:dd/MM HH:mm:ss}\n{result.Detail}" };
            card.Controls.Add(detail);
            card.Controls.Add(status);
            card.Controls.Add(name);
            cards.Add(card);
            states.Add(status);
            Controls.Add(card);
        }
        SetPalette(palette);
    }

    public int Arrange(int width)
    {
        var scale = DeviceDpi / 96f;
        int S(int value) => (int)(value * scale);
        var columns = Math.Max(1, (width - S(56)) / S(300));
        var cardWidth = (width - S(56)) / columns - S(12);
        title.SetBounds(S(16), S(6), width - S(32), S(36));
        empty?.SetBounds(S(24), S(48), width - S(48), S(36));
        branches.Clear();
        for (var i = 0; i < cards.Count; i++)
        {
            var x = S(48) + (i % columns) * (cardWidth + S(12));
            var y = S(64) + (i / columns) * S(156);
            cards[i].SetBounds(x, y, cardWidth, S(128));
            cards[i].Padding = new Padding(S(10));
            foreach (Control child in cards[i].Controls)
                if (child.Dock == DockStyle.Top) child.Height = S(ReferenceEquals(child, states[i]) ? 26 : 28);
            branches.Add((new Point(x + S(20), y - S(16)), new Point(x + S(20), y)));
            branches.Add((new Point(S(24), y - S(16)), new Point(x + S(20), y - S(16))));
        }
        Invalidate();
        return cards.Count == 0 ? S(100) : S(64) + ((cards.Count + columns - 1) / columns) * S(156);
    }

    public void SetPalette(ThemePalette value)
    {
        palette = value;
        ThemeAppearance.Apply(this, palette);
        foreach (var card in cards)
        {
            card.BackColor = palette.Surface;
            foreach (Control child in card.Controls) child.BackColor = palette.Surface;
        }
        foreach (var state in states)
            state.ForeColor = Equals(state.Tag, "Saludable") ? palette.Assistant : palette.Notice;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(palette.Border, Math.Max(1, DeviceDpi / 96f));
        var x = (int)(24 * DeviceDpi / 96f);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        if (branches.Count > 0) e.Graphics.DrawLine(pen, x, title.Bottom, x, branches[^1].Start.Y);
        foreach (var branch in branches) e.Graphics.DrawLine(pen, branch.Start, branch.End);
    }
}
