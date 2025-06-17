using UnityEditor;
using UnityEngine;
using System.IO;
using System.Text.RegularExpressions;

public class NamespaceAdder : UnityEditor.AssetModificationProcessor
{
    private const string DefaultNamespace = "Archer";

    public static void OnWillCreateAsset(string assetPath)
    {
        assetPath = assetPath.Replace(".meta", "");
        if (!assetPath.EndsWith(".cs")) return;

        string fullPath = Path.GetFullPath(assetPath).Replace("\\", "/"); // Normalize path

        // Wait for Unity to create the file
        System.Threading.Thread.Sleep(500);

        AddNamespaceIfMissing(fullPath);
    }

    private static void AddNamespaceIfMissing(string fullPath)
    {
        if (!File.Exists(fullPath)) return;

        string fileContent = File.ReadAllText(fullPath);

        // Ignore scripts that already have a namespace
        if (fileContent.Contains("namespace")) return;

        string namespaceName = GetNamespaceFromPath(fullPath);
        Debug.Log($"Adding namespace: {namespaceName} to {fullPath}");

        string modifiedContent = FormatNamespace(fileContent, namespaceName);
        File.WriteAllText(fullPath, modifiedContent);
        AssetDatabase.Refresh();
    }

    private static string GetNamespaceFromPath(string path)
    {
        string normalizedDataPath = Path.GetFullPath(Application.dataPath).Replace("\\", "/");
        string normalizedFullPath = Path.GetFullPath(path).Replace("\\", "/");

        if (!normalizedFullPath.StartsWith(normalizedDataPath))
        {
            return DefaultNamespace; // Ignore files outside "Assets/"
        }

        string relativePath = normalizedFullPath.Substring(normalizedDataPath.Length).TrimStart('/');
        string directory = Path.GetDirectoryName(relativePath)?.Replace("\\", "/");

        if (string.IsNullOrEmpty(directory))
        {
            return DefaultNamespace; // If script is directly under "Assets/", return base namespace
        }

        string[] folders = directory.Split('/');
        string namespaceName = DefaultNamespace;

        foreach (string folder in folders)
        {
            if (!string.IsNullOrEmpty(folder))
            {
                namespaceName += "." + SanitizeNamespacePart(folder);
            }
        }

        return namespaceName;
    }

    private static string SanitizeNamespacePart(string folder)
    {
        return folder.Replace(" ", "_")
                     .Replace("-", "_")
                     .Replace(".", "_")
                     .Trim();
    }

    private static string FormatNamespace(string content, string namespaceName)
    {
        string usingPattern = @"^using\s+[\w.]+;\s*$";
        var usingMatches = Regex.Matches(content, usingPattern, RegexOptions.Multiline);

        string usingDirectives = "";
        string remainingContent = content;

        if (usingMatches.Count > 0)
        {
            foreach (Match match in usingMatches)
            {
                usingDirectives += match.Value + "\n";
            }
            remainingContent = Regex.Replace(content, usingPattern, "").TrimStart();
        }

        // Remove any using statements inside the namespace
        remainingContent = Regex.Replace(remainingContent, @"\n\s*using\s+[\w.]+;\s*", "").TrimStart();

        string formattedContent = $"{usingDirectives}\nnamespace {namespaceName}\n{{\n{Indent(remainingContent)}\n}}";

        return formattedContent.Trim();
    }

    private static string Indent(string content)
    {
        string[] lines = content.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            lines[i] = "    " + lines[i]; // Indent each line by 4 spaces
        }
        return string.Join("\n", lines);
    }
}
