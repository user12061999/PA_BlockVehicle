using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[System.Serializable]
public sealed class RoadFrictionZone
{
    public string name;
    public float startDistance;
    public float endDistance = 9999f;
    public float friction = 1f;
}

public sealed class BlockVehiclePlayable : MonoBehaviour
{
    enum PlayState
    {
        Aim,
        Run,
        Result
    }

    enum TerrainKind
    {
        Default,
        Dirt,
        Water,
        Air
    }

    [Header("Scene References")]
    [SerializeField] Transform vehicle;
    [SerializeField] Rigidbody vehicleBody;
    [SerializeField] SphereCollider sphereCollider;
    [SerializeField] Transform carView;
    [SerializeField] Collider launchDragBox;
    [SerializeField] Transform trackRoot;
    [SerializeField] LineRenderer launchLine;
    [SerializeField] Transform leftVehicleTailAnchor;
    [SerializeField] Transform rightVehicleTailAnchor;
    [SerializeField] Camera followCamera;
    [SerializeField] RectTransform aimUiBlocker;

    [Header("Launch")]
    [SerializeField] float pullMin = 50f;
    [SerializeField] float pullMax = 600f;
    [SerializeField] Vector2 pullClampX = new Vector2(-350f, 350f);
    [SerializeField] Vector2 pullClampY = new Vector2(-600f, 0f);
    [SerializeField] float baseAddForceWeight = 0.45f;
    [SerializeField] float minLaunchSpeed = 8f;
    [SerializeField] float maxLaunchSpeed = 26f;
    [SerializeField] float screenPullToWorld = 0.015f;

    [Header("Performance")]
    [SerializeField] float dashPerf;
    [SerializeField] float dirtPerf;
    [SerializeField] float waterPerf;
    [SerializeField] float airPerf;

    [Header("Run")]
    [SerializeField] float steeringSpeed = 12f;
    [SerializeField] float goalDistance = 80f;
    [SerializeField] float defaultLinearDamping = 0.6f;
    [SerializeField] float angularDamping = 0.01f;
    [SerializeField] float mass = 5f;
    [SerializeField] float gravityWeight = 1.5f;
    [SerializeField] float baseFriction = 1f;
    [SerializeField] float obstacleSpeedLoss = 8f;
    [SerializeField] bool requireObstacleTag = true;
    [SerializeField] string obstacleTag = "Obstacle";
    [SerializeField] RoadFrictionZone[] frictionZones;

    [Header("Rewards")]
    [SerializeField] VehiclePartGridUi partUi;
    [SerializeField] float coinsPerDistance = 1f;
    [SerializeField] GameObject resultUi;
    [SerializeField] Text earnedCoinText;
    [SerializeField] Button getCoinButton;

    [Header("Steer Visual")]
    [SerializeField] float steerSensitivity = 10f;
    [SerializeField] float steerReturnSpeed = 8f;
    [SerializeField] float maxSteerYaw = 18f;
    [SerializeField] float maxLeanAngle = 12f;

    [Header("Physics")]
    [SerializeField] bool disableVehiclePhysics = true;
    [SerializeField] LayerMask groundMask = ~0;
    [SerializeField] float groundRayHeight = 3f;
    [SerializeField] float groundHeightOffset = 0.08f;
    [SerializeField] float groundAlignSpeed = 18f;

    [Header("Axes")]
    [SerializeField] Vector3 trackForward = Vector3.forward;
    [SerializeField] Vector3 trackRight = Vector3.right;

    PlayState state;
    Vector3 startPosition;
    Quaternion startRotation;
    Vector3 cameraOffset;
    Vector3 launchLineStartPoint;
    Vector3 launchLineEndPoint;
    Vector2 dragStartScreen;
    Vector2 runDragStart;
    Vector2 launchDrag;
    float runDistance;
    float speedBonus;
    float steerInput;
    float visualSteer;
    float headingAngle;
    float pullDistance;
    float speed;
    float sustainForceWeight;
    float sustainDuration;
    float customGravityWeight = 3f;
    Vector3 groundUp = Vector3.up;
    TerrainKind currentTerrain;
    bool draggingLaunch;
    bool draggingSteer;
    bool gameEnded;
    bool hasLaunchLineEndpoints;
    int pendingCoins;
    readonly List<Collider> terrainTriggers = new List<Collider>();

    void Awake()
    {
        if (vehicle == null)
        {
            Debug.LogError("Assign Vehicle on BlockVehiclePlayable.");
            enabled = false;
            return;
        }

        if (followCamera == null) followCamera = Camera.main;
        if (carView == null) carView = vehicle;
        if (launchDragBox == null) launchDragBox = vehicle.GetComponentInChildren<BoxCollider>();
        if (trackRoot != null)
        {
            trackForward = trackRoot.forward;
            trackRight = trackRoot.right;
        }
        trackForward = SafeDirection(trackForward, Vector3.forward);
        trackRight = SafeDirection(trackRight, Vector3.right);
        startPosition = vehicle.position;
        startRotation = vehicle.rotation;
        if (vehicleBody == null) vehicleBody = vehicle.GetComponent<Rigidbody>();
        if (vehicleBody == null) vehicleBody = vehicle.GetComponentInParent<Rigidbody>();
        if (vehicleBody == null) vehicleBody = vehicle.GetComponentInChildren<Rigidbody>();
        if (sphereCollider == null && vehicleBody != null) sphereCollider = vehicleBody.GetComponent<SphereCollider>();
        if (sphereCollider == null) sphereCollider = vehicle.GetComponentInChildren<SphereCollider>();
        if (vehicleBody != null)
        {
            vehicleBody.mass = mass;
            SetLinearDamping(vehicleBody, defaultLinearDamping);
            SetAngularDamping(vehicleBody, angularDamping);
            if (disableVehiclePhysics)
            {
                vehicleBody.isKinematic = true;
                vehicleBody.useGravity = false;
            }
        }
        VehicleCollisionRelay relay = vehicle.GetComponent<VehicleCollisionRelay>();
        if (relay == null) relay = vehicle.gameObject.AddComponent<VehicleCollisionRelay>();
        relay.Initialize(this);
        if (followCamera != null) cameraOffset = followCamera.transform.position - vehicle.position;
        CacheLaunchLineEndpoints();
        if (resultUi != null) resultUi.SetActive(false);
        if (getCoinButton != null)
        {
            getCoinButton.onClick.RemoveListener(GetCoinAndReturnToBuild);
            getCoinButton.onClick.AddListener(GetCoinAndReturnToBuild);
        }
        SetState(PlayState.Aim);
    }

    void OnDestroy()
    {
        if (getCoinButton != null) getCoinButton.onClick.RemoveListener(GetCoinAndReturnToBuild);
    }

    void Update()
    {
        if (state == PlayState.Aim) UpdateAim();
        else if (state == PlayState.Run) UpdateRun();
        UpdateLaunchLine();
    }

    void FixedUpdate()
    {
        ApplyTerrainDamping();
        ApplyCustomGravity();
        ApplySustainDash();
        ApplySlowBrake();
        ApplySlopeAcceleration();
        ApplySteering(Time.fixedDeltaTime);
        UpdateRigidbodyRunDistance();
    }

    void LateUpdate()
    {
        if (followCamera == null || state == PlayState.Aim) return;
        Vector3 target = vehicle.position + cameraOffset;
        followCamera.transform.position = Vector3.Lerp(followCamera.transform.position, target, Time.deltaTime * 6f);
    }

    void UpdateAim()
    {
        if (PointerDown(out Vector2 pointer))
        {
            if (PointerOverUi() || PointerInsideAimUi(pointer)) return;
            if (!CanStartLaunchDrag(pointer)) return;
            draggingLaunch = true;
            launchDrag = Vector2.zero;
            dragStartScreen = pointer;
        }

        if (!draggingLaunch) return;

        if (PointerHeld(out pointer))
        {
            launchDrag = ClampLaunchDrag(pointer - dragStartScreen);
            pullDistance = -launchDrag.y * screenPullToWorld;
            vehicle.position = startPosition + (trackRight * launchDrag.x + trackForward * launchDrag.y) * screenPullToWorld;
            vehicle.rotation = startRotation;
        }

        if (PointerUp(out _))
        {
            draggingLaunch = false;
            launchDrag = ClampLaunchDrag(launchDrag);
            if (launchDrag.y > -pullMin && launchDrag.magnitude < pullMin)
            {
                launchDrag = Vector2.zero;
                pullDistance = 0f;
                vehicle.SetPositionAndRotation(startPosition, startRotation);
                return;
            }

            runDistance = 0f;
            steerInput = 0f;
            visualSteer = 0f;
            headingAngle = 0f;
            SetState(PlayState.Run);
            Dash(new Vector3(-launchDrag.x * 0.7f, 0f, -launchDrag.y), 1.5f, 2f);
        }
    }

    void UpdateRun()
    {
        if (PointerDown(out Vector2 pointer))
        {
            draggingSteer = true;
            runDragStart = pointer;
        }

        if (draggingSteer && PointerHeld(out pointer))
        {
            float delta = (pointer.x - runDragStart.x) / Mathf.Max(1f, Screen.width);
            steerInput = Mathf.Clamp(delta * steerSensitivity, -1f, 1f);
        }

        if (PointerUp(out _))
        {
            draggingSteer = false;
            steerInput = 0f;
        }

        if (!draggingSteer) steerInput = Mathf.MoveTowards(steerInput, 0f, steerReturnSpeed * Time.deltaTime);
        visualSteer = Mathf.MoveTowards(visualSteer, steerInput, steerReturnSpeed * Time.deltaTime);
        if (vehicleBody != null && !vehicleBody.isKinematic) return;

        headingAngle = Mathf.Clamp(headingAngle + visualSteer * steeringSpeed * Time.deltaTime, -maxSteerYaw, maxSteerYaw);

        speed = Mathf.MoveTowards(speed, 0f, CurrentFriction() * Time.deltaTime);
        Vector3 moveDirection = Vector3.ProjectOnPlane(VelocityDirection(trackForward, trackRight), groundUp).normalized;
        if (moveDirection.sqrMagnitude < 0.0001f) moveDirection = VelocityDirection(trackForward, trackRight);
        Vector3 move = moveDirection * speed * Time.deltaTime;
        vehicle.position += move;
        runDistance += Mathf.Max(0f, Vector3.Dot(move, trackForward));
        StickToGround(moveDirection);

        if (runDistance >= goalDistance || speed <= 0f) SetState(PlayState.Result);
    }

    void StickToGround(Vector3 moveDirection)
    {
        Vector3 rayOrigin = vehicle.position + Vector3.up * groundRayHeight;
        float rayDistance = groundRayHeight * 2f + groundHeightOffset;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, rayDistance, groundMask, QueryTriggerInteraction.Ignore))
        {
            groundUp = hit.normal;
            vehicle.position = hit.point + hit.normal * groundHeightOffset;
        }

        Vector3 forward = Vector3.ProjectOnPlane(moveDirection, groundUp).normalized;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.ProjectOnPlane(vehicle.forward, groundUp).normalized;
        if (forward.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(forward, groundUp) * SteerLeanRotation();
            vehicle.rotation = Quaternion.Slerp(vehicle.rotation, targetRotation, Time.deltaTime * groundAlignSpeed);
        }
    }

    Vector3 VelocityDirection(Vector3 forward, Vector3 right)
    {
        return (forward * Mathf.Cos(headingAngle * Mathf.Deg2Rad) + right * Mathf.Sin(headingAngle * Mathf.Deg2Rad)).normalized;
    }

    Quaternion VelocityRotation(Vector3 forward, Vector3 right, Vector3 up)
    {
        Vector3 direction = VelocityDirection(forward, right);
        if (direction.sqrMagnitude < 0.0001f) direction = forward;
        return Quaternion.LookRotation(direction, up);
    }

    Quaternion SteerLeanRotation()
    {
        return Quaternion.Euler(0f, 0f, -visualSteer * maxLeanAngle);
    }

    float CurrentFriction()
    {
        float friction = baseFriction;
        if (frictionZones == null) return friction;
        for (int i = 0; i < frictionZones.Length; i++)
        {
            RoadFrictionZone zone = frictionZones[i];
            if (zone != null && runDistance >= zone.startDistance && runDistance <= zone.endDistance) friction = zone.friction;
        }
        return Mathf.Max(0f, friction);
    }

    public void AddSpeedBonus(float bonus)
    {
        dashPerf += Mathf.Max(0f, bonus);
    }

    public void ApplyVehiclePartStats(float launchSpeedBonus, float steeringBonus, float frictionReduction)
    {
        dashPerf += Mathf.Max(0f, launchSpeedBonus);
        steeringSpeed += Mathf.Max(0f, steeringBonus);
        baseFriction = Mathf.Max(0f, baseFriction - Mathf.Max(0f, frictionReduction));
    }

    public void RemoveVehiclePartStats(float launchSpeedBonus, float steeringBonus, float frictionReduction)
    {
        dashPerf = Mathf.Max(0f, dashPerf - Mathf.Max(0f, launchSpeedBonus));
        steeringSpeed = Mathf.Max(0f, steeringSpeed - Mathf.Max(0f, steeringBonus));
        baseFriction += Mathf.Max(0f, frictionReduction);
    }

    public void ApplyVehiclePartStats(VehiclePartKind kind, float partValue, int level)
    {
        AddPerformance(kind, Mathf.Max(0f, partValue) * LevelCount(level));
    }

    public void RemoveVehiclePartStats(VehiclePartKind kind, float partValue, int level)
    {
        AddPerformance(kind, -Mathf.Max(0f, partValue) * LevelCount(level));
    }

    void Dash(Vector3 forward, float forceMultiplier, float duration)
    {
        float addForceWeight = baseAddForceWeight * forceMultiplier;
        addForceWeight += dashPerf * 0.075f;
        addForceWeight += dirtPerf * 0.065f;
        addForceWeight += waterPerf * 0.065f;
        addForceWeight += airPerf * 0.065f;
        float totalPerf = dashPerf + dirtPerf + waterPerf + airPerf;
        addForceWeight *= totalPerf == 0f ? 0.5f : 0.8f;

        Vector3 flatForward = new Vector3(forward.x, 0f, forward.z);
        if (flatForward.sqrMagnitude <= 0.0001f)
        {
            speed = 0f;
            return;
        }

        if (vehicleBody != null && !vehicleBody.isKinematic)
        {
            Vector3 force = flatForward * addForceWeight;
            Vector3 torque = new Vector3(flatForward.z, -flatForward.x, 0f) * addForceWeight;
            vehicleBody.AddForce(force, ForceMode.Impulse);
            vehicleBody.AddTorque(torque, ForceMode.Impulse);
            SustainDash(addForceWeight, duration);
        }

        Vector3 direction = flatForward.normalized;
        headingAngle = Mathf.Atan2(Vector3.Dot(direction, trackRight), Vector3.Dot(direction, trackForward)) * Mathf.Rad2Deg;
        speed = flatForward.magnitude * addForceWeight;
    }

    void SustainDash(float addForceWeight, float duration)
    {
        sustainForceWeight = addForceWeight;
        sustainDuration += duration;
    }

    void ApplySustainDash()
    {
        if (sustainDuration <= 0f || vehicleBody == null || vehicleBody.isKinematic) return;
        sustainDuration -= Time.fixedDeltaTime;
        Vector3 forward = carView != null ? carView.forward : vehicle.forward;
        Vector3 force = new Vector3(forward.x, 0f, forward.z) * sustainForceWeight;
        Vector3 torque = new Vector3(forward.z, -forward.x, 0f) * sustainForceWeight;
        vehicleBody.AddForce(force, ForceMode.Force);
        vehicleBody.AddTorque(torque, ForceMode.Force);
    }

    void ApplyTerrainDamping()
    {
        if (vehicleBody == null) return;

        float linearDamping = defaultLinearDamping;
        float terrainPerformance = TerrainPerformance();

        if (currentTerrain == TerrainKind.Dirt)
        {
            linearDamping += 0.05f;
            terrainPerformance *= 1.5f;
        }
        else if (currentTerrain == TerrainKind.Water)
        {
            linearDamping += 0.075f;
            terrainPerformance *= 2.5f;
        }
        else if (currentTerrain == TerrainKind.Air)
        {
            linearDamping = 0.01f;
        }

        SetLinearDamping(vehicleBody, Mathf.Max(linearDamping - terrainPerformance, 0.01f));
    }

    void ApplyCustomGravity()
    {
        if (vehicleBody == null || vehicleBody.isKinematic) return;
        customGravityWeight = currentTerrain == TerrainKind.Air ? 3f - airPerf * 0.75f : 3f;
        vehicleBody.AddForce(Physics.gravity * gravityWeight * customGravityWeight, ForceMode.Acceleration);
    }

    void ApplySlowBrake()
    {
        if (vehicleBody == null || vehicleBody.isKinematic) return;
        Vector3 velocity = BodyVelocity;
        if (velocity.sqrMagnitude < 7f * 7f) BodyVelocity = Vector3.MoveTowards(velocity, Vector3.zero, 10f * Time.fixedDeltaTime);
    }

    void ApplySlopeAcceleration()
    {
        if (vehicleBody == null || vehicleBody.isKinematic) return;
        Vector3 velocity = BodyVelocity;
        if (velocity.sqrMagnitude < 0.0001f) return;

        Vector3 velocityDir = velocity.normalized;
        float slopeAngle = Vector3.Angle(Vector3.up, velocityDir) - 90f;
        if (slopeAngle <= 0f) return;

        float slopeAcceleration = currentTerrain == TerrainKind.Air ? 5f : 15f;
        float acceleration = Mathf.Lerp(0f, slopeAcceleration, slopeAngle / 90f);
        vehicleBody.AddForce(velocityDir * acceleration, ForceMode.Force);
    }

    void ApplySteering(float deltaTime)
    {
        if (state != PlayState.Run || vehicleBody == null || vehicleBody.isKinematic) return;
        Vector3 velocity = BodyVelocity;
        float currentSpeed = velocity.magnitude;
        if (currentSpeed <= 0.001f) return;

        Vector3 targetDirection = (trackRight * steerInput + trackForward).normalized;
        Vector3 forward = velocity.normalized;
        if (Vector3.Angle(forward, targetDirection) > 15f)
        {
            targetDirection = Vector3.RotateTowards(forward, targetDirection, 15f * Mathf.Deg2Rad, 0f);
        }

        Vector3 newDirection = Vector3.Lerp(forward, targetDirection, 1.5f * deltaTime);
        if (newDirection.sqrMagnitude > 0.0001f) BodyVelocity = newDirection.normalized * currentSpeed;

        if (sphereCollider == null) return;
        float radius = sphereCollider.radius * vehicleBody.transform.localScale.x;
        if (radius > 0.0001f) vehicleBody.angularVelocity = Vector3.Cross(Vector3.up, BodyVelocity) / radius;
    }

    void UpdateRigidbodyRunDistance()
    {
        if (state != PlayState.Run || vehicleBody == null || vehicleBody.isKinematic) return;
        Vector3 velocity = BodyVelocity;
        speed = velocity.magnitude;
        runDistance += Mathf.Max(0f, Vector3.Dot(velocity * Time.fixedDeltaTime, trackForward));
        if (runDistance >= goalDistance || speed <= 0.05f) SetState(PlayState.Result);
    }

    float TerrainPerformance()
    {
        if (currentTerrain == TerrainKind.Dirt) return dirtPerf;
        if (currentTerrain == TerrainKind.Water) return waterPerf;
        if (currentTerrain == TerrainKind.Air) return airPerf;
        return 0f;
    }

    void AddPerformance(VehiclePartKind kind, float value)
    {
        if (kind == VehiclePartKind.Wheel) dashPerf = Mathf.Max(0f, dashPerf + value);
        else if (kind == VehiclePartKind.Caterpillar) dirtPerf = Mathf.Max(0f, dirtPerf + value);
        else if (kind == VehiclePartKind.Chimney) waterPerf = Mathf.Max(0f, waterPerf + value);
        else if (kind == VehiclePartKind.Wing) airPerf = Mathf.Max(0f, airPerf + value);
    }

    static int LevelCount(int level)
    {
        return 1 << Mathf.Clamp(level, 0, 30);
    }

    public void OnVehicleObstacleHit(GameObject obstacle)
    {
        if (state != PlayState.Run) return;
        if (obstacle != null && IsTerrainTag(obstacle.tag)) return;
        if (requireObstacleTag && (obstacle == null || obstacle.tag != obstacleTag)) return;
        speed = Mathf.Max(0f, speed - obstacleSpeedLoss);
    }

    public void OnVehicleTriggerEnter(Collider other)
    {
        if (other == null || !IsTerrainTag(other.tag)) return;
        terrainTriggers.Remove(other);
        terrainTriggers.Add(other);
        currentTerrain = TerrainFromTag(other.tag);
    }

    public void OnVehicleTriggerExit(Collider other)
    {
        if (other == null || !terrainTriggers.Remove(other)) return;
        currentTerrain = terrainTriggers.Count > 0 ? TerrainFromTag(terrainTriggers[terrainTriggers.Count - 1].tag) : TerrainKind.Default;
    }

    public void GetCoinAndReturnToBuild()
    {
        if (partUi != null) partUi.AddCoins(pendingCoins);
        pendingCoins = 0;
        if (resultUi != null) resultUi.SetActive(false);
        ResetVehicleToStart();
        gameEnded = false;
        SetState(PlayState.Aim);
    }

    void SetState(PlayState next)
    {
        state = next;
        if (launchLine != null) launchLine.enabled = next == PlayState.Aim;
        if (next == PlayState.Result && !gameEnded)
        {
            gameEnded = true;
            pendingCoins = Mathf.FloorToInt(Mathf.Max(0f, runDistance) * Mathf.Max(0f, coinsPerDistance));
            if (earnedCoinText != null) earnedCoinText.text = pendingCoins.ToString();
            if (resultUi != null) resultUi.SetActive(true);
            PlayworksBridge.GameEnded();
        }
    }

    void ResetVehicleToStart()
    {
        draggingLaunch = false;
        draggingSteer = false;
        launchDrag = Vector2.zero;
        pullDistance = 0f;
        runDistance = 0f;
        speed = 0f;
        sustainForceWeight = 0f;
        sustainDuration = 0f;
        customGravityWeight = 3f;
        steerInput = 0f;
        visualSteer = 0f;
        headingAngle = 0f;
        groundUp = Vector3.up;
        vehicle.SetPositionAndRotation(startPosition, startRotation);
        if (followCamera != null) followCamera.transform.position = startPosition + cameraOffset;
    }

    void UpdateLaunchLine()
    {
        if (launchLine == null || state != PlayState.Aim) return;
        if (!hasLaunchLineEndpoints) CacheLaunchLineEndpoints();
        if (!hasLaunchLineEndpoints) return;

        Vector3 leftTail = leftVehicleTailAnchor != null ? leftVehicleTailAnchor.position : vehicle.position - vehicle.right * 0.35f;
        Vector3 rightTail = rightVehicleTailAnchor != null ? rightVehicleTailAnchor.position : vehicle.position + vehicle.right * 0.35f;
        launchLine.positionCount = 4;
        launchLine.SetPosition(0, launchLineStartPoint);
        launchLine.SetPosition(1, LaunchLinePoint(leftTail));
        launchLine.SetPosition(2, LaunchLinePoint(rightTail));
        launchLine.SetPosition(3, launchLineEndPoint);
    }

    Vector2 ClampLaunchDrag(Vector2 drag)
    {
        drag.x = Mathf.Clamp(drag.x, pullClampX.x, pullClampX.y);
        drag.y = Mathf.Clamp(drag.y, pullClampY.x, pullClampY.y);
        if (drag.magnitude > pullMax) drag = drag.normalized * pullMax;
        return drag;
    }

    void CacheLaunchLineEndpoints()
    {
        if (launchLine == null || launchLine.positionCount <= 0) return;
        int lastPoint = launchLine.positionCount - 1;
        launchLineStartPoint = launchLine.GetPosition(0);
        launchLineEndPoint = launchLine.GetPosition(lastPoint);
        hasLaunchLineEndpoints = true;
    }

    Vector3 LaunchLinePoint(Vector3 worldPoint)
    {
        return launchLine.useWorldSpace ? worldPoint : launchLine.transform.InverseTransformPoint(worldPoint);
    }

    bool CanStartLaunchDrag(Vector2 screenPosition)
    {
        if (launchDragBox == null) return true;
        Camera rayCamera = followCamera != null ? followCamera : Camera.main;
        return rayCamera != null && launchDragBox.Raycast(rayCamera.ScreenPointToRay(screenPosition), out _, 1000f);
    }

    bool PointerInsideAimUi(Vector2 screenPosition)
    {
        return aimUiBlocker != null && RectTransformUtility.RectangleContainsScreenPoint(aimUiBlocker, screenPosition);
    }

    bool IsTerrainTag(string tagName)
    {
        return tagName == "Default" || tagName == "Dirt" || tagName == "Water" || tagName == "Air";
    }

    TerrainKind TerrainFromTag(string tagName)
    {
        if (tagName == "Dirt") return TerrainKind.Dirt;
        if (tagName == "Water") return TerrainKind.Water;
        if (tagName == "Air") return TerrainKind.Air;
        return TerrainKind.Default;
    }

    Vector3 BodyVelocity
    {
        get
        {
#if UNITY_6000_0_OR_NEWER
            return vehicleBody.linearVelocity;
#else
            return vehicleBody.velocity;
#endif
        }
        set
        {
#if UNITY_6000_0_OR_NEWER
            vehicleBody.linearVelocity = value;
#else
            vehicleBody.velocity = value;
#endif
        }
    }

    static void SetLinearDamping(Rigidbody body, float value)
    {
#if UNITY_6000_0_OR_NEWER
        body.linearDamping = value;
#else
        body.drag = value;
#endif
    }

    static void SetAngularDamping(Rigidbody body, float value)
    {
#if UNITY_6000_0_OR_NEWER
        body.angularDamping = value;
#else
        body.angularDrag = value;
#endif
    }

    static Vector3 SafeDirection(Vector3 value, Vector3 fallback)
    {
        return value.sqrMagnitude > 0.0001f ? value.normalized : fallback;
    }

    static bool PointerDown(out Vector2 position)
    {
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            position = Input.GetTouch(0).position;
            return true;
        }
        if (Input.GetMouseButtonDown(0))
        {
            position = Input.mousePosition;
            return true;
        }
        position = default;
        return false;
    }

    static bool PointerHeld(out Vector2 position)
    {
        if (Input.touchCount > 0)
        {
            position = Input.GetTouch(0).position;
            return true;
        }
        if (Input.GetMouseButton(0))
        {
            position = Input.mousePosition;
            return true;
        }
        position = default;
        return false;
    }

    static bool PointerUp(out Vector2 position)
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                position = touch.position;
                return true;
            }
        }
        if (Input.GetMouseButtonUp(0))
        {
            position = Input.mousePosition;
            return true;
        }
        position = default;
        return false;
    }

    static bool PointerOverUi()
    {
        if (EventSystem.current == null) return false;
        if (Input.touchCount > 0) return EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
        return EventSystem.current.IsPointerOverGameObject();
    }
}

public sealed class VehicleCollisionRelay : MonoBehaviour
{
    BlockVehiclePlayable owner;

    public void Initialize(BlockVehiclePlayable playable)
    {
        owner = playable;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (owner != null) owner.OnVehicleObstacleHit(collision.gameObject);
    }

    void OnTriggerEnter(Collider other)
    {
        if (owner == null) return;
        owner.OnVehicleTriggerEnter(other);
        owner.OnVehicleObstacleHit(other.gameObject);
    }

    void OnTriggerExit(Collider other)
    {
        if (owner != null) owner.OnVehicleTriggerExit(other);
    }
}
