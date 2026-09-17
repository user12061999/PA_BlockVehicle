using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class VehicleSurfaceCrossingCheck
{
    [MenuItem("Tools/Playable/Check Vehicle Surface Crossing")]
    public static void Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var root = new GameObject("SurfaceCrossingCheck");
        root.SetActive(false);
        var road = GameObject.CreatePrimitive(PrimitiveType.Plane);
        var car = new GameObject("TestSphere");
        try
        {
            Vector3 origin = new Vector3(10000f, 10000f, 10000f);
            road.transform.position = origin;
            var sphere = car.AddComponent<SphereCollider>();
            sphere.radius = 0.5f;
            car.transform.localScale = Vector3.one * 2f;
            var body = car.AddComponent<Rigidbody>();
            body.useGravity = false;
            var driver = root.AddComponent<PlayableBootstrap>();
            typeof(PlayableBootstrap).GetField("sphereBody", flags).SetValue(driver, body);
            var previous = typeof(PlayableBootstrap).GetField("previousPhysicsPosition", flags);
            var recover = typeof(PlayableBootstrap).GetMethod("RecoverCrossedSurface", flags);
            foreach (float slope in new[] { 0f, 25f })
            {
                road.transform.rotation = Quaternion.Euler(slope, 0f, 0f);
                Vector3 normal = road.transform.up;
                Vector3 tangent = road.transform.forward;
                previous.SetValue(driver, origin + normal * 3f - tangent);
                // Emulate a missed CCD step, traversing the thin mesh at high speed.
                body.position = origin - normal * 3f + tangent;
                body.linearVelocity = tangent * 100f - normal * 300f;
                Physics.SyncTransforms();
                recover.Invoke(driver, null);
                Require(Vector3.Dot(body.position - origin, normal) >= 1f, "Recover outside the road using world sphere radius");
                Require(Mathf.Abs(Vector3.Dot(body.linearVelocity, normal)) < 0.001f, "Remove inward velocity");
                Require(Mathf.Abs(Vector3.Dot(body.linearVelocity, tangent) - 100f) < 0.001f, "Preserve tangential speed");
            }
            road.SetActive(false);
            previous.SetValue(driver, origin + Vector3.up * 3f);
            Vector3 airborne = origin - Vector3.up * 3f;
            body.position = airborne;
            body.linearVelocity = Vector3.down * 300f;
            Physics.SyncTransforms();
            recover.Invoke(driver, null);
            Require(body.position == airborne && body.linearVelocity.y == -300f, "Do not snap across gaps");
            Debug.Log("VehicleSurfaceCrossingCheck PASS: flat/sloped mesh, scaled sphere, high speed and gaps.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(car);
            UnityEngine.Object.DestroyImmediate(road);
        }
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
