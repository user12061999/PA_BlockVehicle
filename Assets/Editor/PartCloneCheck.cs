using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using Gre.pjcode.Scenes.InGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PartCloneCheck
{
    [MenuItem("Tools/Playable/Check Cloned Parts")]
    public static void Run()
    {
        string[] names = { "caterpillar", "color_wheel", "jet_booster", "propeller", "rear_wing", "water_wheel", "wing", "wing_r" };
        int[] ids = { 0, 1, 2, 4, 3, 5, 6 };
        int[] shapes = { 9, 7, 10, 9, 6, 9, 3 };
        int[] rotations = { 3, 0, 0, 1, 2, 0, 0 };
        TerrainType[] terrains = { TerrainType.Dirt, TerrainType.Default, TerrainType.Default, TerrainType.Air, TerrainType.Default, TerrainType.Water, TerrainType.Air };
        float[] values = { .11f, .035f, .11f, .11f, .15f, .11f, .15f };
        var data = AssetDatabase.LoadAssetAtPath<PartDataAsset>("Assets/AddressableAssets/DataAsset/dat_part_playable.asset");
        Require(data != null, "Part data asset");
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            for (int i = 0; i < names.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Data/Prefab/Part/pfb_part_" + names[i] + ".prefab");
                Require(prefab != null && prefab.GetComponent<PartView>() != null, names[i] + " prefab script");
                if (i < ids.Length)
                {
                    Require(data.TryGetPartData(ids[i], out var part), names[i] + " data ID");
                    Require(part.GetPrefab(1) == prefab.GetComponent<PartView>() && part.GetMinoSprite(1) != null, names[i] + " asset bindings");
                    Require((int)part.ShapeType == shapes[i] && part.Rotate == rotations[i], names[i] + " shape/rotation");
                    Require(part.PerformanceData.TerrainType == terrains[i] && Mathf.Approximately(part.PerformanceData.Value, values[i]), names[i] + " source performance");
                }
                if (names[i] != "propeller" && names[i] != "water_wheel") continue;
                foreach (PartAttachSideType side in new[] { PartAttachSideType.Left, PartAttachSideType.Right })
                {
                    var instance = UnityEngine.Object.Instantiate(prefab);
                    SceneManager.MoveGameObjectToScene(instance, scene);
                    var view = instance.GetComponent<PartView>();
                    view.Initialize(ids[i], "part-clone-check", side);
                    var serialized = new SerializedObject(view);
                    var root = (Transform)serialized.FindProperty("_root").objectReferenceValue;
                    Require(root != null, names[i] + " rotation root");
                    if (names[i] == "water_wheel") Require(serialized.FindProperty("_splashEffect").objectReferenceValue != null, "Water splash binding");
                    var before = root.localRotation;
                    view.Activate(1f);
                    view.GetType().GetMethod("UpdateInternal", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, new object[] { .01f });
                    // Mirroring the left attachment changes the local rotation sign in Unity.
                    float angle = Quaternion.Angle(before, root.localRotation);
                    Require(Mathf.Abs(angle - (names[i] == "propeller" ? 12f : 4f)) < .1f, names[i] + " source rotation speed");
                    if (names[i] == "water_wheel")
                    {
                        var axis = (Vector3)view.GetType().GetField("_rotateAxis", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
                        Require(axis == (side == PartAttachSideType.Right ? Vector3.right : Vector3.left), "Water wheel side direction");
                    }
                    view.Inactivate();
                    Require(!view.enabled, names[i] + " deactivation");
                }
            }
            CheckGameplay();
            Debug.Log("PartCloneCheck PASS: 8 prefabs, 7 source data entries, propeller 1200 deg/s and water wheel 400 deg/s on both sides.");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    static void CheckGameplay()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/_Playable/Scenes/BlockVehiclePlayable.unity");
        try
        {
            var ui = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<InGamePuzzleUiView>(true)).Single();
            var serialized = new SerializedObject(ui);
            var data = (PartDataAsset)serialized.FindProperty("_partDataAsset").objectReferenceValue;
            Require(AssetDatabase.GetAssetPath(data).EndsWith("dat_part_playable.asset") && data.PartDataList.Count == 7, "Seven-part scene data binding");
            string[] names = { "caterpillar", "color_wheel", "jet_booster", "rear_wing", "propeller", "water_wheel", "wing" };
            int[] terrain = { 1, 0, 0, 0, 3, 2, 3 };
            int[] level1 = { 11, 3, 11, 15, 11, 11, 15 };
            int[] level2 = { 27, 12, 27, 35, 27, 27, 35 };
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var placed = (List<RuntimePuzzlePartIcon>)typeof(InGamePuzzleUiView).GetField("_placedParts", flags).GetValue(ui);
            var recalculate = typeof(InGamePuzzleUiView).GetMethod("UpdatePerformanceFromPlacedParts", flags);
            var preview = typeof(InGamePuzzleUiView).GetMethod("ShowPerformancePreview", flags);
            var scores = (int[])typeof(InGamePuzzleUiView).GetField("_performanceValues", flags).GetValue(ui);
            var weights = (float[])typeof(InGamePuzzleUiView).GetField("_runTerrainPerformances", flags).GetValue(ui);
            var labels = (Gre.UI.CustomText[])typeof(InGamePuzzleUiView).GetField("_performanceValueTexts", flags).GetValue(ui);
            for (int i = 0; i < names.Length; i++)
            {
                var part = data.PartDataList[i];
                Require(part.Prefab.gameObject.name == "pfb_part_" + names[i], "Purchase order: " + names[i]);
                var go = new GameObject("Part score check", typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(go, scene);
                var icon = go.AddComponent<RuntimePuzzlePartIcon>();
                typeof(RuntimePuzzlePartIcon).GetProperty("PartId").SetValue(icon, i);
                recalculate.Invoke(ui, null);
                preview.Invoke(ui, new object[] { icon });
                Require(labels[terrain[i]].text.Contains("+" + level1[i]), names[i] + " drag preview: " + labels[terrain[i]].text + " level=" + icon.Level + " placed=" + icon.IsPlaced + " value=" + part.PerformanceData.Value);
                placed.Add(icon);
                for (int level = 1; level <= 2; level++)
                {
                    typeof(RuntimePuzzlePartIcon).GetProperty("Level").SetValue(icon, level);
                    recalculate.Invoke(ui, null);
                    for (int channel = 0; channel < 4; channel++)
                    {
                        int expected = (channel == 0 ? 5 : 0) + (channel == terrain[i] ? (level == 1 ? level1[i] : level2[i]) : 0);
                        Require(scores[channel] == expected && labels[channel].text == expected.ToString(), names[i] + " level " + level + " terrain score " + channel);
                        float expectedWeight = (channel == 0 ? .05f : 0f) + (channel == terrain[i] ? part.PerformanceData.Value * level : 0f);
                        Require(Mathf.Approximately(weights[channel], expectedWeight), names[i] + " driving performance");
                    }
                }
                placed.Clear();
                recalculate.Invoke(ui, null);
                Require(scores.SequenceEqual(new[] { 5, 0, 0, 0 }), names[i] + " removal resets scores");
            }
            Debug.Log("Part gameplay PASS: seven-part asset, drag preview, placement, level 2 upgrades, driving weights and removal for all terrain channels.");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    static void Require(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException("PartCloneCheck: " + description);
    }
}


