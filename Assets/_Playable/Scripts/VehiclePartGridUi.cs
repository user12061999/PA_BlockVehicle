using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum VehiclePartKind
{
    Wheel,
    Caterpillar,
    Chimney,
    Wing
}

[System.Serializable]
public sealed class VehiclePartDefinition
{
    public string id;
    public VehiclePartKind kind;
    public bool useDefaultShape = true;
    public GameObject uiPrefab;
    public Sprite minoSprite;
    public Sprite iconSprite;
    [Range(0, 3)] public int initialLevel;
    public Vector2Int[] cells = { Vector2Int.zero };
    public float launchSpeedBonus;
    public float steeringBonus;
    public float frictionReduction;
    public GameObject[] vehicleObjectsToDisable;
    public GameObject[] vehicleObjectsToEnable;
}

public sealed class VehiclePartGridUi : MonoBehaviour
{
    [Header("References")]
    [SerializeField] BlockVehiclePlayable playable;
    [SerializeField] RectTransform boardRoot;
    [SerializeField] RectTransform trayRoot;
    [SerializeField] GameObject uiRootToHideOnPlay;
    [SerializeField] Sprite emptyCellSprite;
    [SerializeField] Button buyButton;
    [SerializeField] Button playButton;

    [Header("Grid")]
    [SerializeField] int columns = 4;
    [SerializeField] int rows = 4;
    [SerializeField] float cellSize = 42f;
    [SerializeField] float spacing = 4f;
    [SerializeField] Color emptyCellColor = new Color(0f, 0.55f, 1f, 0.35f);
    [SerializeField] Color previewValidColor = new Color(0f, 0.9f, 1f, 0.65f);
    [SerializeField] Color previewInvalidColor = new Color(1f, 0.2f, 0.15f, 0.55f);
    [SerializeField] Color[] levelColors = { Color.white, new Color(0.15f, 0.55f, 1f, 1f), new Color(0.65f, 0.25f, 1f, 1f), new Color(1f, 0.78f, 0.05f, 1f) };

    [Header("Parts")]
    [SerializeField] VehiclePartDefinition[] parts;

    [Header("Currency")]
    [SerializeField] int currentCoins;
    [SerializeField] int buyCost = 200;
    [SerializeField] int buyCostIncrease = 50;
    [SerializeField] Text coinText;
    [SerializeField] Text buyCostText;

    bool[,] occupied;
    VehiclePartDragItem[,] cellItems;
    Image[,] boardImages;
    Canvas rootCanvas;
    Camera eventCamera;

    void Awake()
    {
        rootCanvas = GetComponentInParent<Canvas>();
        eventCamera = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? rootCanvas.worldCamera : null;
        BindButtons();
        Rebuild();
        RefreshCurrencyText();
    }

    void OnDestroy()
    {
        if (buyButton != null) buyButton.onClick.RemoveListener(BuyRandomPart);
        if (playButton != null) playButton.onClick.RemoveListener(OnPlayButtonClicked);
    }

    void BindButtons()
    {
        if (buyButton != null)
        {
            buyButton.onClick.RemoveListener(BuyRandomPart);
            buyButton.onClick.AddListener(BuyRandomPart);
        }
        if (playButton != null)
        {
            playButton.onClick.RemoveListener(OnPlayButtonClicked);
            playButton.onClick.AddListener(OnPlayButtonClicked);
        }
    }

    [ContextMenu("Rebuild")]
    public void Rebuild()
    {
        if (boardRoot == null || trayRoot == null) return;
        columns = Mathf.Max(1, columns);
        rows = Mathf.Max(1, rows);
        occupied = new bool[columns, rows];
        cellItems = new VehiclePartDragItem[columns, rows];
        ClearChildren(boardRoot);
        ClearChildren(trayRoot);
        BuildBoard();
        BuildTray();
    }

    [ContextMenu("Use Default Vehicle Parts")]
    void UseDefaultVehicleParts()
    {
        parts = new[]
        {
            new VehiclePartDefinition { id = "wheel", kind = VehiclePartKind.Wheel, useDefaultShape = true },
            new VehiclePartDefinition { id = "caterpillar", kind = VehiclePartKind.Caterpillar, useDefaultShape = true },
            new VehiclePartDefinition { id = "chimney", kind = VehiclePartKind.Chimney, useDefaultShape = true },
            new VehiclePartDefinition { id = "wing", kind = VehiclePartKind.Wing, useDefaultShape = true }
        };
        Rebuild();
    }

    public bool TryPlacePart(VehiclePartDragItem dragItem, bool applyStats, out Vector2Int placedOrigin)
    {
        placedOrigin = default;
        if (dragItem == null) return false;
        VehiclePartDefinition part = dragItem.Part;
        RectTransform item = dragItem.RectTransform;
        if (part == null) return false;
        EnsureCells(part);
        if (!TryGetOriginFromItem(part, item, out placedOrigin)) return false;
        if (!CanPlace(part, placedOrigin)) return false;

        for (int i = 0; i < part.cells.Length; i++)
        {
            Vector2Int cell = placedOrigin + part.cells[i];
            occupied[cell.x, cell.y] = true;
            cellItems[cell.x, cell.y] = dragItem;
        }
        MoveItemToBoard(part, item, placedOrigin);
        if (applyStats) ApplyPart(part, dragItem.Level);
        return true;
    }

    public void ReleasePartCells(VehiclePartDefinition part, Vector2Int origin)
    {
        if (part == null || occupied == null) return;
        EnsureCells(part);
        for (int i = 0; i < part.cells.Length; i++)
        {
            Vector2Int cell = origin + part.cells[i];
            if (cell.x >= 0 && cell.x < columns && cell.y >= 0 && cell.y < rows)
            {
                occupied[cell.x, cell.y] = false;
                cellItems[cell.x, cell.y] = null;
            }
        }
    }

    public void RestorePlacedPart(VehiclePartDragItem dragItem, Vector2Int origin)
    {
        if (dragItem == null) return;
        VehiclePartDefinition part = dragItem.Part;
        RectTransform item = dragItem.RectTransform;
        if (part == null || item == null) return;
        EnsureCells(part);
        for (int i = 0; i < part.cells.Length; i++)
        {
            Vector2Int cell = origin + part.cells[i];
            if (cell.x >= 0 && cell.x < columns && cell.y >= 0 && cell.y < rows)
            {
                occupied[cell.x, cell.y] = true;
                cellItems[cell.x, cell.y] = dragItem;
            }
        }
        MoveItemToBoard(part, item, origin);
    }

    public bool IsOverTray(Vector2 screenPosition)
    {
        return trayRoot != null && RectTransformUtility.RectangleContainsScreenPoint(trayRoot, screenPosition, eventCamera);
    }

    public void MoveItemToTray(VehiclePartDefinition part, RectTransform item)
    {
        if (part == null || item == null || trayRoot == null) return;
        EnsureCells(part);
        Vector2Int size = ShapeSize(part);
        item.SetParent(trayRoot, false);
        item.anchorMin = new Vector2(0.5f, 0.5f);
        item.anchorMax = new Vector2(0.5f, 0.5f);
        item.pivot = new Vector2(0.5f, 0.5f);
        item.sizeDelta = new Vector2(size.x * cellSize + Mathf.Max(0, size.x - 1) * spacing, size.y * cellSize + Mathf.Max(0, size.y - 1) * spacing);
        item.anchoredPosition = Vector2.zero;
        item.localScale = Vector3.one;
        CanvasGroup group = item.GetComponent<CanvasGroup>();
        if (group != null) group.blocksRaycasts = true;
    }

    public Transform DragRoot
    {
        get { return rootCanvas != null ? rootCanvas.transform : transform; }
    }

    public void MoveDraggedItem(RectTransform item, Vector2 screenPosition, Vector2 pointerOffset)
    {
        screenPosition += pointerOffset;
        RectTransform dragRect = DragRoot as RectTransform;
        if (dragRect != null && RectTransformUtility.ScreenPointToWorldPointInRectangle(dragRect, screenPosition, eventCamera, out Vector3 worldPosition))
        {
            item.position = worldPosition;
            return;
        }
        item.position = screenPosition;
    }

    public void PreviewPart(VehiclePartDefinition part, RectTransform item)
    {
        ClearPreview();
        if (part == null || boardImages == null) return;
        EnsureCells(part);
        if (!TryGetOriginFromItem(part, item, out Vector2Int origin)) return;

        VehiclePartDragItem dragItem = item.GetComponent<VehiclePartDragItem>();
        bool valid = CanPlace(part, origin) || CanMergePart(dragItem, out _);
        Color color = valid ? previewValidColor : previewInvalidColor;
        for (int i = 0; i < part.cells.Length; i++)
        {
            Vector2Int cell = origin + part.cells[i];
            if (cell.x >= 0 && cell.x < columns && cell.y >= 0 && cell.y < rows && boardImages[cell.x, cell.y] != null)
            {
                boardImages[cell.x, cell.y].color = color;
            }
        }
    }

    public bool TryMergePart(VehiclePartDragItem source, bool sourceWasPlaced)
    {
        if (!CanMergePart(source, out VehiclePartDragItem target)) return false;
        if (sourceWasPlaced) RemovePartStats(source.Part, source.Level);
        target.SetLevel(target.Level + 1);
        ApplyLevelUpgrade(target.Part, target.Level);
        return true;
    }

    bool CanMergePart(VehiclePartDragItem source, out VehiclePartDragItem target)
    {
        target = null;
        if (source == null || source.Part == null || source.Level >= 3) return false;
        VehiclePartDefinition part = source.Part;
        EnsureCells(part);
        if (!TryGetOriginFromItem(part, source.RectTransform, out Vector2Int origin)) return false;

        for (int i = 0; i < part.cells.Length; i++)
        {
            Vector2Int cell = origin + part.cells[i];
            if (cell.x < 0 || cell.x >= columns || cell.y < 0 || cell.y >= rows) return false;
            VehiclePartDragItem item = cellItems[cell.x, cell.y];
            if (item == null || item == source) return false;
            if (target == null) target = item;
            else if (target != item) return false;
        }

        return target != null && target.Part.kind == part.kind && target.Level == source.Level && target.Level < 3;
    }

    public void ClearPreview()
    {
        if (boardImages == null) return;
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                if (boardImages[x, y] != null) boardImages[x, y].color = emptyCellColor;
            }
        }
    }

    public void OnPlayButtonClicked()
    {
        ClearPreview();
        GameObject target = uiRootToHideOnPlay != null ? uiRootToHideOnPlay : gameObject;
        target.SetActive(false);
    }

    public void AddCoins(int amount)
    {
        currentCoins += Mathf.Max(0, amount);
        ShowBuildUi();
        RefreshCurrencyText();
    }

    public void BuyRandomPart()
    {
        if (parts == null || parts.Length == 0 || trayRoot == null) return;
        buyCost = Mathf.Max(0, buyCost);
        if (currentCoins < buyCost) return;

        currentCoins -= buyCost;
        buyCost += Mathf.Max(0, buyCostIncrease);
        CreatePartItem(parts[Random.Range(0, parts.Length)], trayRoot);
        RefreshCurrencyText();
    }

    void RefreshCurrencyText()
    {
        if (coinText != null) coinText.text = currentCoins.ToString();
        if (buyCostText != null) buyCostText.text = buyCost.ToString();
        if (buyButton != null) buyButton.interactable = currentCoins >= Mathf.Max(0, buyCost);
    }

    void ShowBuildUi()
    {
        GameObject target = uiRootToHideOnPlay != null ? uiRootToHideOnPlay : gameObject;
        target.SetActive(true);
    }

    void BuildBoard()
    {
        Vector2 total = BoardSize();
        boardRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, total.x);
        boardRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, total.y);
        boardImages = new Image[columns, rows];

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                GameObject cell = new GameObject("Cell " + x + "," + y, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                RectTransform rect = (RectTransform)cell.transform;
                rect.SetParent(boardRoot, false);
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(cellSize, cellSize);
                rect.anchoredPosition = CellCenter(x, y);

                Image image = cell.GetComponent<Image>();
                image.sprite = emptyCellSprite;
                image.color = emptyCellColor;
                image.type = emptyCellSprite != null ? Image.Type.Sliced : Image.Type.Simple;
                image.raycastTarget = false;
                boardImages[x, y] = image;
            }
        }
    }

    void BuildTray()
    {
        HorizontalLayoutGroup layout = trayRoot.GetComponent<HorizontalLayoutGroup>();
        if (layout == null) layout = trayRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.spacing = spacing * 2f;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        if (parts == null) return;
        for (int i = 0; i < parts.Length; i++)
        {
            VehiclePartDefinition part = parts[i];
            if (part == null) continue;
            CreatePartItem(part, trayRoot);
        }
    }

    VehiclePartDragItem CreatePartItem(VehiclePartDefinition part, Transform parent)
    {
        EnsureCells(part);
        Vector2Int size = ShapeSize(part);
        GameObject item = part.uiPrefab != null ? Instantiate(part.uiPrefab) : new GameObject(string.IsNullOrEmpty(part.id) ? "Vehicle Part" : part.id, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        item.name = string.IsNullOrEmpty(part.id) ? "Vehicle Part" : part.id;
        RectTransform rect = (RectTransform)item.transform;
        rect.SetParent(parent, false);
        rect.sizeDelta = new Vector2(size.x * cellSize + Mathf.Max(0, size.x - 1) * spacing, size.y * cellSize + Mathf.Max(0, size.y - 1) * spacing);

        Image background = item.GetComponent<Image>();
        if (background == null) background = item.GetComponentInChildren<Image>(true);
        if (background == null) background = item.AddComponent<Image>();
        if (part.uiPrefab == null) background.sprite = part.minoSprite;
        background.color = background.sprite != null ? GetLevelColor(part.initialLevel) : new Color(1f, 1f, 1f, 0.18f);
        background.raycastTarget = true;
        background.preserveAspect = true;

        if (part.uiPrefab == null) AddIcon(part, rect);

        CanvasGroup group = item.GetComponent<CanvasGroup>();
        if (group == null) group = item.AddComponent<CanvasGroup>();
        VehiclePartDragItem drag = item.GetComponent<VehiclePartDragItem>();
        if (drag == null) drag = item.AddComponent<VehiclePartDragItem>();
        drag.Initialize(this, part, part.initialLevel, background);
        return drag;
    }

    void MoveItemToBoard(VehiclePartDefinition part, RectTransform rect, Vector2Int origin)
    {
        Vector2Int min = ShapeMin(part);
        rect.SetParent(boardRoot, false);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = ShapeCenter(origin + min, ShapeSize(part));
        rect.localScale = Vector3.one;
        CanvasGroup group = rect.GetComponent<CanvasGroup>();
        if (group != null) group.blocksRaycasts = true;
    }

    void AddIcon(VehiclePartDefinition part, RectTransform parent)
    {
        if (part.iconSprite == null) return;
        GameObject icon = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = (RectTransform)icon.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = icon.GetComponent<Image>();
        image.sprite = part.iconSprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
    }

    public Color GetLevelColor(int level)
    {
        level = Mathf.Clamp(level, 0, 3);
        if (levelColors != null && levelColors.Length > level) return levelColors[level];
        return Color.white;
    }

    void ApplyPart(VehiclePartDefinition part, int level)
    {
        if (part.vehicleObjectsToDisable != null)
        {
            for (int i = 0; i < part.vehicleObjectsToDisable.Length; i++)
            {
                if (part.vehicleObjectsToDisable[i] != null) part.vehicleObjectsToDisable[i].SetActive(false);
            }
        }

        if (part.vehicleObjectsToEnable != null)
        {
            for (int i = 0; i < part.vehicleObjectsToEnable.Length; i++)
            {
                if (part.vehicleObjectsToEnable[i] != null) part.vehicleObjectsToEnable[i].SetActive(true);
            }
        }
        if (playable != null)
        {
            playable.ApplyVehiclePartStats(part.kind, part.launchSpeedBonus, level);
        }
    }

    public void RemovePart(VehiclePartDefinition part, int level)
    {
        if (part == null) return;
        if (part.vehicleObjectsToEnable != null)
        {
            for (int i = 0; i < part.vehicleObjectsToEnable.Length; i++)
            {
                if (part.vehicleObjectsToEnable[i] != null) part.vehicleObjectsToEnable[i].SetActive(false);
            }
        }

        if (part.vehicleObjectsToDisable != null)
        {
            for (int i = 0; i < part.vehicleObjectsToDisable.Length; i++)
            {
                if (part.vehicleObjectsToDisable[i] != null) part.vehicleObjectsToDisable[i].SetActive(true);
            }
        }
        RemovePartStats(part, level);
    }

    void ApplyLevelUpgrade(VehiclePartDefinition part, int level)
    {
        if (playable != null) playable.ApplyVehiclePartStats(part.kind, part.launchSpeedBonus, level - 1);
    }

    void RemovePartStats(VehiclePartDefinition part, int level)
    {
        if (playable == null || part == null) return;
        playable.RemoveVehiclePartStats(part.kind, part.launchSpeedBonus, level);
    }

    bool TryGetOriginFromItem(VehiclePartDefinition part, RectTransform item, out Vector2Int origin)
    {
        origin = default;
        if (item == null) return false;
        Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(eventCamera, item.position);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(boardRoot, screenPosition, eventCamera, out Vector2 local)) return false;

        Vector2 total = BoardSize();
        float x = local.x + total.x * boardRoot.pivot.x;
        float y = total.y * (1f - boardRoot.pivot.y) - local.y;

        float step = cellSize + spacing;
        Vector2Int min = ShapeMin(part);
        Vector2Int size = ShapeSize(part);
        float shapeWidth = size.x * cellSize + Mathf.Max(0, size.x - 1) * spacing;
        float shapeHeight = size.y * cellSize + Mathf.Max(0, size.y - 1) * spacing;
        Vector2Int topLeft = new Vector2Int(Mathf.RoundToInt((x - shapeWidth * 0.5f) / step), Mathf.RoundToInt((y - shapeHeight * 0.5f) / step));
        origin = topLeft - min;
        return true;
    }

    bool CanPlace(VehiclePartDefinition part, Vector2Int origin)
    {
        for (int i = 0; i < part.cells.Length; i++)
        {
            Vector2Int cell = origin + part.cells[i];
            if (cell.x < 0 || cell.x >= columns || cell.y < 0 || cell.y >= rows || occupied[cell.x, cell.y]) return false;
        }
        return true;
    }

    Vector2 BoardSize()
    {
        return new Vector2(columns * cellSize + Mathf.Max(0, columns - 1) * spacing, rows * cellSize + Mathf.Max(0, rows - 1) * spacing);
    }

    Vector2 CellCenter(int x, int y)
    {
        return new Vector2(x * (cellSize + spacing) + cellSize * 0.5f, -y * (cellSize + spacing) - cellSize * 0.5f);
    }

    Vector2 ShapeCenter(Vector2Int minCell, Vector2Int size)
    {
        return new Vector2(minCell.x * (cellSize + spacing) + (size.x * cellSize + Mathf.Max(0, size.x - 1) * spacing) * 0.5f,
            -minCell.y * (cellSize + spacing) - (size.y * cellSize + Mathf.Max(0, size.y - 1) * spacing) * 0.5f);
    }

    static Vector2Int ShapeMin(VehiclePartDefinition part)
    {
        Vector2Int min = part.cells[0];
        for (int i = 1; i < part.cells.Length; i++) min = new Vector2Int(Mathf.Min(min.x, part.cells[i].x), Mathf.Min(min.y, part.cells[i].y));
        return min;
    }

    static Vector2Int ShapeSize(VehiclePartDefinition part)
    {
        Vector2Int min = ShapeMin(part);
        Vector2Int max = part.cells[0];
        for (int i = 1; i < part.cells.Length; i++) max = new Vector2Int(Mathf.Max(max.x, part.cells[i].x), Mathf.Max(max.y, part.cells[i].y));
        return max - min + Vector2Int.one;
    }

    static void EnsureCells(VehiclePartDefinition part)
    {
        if (part.useDefaultShape) part.cells = DefaultCells(part.kind);
        if (part.cells == null || part.cells.Length == 0) part.cells = new[] { Vector2Int.zero };
    }

    static Vector2Int[] DefaultCells(VehiclePartKind kind)
    {
        if (kind == VehiclePartKind.Caterpillar)
        {
            return new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(3, 0) };
        }
        if (kind == VehiclePartKind.Chimney)
        {
            return new[] { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 1) };
        }
        if (kind == VehiclePartKind.Wing)
        {
            return new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(1, 1) };
        }
        return new[] { Vector2Int.zero };
    }

    static void ClearChildren(Transform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            if (Application.isPlaying) Destroy(root.GetChild(i).gameObject);
            else DestroyImmediate(root.GetChild(i).gameObject);
        }
    }
}

public sealed class VehiclePartDragItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    VehiclePartGridUi owner;
    VehiclePartDefinition part;
    RectTransform rect;
    Image background;
    CanvasGroup group;
    Transform startParent;
    Vector2 startPosition;
    Vector2 pointerOffset;
    Vector2Int placedOrigin;
    int level;
    bool placed;
    bool wasPlaced;

    public VehiclePartDefinition Part
    {
        get { return part; }
    }

    public RectTransform RectTransform
    {
        get { return rect; }
    }

    public int Level
    {
        get { return level; }
    }

    public void Initialize(VehiclePartGridUi grid, VehiclePartDefinition definition, int startLevel, Image backgroundImage)
    {
        owner = grid;
        part = definition;
        rect = (RectTransform)transform;
        background = backgroundImage;
        group = GetComponent<CanvasGroup>();
        SetLevel(startLevel);
    }

    public void SetLevel(int nextLevel)
    {
        level = Mathf.Clamp(nextLevel, 0, 3);
        if (background != null) background.color = owner.GetLevelColor(level);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        startParent = transform.parent;
        startPosition = rect.anchoredPosition;
        wasPlaced = placed;
        if (placed) owner.ReleasePartCells(part, placedOrigin);
        pointerOffset = (Vector2)RectTransformUtility.WorldToScreenPoint(eventData.pressEventCamera, rect.position) - eventData.position;
        transform.SetParent(owner.DragRoot, true);
        group.blocksRaycasts = false;
        owner.MoveDraggedItem(rect, eventData.position, pointerOffset);
        owner.PreviewPart(part, rect);
    }

    public void OnDrag(PointerEventData eventData)
    {
        owner.MoveDraggedItem(rect, eventData.position, pointerOffset);
        owner.PreviewPart(part, rect);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        group.blocksRaycasts = true;
        owner.ClearPreview();
        if (owner.TryMergePart(this, wasPlaced))
        {
            Destroy(gameObject);
            return;
        }

        if (owner.TryPlacePart(this, !wasPlaced, out Vector2Int newOrigin))
        {
            placed = true;
            placedOrigin = newOrigin;
            return;
        }

        if (wasPlaced && owner.IsOverTray(eventData.position))
        {
            owner.RemovePart(part, level);
            owner.MoveItemToTray(part, rect);
            placed = false;
            return;
        }

        if (wasPlaced)
        {
            owner.RestorePlacedPart(this, placedOrigin);
            placed = true;
            return;
        }

        transform.SetParent(startParent, false);
        rect.anchoredPosition = startPosition;
    }
}
