using agentest.Forms.Theming;
using agentest.Models;

namespace agentest.Controls.Chat;

public sealed partial class MessageComposerControl
{
    private TableLayoutPanel layout = null!;
    private TextBox input = null!;
    private ListBox attachments = null!;
    private Button send = null!, cancel = null!, attach = null!, remove = null!;

    private void InitializeComponent()
    {
        Dock = DockStyle.Fill;
        layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        attachments = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, DisplayMember = nameof(ChatAttachment.Name) };
        input = new TextBox { Dock = DockStyle.Fill, Multiline = true, AcceptsReturn = true, ScrollBars = ScrollBars.Vertical, PlaceholderText = "Escribe un mensaje. Ctrl+Enter para enviar." };
        var composer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        composer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        composer.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
        composer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        composer.Controls.Add(new Label { Text = "Mensaje · Enter agrega una línea", Dock = DockStyle.Fill }, 0, 0);
        composer.Controls.Add(input, 0, 1);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        send = MakeAction("Enviar (Ctrl+Enter)"); cancel = MakeAction("Cancelar"); attach = MakeAction("Adjuntar"); remove = MakeAction("Quitar adjunto");
        actions.Controls.AddRange([send, cancel, attach, remove]);
        layout.Controls.Add(attachments, 0, 0); layout.Controls.Add(composer, 0, 1); layout.Controls.Add(actions, 0, 2);
        Controls.Add(layout);
        input.TextChanged += (_, _) => DraftChanged?.Invoke(this, EventArgs.Empty);
        attachments.SelectedIndexChanged += (_, _) => DraftChanged?.Invoke(this, EventArgs.Empty);
        input.KeyDown += (_, e) => { if (e.Control && e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; if (send.Enabled) SendRequested?.Invoke(this, EventArgs.Empty); } };
        send.Click += (_, _) => SendRequested?.Invoke(this, EventArgs.Empty);
        cancel.Click += (_, _) => CancelRequested?.Invoke(this, EventArgs.Empty);
        attach.Click += (_, _) => AttachRequested?.Invoke(this, EventArgs.Empty);
        remove.Click += (_, _) => RemoveRequested?.Invoke(this, EventArgs.Empty);
    }
    private static Button MakeAction(string text) => new ThemeButton { Text = text, AutoSize = true, Padding = new Padding(5), Margin = new Padding(3) };
}
