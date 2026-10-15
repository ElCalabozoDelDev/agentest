using agentest.Models;
using agentest.Forms.Theming;

namespace agentest.Controls.Conversations;

public sealed class ConversationListBox : ListBox
{
    private ThemePalette palette = ThemePalette.Light;

    public ConversationListBox()
    {
        Name = "AgentConversations";
        AccessibleName = "Conversaciones del agente seleccionado";
        Dock = DockStyle.Fill;
        IntegralHeight = false;
        BorderStyle = BorderStyle.None;
        DrawMode = DrawMode.OwnerDrawFixed;
        UpdateItemHeight();
    }

    public void SetPalette(ThemePalette value)
    {
        palette = value;
        BackColor = value.Surface;
        ForeColor = value.Text;
        Invalidate();
    }

    protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); UpdateItemHeight(); }
    protected override void OnDpiChangedAfterParent(EventArgs e) { base.OnDpiChangedAfterParent(e); UpdateItemHeight(); }
    private void UpdateItemHeight() => ItemHeight = Font.Height * 2 + (int)(24 * DeviceDpi / 96f);

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0 || Items[e.Index] is not Conversation conversation) return;
        var selected = e.Index == SelectedIndex;
        using var surface = new SolidBrush(palette.Surface);
        e.Graphics.FillRectangle(surface, e.Bounds);
        var bounds = Rectangle.Inflate(e.Bounds, -2, -3);
        var background = selected ? (palette.IsDark ? Color.FromArgb(41, 65, 99) : Color.FromArgb(218, 233, 255)) : palette.Surface;
        using var fill = new SolidBrush(background);
        e.Graphics.FillRectangle(fill, bounds);
        if (selected)
        {
            using var border = new Pen(palette.User);
            e.Graphics.DrawRectangle(border, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
            using var accent = new SolidBrush(palette.User);
            e.Graphics.FillRectangle(accent, bounds.X, bounds.Y, Math.Max(4, DeviceDpi / 24), bounds.Height);
        }
        var inset = (int)(12 * DeviceDpi / 96f);
        var title = new Rectangle(bounds.X + inset, bounds.Y + 5, Math.Max(1, bounds.Width - inset * 2), Font.Height + 4);
        using var bold = new Font(Font, FontStyle.Bold);
        var flags = TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
        TextRenderer.DrawText(e.Graphics, conversation.Title, selected ? bold : Font, title, palette.Text, flags);
        var detail = new Rectangle(title.X, title.Bottom + 2, title.Width, Font.Height + 2);
        TextRenderer.DrawText(e.Graphics, (selected ? "Activa · " : "") + conversation.UpdatedAt.LocalDateTime.ToString("dd/MM HH:mm"), Font, detail, selected ? palette.Text : palette.Muted, flags);
        if (selected && Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(bounds, -5, -3), palette.Text, background);
    }
}
