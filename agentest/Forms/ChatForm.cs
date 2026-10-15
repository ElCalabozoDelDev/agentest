using System.Diagnostics;
using agentest.Models;
using agentest.Persistence;
using agentest.Services;
using agentest.Forms.Theming;
using Microsoft.Win32;

namespace agentest.Forms;

public sealed class ChatForm : Form
{
    private readonly LocalStore store;
    private readonly OrchestratorClient client = new();
    private readonly Dictionary<string, string> credentials = [];
    private readonly Dictionary<string, string> healthCredentials = [];
    private readonly HealthClient healthClient;
    private readonly Dictionary<string, AgentHealth> healthResults = [];
    private readonly CancellationTokenSource monitoring = new();
    private readonly System.Windows.Forms.Timer healthTimer = new() { Interval = 1000 };
    private readonly AgentsOverview agentsOverview = new();
    private bool checkingHealth;
    private bool resourcesDisposed;
    private List<AgentProfile> profiles = [];
    private List<string> agentGroups = ["DEFAULT"];
    private readonly Button manageGroups = MakeButton("Administrar grupos");
    private List<Conversation> conversations = [];
    private readonly List<ChatAttachment> pendingAttachments = [];
    private Conversation? current;
    private CancellationTokenSource? sending;
    private bool refreshing;
    private bool ready;
    private bool readingAttachments;
    private bool savingProfile;
    private bool savingTheme;
    private AppSettings settings = new();
    private ThemePalette palette = ThemePalette.Light;
    private readonly Button themeButton = MakeButton("Tema: Sistema ▾");
    private readonly ContextMenuStrip themeMenu = new();
    private readonly ContextMenuStrip agentsMenu = new();
    private readonly Button manageAgents = MakeButton("Administrar agentes");
    private readonly ToolStripMenuItem newProfile = new("Nuevo agente"), editProfile = new("Editar agente"), deleteProfile = new("Eliminar agente");
    private readonly ComboBox systemList = new() { Name = "ChatSystem", AccessibleName = "Sistema", DropDownStyle = ComboBoxStyle.DropDownList, Anchor = AnchorStyles.Left | AnchorStyles.Right };
    private string activeSystem = "DEFAULT";
    private TableLayoutPanel chatLayout = null!;
    private readonly ListBox conversationList = new() { Dock = DockStyle.Fill, IntegralHeight = false, BorderStyle = BorderStyle.None };
    private readonly ComboBox profileList = new() { Name = "ChatAgent", AccessibleName = "Agente", DropDownStyle = ComboBoxStyle.DropDownList, Anchor = AnchorStyles.Left | AnchorStyles.Right };
    private readonly RichTextBox transcript = new() { Dock = DockStyle.Fill, ReadOnly = true, BackColor = Color.White, BorderStyle = BorderStyle.None, DetectUrls = false };
    private readonly TextBox input = new() { Dock = DockStyle.Fill, Multiline = true, AcceptsReturn = true, ScrollBars = ScrollBars.Vertical };
    private readonly Label status = new() { Dock = DockStyle.Fill, Text = "Cargando…", AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Label capabilities = new() { Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ListBox attachmentList = new() { Dock = DockStyle.Fill, IntegralHeight = false, DisplayMember = nameof(ChatAttachment.Name) };
    private readonly Button send = MakeButton("Enviar (Ctrl+Enter)"), cancel = MakeButton("Cancelar"), attach = MakeButton("Adjuntar"), removeAttachment = MakeButton("Quitar adjunto");
    private readonly Button newChat = MakeButton("Nueva conversación");
    private AgentProfile? SelectedProfile => profileList.SelectedItem as AgentProfile;
    private bool Busy => sending is not null || readingAttachments || savingProfile || savingTheme;

    public ChatForm(LocalStore? localStore = null, HealthClient? healthMonitor = null)
    {
        store = localStore ?? new LocalStore();
        healthClient = healthMonitor ?? new HealthClient();
        Text = "Agentest · Chat con agentes";
        Size = new Size(1180, 820);
        MinimumSize = new Size(960, 700);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(243, 246, 250);
        BuildLayout();
        healthTimer.Tick += async (_, _) => await RefreshHealthAsync();
        agentsOverview.RefreshRequested += async (_, _) => await RefreshHealthAsync(force: true);
        agentsOverview.ManageGroupsRequested += async (_, _) => await ManageGroupsAsync();
        manageGroups.Click += async (_, _) => await ManageGroupsAsync();
        BuildThemeMenu();
        agentsMenu.Items.AddRange([newProfile, editProfile, deleteProfile]);
        manageAgents.ContextMenuStrip = agentsMenu;
        manageAgents.Click += (_, _) => agentsMenu.Show(manageAgents, new Point(0, manageAgents.Height));
        ApplyTheme();
        SystemEvents.UserPreferenceChanged += SystemPreferenceChanged;
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
        systemList.SelectedIndexChanged += (_, _) =>
        {
            if (refreshing) return;
            var system = systemList.SelectedItem as string;
            if (system is null) return;
            if (!ready || Busy || current?.Messages.Count > 0)
            {
                RefreshProfiles(current?.AgentProfileId, activeSystem);
                return;
            }
            if (pendingAttachments.Count > 0 && MessageBox.Show(this,
                    "Cambiar de sistema quitará los adjuntos del borrador. ¿Continuar?", "Cambiar sistema",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                RefreshProfiles(current?.AgentProfileId, activeSystem);
                return;
            }
            RefreshProfiles(profiles.FirstOrDefault(p => string.Equals(p.AgentSystem, system, StringComparison.OrdinalIgnoreCase))?.Id, system);
            if (current is not null) current.AgentProfileId = SelectedProfile?.Id ?? "";
            pendingAttachments.Clear();
            RefreshAttachments();
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

    private static Button MakeButton(string text) => new ThemeButton { Text = text, AutoSize = true, Padding = new Padding(5), Margin = new Padding(3) };

    private void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 2, RowCount = 1 };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
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
        chat.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        chatLayout = chat;
        chat.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        chat.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        chat.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        chat.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        chat.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        chat.RowStyles.Add(new RowStyle(SizeType.Absolute, 108));
        chat.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        chat.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        var profileBar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1 };
        profileBar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        profileBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        profileBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        profileBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        profileBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        profileBar.Controls.Add(new Label { Text = "Sistema", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, 8, 0) }, 0, 0);
        profileBar.Controls.Add(systemList, 1, 0);
        profileBar.Controls.Add(new Label { Text = "Agente", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(12, 0, 8, 0) }, 2, 0);
        profileBar.Controls.Add(profileList, 3, 0);
        chat.Controls.Add(profileBar, 0, 0);
        var profileButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            Margin = new Padding(0, 0, 0, 6)
        };
        profileButtons.Controls.AddRange([manageAgents, manageGroups, themeButton]);
        chat.Controls.Add(profileButtons, 0, 1);
        chat.Controls.Add(capabilities, 0, 2);
        transcript.Font = new Font("Segoe UI", 11);
        chat.Controls.Add(transcript, 0, 3);
        chat.Controls.Add(attachmentList, 0, 4);
        input.PlaceholderText = "Escribe un mensaje. Ctrl+Enter para enviar.";
        var composer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        composer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
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
        var tabs = new TabControl { Dock = DockStyle.Fill };
        var chatTab = new TabPage("Chat") { UseVisualStyleBackColor = false };
        var agentsTab = new TabPage("Agentes") { UseVisualStyleBackColor = false };
        chatTab.Controls.Add(root);
        agentsTab.Controls.Add(agentsOverview);
        tabs.TabPages.AddRange([chatTab, agentsTab]);
        Controls.Add(tabs);
    }

    private async Task InitializeAsync()
    {
        try
        {
            try { settings = await store.LoadSettingsAsync(); }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or IOException or UnauthorizedAccessException)
            {
                ShowError("No se pudo cargar la apariencia guardada. Se usará el tema del sistema.\n" + ex.Message);
            }
            ApplyTheme();
            var catalog = await store.LoadCatalogAsync();
            profiles = catalog.Profiles;
            agentGroups = catalog.Groups;
            await store.SaveCatalogAsync(catalog);
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
            agentsOverview.Render(profiles, healthResults, agentGroups);
            healthTimer.Start();
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

    private void BuildThemeMenu()
    {
        themeButton.ContextMenuStrip = themeMenu;
        foreach (var (mode, label) in new[]
                 { (ThemeMode.System, "Usar tema del sistema"), (ThemeMode.Light, "Interfaz clara"), (ThemeMode.Dark, "Interfaz oscura") })
        {
            var item = new ToolStripMenuItem(label) { Tag = mode };
            item.Click += async (_, _) => await ChangeThemeAsync(mode);
            themeMenu.Items.Add(item);
        }
        themeButton.Click += (_, _) => themeMenu.Show(themeButton, new Point(0, themeButton.Height));
    }

    private async Task ChangeThemeAsync(ThemeMode mode)
    {
        if (!ready || savingTheme) return;
        savingTheme = true;
        UpdateControls();
        try
        {
            var updated = new AppSettings { Theme = mode };
            await store.SaveSettingsAsync(updated);
            settings = updated;
            ApplyTheme();
        }
        catch (Exception ex) { ShowError("No se pudo guardar el tema: " + ex.Message); }
        finally { savingTheme = false; UpdateControls(); }
    }

    private void ApplyTheme()
    {
        palette = ThemeAppearance.Resolve(settings.Theme);
        // Resolve System on every change so Windows preferences can be followed without restarting.
        Application.SetColorMode(palette.IsDark ? SystemColorMode.Dark : SystemColorMode.Classic);
        foreach (Form form in Application.OpenForms) ThemeAppearance.Apply(form, palette);
        ThemeAppearance.Apply(this, palette);
        ThemeAppearance.Apply(themeMenu, palette);
        ThemeAppearance.Apply(agentsMenu, palette);
        agentsOverview.SetPalette(palette);
        themeButton.Text = settings.Theme switch
        {
            ThemeMode.Light => "Tema: Claro ▾",
            ThemeMode.Dark => "Tema: Oscuro ▾",
            _ => $"Tema: Sistema ({(palette.IsDark ? "oscuro" : "claro")}) ▾"
        };
        foreach (ToolStripMenuItem item in themeMenu.Items) item.Checked = Equals(item.Tag, settings.Theme);
        RenderTranscript();
    }

    private void SystemPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (IsDisposed || Disposing || !IsHandleCreated) return;
        try
        {
            BeginInvoke((Action)(() =>
            {
                if (!IsDisposed && settings.Theme == ThemeMode.System &&
                    ThemeAppearance.Resolve(settings.Theme).IsDark != palette.IsDark) ApplyTheme();
            }));
        }
        catch (InvalidOperationException) { /* The window may close before the notification is dispatched. */ }
    }

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

    private void RefreshProfiles(string? selectedId, string? system = null)
    {
        var selected = profiles.FirstOrDefault(p => p.Id == selectedId);
        var desired = selected?.AgentSystem ?? system ?? activeSystem;
        activeSystem = agentGroups.FirstOrDefault(g => string.Equals(g, desired, StringComparison.OrdinalIgnoreCase)) ?? "DEFAULT";
        refreshing = true;
        systemList.Items.Clear();
        systemList.Items.AddRange(agentGroups.OrderBy(g => g).Cast<object>().ToArray());
        systemList.SelectedItem = activeSystem;
        profileList.Items.Clear();
        profileList.Items.AddRange(profiles.Where(p => string.Equals(p.AgentSystem, activeSystem, StringComparison.OrdinalIgnoreCase)).Cast<object>().ToArray());
        profileList.SelectedItem = selected;
        refreshing = false;
        foreach (var id in healthResults.Keys.Where(id => profiles.All(p => p.Id != id)).ToList()) healthResults.Remove(id);
        agentsOverview.Render(profiles, healthResults, agentGroups);
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
        agentsOverview.SetRefreshing(checkingHealth, ready);
        manageGroups.Enabled = ready && !Busy;
        agentsOverview.SetManagementEnabled(ready && !Busy);
        var profile = SelectedProfile;
        themeButton.Enabled = ready && !savingTheme;
        newChat.Enabled = ready && !Busy;
        manageAgents.Enabled = ready && !Busy;
        newProfile.Enabled = ready && !Busy;
        editProfile.Enabled = ready && !Busy && profile is not null;
        deleteProfile.Enabled = editProfile.Enabled;
        profileList.Enabled = ready && !Busy && (current is null || current.Messages.Count == 0);
        systemList.Enabled = profileList.Enabled;
        profileList.Enabled &= profileList.Items.Count > 0;
        conversationList.Enabled = ready && !Busy;
        input.Enabled = ready && !Busy;
        send.Enabled = ready && !Busy && profile is not null && HasDraft();
        cancel.Enabled = sending is not null && !sending.IsCancellationRequested;
        attach.Enabled = ready && !Busy && profile?.AcceptsAttachments == true;
        removeAttachment.Enabled = ready && !Busy && attachmentList.SelectedIndex >= 0;
        chatLayout.RowStyles[4].Height = pendingAttachments.Count > 0 ? 76 : 0;
        capabilities.Text = profile is null ? "Selecciona un agente o usa Administrar agentes para crear uno en este sistema." : profile.AcceptsAttachments
            ? $"Adjuntos: {profile.AllowedExtensions} · hasta {profile.MaxAttachmentMegabytes} MB por archivo"
            : "Este agente admite mensajes de texto.";
    }

    private void RenderTranscript()
    {
        transcript.Clear();
        if (current is null || current.Messages.Count == 0)
        {
            transcript.Text = "Inicia una conversación\n\nSelecciona un sistema y un agente. Usa Administrar agentes para configurar la conexión de tu orquestador.\n\nLos mensajes enviados y las respuestas se guardan en la carpeta local de Agentest.";
            return;
        }
        foreach (var message in current.Messages)
        {
            transcript.SelectionColor = message.Role == "user" ? palette.User : message.Role == "assistant" ? palette.Assistant : palette.Notice;
            transcript.SelectionFont = new Font(transcript.Font, FontStyle.Bold);
            var role = message.Role == "user" ? "Tú" : message.Role == "assistant" ? "Agente" : "Sistema";
            var delivery = message.DeliveryStatus switch { "pending" => " · enviando", "failed" => " · fallido", "cancelled" => " · cancelado", "interrupted" => " · interrumpido", _ => "" };
            transcript.AppendText($"{role} · {message.CreatedAt.LocalDateTime:dd/MM HH:mm}{delivery}\n");
            transcript.SelectionColor = palette.Text;
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
        var profile = create ? new AgentProfile { Name = "Nuevo agente", AgentSystem = activeSystem } : SelectedProfile;
        if (profile is null) return;
        using var editor = new ProfileEditorForm(profile, credentials.GetValueOrDefault(profile.Id, ""), palette, healthCredentials.GetValueOrDefault(profile.Id, ""), agentGroups);
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
            healthCredentials[editor.Profile.Id] = editor.HealthSessionToken;
            healthResults.Remove(editor.Profile.Id);
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
            healthCredentials.Remove(profile.Id);
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

    private async Task ManageGroupsAsync()
    {
        if (!ready || Busy) return;
        using var editor = new GroupManagerForm(new AgentCatalog { Groups = agentGroups, Profiles = profiles }, palette);
        if (editor.ShowDialog(this) != DialogResult.OK) return;
        savingProfile = true;
        UpdateControls();
        try
        {
            await store.SaveCatalogAsync(editor.Catalog);
            var selectedId = SelectedProfile?.Id;
            agentGroups = editor.Catalog.Groups;
            profiles = editor.Catalog.Profiles;
            RefreshProfiles(selectedId);
            status.Text = "Grupos guardados. Los agentes conservan sus conversaciones y configuración.";
        }
        catch (Exception ex) { ShowError("No se pudieron guardar los grupos: " + ex.Message); }
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

    private async Task RefreshHealthAsync(bool force = false)
    {
        if (!ready || checkingHealth || monitoring.IsCancellationRequested) return;
        var due = profiles.Where(p => !string.IsNullOrWhiteSpace(p.Health.Endpoint) &&
            (force || !healthResults.TryGetValue(p.Id, out var result) ||
             (DateTimeOffset.Now - result.CheckedAt).TotalSeconds >= p.Health.IntervalSeconds)).ToList();
        if (due.Count == 0) return;
        checkingHealth = true;
        agentsOverview.SetRefreshing(true, ready);
        try
        {
            await Task.WhenAll(due.Select(async profile =>
            {
                var result = await healthClient.CheckAsync(profile.Health, healthCredentials.GetValueOrDefault(profile.Id, ""), monitoring.Token);
                // Ignore results for profiles replaced or removed during an in-flight check.
                if (!monitoring.IsCancellationRequested && profiles.Any(p => ReferenceEquals(p, profile)))
                    healthResults[profile.Id] = result;
            }));
            if (!monitoring.IsCancellationRequested) agentsOverview.Render(profiles, healthResults, agentGroups);
        }
        catch (OperationCanceledException) when (monitoring.IsCancellationRequested) { }
        finally
        {
            checkingHealth = false;
            if (!IsDisposed && !Disposing) agentsOverview.SetRefreshing(false, ready);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !resourcesDisposed)
        {
            resourcesDisposed = true;
            SystemEvents.UserPreferenceChanged -= SystemPreferenceChanged;
            healthTimer.Stop();
            healthTimer.Dispose();
            monitoring.Cancel();
            healthClient.Dispose();
            monitoring.Dispose();
            themeMenu.Dispose();
            agentsMenu.Dispose();
            sending?.Dispose();
            client.Dispose();
        }
        base.Dispose(disposing);
    }
}
