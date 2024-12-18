using System.Collections;
using System.Collections.Generic;
using StargateNet;
using UnityEngine;
using UnityEngine.Serialization;

public class FPSController : NetworkBehavior
{
    public Transform cameraPoint;
    public float moveSpeed = 5f;
    public float lookSpeedX = 2f;
    public float lookSpeedY = 2f;
    private Vector2 _localYawPitch;

    public override void NetworkStart(SgNetworkGalaxy galaxy)
    {
        if (this.IsLocalPlayer())
        {
            Camera mainCamera = Camera.main;
            if (cameraPoint != null && mainCamera != null)
            {
                mainCamera.GetComponent<Camera>().fieldOfView = 120f;
                Transform transform1 = mainCamera.transform;
                transform1.forward = transform.forward;
                transform1.SetParent(cameraPoint);
                transform1.localPosition = Vector3.zero;
            }
            
            Cursor.lockState = CursorLockMode.Locked;
        }
    }

    public override void NetworkFixedUpdate(SgNetworkGalaxy galaxy)
    {
        if (this.FetchInput(out NetworkInput input))
        {
            // 客户端为权威的旋转
            Vector2 yawPitch = input.YawPitch;
            transform.rotation = Quaternion.Euler(0, yawPitch.x, 0);
            cameraPoint.localRotation = Quaternion.Euler(yawPitch.y, 0, 0);
            
            Vector3 deltaMovement = new Vector3(input.Input.x, 0, input.Input.y) * (galaxy.NetworkDeltaTime * moveSpeed);
            transform.position += deltaMovement;
        }
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
            networkInput.Input = new Vector2(moveDirection.x, moveDirection.z);
        }

        float mouseX = Input.GetAxis("Mouse X") * lookSpeedX;
        float mouseY = Input.GetAxis("Mouse Y") * lookSpeedY;
        _localYawPitch = ClampAngles(_localYawPitch.x + mouseX, _localYawPitch.y - mouseY);
        // 在Update中旋转，将结果作为Input传入
        transform.rotation = Quaternion.Euler(0, _localYawPitch.x, 0);
        cameraPoint.localRotation = Quaternion.Euler(_localYawPitch.y, 0, 0);
        networkInput.YawPitch = new Vector2(_localYawPitch.x, _localYawPitch.y);
        
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