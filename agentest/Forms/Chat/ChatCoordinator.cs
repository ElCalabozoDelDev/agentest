using System.Diagnostics;
using Microsoft.Win32;
using agentest.Controls.Chat;
using agentest.Controls.Agents;
using agentest.Forms.Agents;
using agentest.Forms.Groups;
using agentest.Forms.Conversations;
using agentest.Forms.Theming;
using agentest.Models;
using agentest.Persistence;
using agentest.Services;

namespace agentest.Forms.Chat;

// Coordina las vistas; los controles no conocen los clientes ni el almacenamiento.
internal sealed class ChatCoordinator : IDisposable
{
    private readonly Form owner;
    private readonly ChatView view;
    private readonly AgentsOverviewControl overview;
    private readonly LocalStore store;
    private readonly ChatSession session = new();
    private readonly ChatSessionService sender;
    private readonly HealthMonitorService monitor;
    private readonly Dictionary<string, string> credentials = [], healthCredentials = [];
    private AppSettings settings = new();
    private ThemePalette palette = ThemePalette.Light;
    private string activeSystem = "DEFAULT";
    private bool refreshing, ready, readingAttachments, savingProfile, savingTheme, deletingConversation, disposed;
    private List<AgentProfile> Profiles => session.Catalog.Profiles;
    private List<string> Groups => session.Catalog.Groups;
    private AgentProfile? Agent => view.Selector.SelectedAgent;
    private bool Busy => sender.IsSending || readingAttachments || savingProfile || savingTheme || deletingConversation;

    public ChatCoordinator(Form owner, ChatView view, AgentsOverviewControl overview, LocalStore store, HealthClient? healthClient, OrchestratorClient? chatClient)
    {
        this.owner = owner; this.view = view; this.overview = overview; this.store = store;
        sender = new ChatSessionService(store, chatClient);
        monitor = new HealthMonitorService(healthClient);
        sender.Changed += SessionChanged;
        monitor.Changed += HealthChanged;
        SystemEvents.UserPreferenceChanged += SystemPreferenceChanged;
        view.Selector.AgentChanged += (_, _) => ChangeAgent();
        view.Selector.SystemChanged += (_, _) => ChangeSystem();
        view.Sidebar.NewRequested += (_, _) => StartConversation();
        view.Sidebar.ConversationSelected += (_, _) => SelectHistory();
        view.Sidebar.DeleteRequested += async (_, _) => await DeleteConversationAsync();
        view.Sidebar.OpenFolderRequested += (_, _) => OpenFolder();
        view.Composer.DraftChanged += (_, _) => { session.Draft = view.Composer.Draft; UpdateControls(); };
        view.Composer.SendRequested += async (_, _) => await SendAsync();
        view.Composer.CancelRequested += (_, _) => sender.Cancel();
        view.Composer.AttachRequested += async (_, _) => await AddAttachmentsAsync();
        view.Composer.RemoveRequested += (_, _) =>
        {
            var index = view.Composer.SelectedAttachmentIndex;
            if (index >= 0 && index < session.PendingAttachments.Count) session.PendingAttachments.RemoveAt(index);
            RenderSession();
        };
        view.NewAgentRequested += async (_, _) => await EditProfileAsync(true);
        view.EditAgentRequested += async (_, _) => await EditProfileAsync(false);
        view.DeleteAgentRequested += async (_, _) => await DeleteProfileAsync();
        view.ManageGroupsRequested += async (_, _) => await ManageGroupsAsync();
        view.ThemeRequested += async mode => await ChangeThemeAsync(mode);
        overview.ManageGroupsRequested += async (_, _) => await ManageGroupsAsync();
        overview.RefreshRequested += async (_, _) =>
        {
            try { await monitor.RefreshAsync(true); }
            catch (OperationCanceledException) when (disposed) { }
        };
        ApplyTheme();
        UpdateControls();
    }

    public async Task InitializeAsync()
    {
        try
        {
            try { settings = await store.LoadSettingsAsync(); }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or IOException or UnauthorizedAccessException)
            { ShowError("No se pudo cargar la apariencia guardada. Se usará el tema del sistema.\n" + ex.Message); }
            if (disposed) return;
            ApplyTheme();
            session.Catalog = await store.LoadCatalogAsync();
            await store.SaveCatalogAsync(session.Catalog);
            var loaded = await store.LoadConversationsAsync();
            session.Conversations = loaded.Items;
            foreach (var conversation in session.Conversations)
            {
                var interrupted = conversation.Messages.Where(m => m.DeliveryStatus == "pending").ToList();
                foreach (var message in interrupted) message.DeliveryStatus = "interrupted";
                if (interrupted.Count > 0) await store.SaveConversationAsync(conversation);
            }
            if (disposed) return;
            RefreshProfiles(Profiles.FirstOrDefault()?.Id);
            ready = true;
            StartConversation();
            RenderHealth();
            monitor.Start();
            view.SetStatus("Listo. Configura el perfil para conectar con tu orquestador.");
            if (loaded.Errors.Count > 0) ShowError("Algunos historiales no pudieron abrirse y se conservaron en disco:\n" + string.Join("\n", loaded.Errors));
        }
        catch (Exception ex)
        {
            if (disposed) return;
            view.SetStatus("No se pudo cargar el almacenamiento local."); ShowError(ex.Message);
        }
        if (!disposed) UpdateControls();
    }

    private void ChangeAgent()
    {
        if (refreshing) return;
        if (Busy || !ConfirmAgentChange()) { RefreshProfiles(session.Current?.AgentProfileId); return; }
        ActivateAgent();
    }
    private void ChangeSystem()
    {
        if (refreshing || view.Selector.SelectedSystem is not string system) return;
        if (!ready || Busy || !ConfirmAgentChange()) { RefreshProfiles(session.Current?.AgentProfileId); return; }
        RefreshProfiles(Profiles.FirstOrDefault(p => string.Equals(p.AgentSystem, system, StringComparison.OrdinalIgnoreCase))?.Id, system);
        ActivateAgent();
    }
    private bool ConfirmAgentChange()
    {
        if (session.Current?.Messages.Count > 0 && session.HasDraft)
            return Confirm("Al cambiar de agente se descartará el borrador de esta conversación. ¿Continuar?", "Borrador pendiente");
        return session.PendingAttachments.Count == 0 || Confirm("Cambiar de agente o sistema quitará los adjuntos del borrador. ¿Continuar?", "Cambiar agente");
    }
    private void ActivateAgent()
    {
        if (session.Current is null || session.Current.Messages.Count > 0)
        {
            session.Current = new Conversation { AgentProfileId = Agent?.Id ?? "" };
            session.Draft = "";
        }
        else session.Current.AgentProfileId = Agent?.Id ?? "";
        session.PendingAttachments.Clear();
        RenderSession();
    }
    private void StartConversation()
    {
        if (Busy || (session.HasDraft && !Confirm("Se descartará el borrador actual. ¿Crear una conversación?", "Borrador pendiente"))) return;
        session.Current = new Conversation { AgentProfileId = Agent?.Id ?? "" };
        session.ClearDraft(); RenderSession(); view.Composer.FocusInput();
    }
    private void SelectHistory()
    {
        if (refreshing || Busy || view.Sidebar.SelectedConversation is not Conversation conversation || conversation.AgentProfileId != Agent?.Id) return;
        if (session.HasDraft && !Confirm("Al cambiar de conversación se descartará el borrador actual. ¿Continuar?", "Borrador pendiente"))
        { view.Sidebar.RestoreSelection(session.Current); return; }
        SelectConversation(conversation);
    }
    private void SelectConversation(Conversation conversation)
    {
        session.Current = conversation; session.ClearDraft();
        RefreshProfiles(conversation.AgentProfileId);
        view.SetStatus(Agent is null ? "El perfil de esta conversación ya no existe." : "Conversación cargada.");
    }
    private void RefreshProfiles(string? selectedId, string? system = null)
    {
        var selected = Profiles.FirstOrDefault(p => p.Id == selectedId);
        var desired = selected?.AgentSystem ?? system ?? activeSystem;
        activeSystem = Groups.FirstOrDefault(g => string.Equals(g, desired, StringComparison.OrdinalIgnoreCase)) ?? "DEFAULT";
        refreshing = true;
        try { view.Selector.Bind(Groups, Profiles, activeSystem, selectedId); }
        finally { refreshing = false; }
        monitor.UpdateProfiles(Profiles, healthCredentials);
        RenderHealth(); RenderSession();
    }
    private void RenderSession()
    {
        view.Composer.Draft = session.Draft;
        view.SetAttachments(session.PendingAttachments);
        view.Sidebar.Bind(session.Conversations, session.Current, Agent);
        view.Transcript.Render(session.Current, palette);
        UpdateControls();
    }
    private void UpdateControls()
    {
        view.SetState(ready, Busy, sender.IsSending, sender.CanCancel, Agent, session.HasDraft, savingTheme);
        overview.SetManagementEnabled(ready && !Busy);
        overview.SetRefreshing(monitor.IsRefreshing, ready);
    }
    private void RenderHealth() { overview.Render(Profiles, monitor.Results, Groups); UpdateControls(); }
    private void SessionChanged(object? sender, EventArgs e) => OnUi(() => { RenderSession(); if (!string.IsNullOrEmpty(this.sender.Status)) view.SetStatus(this.sender.Status); });
    private void HealthChanged(object? sender, EventArgs e) => OnUi(RenderHealth);

    private async Task SendAsync()
    {
        if (Busy || !ready || Agent is not AgentProfile agent || session.Current is null || !session.HasDraft) return;
        try
        {
            var warning = await sender.SendAsync(session, agent, credentials.GetValueOrDefault(agent.Id, ""));
            if (warning is not null) ShowError(warning);
        }
        catch (Exception ex) { ShowError(ex.Message); }
        if (!disposed) { RenderSession(); view.Composer.FocusInput(); }
    }
    private async Task AddAttachmentsAsync()
    {
        if (Agent is not AgentProfile agent || Busy) return;
        using var dialog = new OpenFileDialog { Multiselect = true, Title = "Adjuntar archivos permitidos por el agente", Filter = "Todos los archivos|*.*" };
        if (dialog.ShowDialog(owner) != DialogResult.OK) return;
        readingAttachments = true; UpdateControls();
        try
        {
            List<ChatAttachment> additions = [];
            foreach (var file in dialog.FileNames)
            {
                if (session.PendingAttachments.Count + additions.Count >= 20) throw new InvalidOperationException("Puedes enviar hasta 20 adjuntos por mensaje.");
                var item = await AttachmentService.ReadAsync(file, agent);
                if (session.PendingAttachments.Sum(a => a.Size) + additions.Sum(a => a.Size) + item.Size > 100L * 1024 * 1024)
                    throw new InvalidOperationException("Los adjuntos del mensaje no pueden superar 100 MB en total.");
                additions.Add(item);
            }
            session.PendingAttachments.AddRange(additions); view.SetStatus("Adjuntos preparados para el próximo mensaje.");
        }
        catch (Exception ex) { ShowError(ex.Message); }
        finally { readingAttachments = false; RenderSession(); }
    }
    private async Task EditProfileAsync(bool create)
    {
        var profile = create ? new AgentProfile { Name = "Nuevo agente", AgentSystem = activeSystem } : Agent;
        if (profile is null || Busy) return;
        using var editor = new ProfileEditorForm(profile, credentials.GetValueOrDefault(profile.Id, ""), palette, healthCredentials.GetValueOrDefault(profile.Id, ""), Groups);
        if (editor.ShowDialog(owner) != DialogResult.OK) return;
        if (session.PendingAttachments.Count > 0 && !Confirm("Guardar este perfil quitará los adjuntos del borrador. ¿Continuar?", "Actualizar perfil")) return;
        var updated = Profiles.Where(p => p.Id != editor.Profile.Id).Append(editor.Profile).ToList();
        savingProfile = true; UpdateControls();
        try
        {
            await store.SaveProfilesAsync(updated); session.Catalog.Profiles = updated;
            credentials[editor.Profile.Id] = editor.SessionToken; healthCredentials[editor.Profile.Id] = editor.HealthSessionToken;
            var selected = session.Current?.Messages.Count > 0 ? session.Current.AgentProfileId : editor.Profile.Id;
            if (session.Current is not null && session.Current.Messages.Count == 0) session.Current.AgentProfileId = selected;
            session.PendingAttachments.Clear(); RefreshProfiles(selected);
            view.SetStatus("Perfil guardado. La credencial se conserva solo durante esta sesión.");
        }
        catch (Exception ex) { ShowError("No se pudo guardar el perfil: " + ex.Message); }
        finally { savingProfile = false; UpdateControls(); }
    }
    private async Task DeleteProfileAsync()
    {
        if (Agent is not AgentProfile profile || Busy) return;
        if (session.Conversations.Any(c => c.AgentProfileId == profile.Id)) { ShowError("Este perfil tiene conversaciones guardadas. Puedes editarlo; no se puede eliminar mientras esté en uso."); return; }
        if (!Confirm($"¿Eliminar el perfil '{profile.Name}'?", "Eliminar perfil")) return;
        if (session.HasDraft && !Confirm("Eliminar el perfil descartará el borrador actual. ¿Continuar?", "Borrador pendiente")) return;
        savingProfile = true; UpdateControls();
        try
        {
            var updated = Profiles.Where(p => p.Id != profile.Id).ToList();
            await store.SaveProfilesAsync(updated); session.Catalog.Profiles = updated;
            credentials.Remove(profile.Id); healthCredentials.Remove(profile.Id);
            session.ClearDraft();
            session.Current = new Conversation { AgentProfileId = updated.FirstOrDefault()?.Id ?? "" };
            RefreshProfiles(session.Current.AgentProfileId); view.SetStatus("Perfil eliminado.");
        }
        catch (Exception ex) { ShowError(ex.Message); }
        finally { savingProfile = false; UpdateControls(); }
    }
    private async Task DeleteConversationAsync()
    {
        if (!ready || Busy || view.Sidebar.SelectedConversation is not Conversation conversation || conversation.AgentProfileId != Agent?.Id) return;
        using var confirmation = new DeleteConversationForm(conversation, session.HasDraft, palette);
        if (confirmation.ShowDialog(owner) != DialogResult.OK) return;
        deletingConversation = true; UpdateControls();
        try
        {
            await store.DeleteConversationAsync(conversation.Id); session.Conversations.Remove(conversation);
            var next = session.Conversations.Where(c => c.AgentProfileId == Agent?.Id).OrderByDescending(c => c.UpdatedAt).FirstOrDefault();
            session.ClearDraft();
            if (next is not null) SelectConversation(next);
            else { session.Current = new Conversation { AgentProfileId = Agent?.Id ?? "" }; RenderSession(); }
            view.SetStatus("Conversación eliminada del historial local.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { ShowError("No se pudo eliminar la conversación; se conserva en la lista. " + ex.Message); }
        finally { deletingConversation = false; UpdateControls(); }
    }
    private async Task ManageGroupsAsync()
    {
        if (!ready || Busy) return;
        using var editor = new GroupManagerForm(session.Catalog, palette);
        if (editor.ShowDialog(owner) != DialogResult.OK) return;
        savingProfile = true; UpdateControls();
        try
        {
            await store.SaveCatalogAsync(editor.Catalog);
            var selected = Agent?.Id; session.Catalog = editor.Catalog; RefreshProfiles(selected);
            view.SetStatus("Grupos guardados. Los agentes conservan sus conversaciones y configuración.");
        }
        catch (Exception ex) { ShowError("No se pudieron guardar los grupos: " + ex.Message); }
        finally { savingProfile = false; UpdateControls(); }
    }
    private async Task ChangeThemeAsync(ThemeMode mode)
    {
        if (!ready || savingTheme) return;
        savingTheme = true; UpdateControls();
        try { var updated = new AppSettings { Theme = mode }; await store.SaveSettingsAsync(updated); settings = updated; ApplyTheme(); }
        catch (Exception ex) { ShowError("No se pudo guardar el tema: " + ex.Message); }
        finally { savingTheme = false; UpdateControls(); }
    }
    private void ApplyTheme()
    {
        palette = ThemeAppearance.Resolve(settings.Theme);
        Application.SetColorMode(palette.IsDark ? SystemColorMode.Dark : SystemColorMode.Classic);
        foreach (Form form in Application.OpenForms) ThemeAppearance.Apply(form, palette);
        ThemeAppearance.Apply(owner, palette); view.SetTheme(settings.Theme, palette); overview.SetPalette(palette);
        view.Transcript.Render(session.Current, palette);
    }
    private void SystemPreferenceChanged(object sender, UserPreferenceChangedEventArgs e) => OnUi(() =>
    {
        if (settings.Theme == ThemeMode.System && ThemeAppearance.Resolve(settings.Theme).IsDark != palette.IsDark) ApplyTheme();
    });
    private void OpenFolder()
    {
        try { store.EnsureRootDirectory(); Process.Start(new ProcessStartInfo(store.RootPath) { UseShellExecute = true }); }
        catch (Exception ex) { ShowError(ex.Message); }
    }
    public bool CanClose()
    {
        if (Busy) { sender.Cancel(); view.SetStatus("Espera a que termine la operación antes de cerrar."); return false; }
        return !session.HasDraft || Confirm("El borrador no se guarda. ¿Cerrar la aplicación?", "Borrador pendiente");
    }
    private bool Confirm(string message, string title) => MessageBox.Show(owner, message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
    private void ShowError(string text) { if (!disposed) MessageBox.Show(owner, text, "Agentest", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    private void OnUi(Action action)
    {
        if (disposed || owner.IsDisposed || !owner.IsHandleCreated) return;
        try { if (owner.InvokeRequired) owner.BeginInvoke((Action)(() => { if (!disposed) action(); })); else action(); }
        catch (InvalidOperationException) when (disposed || owner.IsDisposed) { }
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true;
        SystemEvents.UserPreferenceChanged -= SystemPreferenceChanged;
        sender.Changed -= SessionChanged; monitor.Changed -= HealthChanged;
        monitor.Dispose(); sender.Dispose(); credentials.Clear(); healthCredentials.Clear();
    }
}
