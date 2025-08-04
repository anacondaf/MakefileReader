using Microsoft.VisualStudio.Threading;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace MakefileReader;

/// <summary>
/// Window for executing make commands with real-time output display
/// </summary>
public partial class MakeExecutionWindow : Window
{
    private Process? _makeProcess;
    private readonly StringBuilder _outputBuffer = new();
    private readonly Stopwatch _executionTimer = new();
    private readonly System.Timers.Timer _uiUpdateTimer;
    private int _outputLineCount = 0;
    private readonly Random _progressRandom = new();

    public MakeExecutionWindow(string command, string workingDirectory)
    {
        InitializeComponent();

        // Set window info
        CommandTextBlock.Text = command;
        DirectoryTextBlock.Text = $"Working directory: {workingDirectory}";

        // Setup UI update timer
        _uiUpdateTimer = new System.Timers.Timer(100); // Update UI every 100ms
        _uiUpdateTimer.Elapsed += UpdateExecutionTime;

        // Start execution automatically
        _ = Task.Run(() => ExecuteCommandAsync(command, workingDirectory));
    }

    private async Task ExecuteCommandAsync(string command, string workingDirectory)
    {
        try
        {
            await Dispatcher.InvokeAsync(() =>
            {
                AppendOutput($"Starting execution: {command}");
                AppendOutput($"Working directory: {workingDirectory}");
                AppendOutput("".PadRight(50, '='));
                _executionTimer.Start();
                _uiUpdateTimer.Start();
                UpdateProgress(true, 0);
            });

            // Setup process
            var processInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c cd /d \"{workingDirectory}\" && {command}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory
            };

            _makeProcess = new Process { StartInfo = processInfo };

            // Setup output handlers
            _makeProcess.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Dispatcher.InvokeAsync(() =>
                    {
                        AppendOutput(e.Data);
                        _outputLineCount++;

                        // Simulate progress based on output lines (with some randomness)
                        int simulatedProgress = Math.Min(90, _outputLineCount * 5 + _progressRandom.Next(0, 10));
                        UpdateProgress(true, simulatedProgress);
                    });
                }
            };

            _makeProcess.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Dispatcher.InvokeAsync(() => AppendOutput($"ERROR: {e.Data}", true));
                }
            };

            // Start process
            _makeProcess.Start();
            _makeProcess.BeginOutputReadLine();
            _makeProcess.BeginErrorReadLine();

            // Wait for completion
            await _makeProcess.WaitForExitAsync();

            // Update UI on completion
            await Dispatcher.InvokeAsync(() =>
            {
                _executionTimer.Stop();
                _uiUpdateTimer.Stop();

                var exitCode = _makeProcess.ExitCode;
                var isSuccess = exitCode == 0;

                AppendOutput("".PadRight(50, '='));
                AppendOutput($"Process completed with exit code: {exitCode}");

                if (isSuccess)
                {
                    AppendOutput("✅ Execution completed successfully!");
                    UpdateStatus("✅ Completed Successfully", Brushes.Green);
                    UpdateProgress(true, 100); // Show 100% completion
                }
                else
                {
                    AppendOutput("❌ Execution failed!");
                    UpdateStatus("❌ Execution Failed", Brushes.Red);
                    UpdateProgress(false); // Hide progress on failure
                }

                // Update UI elements
                if (!isSuccess)
                {
                    ProgressSpinner.Visibility = Visibility.Hidden;
                }
                CloseButton.IsEnabled = true;
                CloseButton.Content = isSuccess ? "Close" : "Close (Failed)";

                // Update execution time one final time
                UpdateExecutionTimeDisplay();
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                _executionTimer.Stop();
                _uiUpdateTimer.Stop();

                AppendOutput($"❌ Exception occurred: {ex.Message}", true);
                UpdateStatus("❌ Execution Error", Brushes.Red);
                UpdateProgress(false); // Hide progress on error
                ProgressSpinner.Visibility = Visibility.Hidden;
                CloseButton.IsEnabled = true;
                CloseButton.Content = "Close (Error)";
            });
        }
    }

    private void AppendOutput(string text, bool isError = false)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var line = $"[{timestamp}] {text}";

        _outputBuffer.AppendLine(line);

        // Update UI
        OutputTextBlock.Text = _outputBuffer.ToString();

        // Change color for errors
        if (isError)
        {
            // You could implement colored text here using Runs or other WPF techniques
        }

        // Auto-scroll to bottom
        OutputTextBlock.ScrollToEnd();
    }

    private void UpdateStatus(string status, Brush backgroundColor)
    {
        StatusText.Text = status;
        StatusBanner.Background = backgroundColor;
    }

    private void UpdateExecutionTime(object? sender, System.Timers.ElapsedEventArgs e)
    {
        Dispatcher.InvokeAsync(UpdateExecutionTimeDisplay);
    }

    private void UpdateExecutionTimeDisplay()
    {
        var elapsed = _executionTimer.Elapsed;
        ExecutionTimeText.Text = $"Execution time: {elapsed.TotalSeconds:F1}s";
    }

    private void CopyOutputButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(_outputBuffer.ToString());
            // Briefly show feedback
            var originalContent = CopyOutputButton.Content;
            CopyOutputButton.Content = "Copied!";

            _ = Task.Delay(1000).ContinueWith(_ =>
            {
                Dispatcher.InvokeAsync(() => CopyOutputButton.Content = originalContent);
            });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to copy output: {ex.Message}", "Copy Error",
                           MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        // Kill process if still running
        try
        {
            if (_makeProcess != null && !_makeProcess.HasExited)
            {
                var result = System.Windows.MessageBox.Show(
                    "The process is still running. Do you want to terminate it?",
                    "Process Running",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _makeProcess.Kill();
                }
                else
                {
                    return; // Don't close window
                }
            }
        }
        catch
        {
            // Ignore errors when trying to kill process
        }

        _uiUpdateTimer?.Stop();
        _uiUpdateTimer?.Dispose();
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _uiUpdateTimer?.Stop();
        _uiUpdateTimer?.Dispose();

        try
        {
            if (_makeProcess != null && !_makeProcess.HasExited)
            {
                _makeProcess.Kill();
            }
        }
        catch
        {
            // Ignore cleanup errors
        }

        base.OnClosed(e);
    }

    private void UpdateProgress(bool isRunning, int percentage = 0)
    {
        Dispatcher.InvokeAsync(() =>
        {
            if (isRunning)
            {
                // Show and update horizontal progress bar
                ProgressSpinner.Visibility = Visibility.Visible;
                ProgressSpinner.IsIndeterminate = false;
                ProgressSpinner.Value = percentage;
            }
            else
            {
                // Hide progress when done
                ProgressSpinner.Visibility = Visibility.Hidden;
            }
        });
    }
}