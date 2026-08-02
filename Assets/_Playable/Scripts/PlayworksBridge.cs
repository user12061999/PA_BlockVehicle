using System;
using System.Reflection;
using UnityEngine;

public static class PlayworksBridge
{
    public static void GameEnded()
    {
        Invoke("Luna.Unity.LifeCycle", "GameEnded");
    }

    public static void InstallFullGame()
    {
        Invoke("Luna.Unity.Playable", "InstallFullGame");
    }

    static void Invoke(string typeName, string methodName)
    {
        Type type = Type.GetType(typeName + ", Unity.Luna") ?? Type.GetType(typeName);
        MethodInfo method = type != null ? type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static) : null;
        if (method != null) method.Invoke(null, null);
        else Debug.Log("Playworks API not available in editor: " + typeName + "." + methodName);
    }
}
