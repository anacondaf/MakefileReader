namespace MakefileReader.Commands;

/// <summary>
/// Command to open the Makefile tool window
/// </summary>
[Command(PackageIds.View_OtherWindows_Makefile_Button_0)]
internal sealed class OpenMakefileToolWindowCommand : BaseCommand<OpenMakefileToolWindowCommand>
{
    protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
    {
        await MakefileToolWindow.ShowAsync();
    }
}