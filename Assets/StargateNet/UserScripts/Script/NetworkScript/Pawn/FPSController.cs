using StargateNet;
using Unity.VisualScripting;
using UnityEngine;

public class FPSController : NetworkBehavior
{
    public GameObject deadVfx;
    public GameObject hitVfx;
    public Transform playerClientView;
    public CharacterController cc;
    public Transform controlAncor; // 玩家控制角色的俯仰角
    public Transform cameraPoint;  // 相机受后坐力影响，和controlAncor独立。射击时用到这个在localSpace有偏移的方向
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
    [Header("Recoile")]
    public float recoilX = 2;
    public float recoilY = 2;
    public float recoilZ = 2;
    public float snappiness = 2;
    public float returnSpeed = 2;

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
    private Vector3 _currentYawPitchForRecoil;
    private Vector3 _targetYawPitchForRecoil;

    [Header("Visual Recoil Settings")]
    [SerializeField] private float visualRecoilPosStrength = 0.5f;
    [SerializeField] private float visualRecoilRotStrength = 15f;
    [SerializeField] private float visualRecoilPosReturnSpeed = 8f;
    [SerializeField] private float visualRecoilRotReturnSpeed = 15f;
    [SerializeField] private float visualRecoilMaxPos = 0.5f;
    [SerializeField] private float visualRecoilMaxRot = 30f;
    [SerializeField] private Vector2 visualRecoilBackwardRange = new Vector2(-0.8f, -0.5f); // 增强后坐力后移
    [SerializeField] private Vector2 visualRecoilUpwardRange = new Vector2(0.2f, 0.4f);    // 增强上移幅度

    [Header("Visual Recoil Rotation Settings")]
    [SerializeField] private Vector2 recoilRotationRangeX = new Vector2(-20f, -15f);
    [SerializeField] private Vector2 recoilRotationRangeY = new Vector2(-7f, 7f);
    [SerializeField] private Vector2 recoilRotationRangeZ = new Vector2(-10f, 10f);

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
    public AttributeComponent attributeComponent;
    // ---------------------------------- Component ---------------------------------- //

    public WeaponPresenter weaponPresenter;

    public override void NetworkStart(SgNetworkGalaxy galaxy)
    {
        attributeComponent = GetComponent<AttributeComponent>();
        attributeComponent.owner = this;

        controlAncor.forward = transform.forward;
        if (this.IsLocalPlayer())
        {
            mainCamera = galaxy.FindSceneComponent<Camera>();
            galaxy.FindSceneComponent<BattleManager>().SetLocalPlayerEntityId(this.Entity.NetworkId.refValue);
            var view = playerClientView.AddComponent<AllyEnemyTagFiliter>();
            view.Init(mainCamera, this);
            weaponPresenter = new WeaponPresenter(handPoint)
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
                multiplier = multiplier,
                visualRecoilPosStrength = visualRecoilPosStrength,
                visualRecoilRotStrength = visualRecoilRotStrength,
                visualRecoilPosReturnSpeed = visualRecoilPosReturnSpeed,
                visualRecoilRotReturnSpeed = visualRecoilRotReturnSpeed,
                visualRecoilMaxPos = visualRecoilMaxPos,
                visualRecoilMaxRot = visualRecoilMaxRot,
                visualRecoilBackwardRange = visualRecoilBackwardRange,
                visualRecoilUpwardRange = visualRecoilUpwardRange,
                recoilRotationRangeX = recoilRotationRangeX,
                recoilRotationRangeY = recoilRotationRangeY,
                recoilRotationRangeZ = recoilRotationRangeZ
            };
        }

        HandleRespawn();
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
            controlAncor.localRotation = Quaternion.Euler(yawPitch.y, 0, 0);
            cameraPoint.localRotation = Quaternion.Euler(input.CameraPoint);

            movement = new Vector3(input.Move.x, 0, input.Move.y) * moveSpeed;

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
                GizmoTimerDrawer.Instance.DrawWireSphereWithTimer(hit.point, .5f, 5f, Color.green);

                if (hit.collider != null)
                {
                    // GizmoTimerDrawer.Instance.DrawWireSphereWithTimer(hit.point, .5f, 5f, Color.red);
                    // Debug.LogWarning(hit.collider.gameObject.name);
                    if (hit.collider.gameObject.TryGetComponent(out IHitable hitable))
                    {
                        if (IsServer)
                        {
                            hitable.OnHit(-19, hit.point, hit.normal, this);
                            AddHitVfx(hit.point, hit.normal);
                        }
                        if (IsClient && !galaxy.IsResimulation)
                        {
                            UIManager.Instance.GetUIPanel<UIHitmarker>().HitToShowMarker();
                        }
                    }
                }

                if (IsClient && !galaxy.IsResimulation)
                    // 后坐力
                    HandleRecoile();
            }

            if (input.IsInteract && IsServer)
            {
                GizmoTimerDrawer.Instance.DrawRayWithTimer(controlAncor.position, controlAncor.forward * 20f, 5f,
                    Color.red);
                galaxy.NetworkRaycast(controlAncor.transform.position, controlAncor.forward, this.InputSource, out RaycastHit hit, 20f, ~0);
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

        // 检查是否正在换弹
        if (attributeComponent.networkWeapon != null && attributeComponent.networkWeapon.IsReloading)
        {
            float reloadProgress = (galaxy.tick.tickValue - attributeComponent.networkWeapon.LastReloadTick)
                * galaxy.FixedDeltaTime / attributeComponent.networkWeapon.loadTime;

            if (reloadProgress <= 1.0f)
            {
                float rotationAngle = 360f * reloadProgress;
                weaponPresenter.SetReloadRotation(rotationAngle);
            }
        }
        else
        {
            weaponPresenter.SetReloadRotation(0);
        }

        weaponPresenter.UpdatePresentation(deltaYawPitchInput, deltaMove, cc.velocity.magnitude, IsGrounded);
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

        var transform1 = mainCamera.transform;
        Vector3 forward = transform1.forward;
        Vector3 right = transform1.right;
        forward.y = 0;
        right.y = 0;
        forward.Normalize();
        right.Normalize();
        Vector3 moveDirection = forward * Input.GetAxis("Vertical") + right * Input.GetAxis("Horizontal");
        playerInput.Move = new Vector2(moveDirection.x, moveDirection.z);

        Vector2 deltaRawPitchInput = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
        float mouseX = deltaRawPitchInput.x * lookSpeedX;
        float mouseY = deltaRawPitchInput.y * lookSpeedY;
        // 后坐力作用于CameraPoint上，不影响角色本身旋转
        _targetYawPitchForRecoil = Vector3.Lerp(_targetYawPitchForRecoil, Vector3.zero, returnSpeed * Time.deltaTime);
        _currentYawPitchForRecoil = Vector3.Lerp(_currentYawPitchForRecoil, _targetYawPitchForRecoil, snappiness * Time.deltaTime);

        // 移动视角
        _localYawPitch = ClampAngles(_localYawPitch.x + mouseX, _localYawPitch.y - mouseY);
        // 在Update中旋转，将结果作为Input传入,避免丢包导致视角抽搐
        transform.rotation = Quaternion.Euler(0, _localYawPitch.x, 0);
        controlAncor.localRotation = Quaternion.Euler(_localYawPitch.y, 0, 0);
        cameraPoint.localEulerAngles = _currentYawPitchForRecoil;
        bool isFiring = Input.GetMouseButtonDown(0);
        bool isHoldFiring = Input.GetMouseButton(0);

        playerInput.YawPitch = new Vector2(_localYawPitch.x, _localYawPitch.y);
        playerInput.CameraPoint = cameraPoint.localEulerAngles;
        playerInput.IsJump |= Input.GetKeyDown(KeyCode.Space);
        playerInput.IsFire |= isFiring;
        playerInput.IsHoldFire |= isHoldFiring;
        playerInput.IsInteract |= Input.GetKeyDown(KeyCode.E);
        playerInput.IsThrowing |= Input.GetKeyDown(KeyCode.G);
        playerInput.Reload |= Input.GetKeyDown(KeyCode.R);
        // 处理延迟补偿
        //TODO:暂时这么写！！还在想办法解决怎么把这个狗屎延迟补偿输入给提取出用户代码
        galaxy.SetInput(playerInput, isHoldFiring || isFiring);

        return new Inputs { a = deltaRawPitchInput, b = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical")) };
    }

    private void SetFPSCamera()
    {
        if (controlAncor != null && mainCamera != null)
        {
            mainCamera.fieldOfView = 95f;
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
        if (IsLocalPlayer())
        {
            RemoveFPSCamera();
            UIManager.Instance.GetUIPanel<UIPlayerInterface>().Close();
            UIManager.Instance.GetUIPanel<UIAllyPanel>().Close();
        }
        else if (IsClient)
        {
            Instantiate(deadVfx, transform.position, Quaternion.identity);
            UIManager.Instance.GetUIPanel<UIKillPointHint>().PlayKillAnimation("Kill", 1);
        }
        gameObject.SetActive(false);
    }

    private void HandleRespawn()
    {
        gameObject.SetActive(true);

        // 重置旋转和后坐力
        _localYawPitch = Vector2.zero;
        _currentYawPitchForRecoil = Vector3.zero;
        _targetYawPitchForRecoil = Vector3.zero;

        // 重置控制点的旋转
        transform.rotation = Quaternion.identity;
        controlAncor.localRotation = Quaternion.identity;
        cameraPoint.localRotation = Quaternion.identity;

        if (IsLocalPlayer())
        {
            SetFPSCamera();
            UIManager.Instance.GetUIPanel<UIPlayerInterface>().Open();
            UIManager.Instance.GetUIPanel<UIPlayerInterface>().UpdateHP(attributeComponent.HPoint);
            UIManager.Instance.GetUIPanel<UIBattleInterface>().Open();
            UIManager.Instance.GetUIPanel<UIAllyPanel>().Open();
            UIManager.Instance.GetUIPanel<UIEliminateInfo>().Open();

            // 如果有武器系统，也需要重置
            if (weaponPresenter != null)
            {
                weaponPresenter.ResetAll();
            }
        }
    }

    [NetworkRPC(NetworkRPCFrom.ServerCall)]
    public void AddHitVfx(Vector3 position, Vector3 normal)
    {
        var rot = Quaternion.LookRotation(normal);
        Instantiate(hitVfx, position, rot);
    }

    private void HandleRecoile()
    {
        // 相机后坐力
        _targetYawPitchForRecoil += new Vector3(recoilX, Random.Range(-recoilX, recoilX), Random.Range(-recoilY, recoilY));

        // 添加武器视觉后坐力
        if (weaponPresenter != null)
        {
            weaponPresenter.ApplyVisualRecoil();
        }
    }
}