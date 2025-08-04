using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace MakefileReader;

/// <summary>
/// Dialog for inputting Makefile variable arguments
/// </summary>
public partial class ArgumentInputDialog : Window
{
    private readonly List<string> _requiredVariables;
    private readonly Dictionary<string, string> _defaultValues;
    private readonly Dictionary<string, TextBox> _inputControls = new();

    public Dictionary<string, string> ArgumentValues { get; private set; } = new();
    public bool WasExecuteClicked { get; private set; } = false;

    public ArgumentInputDialog(string targetName, List<string> requiredVariables, Dictionary<string, string> defaultValues = null)
    {
        InitializeComponent();
        
        _requiredVariables = requiredVariables;
        _defaultValues = defaultValues ?? new Dictionary<string, string>();
        
        TargetNameText.Text = targetName;
        CreateArgumentInputs();
        UpdatePreviewCommand();
    }

    private void CreateArgumentInputs()
    {
        ArgumentsPanel.Children.Clear();
        _inputControls.Clear();

        foreach (var variable in _requiredVariables)
        {
            // Variable container
            var container = new Border
            {
                Background = System.Windows.Media.Brushes.Transparent,
                BorderBrush = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var stackPanel = new StackPanel();

            // Variable label
            var label = new TextBlock
            {
                Text = $"{variable}:",
                FontWeight = FontWeights.Bold,
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 5)
            };

            // Input textbox
            var textBox = new TextBox
            {
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x44, 0x47, 0x5a)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x62, 0x72, 0xa4)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8),
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 12,
                Text = _defaultValues.ContainsKey(variable) ? _defaultValues[variable] : ""
            };

            // Update preview when text changes
            textBox.TextChanged += (s, e) => UpdatePreviewCommand();

            _inputControls[variable] = textBox;

            stackPanel.Children.Add(label);
            stackPanel.Children.Add(textBox);
            container.Child = stackPanel;

            ArgumentsPanel.Children.Add(container);
        }

        // Focus first input
        if (_inputControls.Count > 0)
        {
            var firstInput = _inputControls.Values.First();
            firstInput.Focus();
            firstInput.SelectAll();
        }
    }

    private void UpdatePreviewCommand()
    {
        var commandParts = new List<string> { "make", TargetNameText.Text };

        foreach (var variable in _requiredVariables)
        {
            if (_inputControls.TryGetValue(variable, out var textBox))
            {
                var value = textBox.Text.Trim();
                if (!string.IsNullOrEmpty(value))
                {
                    commandParts.Add($"{variable}={value}");
                }
            }
        }

        PreviewCommandText.Text = string.Join(" ", commandParts);
    }

    private void ExecuteButton_Click(object sender, RoutedEventArgs e)
    {
        // Collect argument values
        ArgumentValues.Clear();
        
        foreach (var variable in _requiredVariables)
        {
            if (_inputControls.TryGetValue(variable, out var textBox))
            {
                var value = textBox.Text.Trim();
                if (!string.IsNullOrEmpty(value))
                {
                    ArgumentValues[variable] = value;
                }
            }
        }

        WasExecuteClicked = true;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        WasExecuteClicked = false;
        DialogResult = false;
        Close();
    }

    protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            ExecuteButton_Click(this, new RoutedEventArgs());
        }
        else if (e.Key == System.Windows.Input.Key.Escape)
        {
            CancelButton_Click(this, new RoutedEventArgs());
        }
        
        base.OnKeyDown(e);
    }
}