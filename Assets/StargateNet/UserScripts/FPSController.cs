using System.Collections;
using System.Collections.Generic;
using StargateNet;
using UnityEngine;
using UnityEngine.Serialization;

public class FPSController : NetworkBehavior
{
    protected NetworkTransform networkTransform;
    public Transform cameraPoint;
    public float moveSpeed = 5f;
    public float lookSpeedX = 2f;
    public float lookSpeedY = 2f;
    private Vector2 _localRotation;

    public override void NetworkStart(SgNetworkGalaxy galaxy)
    {
        networkTransform = GetComponent<NetworkTransform>();

        if (this.IsLocalPlayer())
        {
            Camera mainCamera = Camera.main;
            if (cameraPoint != null && mainCamera != null)
            {
                Transform transform1 = mainCamera.transform;
                transform1.forward = transform.forward;
                transform1.SetParent(cameraPoint);
                transform1.localPosition = Vector3.zero;
            }
        }
    }

    public override void NetworkFixedUpdate(SgNetworkGalaxy galaxy)
    {
        if (this.FetchInput(out NetworkInput input))
        {
            Vector3 deltaMovement = new Vector3(input.input.x, 0, input.input.y) * (galaxy.NetworkDeltaTime * moveSpeed);
            networkTransform.Position += deltaMovement;

            // 客户端为权威的旋转
            Vector2 rotation = input.axis;
            cameraPoint.localRotation = Quaternion.Euler(rotation.x, 0f, 0f);
            networkTransform.Rotation = new Vector3(0f, rotation.y, 0f); 
        }

        _localRotation.x = cameraPoint.localRotation.eulerAngles.x;
        _localRotation.y = transform.rotation.y;
    }

    public override void NetworkUpdate(SgNetworkGalaxy galaxy)
    {
        if (!this.IsLocalPlayer()) return;
        NetworkInput networkInput = new NetworkInput();

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            // 获取前向方向和右向方向
            var transform1 = mainCamera.transform;
            Vector3 forward = transform1.forward;
            Vector3 right = transform1.right;
            forward.y = 0;
            right.y = 0;
            forward.Normalize();
            right.Normalize();
            Vector3 moveDirection = forward * Input.GetAxis("Vertical") + right * Input.GetAxis("Horizontal");
            networkInput.input = new Vector2(moveDirection.x, moveDirection.z);
        }

        float mouseX = Input.GetAxis("Mouse X") * lookSpeedX;
        float mouseY = Input.GetAxis("Mouse Y") * lookSpeedY;
        _localRotation.y +=　mouseX;
        _localRotation.x -= mouseY;
        _localRotation.x = ClampAngle(_localRotation.x, -80, 80);
        networkInput.axis = new Vector2(_localRotation.x, _localRotation.y);
        galaxy.SetInput(networkInput);
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
}