using agentest.Forms.Theming;
using agentest.Models;

namespace agentest.Forms;

public sealed class AgentsOverview : UserControl
{
    private readonly Label summary = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
    private readonly Button refresh = new ThemeButton { Text = "Actualizar ahora", AutoSize = true, Dock = DockStyle.Right };
    private readonly Button manage = new ThemeButton { Text = "Administrar grupos", AutoSize = true, Dock = DockStyle.Right };
    private readonly FlowLayoutPanel canvas = new() { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
    private readonly List<SystemDiagram> diagrams = [];
    private ThemePalette palette = ThemePalette.Light;
    private bool arranging;
    public event EventHandler? RefreshRequested;
    public event EventHandler? ManageGroupsRequested;

    public AgentsOverview()
    {
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
            var diagram = new SystemDiagram(group, profiles.Where(p => string.Equals(p.AgentSystem, group, StringComparison.OrdinalIgnoreCase)).ToList(), results, palette);
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

    private sealed class SystemDiagram : Panel
    {
        private readonly Label title;
        private readonly List<Panel> cards = [];
        private readonly List<Label> states = [];
        private readonly Label? empty;
        private ThemePalette palette;
        private readonly List<(Point Start, Point End)> branches = [];

        public SystemDiagram(string system, List<AgentProfile> profiles, IReadOnlyDictionary<string, AgentHealth> results, ThemePalette palette)
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
}
