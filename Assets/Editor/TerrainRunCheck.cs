using System;
using System.Reflection;
using Gre.pjcode.Scenes.InGame;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TerrainRunCheck
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
    static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags).Invoke(target, args);
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }

    [MenuItem("Tools/Playable/Check Terrain Run")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Enter Play Mode before running this physics check.");
        Scene scene = SceneManager.CreateScene("Vehicle physics check", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        try
        {
            var root = new GameObject("Check driver");
            SceneManager.MoveGameObjectToScene(root, scene);
            root.SetActive(false);
            var driver = root.AddComponent<PlayableBootstrap>();
            var ui = root.AddComponent<InGamePuzzleUiView>();
            Set(driver, "puzzleUi", ui);
            var values = (float[])typeof(InGamePuzzleUiView).GetField("_runTerrainPerformances", Flags).GetValue(ui);
            var visual = new GameObject("Visual");
            SceneManager.MoveGameObjectToScene(visual, scene);
            Set(driver, "vehicle", visual.transform);
            Rigidbody actual = CreateBody(scene, "Actual");
            Rigidbody reference = CreateBody(scene, "Reference");
            var contacts = actual.gameObject.AddComponent<CarTerrainCollider>();
            Set(driver, "sphereBody", actual);
            Set(driver, "terrainCollider", contacts);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            SceneManager.MoveGameObjectToScene(ground, scene);
            ground.transform.localScale = new Vector3(100f, 1f, 1000f);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            var zone = new GameObject("Terrain");
            SceneManager.MoveGameObjectToScene(zone, scene);
            var volume = zone.AddComponent<BoxCollider>();
            volume.isTrigger = true;
            volume.size = new Vector3(100f, 10f, 1000f);
            PhysicsScene physics = scene.GetPhysicsScene();
            foreach (TerrainType terrain in new[] { TerrainType.Default, TerrainType.Dirt, TerrainType.Water, TerrainType.Air })
            foreach (bool equipped in new[] { false, true })
            {
                Array.Clear(values, 0, values.Length);
                if (equipped) values[(int)terrain] = 0.11f;
                contacts.ResetContacts();
                zone.tag = terrain == TerrainType.Dirt ? "Dirt" : terrain == TerrainType.Water ? "Water" : "Untagged";
                ground.SetActive(terrain != TerrainType.Air);
                actual.position = new Vector3(-10f, terrain == TerrainType.Air ? 30f : 1f, 0f);
                reference.position = actual.position + Vector3.right * 20f;
                actual.linearVelocity = reference.linearVelocity = Vector3.zero;
                actual.angularVelocity = reference.angularVelocity = Vector3.zero;
                actual.linearDamping = reference.linearDamping = 0.6f;
                actual.rotation = reference.rotation = Quaternion.identity;
                Physics.SyncTransforms();
                physics.Simulate(0.02f);
                float weight = 0.45f * 1.2f + (equipped ? 0.11f * (terrain == TerrainType.Default ? 0.075f : 0.065f) : 0f);
                if (!equipped) weight *= 0.7f;
                Require(Mathf.Abs((float)Call(driver, "GetForceWeight", 1.2f) - weight) < 0.00001f, "Original launch force, " + terrain);
                Call(driver, "ApplyDash", Vector3.forward * 600f, 1.2f);
                reference.AddForce(Vector3.forward * (600f * weight), ForceMode.Impulse);
                reference.AddTorque(Vector3.right * (600f * weight), ForceMode.Impulse);
                for (int frame = 0; frame < 150; frame++)
                {
                    if (frame == 40)
                    {
                        float dashWeight = 0.45f + (equipped ? 0.11f * (terrain == TerrainType.Default ? 0.075f : 0.065f) : 0f);
                        if (!equipped) dashWeight *= 0.7f;
                        Call(driver, "ApplyDash", Vector3.forward * 210f, 1f);
                        reference.AddForce(Vector3.forward * (210f * dashWeight), ForceMode.Impulse);
                        reference.AddTorque(Vector3.right * (210f * dashWeight), ForceMode.Impulse);
                    }
                    // Independently replay the original InGameStateRun / CarSphereTracer force rules.
                    TerrainType current = contacts.IsGrounded ? contacts.Terrain : TerrainType.Air;
                    float performance = values[(int)current];
                    float damping = current == TerrainType.Air ? 0.01f : 0.6f;
                    if (current == TerrainType.Dirt) { damping += 0.05f; performance *= 1.5f; }
                    if (current == TerrainType.Water) { damping += 0.075f; performance *= 2.5f; }
                    reference.linearDamping = Mathf.Max(damping - performance, 0.01f);
                    reference.AddForce(Physics.gravity * (1.5f * (contacts.IsGrounded ? 3f : 3f - values[(int)TerrainType.Air] * 2f)), ForceMode.Acceleration);
                    if (reference.linearVelocity.sqrMagnitude < 49f)
                        reference.linearVelocity = Vector3.MoveTowards(reference.linearVelocity, Vector3.zero, 0.2f);
                    else
                    {
                        Vector3 direction = reference.linearVelocity.normalized;
                        float slope = Vector3.Angle(Vector3.up, direction) - 90f;
                        if (slope > 0f) reference.AddForce(direction * Mathf.Lerp(0f, 15f, slope / 90f), ForceMode.Force);
                    }
                    Call(driver, "StepPhysics", 0.02f);
                    physics.Simulate(0.02f);
                    Require(Vector3.Distance(actual.linearVelocity, reference.linearVelocity) < 0.002f, "Velocity diverged from original rules: " + terrain + " frame " + frame);
                    Require(Vector3.Distance(actual.position + Vector3.right * 20f, reference.position) < 0.003f, "Trajectory diverged from original rules: " + terrain);
                }
            }
            // Crossing a zone changes drag only. Leaving must never restore lost speed.
            ground.SetActive(true);
            Array.Clear(values, 0, values.Length);
            actual.position = new Vector3(-10f, 1f, 0f);
            actual.linearVelocity = Vector3.forward * 30f;
            Physics.SyncTransforms();
            physics.Simulate(0.02f);
            foreach (string tag in new[] { "Dirt", "Water" })
            {
                volume.enabled = false;
                physics.Simulate(0.02f);
                zone.tag = tag;
                volume.enabled = true;
                Physics.SyncTransforms();
                physics.Simulate(0.02f);
                Require(contacts.Terrain.ToString() == tag, "Trigger entry must select " + tag);
                float before = actual.linearVelocity.magnitude;
                Call(driver, "StepPhysics", 0.02f);
                Require(Mathf.Abs(actual.linearVelocity.magnitude - before) < 0.0001f, "Entry must not multiply velocity");
                physics.Simulate(0.02f);
                volume.enabled = false;
                physics.Simulate(0.02f);
                before = actual.linearVelocity.magnitude;
                Call(driver, "StepPhysics", 0.02f);
                Require(Mathf.Abs(actual.linearVelocity.magnitude - before) < 0.0001f, "Exit must not multiply velocity");
            }
            actual.linearVelocity = new Vector3(0f, 0f, 30f);
            Set(driver, "steer", 1f);
            Call(driver, "ControlSphere", 0.02f);
            Require(Mathf.Abs(actual.linearVelocity.magnitude - 30f) < 0.0001f && Vector3.Angle(Vector3.forward, actual.linearVelocity) <= 15f, "Steering preserves speed and original angle limit");
            Vector3 physicalPosition = actual.position;
            Vector3 physicalVelocity = actual.linearVelocity;
            visual.transform.position = Vector3.one * -100f;
            Call(driver, "UpdateVehiclePresentation", 1f / 120f);
            Require(Vector3.Distance(visual.transform.position, actual.transform.position) < 0.0001f, "Visual must follow the rendered Rigidbody transform");
            Require(actual.position == physicalPosition && actual.linearVelocity == physicalVelocity, "Render updates must not change physics");
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            SceneManager.MoveGameObjectToScene(wall, scene);
            wall.transform.localScale = new Vector3(10f, 10f, 1f);
            wall.transform.position = actual.position + Vector3.forward * 4f;
            actual.linearVelocity = Vector3.forward * 30f;
            Physics.SyncTransforms();
            for (int i = 0; i < 12; i++) physics.Simulate(0.02f);
            Require(actual.position.z < wall.transform.position.z - 1f && actual.linearVelocity.z < 1f, "Native sphere contact must stop/deflect at a wall without tunneling");
            var pad = new GameObject("pfb_dash_board_test");
            SceneManager.MoveGameObjectToScene(pad, scene);
            var padTrigger = new GameObject("Cube");
            padTrigger.transform.SetParent(pad.transform);
            padTrigger.tag = "Dash";
            var padCollider = padTrigger.AddComponent<BoxCollider>();
            padCollider.isTrigger = true;
            var boost = new GameObject("pfb_boost_unlock");
            SceneManager.MoveGameObjectToScene(boost, scene);
            boost.tag = "BoostUnlock";
            var boostCollider = boost.AddComponent<BoxCollider>();
            boostCollider.isTrigger = true;
            var state = typeof(PlayableBootstrap).GetField("state", Flags);
            state.SetValue(driver, Enum.Parse(state.FieldType, "Run"));
            var evolve = new GameObject("BoosterEvolve");
            evolve.transform.SetParent(root.transform);
            evolve.SetActive(false);
            Set(ui, "_boostEvolveRoot", evolve);
            Require(!(bool)Call(driver, "TryCollectBoardUpgrade", padCollider), "Dash board must not expand the grid");
            Require(!(bool)Call(driver, "TryCollectBoost", padCollider), "Dash board must not unlock BoosterEvolve");
            Require(!(bool)Call(driver, "TryCollectBoardUpgrade", boostCollider), "Boost unlock must not expand the grid");
            actual.position = Vector3.one * 200f;
            actual.useGravity = false;
            actual.linearDamping = 0f;
            actual.linearVelocity = Vector3.zero;
            Call(driver, "TryCollectBoost", boostCollider);
            Call(driver, "TryCollectBoost", boostCollider);
            physics.Simulate(0.02f);
            Require(actual.linearVelocity == Vector3.zero && !boost.activeSelf && evolve.activeSelf, "Boost pickup unlocks BoosterEvolve without adding speed");
            Call(driver, "TryTriggerDash", padCollider);
            Call(driver, "TryTriggerDash", padCollider);
            physics.Simulate(0.02f);
            float expectedBoost = 210f * (float)Call(driver, "GetForceWeight", 1f) / actual.mass;
            Require(Mathf.Abs(actual.linearVelocity.z - expectedBoost) < 0.001f && pad.activeSelf, "Dash pad applies one impulse per entry and remains on map");
            Call(driver, "HandlePhysicsTriggerExit", padCollider);
            Call(driver, "TryTriggerDash", padCollider);
            physics.Simulate(0.02f);
            Require(Mathf.Abs(actual.linearVelocity.z - 2f * expectedBoost) < 0.001f, "Re-entering dash pad boosts again");
            var attachment = new GameObject("pfb_attachment");
            SceneManager.MoveGameObjectToScene(attachment, scene);
            attachment.tag = "Attachment";
            var attachmentCollider = attachment.AddComponent<BoxCollider>();
            attachmentCollider.isTrigger = true;
            Call(driver, "TryCollectBoardUpgrade", attachmentCollider);
            Call(driver, "TryCollectBoardUpgrade", attachmentCollider);
            Require((int)typeof(PlayableBootstrap).GetField("pendingBoardColumns", Flags).GetValue(driver) == 1 && !attachment.activeSelf, "Only attachment awards one grid column");
            Call(driver, "RestoreCoins");
            Require(attachment.activeSelf && pad.activeSelf && !boost.activeSelf && evolve.activeSelf, "Reset preserves booster unlock and restores attachment");
            ui.SetBoostLevel(0, false);
            Call(ui, "SetGold", 1000);
            Call(ui, "UpgradeBooster");
            Require(ui.BoostLevel == 0, "Booster upgrade stays locked before pickup");
            ui.SetBoostLevel(0, true);
            Call(ui, "UpgradeBooster");
            Require(ui.BoostLevel == 1 && (int)typeof(InGamePuzzleUiView).GetField("_gold", Flags).GetValue(ui) == 1000, "First booster level is free");
            Call(ui, "UpgradeBooster");
            Require(ui.BoostLevel == 1, "Insufficient gold cannot upgrade");
            Call(ui, "SetGold", 2000);
            Call(ui, "UpgradeBooster");
            Require(ui.BoostLevel == 2 && (int)typeof(InGamePuzzleUiView).GetField("_gold", Flags).GetValue(ui) == 0, "Level two costs 2000 gold");
            Call(ui, "SetGold", 100000);
            for (int i = 0; i < 10; i++) Call(ui, "UpgradeBooster");
            Require(ui.BoostLevel == 6 && (int)typeof(InGamePuzzleUiView).GetField("_gold", Flags).GetValue(ui) == 74500, "Booster respects original upgrade prices and level-six cap");
            foreach (int level in new[] { 1, 6 })
            {
                ui.SetBoostLevel(level, true);
                Set(driver, "boosterUsed", false);
                actual.linearVelocity = Vector3.zero;
                visual.transform.rotation = Quaternion.identity;
                Call(driver, "UseBooster");
                Call(driver, "UseBooster");
                physics.Simulate(0.02f);
                float expected = 600f * (0.5f + (level - 1) * 0.1f) * (float)Call(driver, "GetForceWeight", 1f) / actual.mass;
                Require(Mathf.Abs(actual.linearVelocity.z - expected) < 0.001f, "Manual boost adds exactly one level-scaled impulse per run");
            }
            var stage = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AddressableAssets/Stage/pfb_stage_001.prefab");
            foreach (Collider c in stage.GetComponentsInChildren<Collider>(true))
                if (c.name == "Water" || c.name == "Water_1") Require(c.CompareTag("Water") && c.gameObject.layer == LayerMask.NameToLayer("Road"), "Water ground configuration");
            Debug.Log("TerrainRunCheck PASS: 8 original-rule Rigidbody trajectories with launch and dash, terrain entry/exit continuity, steering, wall contact and water configuration.");
        }
        finally
        {
            foreach (GameObject root in scene.GetRootGameObjects()) UnityEngine.Object.DestroyImmediate(root);
            SceneManager.UnloadSceneAsync(scene);
        }
    }

    static Rigidbody CreateBody(Scene scene, string name)
    {
        var go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, scene);
        var collider = go.AddComponent<SphereCollider>();
        collider.radius = 1f;
        collider.sharedMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/Data/Material/pmt_car_sphere.physicMaterial");
        var body = go.AddComponent<Rigidbody>();
        body.mass = 5f;
        body.angularDamping = 0.01f;
        body.collisionDetectionMode = CollisionDetectionMode.Continuous;
        return body;
    }
}
