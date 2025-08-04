using System.IO;
using System.Linq;

namespace MakefileReader.Commands;

/// <summary>
/// Command to preview a Makefile in the tool window from context menu
/// </summary>
[Command(PackageIds.Edit_Makefile_Button_0)]
internal sealed class PreviewMakefileCommand : BaseCommand<PreviewMakefileCommand>
{
    protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
    {
        try
        {
            // Get the selected item in Solution Explorer
            var selection = await VS.Solutions.GetActiveItemAsync();
            if (selection?.FullPath == null)
            {
                await VS.MessageBox.ShowWarningAsync("No Selection", "Please select a Makefile in Solution Explorer.");
                return;
            }

            var filePath = selection.FullPath;

            // Check if it's a Makefile
            if (!IsMakefileType(filePath))
            {
                await VS.MessageBox.ShowErrorAsync("Incompatible File", "Incompatibility file. File must be Makefile");
                return;
            }

            // Show the tool window
            var toolWindow = await MakefileToolWindow.ShowAsync();

            // Load the Makefile in the tool window
            if (toolWindow?.Content is MakefileToolWindowControl control)
            {
                await control.LoadMakefileAsync(filePath);
            }
        }
        catch (Exception ex)
        {
            await VS.MessageBox.ShowErrorAsync("Error", $"Failed to preview Makefile: {ex.Message}");
        }
    }

    protected override void BeforeQueryStatus(EventArgs e)
    {
        Command.Visible = false;
        Command.Enabled = false;

        try
        {
            // Show for any selected file in Solution Explorer
            var selection = ThreadHelper.JoinableTaskFactory.Run(async () => await VS.Solutions.GetActiveItemAsync());
            if (selection?.FullPath != null && File.Exists(selection.FullPath))
            {
                Command.Visible = true;
                Command.Enabled = true;
            }
        }
        catch
        {
            // If there's any error getting selection, hide the command
        }
    }

    /// <summary>
    /// Check if the file is a Makefile type
    /// </summary>
    private static bool IsMakefileType(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return false;

        var fileName = Path.GetFileName(filePath).ToLowerInvariant();

        // Check for common Makefile names
        var makefileNames = new[]
        {
            "makefile",
            "makefile.txt",
            "makefile.mk",
            "gnumakefile",
            "bmakefile"
        };

        // Check exact names
        if (makefileNames.Contains(fileName))
            return true;

        // Check for files starting with "makefile"
        if (fileName.StartsWith("makefile"))
            return true;

        // Check for .mk extension
        if (fileName.EndsWith(".mk"))
            return true;

        // Check file content for Makefile patterns (basic heuristic)
        try
        {
            if (File.Exists(filePath))
            {
                var firstLines = File.ReadLines(filePath).Take(10).ToArray();
                foreach (var line in firstLines)
                {
                    var trimmed = line.Trim();
                    // Look for target patterns (word followed by colon)
                    if (!string.IsNullOrEmpty(trimmed) &&
                        !trimmed.StartsWith("#") &&
                        trimmed.Contains(":") &&
                        !trimmed.Contains("="))
                    {
                        return true;
                    }
                }
            }
        }
        catch
        {
            // If we can't read the file, fall back to name-based detection
        }

        return false;
    }
}