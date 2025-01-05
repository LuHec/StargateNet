using System.Collections;
using System.Collections.Generic;
using StargateNet;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

public class FPSController : NetworkBehavior
{
    public CharacterController cc;
    public Transform cameraPoint;
    public Transform foot;
    public Transform handPoint;

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
    bool IsGrounded() => Physics.Raycast(foot.position, Vector3.down, groundDis);

    /// <summary>
    /// 跳跃和重力的速度
    /// </summary>
    [Replicated]
    public float VerticalSpeed { get; set; }

    public override void NetworkStart(SgNetworkGalaxy galaxy)
    {
        cameraPoint.forward = transform.forward;
        if (this.IsLocalPlayer())
        {
            Camera mainCamera = Camera.main;
            if (cameraPoint != null && mainCamera != null)
            {
                mainCamera.GetComponent<Camera>().fieldOfView = 105f;
                Transform cameraTransform = mainCamera.transform;
                cameraTransform.forward = transform.forward;
                cameraTransform.SetParent(cameraPoint);
                cameraTransform.localPosition = Vector3.zero;
            }
        }
    }

    public override void NetworkFixedUpdate(SgNetworkGalaxy galaxy)
    {
        Vector3 movement = Vector3.zero;
        bool isGrounded = IsGrounded();
        if (isGrounded && VerticalSpeed <= 0f)
        {
            VerticalSpeed = 0f;
        }

        if (this.FetchInput(out NetworkInput input))
        {
            // 客户端为权威的旋转
            Vector2 yawPitch = input.YawPitch;
            transform.rotation = Quaternion.Euler(0, yawPitch.x, 0);
            cameraPoint.localRotation = Quaternion.Euler(yawPitch.y, 0, 0);

            movement = new Vector3(input.Input.x, 0, input.Input.y) * moveSpeed;

            if (input.IsJump && isGrounded)
            {
                VerticalSpeed = jumpSpeed;
            }

            if (input.IsFire)
            {
                GizmoTimerDrawer.Instance.DrawRayWithTimer(cameraPoint.position, cameraPoint.forward * 50f, 5f,
                    Color.green);
                galaxy.NetworkRaycast(cameraPoint.position, cameraPoint.forward, this.InputSource, out RaycastHit hit,
                    50f, ~0);
                if (hit.collider != null)
                {
                    GizmoTimerDrawer.Instance.DrawWireSphereWithTimer(hit.point, .5f, 5f, Color.red);
                }
            }
        }

        // 重力
        VerticalSpeed -= gravity * galaxy.FixedDeltaTime;
        cc.Move((movement + new Vector3(0, VerticalSpeed, 0)) * galaxy.FixedDeltaTime);
    }

    public override void NetworkUpdate(SgNetworkGalaxy galaxy)
    {
        if (!this.IsLocalPlayer()) return;
        var (deltaYawPitchInput, deltaMove) = GetInput(galaxy);
        Sway(deltaYawPitchInput);
        SwayRotation(deltaYawPitchInput);
        BobOffset(deltaMove);
        BobRotation(deltaMove);
        CompositePositionRotation();
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

    /// <summary>
    /// 返回delta
    /// </summary>
    /// <param name="galaxy"></param>
    /// <returns></returns>
    private (Vector2, Vector2) GetInput(SgNetworkGalaxy galaxy)
    {
        if (Input.GetKey(KeyCode.LeftAlt))
        {
            Cursor.lockState = CursorLockMode.None;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
        }

        NetworkInput networkInput = galaxy.GetInput<NetworkInput>();

        Camera mainCamera = Camera.main;

        // 获取前向方向和右向方向
        var transform1 = mainCamera.transform;
        Vector3 forward = transform1.forward;
        Vector3 right = transform1.right;
        forward.y = 0;
        right.y = 0;
        forward.Normalize();
        right.Normalize();
        Vector3 moveDirection = forward * Input.GetAxis("Vertical") + right * Input.GetAxis("Horizontal");
        networkInput.Input = new Vector2(moveDirection.x, moveDirection.z);
        
        Vector2 deltaRawPitchInput = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
        float mouseX = deltaRawPitchInput.x * lookSpeedX;
        float mouseY = deltaRawPitchInput.y * lookSpeedY;
        _localYawPitch = ClampAngles(_localYawPitch.x + mouseX, _localYawPitch.y - mouseY);
        // 在Update中旋转，将结果作为Input传入
        transform.rotation = Quaternion.Euler(0, _localYawPitch.x, 0);
        cameraPoint.localRotation = Quaternion.Euler(_localYawPitch.y, 0, 0);
        networkInput.YawPitch = new Vector2(_localYawPitch.x, _localYawPitch.y);
        networkInput.IsJump |= Input.GetKeyDown(KeyCode.Space);
        networkInput.IsFire |= Input.GetMouseButtonDown(0);
        // 处理延迟补偿
        //TODO:暂时这么写！！还在想办法解决怎么把这个狗屎延迟补偿输入给提取出用户代码
        galaxy.SetInput(networkInput, Input.GetMouseButtonDown(0));

        return (deltaRawPitchInput, new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical")));
    }

    void Sway(Vector3 input)
    {
        if (!sway) return;
        Vector3 invertLook = input * -step;
        invertLook.x = Mathf.Clamp(invertLook.x, -maxStepDistance, maxStepDistance);
        invertLook.y = Mathf.Clamp(invertLook.y, -maxStepDistance, maxStepDistance);

        _swayPos = invertLook;
    }

    void SwayRotation(Vector3 input)
    {
        if (!swayRotation) return;
        Vector2 invertLook = input * -rotationStep;
        invertLook.x = Mathf.Clamp(invertLook.x, -maxRotationStep, maxRotationStep);
        invertLook.y = Mathf.Clamp(invertLook.y, -maxRotationStep, maxRotationStep);

        _swayEulerRot = new Vector3(invertLook.y, invertLook.x, invertLook.x);
    }

    void BobOffset(Vector2 input)
    {
        bool isGrounded = IsGrounded();
        speedCurve += Time.deltaTime * (isGrounded ? cc.velocity.magnitude * speedCurveMultiplier : 1f) + .01f;
        if (!bobOffset)
        {
            _bobPosition = Vector3.zero;
            return;
        }

        _bobPosition.x = CurveCos * bobLimit.x * (isGrounded ? 1 : 0) - input.x * travelLimit.x;
        _bobPosition.y = CurveSin * bobLimit.y * (isGrounded ? 1 : 0) - input.y * travelLimit.y;
        _bobPosition.z = -(input.y * travelLimit.z);
    }

    void BobRotation(Vector2 input)
    {
        if (!bobSway)
        {
            _bobRotation = Vector3.zero;
            return;
        }

        _bobRotation.x = input != Vector2.zero
            ? multiplier.x * (Mathf.Sin(2 * speedCurve))
            : multiplier.x * (Mathf.Sin(2 * speedCurve) / 2);
        _bobRotation.y = input != Vector2.zero ? multiplier.y * CurveCos : 0;
        _bobRotation.z = input != Vector2.zero ? multiplier.z * CurveCos * input.x : 0;
    }

    void CompositePositionRotation()
    {
        handPoint.localPosition =
            Vector3.Lerp(handPoint.localPosition, _swayPos + _bobPosition, Time.deltaTime * smooth);
        handPoint.localRotation = Quaternion.Slerp(handPoint.localRotation,
            Quaternion.Euler(_swayEulerRot) * Quaternion.Euler(_bobRotation),
            Time.deltaTime * smoothRot);
    }
}