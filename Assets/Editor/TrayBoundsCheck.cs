using System;
using System.Reflection;
using Gre.pjcode.Scenes.InGame;
using UnityEditor;
using UnityEngine;

public static class TrayBoundsCheck
{
    [MenuItem("Tools/Playable/Check Tray Bounds")]
    public static void Run()
    {
        var home = new GameObject("Tray bounds check", typeof(RectTransform));
        try
        {
            var parent = (RectTransform)home.transform;
            parent.sizeDelta = new Vector2(200, 160);
            var root = (RectTransform)new GameObject("Part", typeof(RectTransform)).transform;
            root.SetParent(parent, false);
            root.sizeDelta = new Vector2(120, 80);
            var icon = root.gameObject.AddComponent<RuntimePuzzlePartIcon>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(RuntimePuzzlePartIcon).GetField("_rect", flags).SetValue(icon, root);
            typeof(RuntimePuzzlePartIcon).GetField("_homeParent", flags).SetValue(icon, parent);
            var child = (RectTransform)new GameObject("Offset rotated sprite", typeof(RectTransform)).transform;
            child.SetParent(root, false);
            child.sizeDelta = new Vector2(300, 100);
            child.localPosition = new Vector3(90, -50, 0);
            child.localRotation = Quaternion.Euler(0, 0, 35);
            var hidden = (RectTransform)new GameObject("Hidden", typeof(RectTransform)).transform;
            hidden.SetParent(root, false);
            hidden.sizeDelta = Vector2.one * 5000;
            hidden.gameObject.SetActive(false);
            for (int i = 0; i < 2; i++)
            {
                // Editor-only reference API; excluded from the Luna player.
                Bounds expected = RectTransformUtility.CalculateRelativeRectTransformBounds(root);
                float scale = Mathf.Min(1f, 176f / expected.size.x, 136f / expected.size.y);
                icon.FitInTray();
                if (Mathf.Abs(root.localScale.x - scale) > 0.0001f ||
                    Vector2.Distance(root.anchoredPosition, -(Vector2)expected.center * scale) > 0.001f)
                    throw new Exception("Tray bounds differ from Unity reference");
            }
            Debug.Log("TrayBoundsCheck passed: offset, rotation, inactive child and repeated fit");
        }
        finally { UnityEngine.Object.DestroyImmediate(home); }
    }
}
