using Gre.pjcode.Scenes.InGame;
using System.Collections.Generic;
using System.Reflection;
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
    [SerializeField] private LayerMask interactionMask = ~0;
    [SerializeField] private float collisionRadius = 1.35f;
    [SerializeField] private float collisionCenterHeight = 1f;
    [SerializeField] private float collisionSpeedMultiplier = 0.55f;
    [SerializeField] private float collisionTiltAngle = 18f;
    [SerializeField] private float collisionTiltReturnSpeed = 90f;
    [SerializeField] private int coinAmountFallback = 100;
    [Header("Sound Effects")]
    [SerializeField] private AudioClip sfxTap;
    [SerializeField] private AudioClip sfxCancel;
    [SerializeField] private AudioClip sfxBuy;
    [SerializeField] private AudioClip sfxPartPick;
    [SerializeField] private AudioClip sfxPartSet;
    [SerializeField] private AudioClip sfxMerge;
    [SerializeField] private AudioClip sfxPull;
    [SerializeField] private AudioClip sfxLaunch;
    [SerializeField] private AudioClip sfxCoin;
    [SerializeField] private AudioClip sfxCollision;
    [SerializeField] private AudioClip sfxFinish;
    [SerializeField] private AudioClip sfxClaim;

    Transform vehicle;
    CarView carView;
    CarSphereTracer carTracer;
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
    float collisionTilt;
    bool dragging;
    readonly RaycastHit[] interactionHits = new RaycastHit[16];
    readonly Collider[] interactionOverlaps = new Collider[16];
    readonly HashSet<int> collectedCoinIds = new HashSet<int>();
    readonly List<GameObject> collectedCoins = new List<GameObject>();

    void Awake()
    {
        RegisterSoundEffects();

        GameObject found = GameObject.Find(vehicleName);
        if (found == null)
        {
            Debug.LogError("PlayableBootstrap needs a vehicle named " + vehicleName + ".");
            enabled = false;
            return;
        }

        vehicle = found.transform;
        carView = vehicle.GetComponentInChildren<CarView>();
        if (carView == null) carView = FindObjectOfType<CarView>();
        carTracer = vehicle.GetComponent<CarSphereTracer>();
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
        UpdateCollisionTilt();
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
            PlayableSoundEffects.Play(PlayableSfx.Pull);
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
            PlayableSoundEffects.Play(PlayableSfx.Launch);
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
        Vector3 previousPosition = vehicle.position;
        vehicle.position += move;
        HandleRunInteractions(previousPosition, move);
        SnapToGround(move.sqrMagnitude > 0f ? move : forward, false);
        distance += move.magnitude;

        if (speed <= 0f)
        {
            FinishRun();
        }
    }

    void RegisterSoundEffects()
    {
        PlayableSoundEffects.Register(PlayableSfx.Tap, sfxTap);
        PlayableSoundEffects.Register(PlayableSfx.Cancel, sfxCancel);
        PlayableSoundEffects.Register(PlayableSfx.Buy, sfxBuy);
        PlayableSoundEffects.Register(PlayableSfx.PartPick, sfxPartPick);
        PlayableSoundEffects.Register(PlayableSfx.PartSet, sfxPartSet);
        PlayableSoundEffects.Register(PlayableSfx.Merge, sfxMerge);
        PlayableSoundEffects.Register(PlayableSfx.Pull, sfxPull);
        PlayableSoundEffects.Register(PlayableSfx.Launch, sfxLaunch);
        PlayableSoundEffects.Register(PlayableSfx.Coin, sfxCoin);
        PlayableSoundEffects.Register(PlayableSfx.Collision, sfxCollision);
        PlayableSoundEffects.Register(PlayableSfx.Finish, sfxFinish);
        PlayableSoundEffects.Register(PlayableSfx.Claim, sfxClaim);
    }

    void FinishRun()
    {
        if (state == State.Done) return;

        state = State.Done;
        PlayableSoundEffects.Play(PlayableSfx.Finish);
        if (resultUi != null) resultUi.Open((int)distance);
        PlayworksBridge.GameEnded();
    }

    void ResetRun()
    {
        pull = 0f;
        speed = 0f;
        distance = 0f;
        steer = 0f;
        collisionTilt = 0f;
        dragging = false;
        state = State.Aim;
        RestoreCoins();
        vehicle.SetPositionAndRotation(startPosition, startRotation);
        SnapToGround(startRotation * Vector3.forward, true);
        if (carView != null) carView.SetTiltBody(0f);
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

    void HandleRunInteractions(Vector3 previousPosition, Vector3 move)
    {
        if (move.sqrMagnitude <= 0f) return;

        Vector3 center = vehicle.position + Vector3.up * collisionCenterHeight;
        int overlapCount = Physics.OverlapSphereNonAlloc(center, collisionRadius, interactionOverlaps, interactionMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < overlapCount; i++)
        {
            TryCollectCoin(interactionOverlaps[i]);
        }

        Vector3 direction = move.normalized;
        Vector3 origin = previousPosition + Vector3.up * collisionCenterHeight;
        int hitCount = Physics.SphereCastNonAlloc(origin, collisionRadius, direction, interactionHits, move.magnitude + collisionRadius, interactionMask, QueryTriggerInteraction.Collide);
        RaycastHit bestHit = default;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = interactionHits[i];
            if (TryCollectCoin(hit.collider)) continue;
            if (!IsObstacleHit(hit)) continue;
            if (hit.distance >= bestDistance) continue;
            bestHit = hit;
            bestDistance = hit.distance;
        }

        if (bestDistance < float.MaxValue) BounceFrom(bestHit, direction);
    }

    bool TryCollectCoin(Collider collider)
    {
        GameObject coin = GetCoinObject(collider);
        if (coin == null) return false;

        int id = coin.GetInstanceID();
        if (collectedCoinIds.Contains(id)) return true;

        collectedCoinIds.Add(id);
        collectedCoins.Add(coin);
        int amount = GetCoinAmount(coin);
        if (puzzleUi != null) puzzleUi.AddGold(amount);
        if (carTracer != null) carTracer.GetCoin(amount);
        PlayableSoundEffects.Play(PlayableSfx.Coin);
        coin.SetActive(false);
        return true;
    }

    GameObject GetCoinObject(Collider collider)
    {
        if (collider == null || collider.transform.IsChildOf(vehicle)) return null;

        Transform current = collider.transform;
        while (current != null)
        {
            if (current.CompareTag("Coin")) return current.gameObject;
            current = current.parent;
        }

        return null;
    }

    int GetCoinAmount(GameObject coin)
    {
        MonoBehaviour[] behaviours = coin.GetComponents<MonoBehaviour>();
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null) continue;
            FieldInfo field = behaviour.GetType().GetField("_amount", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null && field.FieldType == typeof(int)) return Mathf.Max(0, (int)field.GetValue(behaviour));
        }

        return coinAmountFallback;
    }

    bool IsObstacleHit(RaycastHit hit)
    {
        Collider collider = hit.collider;
        if (collider == null || collider.isTrigger) return false;
        if (collider.transform.IsChildOf(vehicle)) return false;
        if (collider.CompareTag("Coin") || collider.CompareTag("Dash") || collider.CompareTag("Water") || collider.CompareTag("Dirt")) return false;
        if (LayerMask.LayerToName(collider.gameObject.layer) == "Road") return false;
        return hit.normal.y < 0.55f;
    }

    void BounceFrom(RaycastHit hit, Vector3 direction)
    {
        Vector3 normal = Vector3.ProjectOnPlane(hit.normal, Vector3.up);
        if (normal.sqrMagnitude < 0.001f) normal = -direction;

        Vector3 reflected = Vector3.Reflect(direction, normal.normalized);
        reflected = Vector3.ProjectOnPlane(reflected, Vector3.up);
        if (reflected.sqrMagnitude > 0.001f) vehicle.rotation = Quaternion.LookRotation(reflected.normalized, vehicle.up);

        vehicle.position += normal.normalized * collisionRadius * 0.5f;
        speed *= Mathf.Clamp01(collisionSpeedMultiplier);
        collisionTilt = -Mathf.Sign(Vector3.Dot(vehicle.right, normal)) * collisionTiltAngle;
        PlayableSoundEffects.Play(PlayableSfx.Collision);
    }

    void UpdateCollisionTilt()
    {
        if (carView == null) return;
        collisionTilt = Mathf.MoveTowards(collisionTilt, 0f, collisionTiltReturnSpeed * Time.deltaTime);
        carView.SetTiltBody(collisionTilt);
    }

    void RestoreCoins()
    {
        foreach (GameObject coin in collectedCoins)
        {
            if (coin != null) coin.SetActive(true);
        }

        collectedCoins.Clear();
        collectedCoinIds.Clear();
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
