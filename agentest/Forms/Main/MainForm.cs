using agentest.Forms.Chat;
using agentest.Persistence;
using agentest.Services;

namespace agentest.Forms.Main;

public sealed partial class MainForm : Form
{
    private ChatCoordinator? coordinator;
    private readonly LocalStore? suppliedStore;
    private readonly HealthClient? suppliedHealth;
    private readonly OrchestratorClient? suppliedChat;
    public MainForm() : this(null, null) { }
    public MainForm(LocalStore? localStore, HealthClient? healthMonitor = null, OrchestratorClient? chatClient = null)
    {
        suppliedStore = localStore; suppliedHealth = healthMonitor; suppliedChat = chatClient;
        InitializeComponent();
        Shown += async (_, _) =>
        {
            if (coordinator is not null || DesignMode || System.ComponentModel.LicenseManager.UsageMode == System.ComponentModel.LicenseUsageMode.Designtime) return;
            coordinator = new ChatCoordinator(this, chat, overview, suppliedStore ?? new LocalStore(), suppliedHealth, suppliedChat);
            await coordinator.InitializeAsync();
        };
        FormClosing += (_, e) => { if (coordinator is not null) e.Cancel = !coordinator.CanClose(); };
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { coordinator?.Dispose(); coordinator = null; }
        base.Dispose(disposing);
    }
}
