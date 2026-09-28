using Gre.pjcode.Scenes.InGame;
using UnityEngine;
using UnityEngine.UI;

public sealed class PlayableTutorialGuide : MonoBehaviour
{
    enum PuzzleStep { None, Buy, Place, Start }
    const int TutorialDriveLimit = 3;
    public static bool ShouldShowTutorial(int completedDrives) => completedDrives < TutorialDriveLimit;

    InGamePuzzleUiView _puzzle;
    Image _finger;
    RectTransform _fingerRect;
    Transform _target;
    Transform _vehicle;
    Transform _pullStart;
    Camera _projectionCamera;
    bool _pulling;
    bool _aimGuide;
    float _shownAt;
    Canvas _canvas;
    PuzzleStep _step;
    int _completedDrives;

    public static PlayableTutorialGuide Create(InGamePuzzleUiView puzzle, Transform vehicle)
    {
        var host = new GameObject("PlayableTutorialGuide");
        var guide = host.AddComponent<PlayableTutorialGuide>();
        guide.Initialize(puzzle, vehicle);
        return guide;
    }

    void Initialize(InGamePuzzleUiView puzzle, Transform vehicle)
    {
        _puzzle = puzzle;
        _vehicle = vehicle;
        _canvas = puzzle == null ? null : puzzle.GetComponentInParent<Canvas>();
        if (_canvas == null) _canvas = FindObjectOfType<Canvas>();
        if (_canvas == null || puzzle == null) { enabled = false; return; }

        GameObject prefab = Resources.Load<GameObject>("TutorialHand");
        if (prefab == null) { enabled = false; Debug.LogError("PlayableTutorialGuide requires Resources/TutorialHand.prefab."); return; }
        var root = Instantiate(prefab, _canvas.transform, false);
        root.transform.SetAsLastSibling();
        root.name = "TutorialHand";
        _fingerRect = root.GetComponent<RectTransform>();
        _finger = root.GetComponent<Image>();
        _projectionCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? Camera.main : _canvas.worldCamera;
        root.SetActive(false);

        _puzzle.TutorialPartPurchased += OnPartPurchased;
        _puzzle.TutorialPartPlaced += OnPartPlaced;
        _puzzle.TutorialDriveStarted += OnDriveStarted;
    }

    void Update()
    {
        if (_target == null || _finger == null || !_finger.gameObject.activeSelf) return;
        if (_aimGuide)
        {
            Vector2 carCanvasPosition = WorldToCanvasPosition(_vehicle.position);
            float pullOffset = Mathf.PingPong((Time.unscaledTime - _shownAt) * 190f, ((RectTransform)_canvas.transform).rect.height * 0.12f);
            SetCanvasPosition(carCanvasPosition + Vector2.down * pullOffset);
            return;
        }
        if (_pulling)
        {
            float t = Mathf.PingPong((Time.unscaledTime - _shownAt) * 0.55f, 1f);
            Vector2 start = CanvasLocalPosition(_pullStart.position);
            Vector2 end = CanvasLocalPosition(_target.position);
            SetCanvasPosition(Vector2.Lerp(start, end, t));
        }
        else
        {
            SetCanvasPosition(CanvasLocalPosition(_target.position) + Vector2.down * 24f);
            _fingerRect.localScale = Vector3.one * (1f + Mathf.Sin((Time.unscaledTime - _shownAt) * 5f) * 0.08f);
        }
    }

    Vector2 CanvasLocalPosition(Vector3 worldPosition) => _canvas.transform.InverseTransformPoint(worldPosition);

    Vector2 WorldToCanvasPosition(Vector3 worldPosition)
    {
        if (_projectionCamera == null) return CanvasLocalPosition(worldPosition);
        Vector3 viewport = _projectionCamera.WorldToViewportPoint(worldPosition);
        Rect canvasRect = ((RectTransform)_canvas.transform).rect;
        return new Vector2((viewport.x - ((RectTransform)_canvas.transform).pivot.x) * canvasRect.width,
            (viewport.y - ((RectTransform)_canvas.transform).pivot.y) * canvasRect.height);
    }

    void SetCanvasPosition(Vector2 position) => _fingerRect.localPosition = new Vector3(position.x, position.y, 0f);

    public void ShowAimGuide()
    {
        if (_finger == null || !ShouldShowTutorial(_completedDrives) || _vehicle == null) { Hide(); return; }
        _aimGuide = true;
        _target = _vehicle;
        _pulling = false;
        _shownAt = Time.unscaledTime;
        _finger.color = Color.white;
        _fingerRect.localScale = Vector3.one;
        _finger.gameObject.SetActive(true);
    }

    public void FinishAimGuide() => Hide();

    public void CompleteDrive()
    {
        _completedDrives++;
        Hide();
    }

    public void ShowPuzzleGuide()
    {
        if (ShouldShowTutorial(_completedDrives)) ShowBuyGuide();
        else Hide();
    }

    void ShowBuyGuide()
    {
        _step = PuzzleStep.Buy;
        Show(_puzzle.BuyButton == null ? null : _puzzle.BuyButton.transform);
    }

    void OnDriveStarted() => ShowAimGuide();

    void OnPartPurchased()
    {
        if (!IsVisible || _step != PuzzleStep.Buy) return;
        _pullStart = FindTrayPart();
        Transform board = _puzzle.Board == null ? null : _puzzle.Board.transform;
        if (_pullStart == null || board == null) return;
        _pulling = true;
        _step = PuzzleStep.Place;
        _shownAt = Time.unscaledTime;
        _target = board;
    }

    void OnPartPlaced()
    {
        if (IsVisible && _step == PuzzleStep.Place && _pulling)
        {
            _step = PuzzleStep.Start;
            Show(_puzzle.PlayButton == null ? null : _puzzle.PlayButton.transform);
        }
    }

    Transform FindTrayPart()
    {
        foreach (Transform child in _puzzle.GetComponentsInChildren<Transform>(true))
            if (child.name.StartsWith("PartSlot_")) return child;
        return null;
    }

    bool IsVisible => _finger != null && _finger.gameObject.activeSelf;

    void Show(Transform target)
    {
        if (_finger == null) return;
        _target = target;
        _aimGuide = false;
        _pulling = false;
        _shownAt = Time.unscaledTime;
        _finger.color = Color.white;
        _finger.gameObject.SetActive(target != null);
    }

    void Hide()
    {
        _target = null;
        _pulling = false;
        _aimGuide = false;
        if (_finger != null) _finger.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (_puzzle == null) return;
        _puzzle.TutorialPartPurchased -= OnPartPurchased;
        _puzzle.TutorialPartPlaced -= OnPartPlaced;
        _puzzle.TutorialDriveStarted -= OnDriveStarted;
    }
}
