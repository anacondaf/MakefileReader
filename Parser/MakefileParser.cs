#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MakefileReader;

/// <summary>
/// Parser for Makefile targets and commands
/// </summary>
public class MakefileParser
{
    private static readonly Regex TargetPattern = new(@"^([a-zA-Z0-9_\-\.\/]+)\s*:\s*([^=]*?)$", RegexOptions.Compiled);
    private static readonly Regex VariablePattern = new(@"^([A-Z_][A-Z0-9_]*)\s*[:\+\?]?=\s*(.*)$", RegexOptions.Compiled);

    /// <summary>
    /// Parse Makefile content and extract targets
    /// </summary>
    /// <param name="lines">Lines from the Makefile</param>
    /// <returns>List of parsed targets</returns>
    public List<MakefileTarget> ParseTargets(string[] lines)
    {
        var targets = new List<MakefileTarget>();
        var variables = new Dictionary<string, string>();

        MakefileTarget? currentTarget = null;
        var commandLines = new List<string>();

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmedLine = line.TrimStart();

            // Skip empty lines and comments
            if (string.IsNullOrWhiteSpace(line) || trimmedLine.StartsWith("#"))
                continue;

            // Check for variable assignment
            var variableMatch = VariablePattern.Match(trimmedLine);
            if (variableMatch.Success)
            {
                var varName = variableMatch.Groups[1].Value;
                var varValue = variableMatch.Groups[2].Value.Trim();
                variables[varName] = varValue;
                continue;
            }

            // Check for target definition
            var targetMatch = TargetPattern.Match(trimmedLine);
            if (targetMatch.Success && !line.StartsWith("\t") && !line.StartsWith("  "))
            {
                // Save previous target if exists
                if (currentTarget != null)
                {
                    currentTarget.Commands = string.Join("\n", commandLines);
                    targets.Add(currentTarget);
                    commandLines.Clear();
                }

                // Start new target
                var targetName = targetMatch.Groups[1].Value.Trim();
                var dependenciesString = targetMatch.Groups[2].Value.Trim();

                // Skip special targets and targets starting with dot (hidden/special)
                if (IsSpecialTarget(targetName))
                    continue;

                currentTarget = new MakefileTarget
                {
                    Name = targetName,
                    Dependencies = ParseDependencies(dependenciesString),
                    RequiredVariables = new List<string>()
                };
                continue;
            }

            // Check for command line (starts with tab or spaces)
            if ((line.StartsWith("\t") || line.StartsWith("  ")) && currentTarget != null)
            {
                var command = line.TrimStart('\t', ' ');
                if (!string.IsNullOrWhiteSpace(command))
                {
                    // Extract variables from raw command BEFORE expansion
                    var vars = ExtractVariableReferences(command);
                    foreach (var varName in vars)
                    {
                        if (!currentTarget.RequiredVariables.Contains(varName))
                        {
                            currentTarget.RequiredVariables.Add(varName);
                        }
                    }
                    
                    // Then expand variables in command
                    command = ExpandVariables(command, variables);
                    commandLines.Add(command);
                }
                continue;
            }

            // Line continuation check
            if (line.EndsWith("\\") && i + 1 < lines.Length)
            {
                var continuedLine = new StringBuilder(line.TrimEnd('\\'));
                while (++i < lines.Length && lines[i - 1].EndsWith("\\"))
                {
                    continuedLine.Append(" ").Append(lines[i].TrimEnd('\\'));
                }

                // Process the complete continued line
                var completeLine = continuedLine.ToString();
                var continueTargetMatch = TargetPattern.Match(completeLine);
                if (continueTargetMatch.Success)
                {
                    // Handle continued target definition
                    if (currentTarget != null)
                    {
                        currentTarget.Commands = string.Join("\n", commandLines);
                        targets.Add(currentTarget);
                        commandLines.Clear();
                    }

                    var targetName = continueTargetMatch.Groups[1].Value.Trim();
                    if (!IsSpecialTarget(targetName))
                    {
                        currentTarget = new MakefileTarget
                        {
                            Name = targetName,
                            Dependencies = ParseDependencies(continueTargetMatch.Groups[2].Value.Trim()),
                            RequiredVariables = new List<string>()
                        };
                    }
                }
            }
        }

        // Add the last target
        if (currentTarget != null)
        {
            currentTarget.Commands = string.Join("\n", commandLines);
            targets.Add(currentTarget);
        }

        return targets.Where(t => !string.IsNullOrWhiteSpace(t.Name)).ToList();
    }

    /// <summary>
    /// Parse dependencies from a dependency string
    /// </summary>
    private static List<string> ParseDependencies(string dependenciesString)
    {
        if (string.IsNullOrWhiteSpace(dependenciesString))
            return new List<string>();

        return dependenciesString
            .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(dep => !string.IsNullOrWhiteSpace(dep))
            .ToList();
    }

    /// <summary>
    /// Check if a target is a special/built-in target that should be ignored
    /// </summary>
    private static bool IsSpecialTarget(string targetName)
    {
        var specialTargets = new[]
        {
            ".PHONY", ".SUFFIXES", ".DEFAULT", ".PRECIOUS", ".INTERMEDIATE",
            ".SECONDARY", ".SECONDEXPANSION", ".DELETE_ON_ERROR", ".IGNORE",
            ".LOW_RESOLUTION_TIME", ".SILENT", ".EXPORT_ALL_VARIABLES",
            ".NOTPARALLEL", ".ONESHELL", ".POSIX"
        };

        return specialTargets.Contains(targetName) ||
               targetName.StartsWith(".") ||
               targetName.Contains("=") ||
               string.IsNullOrWhiteSpace(targetName);
    }

    /// <summary>
    /// Expand variables in a command string
    /// </summary>
    private static string ExpandVariables(string command, Dictionary<string, string> variables)
    {
        var result = command;

        // Simple variable expansion $(VAR) and ${VAR}
        var variableReferences = Regex.Matches(result, @"\$\{([A-Z_][A-Z0-9_]*)\}|\$\(([A-Z_][A-Z0-9_]*)\)");

        foreach (Match match in variableReferences)
        {
            var varName = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            if (variables.TryGetValue(varName, out var varValue))
            {
                result = result.Replace(match.Value, varValue);
            }
        }

        return result;
    }

    /// <summary>
    /// Extract variable references from a command string
    /// </summary>
    public static List<string> ExtractVariableReferences(string command)
    {
        var variables = new List<string>();
        
        // Find all variable references $(VAR) and ${VAR}
        var variableReferences = Regex.Matches(command, @"\$\{([A-Z_][A-Z0-9_]*)\}|\$\(([A-Z_][A-Z0-9_]*)\)");

        foreach (Match match in variableReferences)
        {
            var varName = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            if (!variables.Contains(varName))
            {
                variables.Add(varName);
            }
        }

        return variables;
    }

    /// <summary>
    /// Get all variable definitions from Makefile
    /// </summary>
    public Dictionary<string, string> ParseVariables(string[] lines)
    {
        var variables = new Dictionary<string, string>();

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith("#"))
                continue;

            var variableMatch = VariablePattern.Match(trimmedLine);
            if (variableMatch.Success)
            {
                var varName = variableMatch.Groups[1].Value;
                var varValue = variableMatch.Groups[2].Value.Trim();
                variables[varName] = varValue;
            }
        }

        return variables;
    }
}