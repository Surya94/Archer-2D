using UnityEditor;
using UnityEngine;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UnityTools.AutoNamespace
{
    /// <summary>
    /// Automatically adds appropriate namespaces to newly created C# scripts in Unity.
    /// Supports complex use cases including nested folders, special characters, and various C# constructs.
    /// </summary>
    public class NamespaceAdder : AssetModificationProcessor
    {
        #region Configuration

        private const string DEFAULT_NAMESPACE = "Archer";
        private const int FILE_CREATION_DELAY_MS = 100; // Reduced delay

        // Folders to exclude from namespace generation
        private static readonly HashSet<string> EXCLUDED_FOLDERS = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
        {
            "Plugins", "ThirdParty", "External", "Libraries", "Packages"
        };

        // File patterns to exclude
        private static readonly HashSet<string> EXCLUDED_PATTERNS = new HashSet<string>
        {
            "*Test.cs", "*Tests.cs", "*.Test.cs", "*.Tests.cs"
        };

        #endregion

        #region Unity Callbacks

        public static void OnWillCreateAsset(string assetPath)
        {
            // Clean up the path from Unity's .meta extension
            string cleanPath = assetPath.Replace(".meta", "");

            // Only process C# files
            if (!cleanPath.EndsWith(".cs", System.StringComparison.OrdinalIgnoreCase))
                return;

            // Skip if in excluded folders
            if (IsPathExcluded(cleanPath))
                return;

            string fullPath = GetFullPath(cleanPath);

            // Use EditorApplication.delayCall instead of Thread.Sleep for better Unity integration
            EditorApplication.delayCall += () => ProcessNewScript(fullPath);
        }

        #endregion

        #region Core Processing

        private static void ProcessNewScript(string fullPath)
        {
            // Validate file exists and is accessible
            if (!IsValidScriptFile(fullPath))
                return;

            try
            {
                string content = File.ReadAllText(fullPath, Encoding.UTF8);

                // Skip if already has namespace or is a special file type
                if (ShouldSkipFile(content, fullPath))
                    return;

                string namespaceName = GenerateNamespace(fullPath);
                string modifiedContent = AddNamespaceToContent(content, namespaceName);

                // Only write if content actually changed
                if (content != modifiedContent)
                {
                    File.WriteAllText(fullPath, modifiedContent, Encoding.UTF8);
                    AssetDatabase.Refresh();

                    Debug.Log($"[NamespaceAdder] Added namespace '{namespaceName}' to: {Path.GetFileName(fullPath)}");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[NamespaceAdder] Failed to process {fullPath}: {ex.Message}");
            }
        }

        #endregion

        #region Path and Validation Utilities

        private static string GetFullPath(string assetPath)
        {
            return Path.GetFullPath(assetPath).Replace("\\", "/");
        }

        private static bool IsPathExcluded(string path)
        {
            string[] pathParts = path.Split('/');

            // Check for excluded folders
            foreach (string folder in EXCLUDED_FOLDERS)
            {
                if (pathParts.Any(part => string.Equals(part, folder, System.StringComparison.OrdinalIgnoreCase)))
                    return true;
            }

            // Check for excluded patterns
            string fileName = Path.GetFileName(path);
            foreach (string pattern in EXCLUDED_PATTERNS)
            {
                if (IsPatternMatch(fileName, pattern))
                    return true;
            }

            return false;
        }

        private static bool IsPatternMatch(string fileName, string pattern)
        {
            // Simple wildcard matching
            string regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
            return Regex.IsMatch(fileName, regexPattern, RegexOptions.IgnoreCase);
        }

        private static bool IsValidScriptFile(string fullPath)
        {
            if (!File.Exists(fullPath))
                return false;

            try
            {
                // Check if file is readable and not empty
                var info = new FileInfo(fullPath);
                return info.Length > 0 && info.Length < 1024 * 1024; // Max 1MB for safety
            }
            catch
            {
                return false;
            }
        }

        private static bool ShouldSkipFile(string content, string fullPath)
        {
            // Skip if already has namespace
            if (HasExistingNamespace(content))
                return true;

            // Skip Unity-generated files or special cases
            if (IsSpecialUnityFile(content, fullPath))
                return true;

            // Skip if file doesn't contain class/struct/interface/enum declarations
            if (!HasValidCSharpDeclarations(content))
                return true;

            return false;
        }

        private static bool HasExistingNamespace(string content)
        {
            // More robust namespace detection
            return Regex.IsMatch(content, @"^\s*namespace\s+[\w.]+\s*{", RegexOptions.Multiline);
        }

        private static bool IsSpecialUnityFile(string content, string fullPath)
        {
            // Check for Unity-specific patterns that shouldn't get namespaces
            var specialPatterns = new[]
            {
                @"^\s*\[assembly:\s*", // Assembly attributes
                @"^\s*#if\s+UNITY_EDITOR\s*$.*?#endif", // Editor-only files
                @"class\s+\w+\s*:\s*AssetPostprocessor", // Asset postprocessors
                @"class\s+\w+\s*:\s*BuildPlayer", // Build processors
            };

            return specialPatterns.Any(pattern =>
                Regex.IsMatch(content, pattern, RegexOptions.Multiline | RegexOptions.Singleline));
        }

        private static bool HasValidCSharpDeclarations(string content)
        {
            // Look for class, struct, interface, enum declarations
            var declarationPatterns = new[]
            {
                @"^\s*(public|internal|private|protected)?\s*(abstract|sealed|static|partial)?\s*class\s+\w+",
                @"^\s*(public|internal|private|protected)?\s*(readonly)?\s*struct\s+\w+",
                @"^\s*(public|internal|private|protected)?\s*interface\s+\w+",
                @"^\s*(public|internal|private|protected)?\s*enum\s+\w+",
                @"^\s*(public|internal|private|protected)?\s*delegate\s+\w+"
            };

            return declarationPatterns.Any(pattern =>
                Regex.IsMatch(content, pattern, RegexOptions.Multiline | RegexOptions.IgnoreCase));
        }

        #endregion

        #region Namespace Generation

        private static string GenerateNamespace(string fullPath)
        {
            string dataPath = Path.GetFullPath(Application.dataPath).Replace("\\", "/");
            string normalizedPath = Path.GetFullPath(fullPath).Replace("\\", "/");

            // Ensure file is within Assets folder
            if (!normalizedPath.StartsWith(dataPath, System.StringComparison.OrdinalIgnoreCase))
                return DEFAULT_NAMESPACE;

            // Get relative path from Assets folder
            string relativePath = normalizedPath.Substring(dataPath.Length).TrimStart('/');
            string directoryPath = Path.GetDirectoryName(relativePath)?.Replace("\\", "/");

            if (string.IsNullOrEmpty(directoryPath))
                return DEFAULT_NAMESPACE;

            return BuildNamespaceFromPath(directoryPath);
        }

        private static string BuildNamespaceFromPath(string directoryPath)
        {
            string[] folders = directoryPath.Split('/');
            var namespaceParts = new List<string> { DEFAULT_NAMESPACE };

            foreach (string folder in folders)
            {
                if (string.IsNullOrWhiteSpace(folder))
                    continue;

                string sanitized = SanitizeNamespacePart(folder);
                if (!string.IsNullOrEmpty(sanitized) && IsValidIdentifier(sanitized))
                {
                    namespaceParts.Add(sanitized);
                }
            }

            return string.Join(".", namespaceParts);
        }

        private static string SanitizeNamespacePart(string part)
        {
            if (string.IsNullOrWhiteSpace(part))
                return string.Empty;

            // Replace invalid characters with underscores
            string sanitized = Regex.Replace(part, @"[^\w]", "_");

            // Ensure it starts with a letter or underscore
            if (char.IsDigit(sanitized[0]))
                sanitized = "_" + sanitized;

            // Remove consecutive underscores
            sanitized = Regex.Replace(sanitized, @"_{2,}", "_");

            // Trim underscores from start and end
            return sanitized.Trim('_');
        }

        private static bool IsValidIdentifier(string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
                return false;

            // Check C# identifier rules
            return Regex.IsMatch(identifier, @"^[a-zA-Z_][a-zA-Z0-9_]*$") &&
                   !IsCSharpKeyword(identifier);
        }

        private static bool IsCSharpKeyword(string identifier)
        {
            var keywords = new HashSet<string>
            {
                "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char",
                "checked", "class", "const", "continue", "decimal", "default", "delegate",
                "do", "double", "else", "enum", "event", "explicit", "extern", "false",
                "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit",
                "in", "int", "interface", "internal", "is", "lock", "long", "namespace",
                "new", "null", "object", "operator", "out", "override", "params", "private",
                "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
                "short", "sizeof", "stackalloc", "static", "string", "struct", "switch",
                "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked",
                "unsafe", "ushort", "using", "virtual", "void", "volatile", "while"
            };

            return keywords.Contains(identifier.ToLowerInvariant());
        }

        #endregion

        #region Content Formatting

        private static string AddNamespaceToContent(string content, string namespaceName)
        {
            var parser = new CSharpContentParser(content);
            return parser.WrapInNamespace(namespaceName);
        }

        #endregion
    }

    /// <summary>
    /// Handles parsing and formatting of C# content for namespace wrapping
    /// </summary>
    internal class CSharpContentParser
    {
        private readonly string _originalContent;
        private readonly List<string> _usingDirectives;
        private readonly string _remainingContent;

        public CSharpContentParser(string content)
        {
            _originalContent = content;
            (_usingDirectives, _remainingContent) = ExtractUsingDirectives(content);
        }

        public string WrapInNamespace(string namespaceName)
        {
            var sb = new StringBuilder();

            // Add using directives at the top
            foreach (string usingDirective in _usingDirectives)
            {
                sb.AppendLine(usingDirective);
            }

            if (_usingDirectives.Count > 0)
                sb.AppendLine(); // Empty line after using directives

            // Add namespace declaration
            sb.AppendLine($"namespace {namespaceName}");
            sb.AppendLine("{");

            // Add indented content
            string indentedContent = IndentContent(_remainingContent);
            sb.Append(indentedContent);

            sb.AppendLine("}");

            return sb.ToString();
        }

        private static (List<string> usingDirectives, string remainingContent) ExtractUsingDirectives(string content)
        {
            var usingDirectives = new List<string>();
            var lines = content.Split('\n');
            int lastUsingIndex = -1;

            // More comprehensive using directive pattern
            var usingPattern = @"^\s*using\s+(?:static\s+|global::)?(?:[a-zA-Z_][\w.]*(?:\s*=\s*[a-zA-Z_][\w.]*)?)\s*;\s*";

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                // Skip comments and empty lines before using directives
                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("//"))
                    continue;

                if (Regex.IsMatch(line, usingPattern))
                {
                    usingDirectives.Add(line.Trim());
                    lastUsingIndex = i;
                }
                else if (usingDirectives.Count > 0)
                {
                    // Stop at first non-using directive after we've found using directives
                    break;
                }
            }

            // Get remaining content after using directives
            string remainingContent;
            if (lastUsingIndex >= 0)
            {
                var remainingLines = lines.Skip(lastUsingIndex + 1);
                remainingContent = string.Join("\n", remainingLines).TrimStart();
            }
            else
            {
                remainingContent = content;
            }

            return (usingDirectives, remainingContent);
        }

        private static string IndentContent(string content)
        {
            if (string.IsNullOrEmpty(content))
                return content;

            var lines = content.Split('\n');
            var indentedLines = new List<string>();

            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    indentedLines.Add(line); // Preserve empty lines
                }
                else
                {
                    indentedLines.Add("    " + line); // 4-space indentation
                }
            }

            return string.Join("\n", indentedLines);
        }
    }
}