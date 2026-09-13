using System;
using Gre.pjcode.Common.VirtualStick;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

public static class VirtualStickCheck
{
    [MenuItem("Tools/Playable/Check Virtual Stick")]
    public static void Run()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Data/Prefab/UiView/InGame/pfb_virtual_stick.prefab");
        var instance = UnityEngine.Object.Instantiate(prefab);
        try
        {
            var stick = instance.GetComponent<VirtualStick>();
            var group = instance.GetComponent<CanvasGroup>();
            var drag = instance.GetComponentInChildren<DragHandler>();
            drag.SendMessage("Awake");
            var pointer = new PointerEventData(EventSystem.current) { pointerId = 0, position = new Vector2(200, 200) };
            stick.SetRunning(false);
            drag.OnPointerDown(pointer);
            drag.OnDrag(pointer);
            Require(group.alpha == 0 && !group.blocksRaycasts, "Hidden and no input outside a run");
            stick.SetRunning(true);
            drag.OnPointerDown(pointer);
            Require(group.alpha == 0 && group.blocksRaycasts, "Wait for drag while still receiving input");
            pointer.position += Vector2.right * 500;
            drag.OnBeginDrag(pointer);
            Require(group.alpha == 1 && Mathf.Approximately(stick.Direction.x, 1), "Drag shows joystick and clamps right input");
            var other = new PointerEventData(EventSystem.current) { pointerId = 1, position = Vector2.zero };
            drag.OnPointerDown(other);
            drag.OnDrag(other);
            drag.OnPointerUp(other);
            Require(Mathf.Approximately(stick.Direction.x, 1), "Second finger cannot steal or release steering");
            pointer.position = new Vector2(-300, 200);
            drag.OnDrag(pointer);
            Require(Mathf.Approximately(stick.Direction.x, -1), "Drag left steers left");
            drag.OnPointerUp(pointer);
            drag.OnEndDrag(pointer);
            Require(group.alpha == 0 && stick.Direction == Vector2.zero, "Release hides and resets steering");
            drag.OnPointerDown(pointer);
            pointer.position += Vector2.up * 500;
            drag.OnDrag(pointer);
            stick.SetRunning(false);
            Require(group.alpha == 0 && stick.Direction == Vector2.zero && !group.blocksRaycasts, "Finishing during drag resets input");
            Debug.Log("VirtualStickCheck passed");
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
