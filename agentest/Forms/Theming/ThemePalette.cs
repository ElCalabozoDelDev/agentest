namespace agentest.Forms.Theming;

public sealed record ThemePalette(bool IsDark, Color Window, Color Surface, Color Text,
    Color Muted, Color Border, Color Button, Color Hover, Color User, Color Assistant, Color Notice)
{
    public static ThemePalette Light { get; } = new(false,
        Color.FromArgb(243, 246, 250), Color.White, Color.FromArgb(35, 42, 52),
        Color.FromArgb(91, 101, 116), Color.FromArgb(190, 199, 211),
        Color.FromArgb(235, 240, 247), Color.FromArgb(219, 230, 245),
        Color.FromArgb(29, 78, 160), Color.FromArgb(18, 108, 76), Color.FromArgb(170, 65, 25));

    public static ThemePalette Dark { get; } = new(true,
        Color.FromArgb(24, 28, 35), Color.FromArgb(32, 38, 47), Color.FromArgb(232, 237, 244),
        Color.FromArgb(165, 178, 196), Color.FromArgb(70, 82, 100),
        Color.FromArgb(43, 52, 66), Color.FromArgb(58, 73, 93),
        Color.FromArgb(139, 185, 255), Color.FromArgb(115, 217, 172), Color.FromArgb(255, 186, 128));
}
