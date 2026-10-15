using agentest.Models;

namespace agentest.Controls.Chat;

public sealed partial class MessageComposerControl : UserControl
{
    public MessageComposerControl() => InitializeComponent();
    public event EventHandler? SendRequested;
    public event EventHandler? CancelRequested;
    public event EventHandler? AttachRequested;
    public event EventHandler? RemoveRequested;
    public event EventHandler? DraftChanged;
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal string Draft { get => input.Text; set { if (input.Text != value) input.Text = value; } }
    internal int SelectedAttachmentIndex => attachments.SelectedIndex;
    internal void FocusInput() => input.Focus();

    internal void BindAttachments(IReadOnlyList<ChatAttachment> items)
    {
        var selected = attachments.SelectedIndex;
        attachments.Items.Clear();
        attachments.Items.AddRange(items.Cast<object>().ToArray());
        if (selected >= 0 && selected < items.Count) attachments.SelectedIndex = selected;
        layout.RowStyles[0].Height = items.Count > 0 ? 76 : 0;
    }

    internal void SetState(bool available, bool canSend, bool canCancel, bool canAttach)
    {
        input.Enabled = available;
        send.Enabled = canSend;
        cancel.Enabled = canCancel;
        attach.Enabled = canAttach;
        remove.Enabled = available && SelectedAttachmentIndex >= 0;
    }
}
