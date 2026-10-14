using System.Diagnostics;
using agentest.Models;
using agentest.Persistence;
using agentest.Services;

namespace agentest.Forms;

public sealed class ChatForm : Form
{
    private readonly LocalStore store;
    private readonly OrchestratorClient client = new();
    private readonly Dictionary<string, string> credentials = [];
    private List<AgentProfile> profiles = [];
    private List<Conversation> conversations = [];
    private readonly List<ChatAttachment> pendingAttachments = [];
    private Conversation? current;
    private CancellationTokenSource? sending;
    private bool refreshing;
    private bool ready;
    private bool readingAttachments;
    private bool savingProfile;
    private TableLayoutPanel chatLayout = null!;
    private readonly ListBox conversationList = new() { Dock = DockStyle.Fill, IntegralHeight = false, BorderStyle = BorderStyle.None };
    private readonly ComboBox profileList = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 250 };
    private readonly RichTextBox transcript = new() { Dock = DockStyle.Fill, ReadOnly = true, BackColor = Color.White, BorderStyle = BorderStyle.None, DetectUrls = false };
    private readonly TextBox input = new() { Dock = DockStyle.Fill, Multiline = true, AcceptsReturn = true, ScrollBars = ScrollBars.Vertical };
    private readonly Label status = new() { Dock = DockStyle.Fill, Text = "Cargando…", AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Label capabilities = new() { Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ListBox attachmentList = new() { Dock = DockStyle.Fill, IntegralHeight = false, DisplayMember = nameof(ChatAttachment.Name) };
    private readonly Button send = MakeButton("Enviar (Ctrl+Enter)"), cancel = MakeButton("Cancelar"), attach = MakeButton("Adjuntar"), removeAttachment = MakeButton("Quitar adjunto");
    private readonly Button newChat = MakeButton("Nueva conversación"), newProfile = MakeButton("Nuevo perfil"), editProfile = MakeButton("Editar perfil"), deleteProfile = MakeButton("Eliminar perfil");
    private AgentProfile? SelectedProfile => profileList.SelectedItem as AgentProfile;
    private bool Busy => sending is not null || readingAttachments || savingProfile;

    public ChatForm(LocalStore? localStore = null)
    {
        store = localStore ?? new LocalStore();
        Text = "Agentest · Chat con agentes";
        Size = new Size(1180, 820);
        MinimumSize = new Size(960, 700);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(243, 246, 250);
        BuildLayout();
        Shown += async (_, _) => await InitializeAsync();
        send.Click += async (_, _) => await SendAsync();
        cancel.Click += (_, _) => { sending?.Cancel(); status.Text = "Cancelando solicitud…"; cancel.Enabled = false; };
        newChat.Click += (_, _) => StartConversation();
        newProfile.Click += async (_, _) => await EditProfileAsync(create: true);
        editProfile.Click += async (_, _) => await EditProfileAsync(create: false);
        deleteProfile.Click += async (_, _) => await DeleteProfileAsync();
        attach.Click += async (_, _) => await AddAttachmentsAsync();
        removeAttachment.Click += (_, _) =>
        {
            if (attachmentList.SelectedIndex >= 0) pendingAttachments.RemoveAt(attachmentList.SelectedIndex);
            RefreshAttachments();
        };
        attachmentList.SelectedIndexChanged += (_, _) => UpdateControls();
        input.TextChanged += (_, _) => UpdateControls();
        input.KeyDown += async (_, e) =>
        {
            if (e.Control && e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                if (send.Enabled) await SendAsync();
            }
        };
        profileList.SelectedIndexChanged += (_, _) =>
        {
            if (refreshing) return;
            if (current is not null && current.Messages.Count == 0)
            {
                if (pendingAttachments.Count > 0 && MessageBox.Show(this,
                        "Cambiar de agente quitará los adjuntos del borrador. ¿Continuar?", "Cambiar agente",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                {
                    RefreshProfiles(current.AgentProfileId);
                    return;
                }
                current.AgentProfileId = SelectedProfile?.Id ?? "";
                pendingAttachments.Clear();
                RefreshAttachments();
            }
            UpdateControls();
        };
        conversationList.SelectedIndexChanged += (_, _) =>
        {
            if (refreshing || Busy || conversationList.SelectedItem is not Conversation conversation) return;
            if (HasDraft() && MessageBox.Show(this, "Al cambiar de conversación se descartará el borrador actual. ¿Continuar?",
                    "Borrador pendiente", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                refreshing = true;
                conversationList.SelectedItem = current;
                refreshing = false;
                return;
            }
            SelectConversation(conversation);
        };
        FormClosing += (_, e) =>
        {
            if (Busy)
            {
                e.Cancel = true;
                sending?.Cancel();
                status.Text = "Espera a que termine la operación antes de cerrar.";
            }
            else if (HasDraft() && MessageBox.Show(this, "El borrador no se guarda. ¿Cerrar la aplicación?", "Borrador pendiente",
                         MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) e.Cancel = true;
        };
        UpdateControls();
    }

    private static Button MakeButton(string text) => new() { Text = text, AutoSize = true, Padding = new Padding(5), Margin = new Padding(3) };

    private void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 2, RowCount = 1 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var sidebar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(0, 0, 12, 0) };
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        sidebar.Controls.Add(new Label { Text = "Conversaciones", Font = new Font(Font, FontStyle.Bold), Dock = DockStyle.Fill }, 0, 0);
        newChat.Dock = DockStyle.Fill;
        sidebar.Controls.Add(newChat, 0, 1);
        sidebar.Controls.Add(conversationList, 0, 2);
        var openFolder = MakeButton("Abrir carpeta local");
        openFolder.Dock = DockStyle.Fill;
        openFolder.Click += (_, _) =>
        {
            try
            {
                Directory.CreateDirectory(store.RootPath);
                Process.Start(new ProcessStartInfo(store.RootPath) { UseShellExecute = true });
            }
            catch (Exception ex) { ShowError(ex.Message); }
        };
        sidebar.Controls.Add(openFolder, 0, 3);
        root.Controls.Add(sidebar, 0, 0);
        var chat = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 8 };
        chatLayout = chat;
        chat.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        chat.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        chat.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        chat.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        chat.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        chat.RowStyles.Add(new RowStyle(SizeType.Absolute, 108));
        chat.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        chat.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        var profileBar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        profileBar.Controls.Add(new Label { Text = "Agente", AutoSize = true, Margin = new Padding(0, 8, 12, 0) });
        profileList.Margin = new Padding(0, 4, 0, 0);
        profileBar.Controls.Add(profileList);
        chat.Controls.Add(profileBar, 0, 0);
        var profileButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        profileButtons.Controls.AddRange([newProfile, editProfile, deleteProfile]);
        chat.Controls.Add(profileButtons, 0, 1);
        chat.Controls.Add(capabilities, 0, 2);
        transcript.Font = new Font("Segoe UI", 11);
        chat.Controls.Add(transcript, 0, 3);
        chat.Controls.Add(attachmentList, 0, 4);
        input.PlaceholderText = "Escribe un mensaje. Ctrl+Enter para enviar.";
        var composer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        composer.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
        composer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        composer.Controls.Add(new Label { Text = "Mensaje · Enter agrega una línea", Dock = DockStyle.Fill }, 0, 0);
        composer.Controls.Add(input, 0, 1);
        chat.Controls.Add(composer, 0, 5);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        actions.Controls.AddRange([send, cancel, attach, removeAttachment]);
        chat.Controls.Add(actions, 0, 6);
        chat.Controls.Add(status, 0, 7);
        root.Controls.Add(chat, 1, 0);
        Controls.Add(root);
    }

    private async Task InitializeAsync()
    {
        try
        {
            profiles = await store.LoadProfilesAsync();
            await store.SaveProfilesAsync(profiles);
            var loaded = await store.LoadConversationsAsync();
            conversations = loaded.Items;
            // Interrupted requests must not appear delivered after restarting the application.
            foreach (var conversation in conversations)
            {
                var interrupted = conversation.Messages.Where(m => m.DeliveryStatus == "pending").ToList();
                foreach (var message in interrupted) message.DeliveryStatus = "interrupted";
                if (interrupted.Count > 0) await store.SaveConversationAsync(conversation);
            }
            RefreshProfiles(profiles.FirstOrDefault()?.Id);
            ready = true;
            StartConversation();
            status.Text = "Listo. Configura el perfil para conectar con tu orquestador.";
            if (loaded.Errors.Count > 0) ShowError("Algunos historiales no pudieron abrirse y se conservaron en disco:\n" + string.Join("\n", loaded.Errors));
        }
        catch (Exception ex)
        {
            status.Text = "No se pudo cargar el almacenamiento local.";
            ShowError(ex.Message);
        }
        UpdateControls();
    }

    private bool HasDraft() => !string.IsNullOrWhiteSpace(input.Text) || pendingAttachments.Count > 0;

    private void StartConversation()
    {
        if (Busy) return;
        if (HasDraft() && MessageBox.Show(this, "Se descartará el borrador actual. ¿Crear una conversación?", "Borrador pendiente",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        current = new Conversation { AgentProfileId = SelectedProfile?.Id ?? "" };
        input.Clear();
        pendingAttachments.Clear();
        RefreshAttachments();
        RefreshConversationList();
        RenderTranscript();
        UpdateControls();
        input.Focus();
    }

    private void SelectConversation(Conversation conversation)
    {
        current = conversation;
        input.Clear();
        pendingAttachments.Clear();
        RefreshProfiles(conversation.AgentProfileId);
        RefreshAttachments();
        RenderTranscript();
        status.Text = SelectedProfile is null ? "El perfil de esta conversación ya no existe." : "Conversación cargada.";
        UpdateControls();
    }

    private void RefreshProfiles(string? selectedId)
    {
        refreshing = true;
        profileList.Items.Clear();
        profileList.Items.AddRange(profiles.Cast<object>().ToArray());
        profileList.SelectedItem = profiles.FirstOrDefault(p => p.Id == selectedId);
        refreshing = false;
        UpdateControls();
    }

    private void RefreshConversationList()
    {
        refreshing = true;
        conversationList.Items.Clear();
        conversationList.Items.AddRange(conversations.OrderByDescending(c => c.UpdatedAt).Cast<object>().ToArray());
        conversationList.SelectedItem = current;
        refreshing = false;
    }

    private void RefreshAttachments()
    {
        attachmentList.Items.Clear();
        attachmentList.Items.AddRange(pendingAttachments.Cast<object>().ToArray());
        UpdateControls();
    }

    private void UpdateControls()
    {
        var profile = SelectedProfile;
        newChat.Enabled = ready && !Busy;
        newProfile.Enabled = ready && !Busy;
        editProfile.Enabled = ready && !Busy && profile is not null;
        deleteProfile.Enabled = editProfile.Enabled;
        profileList.Enabled = ready && !Busy && (current is null || current.Messages.Count == 0);
        conversationList.Enabled = ready && !Busy;
        input.Enabled = ready && !Busy;
        send.Enabled = ready && !Busy && profile is not null && HasDraft();
        cancel.Enabled = sending is not null && !sending.IsCancellationRequested;
        attach.Enabled = ready && !Busy && profile?.AcceptsAttachments == true;
        removeAttachment.Enabled = ready && !Busy && attachmentList.SelectedIndex >= 0;
        chatLayout.RowStyles[4].Height = pendingAttachments.Count > 0 ? 76 : 0;
        capabilities.Text = profile is null ? "Selecciona o crea un perfil de agente." : profile.AcceptsAttachments
            ? $"Adjuntos: {profile.AllowedExtensions} · hasta {profile.MaxAttachmentMegabytes} MB por archivo"
            : "Este agente admite mensajes de texto.";
    }

    private void RenderTranscript()
    {
        transcript.Clear();
        if (current is null || current.Messages.Count == 0)
        {
            transcript.Text = "Inicia una conversación\n\nSelecciona un perfil y configura el endpoint y el contrato JSON de tu orquestador.\n\nLos mensajes enviados y las respuestas se guardan en la carpeta local de Agentest.";
            return;
        }
        foreach (var message in current.Messages)
        {
            transcript.SelectionColor = message.Role == "user" ? Color.FromArgb(29, 78, 160) : message.Role == "assistant" ? Color.FromArgb(18, 108, 76) : Color.FromArgb(170, 65, 25);
            transcript.SelectionFont = new Font(transcript.Font, FontStyle.Bold);
            var role = message.Role == "user" ? "Tú" : message.Role == "assistant" ? "Agente" : "Sistema";
            var delivery = message.DeliveryStatus switch { "pending" => " · enviando", "failed" => " · fallido", "cancelled" => " · cancelado", "interrupted" => " · interrumpido", _ => "" };
            transcript.AppendText($"{role} · {message.CreatedAt.LocalDateTime:dd/MM HH:mm}{delivery}\n");
            transcript.SelectionColor = Color.FromArgb(35, 42, 52);
            transcript.SelectionFont = transcript.Font;
            transcript.AppendText(message.Text + "\n");
            foreach (var attachment in message.Attachments)
                transcript.AppendText($"  📎 {attachment.Name} ({attachment.Size / 1024.0:N1} KB)\n");
            transcript.AppendText("\n");
        }
        transcript.SelectionStart = transcript.TextLength;
        transcript.ScrollToCaret();
    }

    private async Task EditProfileAsync(bool create)
    {
        var profile = create ? new AgentProfile { Name = "Nuevo agente" } : SelectedProfile;
        if (profile is null) return;
        using var editor = new ProfileEditorForm(profile, credentials.GetValueOrDefault(profile.Id, ""));
        if (editor.ShowDialog(this) != DialogResult.OK) return;
        if (pendingAttachments.Count > 0 && MessageBox.Show(this,
                "Guardar este perfil quitará los adjuntos del borrador. ¿Continuar?", "Actualizar perfil",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        var updated = profiles.Where(p => p.Id != editor.Profile.Id).Append(editor.Profile).ToList();
        savingProfile = true;
        UpdateControls();
        try
        {
            await store.SaveProfilesAsync(updated);
            profiles = updated;
            credentials[editor.Profile.Id] = editor.SessionToken;
            var selectedId = current?.Messages.Count > 0 ? current.AgentProfileId : editor.Profile.Id;
            RefreshProfiles(selectedId);
            if (current is not null && current.Messages.Count == 0) current.AgentProfileId = selectedId;
            pendingAttachments.Clear();
            RefreshAttachments();
            status.Text = "Perfil guardado. La credencial se conserva solo durante esta sesión.";
        }
        catch (Exception ex) { ShowError("No se pudo guardar el perfil: " + ex.Message); }
        finally { savingProfile = false; UpdateControls(); }
    }

    private async Task DeleteProfileAsync()
    {
        var profile = SelectedProfile;
        if (profile is null) return;
        if (conversations.Any(c => c.AgentProfileId == profile.Id))
        {
            ShowError("Este perfil tiene conversaciones guardadas. Puedes editarlo; no se puede eliminar mientras esté en uso.");
            return;
        }
        if (MessageBox.Show(this, $"¿Eliminar el perfil '{profile.Name}'?", "Eliminar perfil", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        if (HasDraft() && MessageBox.Show(this, "Eliminar el perfil descartará el borrador actual. ¿Continuar?", "Borrador pendiente",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        savingProfile = true;
        UpdateControls();
        try
        {
            var updated = profiles.Where(p => p.Id != profile.Id).ToList();
            await store.SaveProfilesAsync(updated);
            profiles = updated;
            credentials.Remove(profile.Id);
            RefreshProfiles(profiles.FirstOrDefault()?.Id);
            input.Clear();
            pendingAttachments.Clear();
            current = new Conversation { AgentProfileId = SelectedProfile?.Id ?? "" };
            RefreshAttachments();
            RefreshConversationList();
            RenderTranscript();
            status.Text = "Perfil eliminado.";
        }
        catch (Exception ex) { ShowError(ex.Message); }
        finally { savingProfile = false; UpdateControls(); }
    }

    private async Task AddAttachmentsAsync()
    {
        var profile = SelectedProfile;
        if (profile is null || Busy) return;
        using var dialog = new OpenFileDialog { Multiselect = true, Title = "Adjuntar archivos permitidos por el agente", Filter = "Todos los archivos|*.*" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        readingAttachments = true;
        UpdateControls();
        try
        {
            List<ChatAttachment> additions = [];
            foreach (var file in dialog.FileNames)
            {
                if (pendingAttachments.Count + additions.Count >= 20)
                    throw new InvalidOperationException("Puedes enviar hasta 20 adjuntos por mensaje.");
                var item = await AttachmentService.ReadAsync(file, profile);
                if (pendingAttachments.Sum(a => a.Size) + additions.Sum(a => a.Size) + item.Size > 100L * 1024 * 1024)
                    throw new InvalidOperationException("Los adjuntos del mensaje no pueden superar 100 MB en total.");
                additions.Add(item);
            }
            pendingAttachments.AddRange(additions);
            status.Text = "Adjuntos preparados para el próximo mensaje.";
        }
        catch (Exception ex) { ShowError(ex.Message); }
        finally { readingAttachments = false; RefreshAttachments(); }
    }

    private async Task SendAsync()
    {
        var profile = SelectedProfile;
        var conversation = current;
        if (Busy || !ready || profile is null || conversation is null || !HasDraft()) return;
        try { OrchestratorClient.ValidateProfile(profile); }
        catch (Exception ex) { ShowError(ex.Message); return; }
        var message = new ChatMessage { Text = input.Text.Trim(), Attachments = pendingAttachments.ToList(), DeliveryStatus = "pending" };
        var previousTitle = conversation.Title;
        var previousUpdated = conversation.UpdatedAt;
        conversation.Messages.Add(message);
        conversation.UpdatedAt = DateTimeOffset.Now;
        if (conversation.Messages.Count == 1)
        {
            var title = string.IsNullOrWhiteSpace(message.Text) ? message.Attachments[0].Name : message.Text.Replace('\r', ' ').Replace('\n', ' ');
            conversation.Title = title.Length > 50 ? title[..50] + "…" : title;
        }
        sending = new CancellationTokenSource();
        UpdateControls();
        try
        {
            await store.SaveConversationAsync(conversation);
        }
        catch (Exception ex)
        {
            conversation.Messages.Remove(message);
            conversation.Title = previousTitle;
            conversation.UpdatedAt = previousUpdated;
            sending.Dispose();
            sending = null;
            UpdateControls();
            ShowError("No se envió el mensaje porque no pudo guardarse: " + ex.Message);
            return;
        }
        if (!conversations.Contains(conversation)) conversations.Add(conversation);
        input.Clear();
        pendingAttachments.Clear();
        RefreshAttachments();
        RefreshConversationList();
        RenderTranscript();
        status.Text = "Esperando respuesta del orquestador…";
        try
        {
            var reply = await client.SendAsync(profile, conversation, message, credentials.GetValueOrDefault(profile.Id, ""), sending.Token);
            message.DeliveryStatus = "completed";
            if (reply.ConversationId is not null) conversation.RemoteConversationId = reply.ConversationId;
            conversation.Messages.Add(new ChatMessage { Role = "assistant", Text = reply.Text, RawResponse = reply.RawResponse });
            foreach (var item in message.Attachments) item.Base64 = "";
            status.Text = "Respuesta recibida.";
        }
        catch (Exception ex)
        {
            message.DeliveryStatus = ex is OperationCanceledException ? "cancelled" : "failed";
            var error = ex is OperationCanceledException ? "Solicitud cancelada. El backend podría haberla procesado; verifica antes de reenviar." : ex.Message;
            conversation.Messages.Add(new ChatMessage { Role = "system", Text = error, DeliveryStatus = "failed" });
            input.Text = message.Text;
            pendingAttachments.AddRange(message.Attachments);
            status.Text = "El mensaje no se completó. Se recuperó el borrador.";
        }
        finally
        {
            conversation.UpdatedAt = DateTimeOffset.Now;
            try { await store.SaveConversationAsync(conversation); }
            catch (Exception ex)
            {
                status.Text = "No se pudo guardar el resultado en disco.";
                ShowError(status.Text + " " + ex.Message);
            }
            sending.Dispose();
            sending = null;
            RefreshAttachments();
            RefreshConversationList();
            RenderTranscript();
            UpdateControls();
            input.Focus();
        }
    }

    private void ShowError(string text) => MessageBox.Show(this, text, "Agentest", MessageBoxButtons.OK, MessageBoxIcon.Warning);

    protected override void Dispose(bool disposing)
    {
        if (disposing) { sending?.Dispose(); client.Dispose(); }
        base.Dispose(disposing);
    }
}
