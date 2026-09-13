using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gre.pjcode.Scenes.InGame;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class AutoMergeBoosterCheck
{
    [MenuItem("Tools/Playable/Check Auto Merge And Booster (Play Mode)")]
    public static void Run()
    {
        Require(Application.isPlaying, "Start a fresh Play Mode session");
        var ui = UnityEngine.Object.FindObjectOfType<InGamePuzzleUiView>(true);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = typeof(InGamePuzzleUiView);
        var parts = (List<RuntimePuzzlePartIcon>)type.GetField("_runtimeParts", flags).GetValue(ui);
        Require(parts.Count == 0, "Fresh inventory required");
        var cells = (List<RectTransform>)type.GetField("_runtimeCells", flags).GetValue(ui);
        float pitch = Vector2.Distance(cells[0].anchoredPosition, cells[1].anchoredPosition);
        var create = type.GetMethod("CreateTrayPart", flags);
        var camera = (Camera)type.GetMethod("GetUiCamera", flags).Invoke(ui, null);
        for (int i = 0; i < 4; i++)
        {
            create.Invoke(ui, new object[] { 0, pitch });
            if (i >= 2) continue;
            var part = parts.Last();
            int index = Enumerable.Range(0, cells.Count).First(c => (bool)type.GetMethod("CanPlace", flags).Invoke(ui, new object[] { part, new Vector2Int(c % ui.GridSize.x, c / ui.GridSize.x) }));
            type.GetMethod("DropPart", flags).Invoke(ui, new object[] { part, RectTransformUtility.WorldToScreenPoint(camera, cells[index].position) });
            Require(part.IsPlaced, "Two parts on grid");
        }
        var button = (Button)type.GetField("_autoMergeButton", flags).GetValue(ui);
        Require(button.gameObject.activeInHierarchy && button.interactable, "Auto merge button available");
        button.onClick.Invoke();
        Require(parts.Count == 1 && parts[0].Level == 3 && parts[0].IsPlaced, "Merge grid pair, tray pair, then mixed pair; keep grid target");
        var occupied = (Dictionary<int, RuntimePuzzlePartIcon>)type.GetField("_occupiedCells", flags).GetValue(ui);
        Require(occupied.Count == parts[0].Pattern.Count && occupied.Values.All(p => p == parts[0]), "Occupancy rebuilt correctly");
        create.Invoke(ui, new object[] { 0, pitch });
        create.Invoke(ui, new object[] { 1, pitch });
        button.onClick.Invoke();
        Require(parts.Count == 3 && parts[0].Level == 3, "Different levels/types remain unchanged");

        type.GetMethod("SetGold", flags).Invoke(ui, new object[] { 100000 });
        ui.SetBoostLevel(0, true);
        var root = (GameObject)type.GetField("_boostEvolveRoot", flags).GetValue(ui);
        var meter = root.transform.Find("LevelMeter");
        var upgrade = (Button)type.GetField("_boostEvolveButton", flags).GetValue(ui);
        for (int level = 0; level <= 5; level++)
        {
            Require(ui.BoostLevel == level, "Booster level matches upgrade count");
            for (int i = 0; i < meter.childCount; i++)
                Require(meter.GetChild(i).Find(meter.GetChild(i).name).gameObject.activeSelf == (i < level), "One green Scale per level");
            upgrade.onClick.Invoke();
        }
        Require(ui.BoostLevel == 5 && !upgrade.gameObject.activeSelf, "Five-level cap");
        Debug.Log("AutoMergeBoosterCheck passed: grid/tray/mixed chain, unmatched parts, occupancy, five booster scales and cap.");
    }

    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
