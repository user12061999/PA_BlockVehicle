using Gre.pjcode.Scenes.InGame;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class PlayableBootstrap : MonoBehaviour
{
    enum State
    {
        Aim,
        Run,
        Done
    }

    [SerializeField] private string vehicleName = "CarSphere";
    [SerializeField] private float maxPull = 4f;
    [SerializeField] private float minSpeed = 12f;
    [SerializeField] private float maxSpeed = 30f;
    [SerializeField] private float friction = 8f;
    [SerializeField] private float steerSpeed = 14f;
    [SerializeField] private float turnSpeed = 110f;
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float groundRayHeight = 8f;
    [SerializeField] private float groundOffset = 0.08f;

    Transform vehicle;
    InGamePuzzleUiView puzzleUi;
    InGameResultUiView resultUi;
    GameObject buildUi;
    Camera followCamera;
    Vector3 startPosition;
    Quaternion startRotation;
    Vector3 cameraOffset;
    Vector2 dragStart;
    State state;
    float pull;
    float speed;
    float distance;
    float steer;
    bool dragging;

    void Awake()
    {
        GameObject found = GameObject.Find(vehicleName);
        if (found == null)
        {
            Debug.LogError("PlayableBootstrap needs a vehicle named " + vehicleName + ".");
            enabled = false;
            return;
        }

        vehicle = found.transform;
        puzzleUi = FindObjectOfType<InGamePuzzleUiView>();
        resultUi = FindSceneObjectOfType<InGameResultUiView>();
        if (resultUi != null) resultUi.SetClaimAction(ResetRun);
        buildUi = GameObject.Find("PuzzleUi");
        startPosition = vehicle.position;
        startRotation = vehicle.rotation;
        followCamera = Camera.main;
        if (followCamera != null) cameraOffset = followCamera.transform.position - vehicle.position;
        SnapToGround(startRotation * Vector3.forward, true);
        startPosition = vehicle.position;
        startRotation = vehicle.rotation;

        Rigidbody body = vehicle.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.isKinematic = true;
            body.useGravity = false;
        }

        foreach (Button button in Resources.FindObjectsOfTypeAll<Button>())
        {
            if (!button.gameObject.scene.IsValid()) continue;
            if (button.name.Contains("Continue") && (resultUi == null || !button.transform.IsChildOf(resultUi.transform))) button.onClick.AddListener(PlayworksBridge.InstallFullGame);
            if (button.name.Contains("Start")) button.onClick.AddListener(HideBuildUi);
        }
    }

    void Update()
    {
        if (state == State.Aim) UpdateAim();
        else if (state == State.Run) UpdateRun();
    }

    void LateUpdate()
    {
        if (followCamera == null || vehicle == null || state == State.Aim) return;
        followCamera.transform.position = Vector3.Lerp(followCamera.transform.position, vehicle.position + cameraOffset, Time.deltaTime * 5f);
    }

    void UpdateAim()
    {
        if (PointerDown(out Vector2 pointer) && !PointerOverUi())
        {
            dragging = true;
            dragStart = pointer;
        }

        if (!dragging) return;

        if (PointerHeld(out pointer))
        {
            pull = Mathf.Clamp((dragStart.y - pointer.y) * 0.015f, 0f, maxPull);
            vehicle.SetPositionAndRotation(startPosition - startRotation * Vector3.forward * pull, startRotation);
            SnapToGround(startRotation * Vector3.forward, true);
        }

        if (PointerUp(out _))
        {
            dragging = false;
            float partMultiplier = puzzleUi == null ? 1f : puzzleUi.RunDistanceMultiplier;
            speed = Mathf.Lerp(minSpeed, maxSpeed, maxPull > 0f ? pull / maxPull : 0f) * partMultiplier;
            state = State.Run;
            HideBuildUi();
        }
    }

    void UpdateRun()
    {
        float targetSteer = 0f;
        if (PointerHeld(out Vector2 pointer)) targetSteer = Mathf.Clamp((pointer.x / Mathf.Max(1f, Screen.width) - 0.5f) * 2f, -1f, 1f);
        steer = Mathf.MoveTowards(steer, targetSteer, Time.deltaTime * 4f);

        speed = Mathf.MoveTowards(speed, 0f, friction * Time.deltaTime);
        Vector3 forward = vehicle.forward;
        if (Mathf.Abs(steer) > 0.001f) forward = Quaternion.AngleAxis(steer * turnSpeed * Time.deltaTime, vehicle.up) * forward;
        Vector3 move = forward.normalized * speed * Time.deltaTime;
        vehicle.position += move;
        SnapToGround(move.sqrMagnitude > 0f ? move : forward, false);
        distance += move.magnitude;

        if (speed <= 0f)
        {
            FinishRun();
        }
    }

    void FinishRun()
    {
        if (state == State.Done) return;

        state = State.Done;
        if (resultUi != null) resultUi.Open((int)distance);
        PlayworksBridge.GameEnded();
    }

    void ResetRun()
    {
        pull = 0f;
        speed = 0f;
        distance = 0f;
        steer = 0f;
        dragging = false;
        state = State.Aim;
        vehicle.SetPositionAndRotation(startPosition, startRotation);
        SnapToGround(startRotation * Vector3.forward, true);
        if (followCamera != null) followCamera.transform.position = vehicle.position + cameraOffset;
        ShowBuildUi();
    }

    void HideBuildUi()
    {
        if (puzzleUi != null) puzzleUi.SetOpen(false);
        else if (buildUi != null) buildUi.SetActive(false);
    }

    void ShowBuildUi()
    {
        if (puzzleUi != null) puzzleUi.SetOpen(true, true);
        else if (buildUi != null) buildUi.SetActive(true);
    }

    static bool PointerDown(out Vector2 position)
    {
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            position = Input.GetTouch(0).position;
            return true;
        }

        position = Input.mousePosition;
        return Input.GetMouseButtonDown(0);
    }

    static bool PointerHeld(out Vector2 position)
    {
        if (Input.touchCount > 0)
        {
            position = Input.GetTouch(0).position;
            return true;
        }

        position = Input.mousePosition;
        return Input.GetMouseButton(0);
    }

    static bool PointerUp(out Vector2 position)
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            position = touch.position;
            return touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled;
        }

        position = Input.mousePosition;
        return Input.GetMouseButtonUp(0);
    }

    static bool PointerOverUi()
    {
        if (EventSystem.current == null) return false;
        if (Input.touchCount > 0) return EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
        return EventSystem.current.IsPointerOverGameObject();
    }

    static T FindSceneObjectOfType<T>() where T : Component
    {
        foreach (T item in Resources.FindObjectsOfTypeAll<T>())
        {
            if (item != null && item.gameObject.scene.IsValid()) return item;
        }

        return null;
    }

    void SnapToGround(Vector3 preferredForward, bool immediate)
    {
        if (vehicle == null) return;

        Vector3 origin = vehicle.position + Vector3.up * groundRayHeight;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, groundRayHeight * 2f, groundMask, QueryTriggerInteraction.Ignore);
        if (hits.Length == 0) return;

        RaycastHit bestHit = default;
        float bestDistance = float.MaxValue;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null || hit.collider.transform.IsChildOf(vehicle)) continue;
            if (hit.distance >= bestDistance) continue;
            bestHit = hit;
            bestDistance = hit.distance;
        }

        if (bestDistance == float.MaxValue) return;

        Vector3 position = vehicle.position;
        position.y = bestHit.point.y + groundOffset;
        vehicle.position = position;

        Vector3 forward = Vector3.ProjectOnPlane(preferredForward, bestHit.normal);
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.ProjectOnPlane(vehicle.forward, bestHit.normal);
        if (forward.sqrMagnitude < 0.001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(forward.normalized, bestHit.normal);
        vehicle.rotation = immediate
            ? targetRotation
            : Quaternion.Slerp(vehicle.rotation, targetRotation, Time.deltaTime * steerSpeed);
    }
}
