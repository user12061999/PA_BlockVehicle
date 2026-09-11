using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gre.pjcode.Scenes.InGame;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class BoardRewardCheck
{
    [MenuItem("Tools/Playable/Check Board Rewards (Play Mode)")]
    public static void Run()
    {
        Require(Application.isPlaying, "Start a fresh Play Mode session first");
        var ui = UnityEngine.Object.FindObjectOfType<InGamePuzzleUiView>(true);
        var bootstrap = UnityEngine.Object.FindObjectOfType<PlayableBootstrap>();
        var reward = UnityEngine.Object.FindObjectOfType<InGameGetItemUiView>(true);
        Require(ui.GridSize == new Vector2Int(4, 4), "Fresh 4x4 board");
        Require(Enumerable.Range(0, 4).All(i => ui.GetAttachmentButton(i) != null && ui.GetAttachmentButton(i).gameObject.activeSelf), "Four locked-column buttons beside the 4x4 board");
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var state = typeof(PlayableBootstrap).GetField("state", flags);
        var collect = typeof(PlayableBootstrap).GetMethod("TryCollectBoardUpgrade", flags);
        var claim = typeof(PlayableBootstrap).GetMethod("ClaimResultAndReset", flags);
        var pending = typeof(PlayableBootstrap).GetField("pendingBoardColumns", flags);
        var pickup = UnityEngine.Object.FindObjectsOfType<Transform>(true).First(t => t.CompareTag("Attachment")).GetComponentInChildren<Collider>(true);
        Require(pickup != null, "Existing attachment pickup collider");
        ui.BuyButton.onClick.Invoke();
        var parts = (List<RuntimePuzzlePartIcon>)typeof(InGamePuzzleUiView).GetField("_runtimeParts", flags).GetValue(ui);
        var cells = (List<RectTransform>)typeof(InGamePuzzleUiView).GetField("_runtimeCells", flags).GetValue(ui);
        Require(parts.Count == 1, "Buy one part before expanding");
        var camera = (Camera)typeof(InGamePuzzleUiView).GetMethod("GetUiCamera", flags).Invoke(ui, null);
        typeof(InGamePuzzleUiView).GetMethod("DropPart", flags).Invoke(ui, new object[] { parts[0], RectTransformUtility.WorldToScreenPoint(camera, cells[1].position) });
        Require(parts[0].IsPlaced, "Part is on board");
        var part = parts[0];
        var scores = ((int[])typeof(InGamePuzzleUiView).GetField("_performanceValues", flags).GetValue(ui)).ToArray();
        int gold = (int)typeof(InGamePuzzleUiView).GetField("_gold", flags).GetValue(ui);
        Vector2 originalPosition = ((RectTransform)part.transform).anchoredPosition;
        Vector2[] originalCells = cells.Select(cell => cell.anchoredPosition).ToArray();
        for (int width = 5; width <= 8; width++)
        {
            state.SetValue(bootstrap, Enum.Parse(state.FieldType, "Run"));
            collect.Invoke(bootstrap, new object[] { pickup });
            collect.Invoke(bootstrap, new object[] { pickup });
            Require((int)pending.GetValue(bootstrap) == 1 && !pickup.gameObject.activeInHierarchy, "Pickup counted exactly once");
            Require(ui.GridSize.x == width - 1, "Reward pending until result claim");
            state.SetValue(bootstrap, Enum.Parse(state.FieldType, "Done"));
            claim.Invoke(bootstrap, null);
            Require(ui.GridSize == new Vector2Int(width, 4) && cells.Count == width * 4, "One new column");
            Require(Enumerable.Range(0, 4).All(i => ui.GetAttachmentButton(i).gameObject.activeSelf == (i >= width - 4)), "Only the corresponding column button disappears");
            int addedLeft = (width - 4 + 1) / 2;
            for (int y = 0; y < 4; y++)
                for (int x = 0; x < 4; x++)
                    Require(Vector2.Distance(cells[y * width + x + addedLeft].anchoredPosition, originalCells[y * 4 + x]) < .01f, "Original 4x4 cells remain fixed at width " + width);
            Require(Vector2.Distance(((RectTransform)part.transform).anchoredPosition, originalPosition) < .01f, "Part does not shift at width " + width);
            Require(parts.Contains(part) && part.IsPlaced, "Preserve placed part");
            var occupied = (Dictionary<int, RuntimePuzzlePartIcon>)typeof(InGamePuzzleUiView).GetField("_occupiedCells", flags).GetValue(ui);
            Require(occupied.Count == part.Pattern.Count && occupied.Values.All(p => p == part), "Reindex occupied cells");
            Require(scores.SequenceEqual((int[])typeof(InGamePuzzleUiView).GetField("_performanceValues", flags).GetValue(ui)), "Preserve performance");
            Require((int)typeof(InGamePuzzleUiView).GetField("_gold", flags).GetValue(ui) == gold, "Preserve gold");
            Require(reward.gameObject.activeInHierarchy && ui.gameObject.activeInHierarchy, "Reward displayed over PuzzleUI");
            Require(reward.GetComponentsInChildren<Text>().Any(t => t.text.Contains((width - 1) + " x 4  >  " + width + " x 4")), "Reward reports actual size change");
            claim.Invoke(bootstrap, null);
            Require(ui.GridSize.x == width, "Duplicate claim ignored");
            reward.GetComponentInChildren<Button>().onClick.Invoke();
            Require(!reward.gameObject.activeSelf, "Continue dismisses reward");
        }
        Require(Vector2.Distance(((RectTransform)part.transform).anchoredPosition, originalPosition) < .01f, "Balanced growth keeps original part center at 8 columns");
        state.SetValue(bootstrap, Enum.Parse(state.FieldType, "Run"));
        collect.Invoke(bootstrap, new object[] { pickup });
        Require((int)pending.GetValue(bootstrap) == 0, "No unusable reward at maximum size");
        state.SetValue(bootstrap, Enum.Parse(state.FieldType, "Done"));
        claim.Invoke(bootstrap, null);
        Require(ui.GridSize == new Vector2Int(8, 4) && !reward.gameObject.activeSelf && ui.ExpandBoard(-1) == 0, "8x4 limit and no false reward");
        Debug.Log("BoardRewardCheck PASS: pickups, duplicate contact/claim, 4x4 to 8x4, left/right alternation and fixed original cells, preserved parts/stats/gold, reward modal and cap.");
    }

    [MenuItem("Tools/Playable/Check Board Reward Sweep (Play Mode)")]
    public static void RunSweep()
    {
        Require(Application.isPlaying, "Start a fresh Play Mode session first");
        var ui = UnityEngine.Object.FindObjectOfType<InGamePuzzleUiView>(true);
        Require(ui.GridSize == new Vector2Int(4, 4), "Fresh 4x4 board");
        var bootstrap = UnityEngine.Object.FindObjectOfType<PlayableBootstrap>();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var state = typeof(PlayableBootstrap).GetField("state", flags);
        var vehicle = (Transform)typeof(PlayableBootstrap).GetField("vehicle", flags).GetValue(bootstrap);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Data/Prefab/Item/pfb_attachment.prefab");
        var first = UnityEngine.Object.Instantiate(prefab, new Vector3(10000, 10000, 10000), Quaternion.identity);
        var second = UnityEngine.Object.Instantiate(prefab, new Vector3(10000, 10000, 10012), Quaternion.identity);
        try
        {
            state.SetValue(bootstrap, Enum.Parse(state.FieldType, "Run"));
            vehicle.position = new Vector3(10000, 10000, 10020);
            Physics.SyncTransforms();
            var scan = typeof(PlayableBootstrap).GetMethod("HandleRunInteractions", flags);
            scan.Invoke(bootstrap, new object[] { new Vector3(10000, 10000, 9990), Vector3.forward * 30 });
            scan.Invoke(bootstrap, new object[] { new Vector3(10000, 10000, 9990), Vector3.forward * 30 });
            int pending = (int)typeof(PlayableBootstrap).GetField("pendingBoardColumns", flags).GetValue(bootstrap);
            Require(pending == 2 && !first.activeSelf && !second.activeSelf, "Fast sweep collects both distinct pickups exactly once");
            state.SetValue(bootstrap, Enum.Parse(state.FieldType, "Done"));
            typeof(PlayableBootstrap).GetMethod("ClaimResultAndReset", flags).Invoke(bootstrap, null);
            var reward = UnityEngine.Object.FindObjectOfType<InGameGetItemUiView>(true);
            Require(ui.GridSize == new Vector2Int(6, 4), "Two pickups grant two columns");
            Require(reward.gameObject.activeInHierarchy && reward.GetComponentsInChildren<Text>().Any(text => text.text.Contains("+2 COLUMNS")), "Reward quantity matches collected items");
            Debug.Log("Board reward sweep PASS: high-speed physics sweep, two pickups, duplicate scan and +2-column reward.");
        }
        finally
        {
            UnityEngine.Object.Destroy(first);
            UnityEngine.Object.Destroy(second);
        }
    }

    [MenuItem("Tools/Playable/Check Grid Drag And Scroll (Play Mode)")]
    public static void CheckGridDragAndScroll()
    {
        Require(Application.isPlaying, "Start Play Mode first");
        var ui = UnityEngine.Object.FindObjectOfType<InGamePuzzleUiView>(true);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var cells = (List<RectTransform>)typeof(InGamePuzzleUiView).GetField("_runtimeCells", flags).GetValue(ui);
        float pitch = Vector2.Distance(cells[0].anchoredPosition, cells[1].anchoredPosition);
        Require(cells[0].rect.width < pitch, "Grid cells have a visible gap");
        typeof(InGamePuzzleUiView).GetMethod("CreateTrayPart", flags).Invoke(ui, new object[] { 0, pitch });
        var parts = (List<RuntimePuzzlePartIcon>)typeof(InGamePuzzleUiView).GetField("_runtimeParts", flags).GetValue(ui);
        var part = parts.Last();
        var home = part.transform.parent;
        var scroll = home.GetComponentInParent<ScrollRect>();
        Require(scroll != null && scroll.viewport.GetComponent<RectMask2D>() != null, "Tray has a clipping viewport");
        Require(home.GetComponent<Image>().raycastTarget && home.GetComponent<UnityEngine.EventSystems.EventTrigger>().triggers.Count == 6, "Whole tray slot forwards selection and drag events");
        Require(part.GetComponentsInChildren<Image>().All(i => i.maskable), "Part graphics respect tray mask");
        for (int i = 1; i <= 5; i++) typeof(InGamePuzzleUiView).GetMethod("CreateTrayPart", flags).Invoke(ui, new object[] { i, pitch });
        Canvas.ForceUpdateCanvases();
        foreach (var icon in parts.Where(p => !p.IsPlaced))
        {
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, icon.transform);
            Require(bounds.min.y >= scroll.viewport.rect.yMin + 10f && bounds.max.y <= scroll.viewport.rect.yMax - 10f, "Entire tray part fits above Buy and below grid: " + icon.PartId);
        }
        var camera = (Camera)typeof(InGamePuzzleUiView).GetMethod("GetUiCamera", flags).Invoke(ui, null);
        var start = RectTransformUtility.WorldToScreenPoint(camera, part.transform.position);
        var e = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { pressPosition = start, position = start };
        part.OnPointerDown(e);
        Require(part.transform.parent == home, "Touch alone must not escape viewport");
        e.position = start + Vector2.right * 40f;
        part.OnInitializePotentialDrag(e);
        part.OnBeginDrag(e);
        part.OnDrag(e);
        part.OnPointerUp(e);
        part.OnEndDrag(e);
        Require(part.transform.parent == home && !part.IsPlaced, "Horizontal swipe scrolls instead of picking up part");
        e.pressPosition = e.position = start;
        part.OnPointerDown(e);
        e.position = start + new Vector2(30f, 40f);
        part.OnBeginDrag(e);
        Require(part.transform.parent != home, "Vertical drag uses drag layer");
        Require(part.transform.localScale == Vector3.one, "Dragging restores full grid size");
        int target = -1;
        var fit = typeof(InGamePuzzleUiView).GetMethod("CanPlace", flags);
        for (int i = 0; i < cells.Count; i++)
            if ((bool)fit.Invoke(ui, new object[] { part, new Vector2Int(i % ui.GridSize.x, i / ui.GridSize.x) })) { target = i; break; }
        Require(target >= 0, "Space exists for test part");
        var offset = (Vector2)typeof(RuntimePuzzlePartIcon).GetField("_dragScreenOffset", flags).GetValue(part);
        e.position = RectTransformUtility.WorldToScreenPoint(camera, cells[target].position) - offset;
        part.OnDrag(e);
        var preview = (List<Image>)typeof(InGamePuzzleUiView).GetField("_placementPreview", flags).GetValue(ui);
        Require(preview.Count(i => i.gameObject.activeSelf) == part.Pattern.Count, "White shadow matches part shape");
        Require(preview.Where(i => i.gameObject.activeSelf).All(i => i.color.r == 1f && i.color.g == 1f && i.color.b == 1f && !i.raycastTarget), "Shadow is white and does not block input");
        part.OnPointerUp(e);
        part.OnEndDrag(e);
        Require(part.IsPlaced && part.CellIndex == target && preview.All(i => !i.gameObject.activeSelf), "Drop uses preview target and clears shadow");
        Debug.Log("GridDragAndScroll PASS: spacing, masked scroll, drag routing, white preview and drop alignment.");
    }

    static void Require(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException("BoardRewardCheck: " + description);
    }
}
