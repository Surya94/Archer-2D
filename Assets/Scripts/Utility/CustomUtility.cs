using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class CustomUtility
{
    private static string colorMsgFormat = "<color={0}>{1}</color>";
    public static string ToColor(this string msg, string color)
    {
        return string.Format(colorMsgFormat, color, msg);
    }

    public static bool IsNullOrEmpty(this string msg)
    {
        return string.IsNullOrEmpty(msg);
    }

    public static string ToJson(this object obj)
    {
        return JsonUtility.ToJson(obj);
    }
    public static T FromJson<T>(this string data)
    {
        return JsonUtility.FromJson<T>(data);
    }

}
