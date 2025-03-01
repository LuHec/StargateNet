using StargateNet;
using UnityEngine;

public class FPSController : NetworkBehavior
{
    public CharacterController cc;
    public Transform cameraPoint;
    public Transform foot;
    public Transform handPoint;
    private Camera mainCamera;

    [Header("Controller")]
    public float groundDis = 2f;

    public float moveSpeed = 10f;
    public float lookSpeedX = 2f;
    public float lookSpeedY = 2f;
    public float jumpSpeed = 12f;
    public float gravity = 24;

    [Header("Sway")]
    public bool sway = true;

    public float smooth = 10f;
    public float step = .01f;
    public float maxStepDistance = .06f;
    private Vector3 _swayPos;

    [Header("Sway Rotation")]
    public bool swayRotation = true;

    public float smoothRot = 12f;
    public float rotationStep = 4f;
    public float maxRotationStep = 5f;
    private Vector3 _swayEulerRot;

    [Header("Bobbing")]
    public bool bobOffset = true;

    public float speedCurveMultiplier = 1f;
    public float speedCurve;

    private float CurveSin => Mathf.Sin(speedCurve);
    private float CurveCos => Mathf.Cos(speedCurve);
    public Vector3 travelLimit = Vector3.one * .025f;
    public Vector3 bobLimit = Vector3.one * .1f;
    private Vector3 _bobPosition;

    [Header("Bob Rotation")]
    public bool bobSway = true;

    public Vector3 multiplier;

    private Vector3 _bobRotation;

    private Vector2 _localYawPitch;

    /// <summary>
    /// 跳跃和重力的速度
    /// </summary>
    [Replicated]
    public float VerticalSpeed { get; set; }
    [Replicated]
    NetworkBool IsGrounded { get; set; }
    [Replicated]
    public NetworkBool IsDead { get; set; }

    // ---------------------------------- Component ---------------------------------- //
    protected AttributeComponent attributeComponent;
    // ---------------------------------- Component ---------------------------------- //

    private WeaponPresenter _weaponPresenter;

    public override void NetworkStart(SgNetworkGalaxy galaxy)
    {
        attributeComponent = GetComponent<AttributeComponent>();
        attributeComponent.owner = this;

        cameraPoint.forward = transform.forward;
        if (this.IsLocalPlayer())
        {
            mainCamera = galaxy.FindSceneComponent<Camera>();
        }

        HandleRespawn();

        _weaponPresenter = new WeaponPresenter(handPoint)
        {
            sway = sway,
            smooth = smooth,
            step = step,
            maxStepDistance = maxStepDistance,
            swayRotation = swayRotation,
            smoothRot = smoothRot,
            rotationStep = rotationStep,
            maxRotationStep = maxRotationStep,
            bobOffset = bobOffset,
            speedCurveMultiplier = speedCurveMultiplier,
            travelLimit = travelLimit,
            bobLimit = bobLimit,
            bobSway = bobSway,
            multiplier = multiplier
        };

        IsDead = false;
    }

    public override void NetworkFixedUpdate(SgNetworkGalaxy galaxy)
    {
        if (IsDead) return;

        Vector3 movement = Vector3.zero;

        // 地面检测
        IsGrounded = Physics.Raycast(foot.position, Vector3.down, groundDis);

        if (this.FetchInput(out PlayerInput input))
        {
            // 客户端为权威的旋转
            Vector2 yawPitch = input.YawPitch;
            transform.rotation = Quaternion.Euler(0, yawPitch.x, 0);
            cameraPoint.localRotation = Quaternion.Euler(yawPitch.y, 0, 0);

            movement = new Vector3(input.Input.x, 0, input.Input.y) * moveSpeed;

            if (input.IsJump && IsGrounded && VerticalSpeed <= 0)
            {
                VerticalSpeed = jumpSpeed;
                IsGrounded = false;
            }

            if (input.Reload)
            {
                attributeComponent.networkWeapon.Reload(galaxy);
            }

            if ((input.IsFire || input.IsHoldFire) && attributeComponent.networkWeapon != null && attributeComponent.networkWeapon.TryFire(galaxy, input.IsFire, input.IsHoldFire))
            {
                GizmoTimerDrawer.Instance.DrawRayWithTimer(cameraPoint.position, cameraPoint.forward * 50f, 5f, Color.green);
                galaxy.NetworkRaycast(cameraPoint.position, cameraPoint.forward, this.InputSource, out RaycastHit hit,
                    50f, ~0);

                if (hit.collider != null)
                {
                    // GizmoTimerDrawer.Instance.DrawWireSphereWithTimer(hit.point, .5f, 5f, Color.red);
                    Debug.LogWarning(hit.collider.gameObject.name);
                    if (IsClient && !galaxy.IsResimulation)
                    {
                        UIManager.Instance.GetUIPanel<UIHitmarker>().HitToShowMarker();
                    }
                    if (IsServer && hit.collider.gameObject.TryGetComponent(out AttributeComponent targetAttribute))
                    {
                        targetAttribute.HPoint -= 10;
                    }
                }
            }

            if (input.IsInteract && IsServer)
            {
                GizmoTimerDrawer.Instance.DrawRayWithTimer(cameraPoint.position, cameraPoint.forward * 20f, 5f,
                    Color.red);
                galaxy.NetworkRaycast(cameraPoint.transform.position, cameraPoint.forward, this.InputSource, out RaycastHit hit, 20f, ~0);
                if (hit.collider != null)
                {
                    attributeComponent.SetNetworkWeapon(hit.collider.GetComponent<NetworkObject>());
                }
            }

            if (input.IsThrowing && IsServer)
            {
                attributeComponent.ThrowWeapon();
            }
        }

        if (!IsGrounded)
        {
            // 在空中时应用重力
            VerticalSpeed -= gravity * galaxy.FixedDeltaTime;
        }
        else if (VerticalSpeed < 0)
        {
            // 着地时重置垂直速度
            VerticalSpeed = 0;
        }

        // 移动处理 1/gt^2
        Vector3 finalMovement = movement + new Vector3(0f, VerticalSpeed * 0.5f, 0);
        cc.Move(finalMovement * galaxy.FixedDeltaTime);
    }

    public override void NetworkUpdate(SgNetworkGalaxy galaxy)
    {
        if (!this.IsLocalPlayer() || IsDead) return;
        Inputs inputs = GetInput(galaxy);
        Vector2 deltaYawPitchInput = inputs.a;
        Vector2 deltaMove = inputs.b;
        _weaponPresenter.UpdatePresentation(deltaYawPitchInput, deltaMove, cc.velocity.magnitude, IsGrounded);
    }

    public override void NetworkRender(SgNetworkGalaxy galaxy)
    {
    }



    private Vector2 ClampAngles(float yaw, float pitch)
    {
        return new Vector2(ClampAngle(yaw, -360, 360), ClampAngle(pitch, -80, 80));
    }

    private float ClampAngle(float angle, float min, float max)
    {
        if (angle < -360F)
            angle += 360F;
        if (angle > 360F)
            angle -= 360F;
        return Mathf.Clamp(angle, min, max);
    }

    struct Inputs
    {
        public Vector2 a;
        public Vector2 b;
    }
    /// <summary>
    /// 返回delta
    /// </summary>
    /// <param name="galaxy"></param>
    /// <returns></returns>
    private Inputs GetInput(SgNetworkGalaxy galaxy)
    {
        if (Input.GetKey(KeyCode.LeftAlt))
        {
            Cursor.lockState = CursorLockMode.None;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
        }

        PlayerInput playerInput = galaxy.GetInput<PlayerInput>();

        // 使用 galaxy.FindSceneComponent 获取相机
        Camera mainCamera = galaxy.FindSceneComponent<Camera>();

        var transform1 = mainCamera.transform;
        Vector3 forward = transform1.forward;
        Vector3 right = transform1.right;
        forward.y = 0;
        right.y = 0;
        forward.Normalize();
        right.Normalize();
        Vector3 moveDirection = forward * Input.GetAxis("Vertical") + right * Input.GetAxis("Horizontal");
        playerInput.Input = new Vector2(moveDirection.x, moveDirection.z);

        Vector2 deltaRawPitchInput = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
        float mouseX = deltaRawPitchInput.x * lookSpeedX;
        float mouseY = deltaRawPitchInput.y * lookSpeedY;
        _localYawPitch = ClampAngles(_localYawPitch.x + mouseX, _localYawPitch.y - mouseY);
        // 在Update中旋转，将结果作为Input传入,避免丢包导致视角抽搐
        transform.rotation = Quaternion.Euler(0, _localYawPitch.x, 0);
        cameraPoint.localRotation = Quaternion.Euler(_localYawPitch.y, 0, 0);
        playerInput.YawPitch = new Vector2(_localYawPitch.x, _localYawPitch.y);
        playerInput.IsJump |= Input.GetKeyDown(KeyCode.Space);
        playerInput.IsFire |= Input.GetMouseButtonDown(0);
        playerInput.IsHoldFire |= Input.GetMouseButton(0);
        playerInput.IsInteract |= Input.GetKeyDown(KeyCode.E);
        playerInput.IsThrowing |= Input.GetKeyDown(KeyCode.G);
        playerInput.Reload |= Input.GetKeyDown(KeyCode.R);
        // 处理延迟补偿
        //TODO:暂时这么写！！还在想办法解决怎么把这个狗屎延迟补偿输入给提取出用户代码
        galaxy.SetInput(playerInput, Input.GetMouseButtonDown(0));

        return new Inputs { a = deltaRawPitchInput, b = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical")) };
    }

    public void OnDead()
    {
        if (IsClient)
        {
            RemoveFPSCamera();
            UIManager.Instance.GetUIPanel<UIPlayerInterface>().Close();
        }
        this.gameObject.SetActive(false);
    }

    private void SetFPSCamera()
    {
        if (cameraPoint != null && mainCamera != null)
        {
            mainCamera.fieldOfView = 105f;
            Transform cameraTransform = mainCamera.transform;
            cameraTransform.forward = transform.forward;
            cameraTransform.SetParent(cameraPoint);
            cameraTransform.localPosition = Vector3.zero;
            if (mainCamera.TryGetComponent<ObsCamera>(out ObsCamera obsCamera))
            {
                obsCamera.enabled = false;
            }
        }

    }

    private void RemoveFPSCamera()
    {
        if (mainCamera != null)
        {
            mainCamera.transform.SetParent(null);
            if (mainCamera.TryGetComponent<ObsCamera>(out ObsCamera obsCamera))
            {
                obsCamera.enabled = true;
            }
        }

    }

    public void SetDead(bool isDead)
    {
        IsDead = isDead;
    }

    [NetworkCallBack(nameof(IsDead), false)]
    private void OnDeadStateChanged(CallbackData data)
    {
        if (IsDead)
        {
            HandleDeath();
        }
        else
        {
            HandleRespawn();
        }
    }

    private void HandleDeath()
    {
        if (IsClient)
        {
            RemoveFPSCamera();
            UIManager.Instance.GetUIPanel<UIPlayerInterface>().Close();
        }
        gameObject.SetActive(false);
    }

    private void HandleRespawn()
    {
        gameObject.SetActive(true);
        if (IsClient)
        {
            SetFPSCamera();
            UIManager.Instance.GetUIPanel<UIPlayerInterface>().Open();
        }
    }
}