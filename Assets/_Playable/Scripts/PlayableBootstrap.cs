using Gre.pjcode.Scenes.InGame;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
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
    [SerializeField] private Rigidbody sphereBody;
    [SerializeField] private float linearDamping = 0.6f;
    [SerializeField] private float steerSpeed = 14f;
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float groundRayHeight = 8f;
    [SerializeField] private float groundOffset = 0.08f;
    [SerializeField] private float dashForceMultiplier = 0.35f;
    [SerializeField] private float dashBoosterEffectDuration = 2f;
    [SerializeField] private int coinAmountFallback = 100;
    [SerializeField] private float gameEndedDelay = 0.25f;
    [Header("Camera")]
    [SerializeField] private Transform puzzleCameraPoint;
    [SerializeField] private float buildToDefaultCameraTransitionDuration = 0.45f;
    [Header("Slingshot")]
    [SerializeField] private string slingshotName = "Slingshot";
    [SerializeField] private LineRenderer slingshotRope;
    [SerializeField] private Transform slingshotStartPoint;
    [SerializeField] private Transform slingshotEndPoint;
    [SerializeField] private Transform slingshotCarPointA;
    [SerializeField] private Transform slingshotCarPointB;
    [SerializeField] private bool hideSlingshotOnLaunch = true;
    [SerializeField, Range(0f, 1f)] private float slingshotCarHeight = 0.55f;
    [SerializeField] private float slingshotFallbackHalfWidth = 0.9f;
    [SerializeField] private float slingshotFallbackHeight = 1.2f;
    [SerializeField] private float slingshotFallbackRearOffset = 0.9f;
    [Header("Slingshot UI")]
    [SerializeField] private GameObject slingshotPullUiRoot;
    [SerializeField] private Image slingshotPullFillImage;
    [SerializeField] private TMP_Text slingshotPullPercentText;
    [SerializeField, Range(0f, 1f)] private float slingshotPullMaxFillAmount = 0.4f;
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
    [SerializeField] private AudioClip sfxDash;
    [SerializeField] private AudioClip sfxCollision;
    [SerializeField] private AudioClip sfxFinish;
    [SerializeField] private AudioClip sfxClaim;
    [Header("Run Markers")]
    [SerializeField] private Transform finishFlag;
    [SerializeField] private Transform recordLine;
    [SerializeField] private float finishFlagAnimationDuration = 0.35f;
    [Header("Loop Audio")]
    [SerializeField] private AudioClip sfxMoveLoop;
    [SerializeField, Range(0f, 1f)] private float moveLoopVolume = 0.75f;
    [SerializeField] private AudioClip music;
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.6f;

    Transform vehicle;
    CarView carView;
    CarSphereTracer carTracer;
    [SerializeField]private InGamePuzzleUiView puzzleUi;
    [SerializeField]private InGameResultUiView resultUi;
    [SerializeField]private InGameRunUiView runUi;
    GameObject buildUi;
    Camera followCamera;
    Vector3 startPosition;
    Quaternion startRotation;
    Vector3 gameplayCameraPosition;
    Quaternion gameplayCameraRotation;
    Vector3 cameraOffset;
    Vector2 dragStart;
    State state;
    float pull;
    Vector3 launchForce;
    float speed;
    float stopTime;
    CarTerrainCollider terrainCollider;
    TerrainType runningTerrain = TerrainType.Max;
    float distance;
    int pendingResultGold;
    int pendingBoardColumns;
    InGameGetItemUiView getItemUi;
    readonly List<GameObject> collectedBoardUpgrades = new List<GameObject>();
    float steer;
    bool dragging;
    Vector3 slingshotStartWorldPosition;
    Vector3 slingshotEndWorldPosition;
    bool slingshotReady;
    Coroutine gameEndedRoutine;
    Coroutine cameraTransitionRoutine;
    Coroutine finishFlagRoutine;
    AudioSource moveLoopSource;
    AudioSource musicSource;
    Vector3 finishFlagBasePosition;
    Vector3 recordLinePosition;
    bool hasRecordLinePosition;
    readonly RaycastHit[] groundHits = new RaycastHit[8];
    readonly HashSet<int> collectedCoinIds = new HashSet<int>();
    readonly HashSet<int> triggeredDashIds = new HashSet<int>();
    readonly List<GameObject> collectedCoins = new List<GameObject>();

    void Awake()
    {
        RegisterSoundEffects();
        SetupLoopAudio();

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
        CacheResultUi();
        CacheRunUi();
        getItemUi = FindSceneObjectOfType<InGameGetItemUiView>();
        //buildUi = GameObject.Find("PuzzleUi");
        startPosition = vehicle.position;
        startRotation = vehicle.rotation;
        followCamera = Camera.main;
        if (followCamera != null)
        {
            gameplayCameraPosition = followCamera.transform.position;
            gameplayCameraRotation = followCamera.transform.rotation;
            cameraOffset = followCamera.transform.position - vehicle.position;
        }
        SnapToGround(startRotation * Vector3.forward, true);
        startPosition = vehicle.position;
        startRotation = vehicle.rotation;
        SetupSlingshot();
        UpdateSlingshotPullUi();
        SetSlingshotPullUiVisible(false);
        SetupRunMarkers();

        SetupPhysics();

        foreach (Button button in Resources.FindObjectsOfTypeAll<Button>())
        {
            if (!button.gameObject.scene.IsValid()) continue;
            if (button.name.Contains("Continue") && (resultUi == null || !button.transform.IsChildOf(resultUi.transform))) button.onClick.AddListener(PlayworksBridge.InstallFullGame);
            if (button.name.Contains("Start"))
            {
                button.onClick.AddListener(PlayMusic);
                button.onClick.AddListener(HideBuildUi);
            }
        }

        PlayMusic();
        if (IsBuildUiVisible()) ApplyPuzzleCameraPose();
    }

    void Update()
    {
        if (state == State.Aim)
        {
            UpdateAim();
            if (state == State.Aim && dragging) UpdateSlingshot();
        }
        else if (state == State.Run) UpdateRun();
        UpdateCarSteeringView();
    }

    void LateUpdate()
    {
        if (state == State.Run && sphereBody != null) UpdateVehiclePresentation(Time.deltaTime);
        if (followCamera == null || vehicle == null || state == State.Aim || cameraTransitionRoutine != null) return;
        followCamera.transform.position = Vector3.Lerp(followCamera.transform.position, vehicle.position + cameraOffset, 1f - Mathf.Exp(-5f * Time.deltaTime));
    }

    void UpdateAim()
    {
        if (PointerDown(out Vector2 pointer) && !PointerOverUi())
        {
            dragging = true;
            dragStart = pointer;
            SetSlingshotPullUiVisible(!IsBuildUiVisible());
            PlayMusic();
            PlayableSoundEffects.Play(PlayableSfx.Pull);
        }

        if (!dragging) return;

        if (PointerHeld(out pointer))
        {
            ApplyAim(pointer);
        }

        if (PointerUp(out pointer))
        {
            ApplyAim(pointer);
            dragging = false;
            SetSlingshotPullUiVisible(false);
            if (pull < maxPull / 12f) return;
            sphereBody.position = vehicle.position;
            sphereBody.isKinematic = false;
            sphereBody.linearVelocity = Vector3.zero;
            sphereBody.angularVelocity = Vector3.zero;
            foreach (Collider c in vehicle.GetComponentsInChildren<Collider>()) c.enabled = false;
            ApplyDash(launchForce, 1.2f);
            stopTime = 0f;
            state = State.Run;
            if (runUi != null) runUi.BeginRun();
            PlayableSoundEffects.Play(PlayableSfx.Launch);
            if (GetCarSpeedMultiplier() > 0f) PlayMoveLoop();
            PlayMusic();
            HideBuildUi();
            SetSlingshotVisible(!hideSlingshotOnLaunch);
        }
    }

    float GetForceWeight(float multiplier)
    {
        // Original Dash: force weight 0.45 * 1.2, plus equipped-part contributions.
        float force = 0.45f * multiplier;
        float total = 0f;
        for (int i = (int)TerrainType.Default; i < (int)TerrainType.Max; i++)
        {
            float performance = puzzleUi == null ? 0f : puzzleUi.GetTerrainPerformance((TerrainType)i);
            total += performance;
            force += performance * (i == (int)TerrainType.Default ? 0.075f : 0.065f);
        }
        if (total == 0f) force *= 0.7f;
        return force * GetCarSpeedMultiplier();
    }

    void ApplyDash(Vector3 forward, float multiplier)
    {
        float weight = GetForceWeight(multiplier);
        sphereBody.AddForce(new Vector3(forward.x, 0f, forward.z) * weight, ForceMode.Impulse);
        sphereBody.AddTorque(new Vector3(forward.z, -forward.x, 0f) * weight, ForceMode.Impulse);
    }

    void SetupPhysics()
    {
        if (sphereBody == null) { Debug.LogError("PlayableBootstrap needs the original CarSphere Rigidbody."); enabled = false; return; }
        sphereBody.gameObject.SetActive(true);
        sphereBody.isKinematic = true;
        sphereBody.useGravity = true;
        sphereBody.mass = 5f;
        sphereBody.linearDamping = linearDamping;
        sphereBody.angularDamping = 0.01f;
        sphereBody.collisionDetectionMode = CollisionDetectionMode.Continuous;
        sphereBody.interpolation = RigidbodyInterpolation.Interpolate;
        sphereBody.position = vehicle.position;
        terrainCollider = sphereBody.GetComponent<CarTerrainCollider>();
        if (terrainCollider == null) terrainCollider = sphereBody.gameObject.AddComponent<CarTerrainCollider>();
        terrainCollider.TriggerEntered = HandlePhysicsTrigger;
        terrainCollider.TriggerExited = HandlePhysicsTriggerExit;
        foreach (Collider c in vehicle.GetComponentsInChildren<Collider>()) c.enabled = false;
    }

    float GetCarSpeedMultiplier()
    {
        return LunaManager.ins == null ? 1f : Mathf.Max(0f, LunaManager.ins.carSpeedMultiplier);
    }

    void SetupRunMarkers()
    {
        if (finishFlag != null)
        {
            finishFlagBasePosition = finishFlag.position;
            finishFlag.gameObject.SetActive(false);
        }

        if (recordLine != null)
        {
            recordLine.gameObject.SetActive(false);
        }
    }

    void ShowRunMarkers()
    {
        Vector3 stopPosition = vehicle == null ? startPosition : vehicle.position;

        // 1. FinishFlag: Cắm ngay vị trí xe dừng lại và bám sát mặt đất
        if (finishFlag != null)
        {
            Vector3 flagPos = stopPosition;
        
            // Raycast từ trên đỉnh vị trí xe xuống để tìm đúng bề mặt đường tại điểm dừng
            Vector3 rayOrigin = new Vector3(stopPosition.x, stopPosition.y + groundRayHeight, stopPosition.z);
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, groundRayHeight * 2f, groundMask, QueryTriggerInteraction.Ignore))
            {
                flagPos.y = hit.point.y + 0.05f;
                // Xoay cờ theo hướng xe đang đỗ và độ nghiêng mặt đường
                finishFlag.rotation = Quaternion.LookRotation(vehicle.forward, hit.normal);
            }
            else
            {
                flagPos.y = stopPosition.y + 0.05f;
                finishFlag.rotation = vehicle.rotation;
            }

            finishFlag.position = flagPos;
            finishFlag.gameObject.SetActive(true);

            if (finishFlagRoutine != null) StopCoroutine(finishFlagRoutine);
            finishFlagRoutine = StartCoroutine(AnimateFinishFlag());
        }

        // 2. RecordLine: Giữ nguyên logic chiếu theo trục dọc giữa đường
        if (recordLine != null)
        {
            recordLinePosition = GetMarkerPosition(stopPosition, recordLine, 0.02f);
            hasRecordLinePosition = true;
        }
    }

    void ShowRecordLineForNextTurn()
    {
        if (recordLine == null || !hasRecordLinePosition) return;
    
        recordLine.position = recordLinePosition;
        recordLine.rotation = Quaternion.LookRotation(startRotation * Vector3.forward, Vector3.up);
        recordLine.gameObject.SetActive(true);
    }

    void HideFinishFlag()
    {
        if (finishFlagRoutine != null)
        {
            StopCoroutine(finishFlagRoutine);
            finishFlagRoutine = null;
        }

        if (finishFlag != null) finishFlag.gameObject.SetActive(false);
    }

    Vector3 GetMarkerPosition(Vector3 stopPosition, Transform markerTransform, float heightOffset = 0.05f)
    {
        Vector3 forward = startRotation * Vector3.forward;
        float forwardDistance = Mathf.Max(0f, Vector3.Dot(stopPosition - startPosition, forward));
    
        // Lấy tọa độ X, Z theo hướng xe chạy
        Vector3 basePos = markerTransform != null ? markerTransform.position : startPosition;
        Vector3 targetPos = basePos + forward * forwardDistance;
    
        // Raycast bắn từ trên xuống để dán chặt Marker vào mặt đường
        Vector3 rayOrigin = new Vector3(targetPos.x, stopPosition.y + groundRayHeight, targetPos.z);
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, groundRayHeight * 2f, groundMask, QueryTriggerInteraction.Ignore))
        {
            targetPos.y = hit.point.y + heightOffset;
        }
        else
        {
            targetPos.y = stopPosition.y + heightOffset;
        }

        return targetPos;
    }

    IEnumerator AnimateFinishFlag()
    {
        if (finishFlag == null) yield break;

        float duration = Mathf.Max(0.01f, finishFlagAnimationDuration);
        Vector3 baseScale = finishFlag.localScale;
        finishFlag.localScale = Vector3.zero;

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            // Hiệu ứng nảy nhẹ khi cờ xuất hiện
            float bounce = Mathf.Sin(t * Mathf.PI * 0.5f);
            finishFlag.localScale = baseScale * bounce;
            yield return null;
        }

        finishFlag.localScale = baseScale;
        finishFlagRoutine = null;
    }

    void SetFinishFlagZ(float z)
    {
        if (finishFlag == null) return;
        Vector3 euler = finishFlag.localEulerAngles;
        euler.z = z;
        finishFlag.localEulerAngles = euler;
    }

    void UpdateRun()
    {
        steer = PointerHeld(out Vector2 pointer) ? Mathf.Clamp((pointer.x / Mathf.Max(1f, Screen.width) - 0.5f) * 2f, -1f, 1f) : 0f;
        ControlSphere(Time.deltaTime);
        speed = sphereBody.linearVelocity.magnitude;
        distance = Mathf.Max(distance, sphereBody.position.z);
        if (runUi != null) runUi.UpdateRun(distance, speed);
        stopTime = sphereBody.linearVelocity.z > 0f && speed > 3f ? stopTime : stopTime + Time.deltaTime;
        if (stopTime >= 0.5f || sphereBody.position.y < startPosition.y - 200f) FinishRun();
    }

    void ControlSphere(float deltaTime)
    {
        Vector3 velocity = sphereBody.linearVelocity;
        float currentSpeed = velocity.magnitude;
        if (Mathf.Abs(steer) <= 0.1f || currentSpeed <= 0.1f) return;
        Vector3 forward = new Vector3(0f, velocity.y, velocity.z).normalized;
        Vector3 target = new Vector3(steer, 0f, 1f).normalized;
        if (Vector3.Angle(forward, target) > 15f) target = Vector3.RotateTowards(forward, target, Mathf.Deg2Rad * 15f, 0f);
        Vector3 newVelocity = Vector3.Lerp(velocity.normalized, target, 1.5f * deltaTime).normalized * currentSpeed;
        sphereBody.linearVelocity = newVelocity;
        float radius = sphereBody.GetComponent<SphereCollider>().radius * sphereBody.transform.localScale.x;
        sphereBody.angularVelocity = Vector3.Cross(Vector3.up, newVelocity) / radius;
    }

    void FixedUpdate()
    {
        if (state != State.Run || sphereBody == null) return;
        StepPhysics(Time.fixedDeltaTime);
    }

    void StepPhysics(float deltaTime)
    {
        TerrainType terrain = terrainCollider.IsGrounded ? terrainCollider.Terrain : TerrainType.Air;
        if (terrain != runningTerrain)
        {
            runningTerrain = terrain;
            if (puzzleUi != null) puzzleUi.ActivateTerrainParts(terrain);
            if (carView != null) carView.SetSink(terrain == TerrainType.Water);
            if (carTracer != null) carTracer.PlayRunningEffect(terrain);
        }
        float performance = puzzleUi == null ? 0f : puzzleUi.GetTerrainPerformance(terrain);
        float damping = terrain == TerrainType.Air ? 0.01f : linearDamping;
        if (terrain == TerrainType.Dirt) { damping += 0.05f; performance *= 1.5f; }
        if (terrain == TerrainType.Water) { damping += 0.075f; performance *= 2.5f; }
        sphereBody.linearDamping = Mathf.Max(0.01f, damping - performance);
        float air = puzzleUi == null ? 0f : puzzleUi.GetTerrainPerformance(TerrainType.Air);
        float gravityWeight = terrainCollider.IsGrounded ? 3f : 3f - air * 2f;
        sphereBody.AddForce(Physics.gravity * (1.5f * gravityWeight), ForceMode.Acceleration);
        bool braking = sphereBody.linearVelocity.sqrMagnitude < 49f;
        if (braking) sphereBody.linearVelocity = Vector3.MoveTowards(sphereBody.linearVelocity, Vector3.zero, 10f * deltaTime);
        else
        {
            Vector3 direction = sphereBody.linearVelocity.normalized;
            float slope = Vector3.Angle(Vector3.up, direction) - 90f;
            if (slope > 0f) sphereBody.AddForce(direction * Mathf.Lerp(0f, 15f, slope / 90f), ForceMode.Force);
        }
    }

    void UpdateVehiclePresentation(float deltaTime)
    {
        // Transform contains the interpolated render pose; Rigidbody.position is the raw physics pose.
        vehicle.position = sphereBody.transform.position;
        if (sphereBody.linearVelocity.sqrMagnitude > 0.0001f)
        {
            Vector3 direction = Vector3.RotateTowards(vehicle.forward, sphereBody.linearVelocity, Mathf.Deg2Rad * deltaTime * 360f, 0f);
            Vector3 angles = Quaternion.LookRotation(direction).eulerAngles;
            angles.y = Mathf.Clamp(Mathf.DeltaAngle(0f, angles.y), -30f, 30f);
            angles.x = sphereBody.linearVelocity.sqrMagnitude < 49f ? vehicle.eulerAngles.x : Mathf.Min(Mathf.DeltaAngle(0f, angles.x), 35f);
            vehicle.rotation = Quaternion.Euler(angles);
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
        PlayableSoundEffects.Register(PlayableSfx.Dash, sfxDash);
        PlayableSoundEffects.Register(PlayableSfx.Collision, sfxCollision);
        PlayableSoundEffects.Register(PlayableSfx.Finish, sfxFinish);
        PlayableSoundEffects.Register(PlayableSfx.Claim, sfxClaim);
    }

    void FinishRun()
    {
        if (state == State.Done) return;

        state = State.Done;
        sphereBody.isKinematic = true;
        if (carView != null) carView.InactivateAllParts();
        if (runUi != null) runUi.FinishRun(distance);
        StopMoveLoop();
        StopMusic();
        ShowRunMarkers();
        PlayableSoundEffects.Play(PlayableSfx.Finish);
        OpenResultUi();
        if (gameEndedRoutine != null) StopCoroutine(gameEndedRoutine);
        gameEndedRoutine = StartCoroutine(NotifyPlayActionAfterResultUi());
    }

    void ResetRun()
    {
        if (gameEndedRoutine != null)
        {
            StopCoroutine(gameEndedRoutine);
            gameEndedRoutine = null;
        }

        pull = 0f;
        UpdateSlingshotPullUi();
        SetSlingshotPullUiVisible(false);
        HideFinishFlag();
        ShowRecordLineForNextTurn();
        speed = 0f;
        sphereBody.isKinematic = true;
        terrainCollider.ResetContacts();
        runningTerrain = TerrainType.Max;
        if (carView != null) carView.InactivateAllParts();
        distance = 0f;
        if (runUi != null) runUi.ResetRun();
        steer = 0f;
        dragging = false;
        state = State.Aim;
        RestoreCoins();
        vehicle.SetPositionAndRotation(startPosition, startRotation);
        SnapToGround(startRotation * Vector3.forward, true);
        if (carView != null) carView.SetTiltBody(0f);
        if (carView != null) carView.SetSink(false, true);
        sphereBody.position = vehicle.position;
        sphereBody.rotation = Quaternion.identity;
        if (followCamera != null) followCamera.transform.position = vehicle.position + cameraOffset;
        SetSlingshotVisible(true);
        UpdateSlingshot();
        StopMoveLoop();
        PlayMusic();
        ShowBuildUi();
    }

    void SetupLoopAudio()
    {
        moveLoopSource = CreateLoopAudioSource("MoveLoopAudio", sfxMoveLoop, moveLoopVolume);
        musicSource = CreateLoopAudioSource("MusicAudio", music, musicVolume);
    }

    AudioSource CreateLoopAudioSource(string sourceName, AudioClip clip, float volume)
    {
        GameObject go = new GameObject(sourceName);
        go.transform.SetParent(transform, false);
        AudioSource source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = volume;
        source.clip = clip;
        return source;
    }

    void PlayMoveLoop()
    {
        if (moveLoopSource == null || sfxMoveLoop == null) return;
        moveLoopSource.clip = sfxMoveLoop;
        moveLoopSource.volume = moveLoopVolume;
        if (!moveLoopSource.isPlaying) moveLoopSource.Play();
    }

    void StopMoveLoop()
    {
        if (moveLoopSource != null) moveLoopSource.Stop();
    }

    void PlayMusic()
    {
        if (musicSource == null || music == null) return;
        musicSource.clip = music;
        musicSource.volume = musicVolume;
        if (!musicSource.isPlaying) musicSource.Play();
    }

    void StopMusic()
    {
        if (musicSource != null) musicSource.Stop();
    }

    void CacheResultUi()
    {
        if (resultUi == null) resultUi = FindSceneObjectOfType<InGameResultUiView>();
        if (resultUi == null)
        {
            Debug.LogWarning("PlayableBootstrap could not find InGameResultUiView. Result UI will not open.");
            return;
        }

        resultUi.SetClaimAction(ClaimResultAndReset);
    }

    void CacheRunUi()
    {
        if (runUi == null) runUi = FindSceneObjectOfType<InGameRunUiView>();
        if (runUi != null) runUi.ResetRun();
    }

    void OpenResultUi()
    {
        CacheResultUi();
        if (resultUi == null) return;
        pendingResultGold = Mathf.Max(0, (int)distance);
        resultUi.transform.SetAsLastSibling();
        resultUi.Open(pendingResultGold);
    }

    void ClaimResultAndReset()
    {
        if (state != State.Done) return;
        if (puzzleUi != null && pendingResultGold > 0) puzzleUi.AddGold(pendingResultGold);
        pendingResultGold = 0;
        Vector2Int before = puzzleUi == null ? Vector2Int.zero : puzzleUi.GridSize;
        int received = puzzleUi == null ? 0 : puzzleUi.ExpandBoard(pendingBoardColumns);
        pendingBoardColumns = 0;
        ResetRun();
        if (received > 0 && getItemUi != null)
        {
            getItemUi.Show(puzzleUi.AttachmentIconSprite, "BOARD EXPANSION", "+" + received + " COLUMN" + (received > 1 ? "S" : "") + "\n" +
                before.x + " x " + before.y + "  >  " + puzzleUi.GridSize.x + " x " + puzzleUi.GridSize.y);
        }
    }

    IEnumerator NotifyPlayActionAfterResultUi()
    {
        yield return null;
        if (gameEndedDelay > 0f) yield return new WaitForSeconds(gameEndedDelay);
        if (LunaManager.ins != null) LunaManager.ins.CheckClickShowEndCard();
        else PlayworksBridge.GameEnded();
        gameEndedRoutine = null;
    }

    void ApplyAim(Vector2 pointer)
    {
        Vector3 forward = startRotation * Vector3.forward;
        Vector3 right = startRotation * Vector3.right;
        Vector2 drag = pointer - dragStart;
        drag.x = Mathf.Clamp(drag.x, -350f, 350f);
        drag.y = Mathf.Clamp(drag.y, -600f, 0f);
        drag = Vector2.ClampMagnitude(drag, 600f);
        Vector3 pullOffset = right * (drag.x * 0.007f) + forward * (drag.y * 0.0125f);
        launchForce = right * (-drag.x * 0.7f) + forward * -drag.y;
        pull = maxPull * drag.magnitude / 600f;
        UpdateSlingshotPullUi();
        Vector3 launchForward = launchForce.sqrMagnitude > 0.001f ? launchForce.normalized : forward;
        vehicle.SetPositionAndRotation(startPosition + pullOffset, Quaternion.LookRotation(launchForward, startRotation * Vector3.up));
        SnapToGround(launchForward, true);
    }

    void UpdateSlingshotPullUi()
    {
        float ratio = maxPull > 0f ? Mathf.Clamp01(pull / maxPull) : 0f;
        if (slingshotPullFillImage != null) slingshotPullFillImage.fillAmount = ratio * slingshotPullMaxFillAmount;
        if (slingshotPullPercentText != null) slingshotPullPercentText.text = Mathf.RoundToInt(ratio * 100f) + "%";
    }

    void SetSlingshotPullUiVisible(bool isVisible)
    {
        if (slingshotPullUiRoot == null) return;

        CanvasGroup canvasGroup = slingshotPullUiRoot.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = isVisible ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        slingshotPullUiRoot.SetActive(isVisible);
    }

    void SetupSlingshot()
    {
        if (slingshotRope == null && !string.IsNullOrEmpty(slingshotName))
        {
            GameObject slingshot = GameObject.Find(slingshotName);
            if (slingshot != null) slingshotRope = slingshot.GetComponentInChildren<LineRenderer>();
        }

        if (slingshotRope == null) return;

        int pointCount = slingshotRope.positionCount;
        slingshotStartWorldPosition = pointCount > 0
            ? RopeToWorld(slingshotRope.GetPosition(0))
            : slingshotRope.transform.position - slingshotRope.transform.right * 2f;
        slingshotEndWorldPosition = pointCount > 1
            ? RopeToWorld(slingshotRope.GetPosition(pointCount - 1))
            : slingshotRope.transform.position + slingshotRope.transform.right * 2f;
        slingshotRope.positionCount = 4;
        slingshotReady = true;
        SetSlingshotVisible(true);
        UpdateSlingshot();
    }

    void SetSlingshotVisible(bool isVisible)
    {
        if (slingshotRope != null) slingshotRope.enabled = isVisible;
    }

    void UpdateSlingshot()
    {
        if (!slingshotReady || slingshotRope == null || !slingshotRope.enabled) return;

        Vector3 startPoint = slingshotStartPoint == null ? slingshotStartWorldPosition : slingshotStartPoint.position;
        Vector3 endPoint = slingshotEndPoint == null ? slingshotEndWorldPosition : slingshotEndPoint.position;
        GetCarEndPoints(startPoint, endPoint, out Vector3 firstCarPoint, out Vector3 secondCarPoint);

        slingshotRope.SetPosition(0, WorldToRope(startPoint));
        slingshotRope.SetPosition(1, WorldToRope(firstCarPoint));
        slingshotRope.SetPosition(2, WorldToRope(secondCarPoint));
        slingshotRope.SetPosition(3, WorldToRope(endPoint));
    }

    void GetCarEndPoints(Vector3 startPoint, Vector3 endPoint, out Vector3 firstCarPoint, out Vector3 secondCarPoint)
    {
        if (slingshotCarPointA != null && slingshotCarPointB != null)
        {
            AssignNearestPair(startPoint, endPoint, slingshotCarPointA.position, slingshotCarPointB.position, out firstCarPoint, out secondCarPoint);
            return;
        }

        Transform carRoot = carView == null ? vehicle : carView.transform;
        if (TryGetLocalRendererBounds(carRoot, out Bounds bounds))
        {
            float y = Mathf.Lerp(bounds.min.y, bounds.max.y, slingshotCarHeight);
            float z = bounds.min.z;
            Vector3 left = carRoot.TransformPoint(new Vector3(bounds.min.x, y, z));
            Vector3 right = carRoot.TransformPoint(new Vector3(bounds.max.x, y, z));
            AssignNearestPair(startPoint, endPoint, left, right, out firstCarPoint, out secondCarPoint);
            return;
        }

        Vector3 fallbackCenter = vehicle.position - vehicle.forward * Mathf.Abs(slingshotFallbackRearOffset) + vehicle.up * slingshotFallbackHeight;
        Vector3 fallbackLeft = fallbackCenter - vehicle.right * Mathf.Abs(slingshotFallbackHalfWidth);
        Vector3 fallbackRight = fallbackCenter + vehicle.right * Mathf.Abs(slingshotFallbackHalfWidth);
        AssignNearestPair(startPoint, endPoint, fallbackLeft, fallbackRight, out firstCarPoint, out secondCarPoint);
    }

    static bool TryGetLocalRendererBounds(Transform root, out Bounds bounds)
    {
        bounds = default;
        if (root == null) return false;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        bool hasBounds = false;
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled) continue;

            Bounds worldBounds = renderer.bounds;
            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;
            EncapsulateLocalPoint(root, new Vector3(min.x, min.y, min.z), ref bounds, ref hasBounds);
            EncapsulateLocalPoint(root, new Vector3(min.x, min.y, max.z), ref bounds, ref hasBounds);
            EncapsulateLocalPoint(root, new Vector3(min.x, max.y, min.z), ref bounds, ref hasBounds);
            EncapsulateLocalPoint(root, new Vector3(min.x, max.y, max.z), ref bounds, ref hasBounds);
            EncapsulateLocalPoint(root, new Vector3(max.x, min.y, min.z), ref bounds, ref hasBounds);
            EncapsulateLocalPoint(root, new Vector3(max.x, min.y, max.z), ref bounds, ref hasBounds);
            EncapsulateLocalPoint(root, new Vector3(max.x, max.y, min.z), ref bounds, ref hasBounds);
            EncapsulateLocalPoint(root, new Vector3(max.x, max.y, max.z), ref bounds, ref hasBounds);
        }

        return hasBounds;
    }

    static void EncapsulateLocalPoint(Transform root, Vector3 worldPoint, ref Bounds bounds, ref bool hasBounds)
    {
        Vector3 localPoint = root.InverseTransformPoint(worldPoint);
        if (!hasBounds)
        {
            bounds = new Bounds(localPoint, Vector3.zero);
            hasBounds = true;
            return;
        }

        bounds.Encapsulate(localPoint);
    }

    static void AssignNearestPair(Vector3 startPoint, Vector3 endPoint, Vector3 first, Vector3 second, out Vector3 nearestStart, out Vector3 nearestEnd)
    {
        float directDistance = (startPoint - first).sqrMagnitude + (endPoint - second).sqrMagnitude;
        float swappedDistance = (startPoint - second).sqrMagnitude + (endPoint - first).sqrMagnitude;
        if (swappedDistance < directDistance)
        {
            nearestStart = second;
            nearestEnd = first;
            return;
        }

        nearestStart = first;
        nearestEnd = second;
    }

    Vector3 RopeToWorld(Vector3 position)
    {
        return slingshotRope.useWorldSpace ? position : slingshotRope.transform.TransformPoint(position);
    }

    Vector3 WorldToRope(Vector3 position)
    {
        return slingshotRope.useWorldSpace ? position : slingshotRope.transform.InverseTransformPoint(position);
    }

    void HideBuildUi()
    {
        RestoreGameplayCameraPose(true);
        if (puzzleUi != null) puzzleUi.SetOpen(false);
        else if (buildUi != null) buildUi.SetActive(false);
    }

    void ShowBuildUi()
    {
        if (puzzleUi != null) puzzleUi.SetOpen(true, true);
        else if (buildUi != null) buildUi.SetActive(true);
        StopCameraTransition();
        ApplyPuzzleCameraPose();
    }

    bool IsBuildUiVisible()
    {
        if (puzzleUi != null) return puzzleUi.gameObject.activeInHierarchy;
        return buildUi != null && buildUi.activeInHierarchy;
    }

    void ApplyPuzzleCameraPose()
    {
        if (followCamera == null || puzzleCameraPoint == null) return;
        StopCameraTransition();
        followCamera.transform.SetPositionAndRotation(puzzleCameraPoint.position, puzzleCameraPoint.rotation);
    }

    void RestoreGameplayCameraPose(bool animated = false)
    {
        if (followCamera == null) return;
        if (animated && buildToDefaultCameraTransitionDuration > 0f)
        {
            StartCameraTransition(gameplayCameraPosition, gameplayCameraRotation);
            return;
        }

        StopCameraTransition();
        followCamera.transform.SetPositionAndRotation(gameplayCameraPosition, gameplayCameraRotation);
    }

    void StartCameraTransition(Vector3 targetPosition, Quaternion targetRotation)
    {
        StopCameraTransition();
        cameraTransitionRoutine = StartCoroutine(AnimateCameraToPose(targetPosition, targetRotation));
    }

    void StopCameraTransition()
    {
        if (cameraTransitionRoutine == null) return;
        StopCoroutine(cameraTransitionRoutine);
        cameraTransitionRoutine = null;
    }

    IEnumerator AnimateCameraToPose(Vector3 targetPosition, Quaternion targetRotation)
    {
        Vector3 startCameraPosition = followCamera.transform.position;
        Quaternion startCameraRotation = followCamera.transform.rotation;
        float duration = Mathf.Max(0.01f, buildToDefaultCameraTransitionDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);
            followCamera.transform.SetPositionAndRotation(
                Vector3.Lerp(startCameraPosition, targetPosition, t),
                Quaternion.Slerp(startCameraRotation, targetRotation, t));
            yield return null;
        }

        followCamera.transform.SetPositionAndRotation(targetPosition, targetRotation);
        cameraTransitionRoutine = null;
    }

    void HandlePhysicsTrigger(Collider other)
    {
        if (state != State.Run) return;
        TryCollectBoardUpgrade(other);
        TryCollectCoin(other);
        TryTriggerDash(other);
    }

    void HandlePhysicsTriggerExit(Collider other)
    {
        GameObject dash = GetDashObject(other);
        if (dash != null) triggeredDashIds.Remove(dash.GetInstanceID());
    }
    bool TryCollectBoardUpgrade(Collider collider)
    {
        GameObject item = GetTaggedObject(collider, "Attachment");
        if (item == null) return false;
        if (state != State.Run || puzzleUi == null || collectedBoardUpgrades.Contains(item)) return true;
        collectedBoardUpgrades.Add(item);
        if (puzzleUi.GridSize.x + pendingBoardColumns < InGamePuzzleUiView.MaxBoardWidth)
        {
            pendingBoardColumns++;
            if (carTracer != null) carTracer.GetAttachment();
            PlayableSoundEffects.Play(PlayableSfx.Coin);
        }
        item.SetActive(false);
        return true;
    }

    bool TryCollectCoin(Collider collider)
    {
        GameObject coin = GetTaggedObject(collider, "Coin");
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

    bool TryTriggerDash(Collider collider)
    {
        GameObject dash = GetDashObject(collider);
        if (dash == null) return false;

        int id = dash.GetInstanceID();
        if (triggeredDashIds.Contains(id)) return true;

        triggeredDashIds.Add(id);
        ApplyDash(dash.transform.forward * (600f * dashForceMultiplier), 1f);
        ActivateJetBoosterEffects();
        if (carTracer != null)
        {
            carTracer.PlayDashEffect();
            carTracer.SustainDash(1f, 0.5f);
        }

        PlayableSoundEffects.Play(PlayableSfx.Dash);
        return true;
    }

    void ActivateJetBoosterEffects()
    {
        if (carView == null) return;

        float duration = Mathf.Max(0.1f, dashBoosterEffectDuration);
        foreach (PartView part in carView.AttachedParts)
        {
            if (part is JetBoosterPartView) part.Activate(duration);
        }
    }

    GameObject GetDashObject(Collider collider)
    {
        if (collider == null || collider.transform.IsChildOf(vehicle)) return null;

        Transform current = collider.transform;
        while (current != null)
        {
            if (current.CompareTag("Dash") || current.name.ToLowerInvariant().Contains("dash")) return current.gameObject;
            current = current.parent;
        }

        return null;
    }

    GameObject GetTaggedObject(Collider collider, string tagName)
    {
        if (collider == null || collider.transform.IsChildOf(vehicle)) return null;

        Transform current = collider.transform;
        while (current != null)
        {
            if (current.CompareTag(tagName)) return current.gameObject;
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

    void UpdateCarSteeringView()
    {
        if (carView == null || state != State.Run) return;
        carView.SetTiltBody(Mathf.Lerp(carView.BodyTiltAngle, steer * 8f, Time.deltaTime * 10f));
        carView.SetWheelSteerAngle(Mathf.Lerp(carView.WheelSteerAngle, steer * 20f, Time.deltaTime * 5f));
    }

    void RestoreCoins()
    {
        foreach (GameObject coin in collectedCoins)
        {
            if (coin != null) coin.SetActive(true);
        }

        foreach (GameObject item in collectedBoardUpgrades)
        {
            if (item != null) item.SetActive(true);
        }
        collectedBoardUpgrades.Clear();
        collectedCoins.Clear();
        collectedCoinIds.Clear();
        triggeredDashIds.Clear();
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
        int hitCount = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits, groundRayHeight * 2f, groundMask, QueryTriggerInteraction.Ignore);
        if (hitCount == 0) return;

        RaycastHit bestHit = default;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = groundHits[i];
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
