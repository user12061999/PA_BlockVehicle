using System;
using System.Reflection;
using Gre.pjcode.Scenes.InGame;
using UnityEditor;
using UnityEngine;

public static class TerrainRunCheck
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
    static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, Flags).GetValue(target);
    static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags).Invoke(target, args);
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }

    [MenuItem("Tools/Playable/Check Terrain Run")]
    public static void Run()
    {
        var root = new GameObject("Terrain check");
        root.SetActive(false);
        var driver = root.AddComponent<PlayableBootstrap>();
        var ui = root.AddComponent<InGamePuzzleUiView>();
        var car = new GameObject("Test car");
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var zone = new GameObject("Terrain zone");
        try
        {
            car.transform.SetParent(root.transform);
            Set(driver, "vehicle", car.transform);
            Set(driver, "puzzleUi", ui);
            var values = Get<float[]>(ui, "_runTerrainPerformances");
            Vector3 start = new Vector3(10000f, 10000.08f, 10000f);
            ground.transform.position = start - Vector3.up * 0.58f;
            ground.transform.localScale = new Vector3(100f, 1f, 100f);
            zone.transform.position = start;
            var volume = zone.AddComponent<BoxCollider>();
            volume.size = new Vector3(100f, 2f, 100f);
            volume.isTrigger = true;
            Physics.SyncTransforms();
            Action reset = () => {
                car.transform.SetPositionAndRotation(start, Quaternion.identity);
                Set(driver, "speed", 30f); Set(driver, "verticalSpeed", 0f);
                Set(driver, "grounded", true); Set(driver, "runningTerrain", TerrainType.Max);
            };
            Func<float> step = () => { Call(driver, "MoveRun", 0.1f); return car.transform.position.z - start.z; };
            reset(); float road = step();
            values[(int)TerrainType.Default] = 0.15f;
            reset(); Require(step() > road, "Default parts must improve road speed");
            values[(int)TerrainType.Default] = 0f;
            zone.tag = "Dirt";
            reset(); float dirt = step();
            Require(Get<TerrainType>(driver, "runningTerrain") == TerrainType.Dirt && dirt < road, "Dirt trigger must slow the car");
            values[(int)TerrainType.Dirt] = 0.11f;
            reset(); Require(step() > dirt, "Caterpillar must improve Dirt speed");
            zone.tag = "Water";
            reset(); float water = step();
            Require(water < dirt, "Water must slow the car more than Dirt");
            values[(int)TerrainType.Dirt] = 0f;
            reset(); Require(Mathf.Abs(step() - water) < 0.002f, "Dirt performance must not improve Water");
            values[(int)TerrainType.Water] = 0.11f;
            reset(); Require(step() > water, "Water wheel must improve Water speed");
            ground.tag = "Water";
            RaycastHit hit;
            Require(Physics.Raycast(ground.transform.position - Vector3.forward * 60f, Vector3.forward, out hit, 20f, ~0, QueryTriggerInteraction.Ignore), "Water side ray must hit");
            Require(!(bool)Call(driver, "IsObstacleHit", hit), "Water surface must never bounce the car");
            ground.tag = "Untagged";
            Require((bool)Call(driver, "IsObstacleHit", hit), "A solid wall must remain an obstacle");
            ground.transform.position -= Vector3.up * 10f;
            Physics.SyncTransforms();
            reset(); step();
            Require(!Get<bool>(driver, "grounded") && car.transform.position.y > start.y - 0.1f, "A drop must release the car instead of snapping down");
            Call(driver, "MoveRun", 0.1f);
            float fall = car.transform.position.y;
            Require(Get<TerrainType>(driver, "runningTerrain") == TerrainType.Air, "Flight must use Air performance");
            values[(int)TerrainType.Air] = 0.15f;
            reset(); step(); Call(driver, "MoveRun", 0.1f);
            Require(car.transform.position.y > fall, "Wing must reduce falling speed");
            for (int i = 0; i < 60 && !Get<bool>(driver, "grounded"); i++) Call(driver, "MoveRun", 0.02f);
            Require(Get<bool>(driver, "grounded"), "Airborne car must land on the lower surface");
            zone.SetActive(false);
            ground.transform.position = start - Vector3.up * 0.58f;
            ground.transform.localScale = new Vector3(20f, 1f, 6f);
            ground.transform.rotation = Quaternion.Euler(-20f, 0f, 0f);
            Physics.SyncTransforms();
            reset(); Call(driver, "SnapToGround", Vector3.forward, true);
            for (int i = 0; i < 30 && Get<bool>(driver, "grounded"); i++) Call(driver, "MoveRun", 0.02f);
            Require(!Get<bool>(driver, "grounded") && Get<float>(driver, "verticalSpeed") > 0f, "Ramp exit must retain upward momentum");
            var stage = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AddressableAssets/Stage/pfb_stage_001.prefab");
            int waters = 0;
            foreach (var c in stage.GetComponentsInChildren<Collider>(true))
                if (c.name == "Water" || c.name == "Water_1") {
                    Require(c.CompareTag("Water") && c.gameObject.layer == LayerMask.NameToLayer("Road"), "Stage water needs Water tag and ground layer");
                    waters++;
                }
            Require(waters == 2, "Both stage water surfaces checked");
            Debug.Log("TerrainRunCheck PASS: Dirt/Water slowdown, matching parts, water collision, takeoff, Air lift and landing.");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(ground); UnityEngine.Object.DestroyImmediate(zone); }
    }
}
