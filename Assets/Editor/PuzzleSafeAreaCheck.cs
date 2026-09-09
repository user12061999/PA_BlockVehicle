using System;
using System.Linq;
using Gre.pjcode.Scenes.InGame;
using Gre.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class PuzzleSafeAreaCheck
{
    [MenuItem("Tools/Playable/Check Puzzle SafeArea")]
    public static void Run()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/_Playable/Scenes/BlockVehiclePlayable.unity");
        try
        {
            var puzzle = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<InGamePuzzleUiView>(true)).Single();
            var area = puzzle.transform.Find("SafeArea");
            string[] names = { "Board", "ScrollViewRoot", "BuyButton", "StartButton", "BoosterEvolve", "MergeGuide", "MinoDragLayer", "CarSecect", "BonusBox", "LuckySpin" };
            Require(area != null && area.childCount == names.Length, "SafeArea child count");
            for (int i = 0; i < names.Length; i++) Require(area.GetChild(i).name == names[i], "SafeArea order: " + names[i]);
            var view = new SerializedObject(puzzle);
            foreach (string field in new[] { "_board", "_minoListRoot", "_gridRoot", "_buyButton", "_playButton", "_autoMergeButton", "_buyPartPriceText", "_bonusBoxOpenButton" })
                Require(view.FindProperty(field).objectReferenceValue != null, "Controller binding: " + field);
            Require(view.FindProperty("_buyButton").objectReferenceValue == area.Find("BuyButton").GetComponent<CustomButton>(), "Buy button binding");
            Require(view.FindProperty("_playButton").objectReferenceValue == area.Find("StartButton").GetComponent<CustomButton>(), "Start button binding");
            var scroll = area.GetComponentInChildren<ScrollRect>(true);
            Require(scroll != null && scroll.horizontal && !scroll.vertical && scroll.viewport != null && scroll.content != null, "Horizontal tray");
            Require(scroll.content == view.FindProperty("_minoListRoot").objectReferenceValue, "Tray content binding");
            foreach (var button in area.GetComponentsInChildren<CustomButton>(true))
                if (!button.transform.IsChildOf(area.Find("Board"))) Require(button.targetGraphic != null && button.interactable, "Button graphic: " + button.name);
            foreach (var badge in area.GetComponentsInChildren<Gre.pjcode.Ui.NotificationBadge>(true))
            {
                var settings = new SerializedObject(badge);
                Require(settings.FindProperty("_flash").objectReferenceValue != null && settings.FindProperty("_cycleDuration").floatValue > 0, "Badge animation settings");
            }
            typeof(InGamePuzzleUiView).GetMethod("BuildRuntimePuzzle", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(puzzle, null);
            var gridSize = view.FindProperty("_runtimeGridSize").vector2IntValue;
            var grid = (RectTransform)view.FindProperty("_gridRoot").objectReferenceValue;
            Require(grid.childCount == gridSize.x * gridSize.y, "Runtime grid construction");
            puzzle.SetBuyPrice(123);
            Require(((TMPro.TMP_Text)view.FindProperty("_buyPartPriceText").objectReferenceValue).text == "123", "Runtime price update");
            Canvas.ForceUpdateCanvases();
            Debug.Log("PuzzleSafeAreaCheck PASS: hierarchy, button targets, runtime bindings, tray, badge dependencies, runtime grid and price update.");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("PuzzleSafeAreaCheck: " + message);
    }
}

