using UnityEngine;

namespace StargateNet
{
    public struct PlayerInput : INetworkInput
    {
        public Vector2 Input;
        public Vector2 YawPitch;
        public Vector3 CameraPoint;
        public bool IsJump;
        public bool IsFire;
        public bool IsHoldFire;
        public bool IsInteract;
        public bool IsThrowing;
        public bool Reload;
    }
}