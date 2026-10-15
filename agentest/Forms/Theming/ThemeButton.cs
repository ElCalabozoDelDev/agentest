namespace agentest.Forms.Theming;

public sealed class ThemeButton : Button
{
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Color DisabledTextColor { get; set; } = SystemColors.GrayText;

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Enabled)
        {
            base.OnPaint(e);
            return;
        }
        // Native disabled text can have insufficient contrast on a custom dark background.
        e.Graphics.Clear(BackColor);
        using var border = new Pen(FlatAppearance.BorderColor);
        e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        var bounds = Rectangle.FromLTRB(Padding.Left, Padding.Top, Width - Padding.Right, Height - Padding.Bottom);
        TextRenderer.DrawText(e.Graphics, Text, Font, bounds, DisabledTextColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
    }
}
