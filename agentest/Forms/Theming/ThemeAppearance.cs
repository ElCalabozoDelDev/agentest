using agentest.Models;

namespace agentest.Forms.Theming;

public static class ThemeAppearance
{
    public static ThemePalette Resolve(ThemeMode mode) => Resolve(mode, Application.SystemColorMode == SystemColorMode.Dark);

    public static ThemePalette Resolve(ThemeMode mode, bool systemIsDark) => mode switch
    {
        ThemeMode.System => systemIsDark ? ThemePalette.Dark : ThemePalette.Light,
        ThemeMode.Light => ThemePalette.Light,
        ThemeMode.Dark => ThemePalette.Dark,
        _ => throw new ArgumentOutOfRangeException(nameof(mode))
    };

    public static void Apply(Control control, ThemePalette palette)
    {
        control.BackColor = control is TextBoxBase or ListBox or ComboBox or NumericUpDown
            ? palette.Surface : palette.Window;
        control.ForeColor = palette.Text;
        if (control is Button button)
        {
            button.UseVisualStyleBackColor = false;
            button.FlatStyle = FlatStyle.Flat;
            button.BackColor = palette.Button;
            button.FlatAppearance.BorderColor = palette.Border;
            button.FlatAppearance.MouseOverBackColor = palette.Hover;
            button.FlatAppearance.MouseDownBackColor = palette.Surface;
            if (button is ThemeButton themeButton) themeButton.DisabledTextColor = palette.Muted;
        }
        if (control is ComboBox combo) combo.FlatStyle = FlatStyle.Flat;
        if (control is CheckBox checkBox) checkBox.FlatStyle = FlatStyle.Flat;
        foreach (Control child in control.Controls) Apply(child, palette);
        control.Invalidate();
    }

    public static void Apply(ContextMenuStrip menu, ThemePalette palette)
    {
        menu.BackColor = palette.Surface;
        menu.ForeColor = palette.Text;
        menu.Renderer = new ThemeMenuRenderer(palette);
        foreach (ToolStripItem item in menu.Items)
        {
            item.BackColor = palette.Surface;
            item.ForeColor = palette.Text;
        }
    }

    private sealed class ThemeMenuRenderer(ThemePalette palette) : ToolStripProfessionalRenderer(new ThemeMenuColors(palette))
    {
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? palette.Text : palette.Muted;
            base.OnRenderItemText(e);
        }
    }

    private sealed class ThemeMenuColors(ThemePalette palette) : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => palette.Surface;
        public override Color ImageMarginGradientBegin => palette.Surface;
        public override Color ImageMarginGradientMiddle => palette.Surface;
        public override Color ImageMarginGradientEnd => palette.Surface;
        public override Color MenuItemSelected => palette.Hover;
        public override Color MenuItemBorder => palette.Border;
        public override Color MenuBorder => palette.Border;
        public override Color CheckBackground => palette.Hover;
        public override Color CheckSelectedBackground => palette.Hover;
        public override Color CheckPressedBackground => palette.Hover;
    }
}
