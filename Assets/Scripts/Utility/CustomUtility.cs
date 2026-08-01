using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class CustomUtility
{
    private static readonly Dictionary<string, string> ColorFormatCache = new Dictionary<string, string>();
    private static readonly StringBuilder StringBuilder = new StringBuilder(256);

    /// <summary>
    /// Wraps text in Unity rich text color tags with caching for performance
    /// </summary>
    public static string ToColor(this string msg, string color)
    {
        if (string.IsNullOrEmpty(msg))
            return msg;

        if (string.IsNullOrEmpty(color))
        {
            Debug.LogWarning("[CustomUtility] Color parameter is null or empty");
            return msg;
        }

        try
        {
            if (!ColorFormatCache.TryGetValue(color, out string openTag))
            {
                openTag = $"<color={color}>";
                ColorFormatCache[color] = openTag;
            }

            StringBuilder.Clear();
            StringBuilder.Append(openTag);
            StringBuilder.Append(msg);
            StringBuilder.Append("</color>");
            return StringBuilder.ToString();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CustomUtility] Error formatting color: {ex.Message}");
            return msg;
        }
    }

    /// <summary>
    /// Null-safe string check with improved performance
    /// </summary>
    public static bool IsNullOrEmpty(this string msg)
    {
        return string.IsNullOrEmpty(msg);
    }

    /// <summary>
    /// Safe JSON serialization with error handling
    /// </summary>
    public static string ToJson(this object obj)
    {
        if (obj == null)
        {
            Debug.LogWarning("[CustomUtility] Attempting to serialize null object");
            return "null";
        }

        try
        {
            return JsonUtility.ToJson(obj);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CustomUtility] JSON serialization failed: {ex.Message}");
            return "{}";
        }
    }

    /// <summary>
    /// Safe JSON deserialization with error handling
    /// </summary>
    public static T FromJson<T>(this string data) where T : class
    {
        if (string.IsNullOrEmpty(data))
        {
            Debug.LogWarning("[CustomUtility] Attempting to deserialize null or empty JSON");
            return default(T);
        }

        try
        {
            return JsonUtility.FromJson<T>(data);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CustomUtility] JSON deserialization failed: {ex.Message}");
            return default(T);
        }
    }

    /// <summary>
    /// Clear color format cache to free memory
    /// </summary>
    public static void ClearColorCache()
    {
        ColorFormatCache.Clear();
    }
}