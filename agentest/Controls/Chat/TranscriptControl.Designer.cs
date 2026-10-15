namespace agentest.Controls.Chat;
public sealed partial class TranscriptControl
{
    private RichTextBox transcript = null!;
    private void InitializeComponent()
    {
        Dock = DockStyle.Fill;
        transcript = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, BorderStyle = BorderStyle.None, DetectUrls = false, Font = new Font("Segoe UI", 11) };
        Controls.Add(transcript);
    }
}
