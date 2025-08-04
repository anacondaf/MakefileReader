using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace MakefileReader;

/// <summary>
/// User control for the Makefile tool window
/// </summary>
public partial class MakefileToolWindowControl : UserControl, INotifyPropertyChanged
{
    private string _currentMakefilePath = string.Empty;
    private readonly ObservableCollection<MakefileTarget> _makefileTargets = new();

    public MakefileToolWindowControl()
    {
        this.InitializeComponent();
        DataContext = this;
        MakefileTargets = new ReadOnlyObservableCollection<MakefileTarget>(_makefileTargets);
        
        // Subscribe to collection changes to update status visibility
        _makefileTargets.CollectionChanged += (s, e) => UpdateStatusText();
        
        // Initial status update
        UpdateStatusText();
    }

    public string CurrentMakefilePath
    {
        get => _currentMakefilePath;
        private set
        {
            _currentMakefilePath = value;
            OnPropertyChanged();
            UpdateStatusText();
        }
    }

    public ReadOnlyObservableCollection<MakefileTarget> MakefileTargets { get; }

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void UpdateStatusText()
    {
        // Update status text visibility based on whether targets are loaded
        OnPropertyChanged(nameof(CurrentMakefilePath));
        OnPropertyChanged(nameof(HasTargetsLoaded));
        OnPropertyChanged(nameof(ShowStatusMessage));
    }

    /// <summary>
    /// Indicates whether any targets have been loaded
    /// </summary>
    public bool HasTargetsLoaded => _makefileTargets.Count > 0;

    /// <summary>
    /// Status message visibility - only show when no targets are loaded
    /// </summary>
    public bool ShowStatusMessage => !HasTargetsLoaded;

    /// <summary>
    /// Load and parse a Makefile
    /// </summary>
    /// <param name="makefilePath">Path to the Makefile</param>
    public async Task LoadMakefileAsync(string makefilePath)
    {
        try
        {
            if (!File.Exists(makefilePath))
            {
                await VS.MessageBox.ShowErrorAsync("Error", $"Makefile not found: {makefilePath}");
                return;
            }

            CurrentMakefilePath = makefilePath;
            await ParseMakefileAsync(makefilePath);
        }
        catch (Exception ex)
        {
            await VS.MessageBox.ShowErrorAsync("Error", $"Failed to load Makefile: {ex.Message}");
        }
    }

    /// <summary>
    /// Parse the Makefile and extract targets
    /// </summary>
    private async Task ParseMakefileAsync(string makefilePath)
    {
        try
        {
            _makefileTargets.Clear();
            UpdateStatusText(); // Update after clearing

            var lines = File.ReadAllLines(makefilePath);
            var parser = new MakefileParser();
            var targets = parser.ParseTargets(lines);

            foreach (var target in targets)
            {
                _makefileTargets.Add(target);
            }

            UpdateStatusText(); // Update after adding targets
        }
        catch (Exception ex)
        {
            await VS.MessageBox.ShowErrorAsync("Parse Error", $"Failed to parse Makefile: {ex.Message}");
        }
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(CurrentMakefilePath))
        {
            await LoadMakefileAsync(CurrentMakefilePath);
        }
    }

    private async void ExecuteTarget_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is MakefileTarget target)
        {
            await ExecuteMakeTargetAsync(target);
        }
    }

    /// <summary>
    /// Execute a make target in a dedicated execution window
    /// </summary>
    private async Task ExecuteMakeTargetAsync(MakefileTarget target)
    {
        try
        {
            var makefileDirectory = Path.GetDirectoryName(CurrentMakefilePath);
            if (string.IsNullOrEmpty(makefileDirectory))
            {
                await VS.MessageBox.ShowErrorAsync("Error", "Cannot determine Makefile directory");
                return;
            }

            var makeCommand = $"make {target.Name}";
            Dictionary<string, string> argumentValues = null;

            // Check if target requires arguments
            if (target.HasVariables)
            {
                // Parse default values from Makefile
                var lines = File.ReadAllLines(CurrentMakefilePath);
                var parser = new MakefileParser();
                var defaultValues = parser.ParseVariables(lines);

                // Show argument input dialog
                var argumentDialog = new ArgumentInputDialog(target.Name, target.RequiredVariables, defaultValues)
                {
                    Owner = Window.GetWindow(this)
                };

                var dialogResult = argumentDialog.ShowDialog();
                if (dialogResult != true || !argumentDialog.WasExecuteClicked)
                {
                    return; // User cancelled
                }

                argumentValues = argumentDialog.ArgumentValues;

                // Build command with arguments
                var commandParts = new List<string> { "make", target.Name };
                foreach (var arg in argumentValues)
                {
                    commandParts.Add($"{arg.Key}={arg.Value}");
                }
                makeCommand = string.Join(" ", commandParts);
            }
            else
            {
                // Show confirmation dialog for targets without arguments
                var result = await VS.MessageBox.ShowAsync(
                    "Execute Make Target",
                    $"Execute '{makeCommand}' in directory:\n{makefileDirectory}\n\nThis will open an execution window with real-time output.",
                    buttons: OLEMSGBUTTON.OLEMSGBUTTON_YESNO);

                if (result != VSConstants.MessageBoxResult.IDYES)
                {
                    return;
                }
            }

            // Open execution window with real-time output
            var executionWindow = new MakeExecutionWindow(makeCommand, makefileDirectory);
            executionWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            await VS.MessageBox.ShowErrorAsync("Execution Error", $"Failed to execute make target: {ex.Message}");
        }
    }
}

/// <summary>
/// Represents a Makefile target
/// </summary>
public class MakefileTarget
{
    public string Name { get; set; } = string.Empty;
    public string Commands { get; set; } = string.Empty;
    public List<string> Dependencies { get; set; } = new();
    public List<string> RequiredVariables { get; set; } = new();
    
    /// <summary>
    /// Indicates if this target requires variable input
    /// </summary>
    public bool HasVariables => RequiredVariables.Count > 0;
}
