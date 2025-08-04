using System.Runtime.InteropServices;
using System.Threading;
using MakefileReader.Commands;

namespace MakefileReader;

[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[InstalledProductRegistration(Vsix.Name, Vsix.Description, Vsix.Version)]
[ProvideMenuResource("Menus.ctmenu", 1)]
[ProvideToolWindow(typeof(MakefileToolWindow.Pane), Style = VsDockStyle.Tabbed, Window = WindowGuids.SolutionExplorer)]
[Guid(PackageGuids.MakefileReaderString)]
public sealed class MakefileReaderPackage : ToolkitPackage
{
    protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
    {
        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        // Register commands
        await this.RegisterCommandsAsync();

        // Initialize tool window
        MakefileToolWindow.Initialize(this);
    }
}