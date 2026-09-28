using System;
using UnityEditor;
using UnityEngine;

public static class PlayableTutorialGuideCheck
{
    [MenuItem("Tools/Playable/Check Tutorial Drive Limit")]
    public static void Run()
    {
        Require(PlayableTutorialGuide.ShouldShowTutorial(0), "Tutorial appears before the first drive");
        Require(PlayableTutorialGuide.ShouldShowTutorial(2), "Tutorial remains through the third drive");
        Require(!PlayableTutorialGuide.ShouldShowTutorial(3), "Tutorial ends after the third drive");
        Require(Resources.Load<Sprite>("tutorial_finger") != null, "tutorial_finger is available from Resources");
        Debug.Log("PlayableTutorialGuideCheck passed");
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
