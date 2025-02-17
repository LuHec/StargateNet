using UnityEngine;

namespace StargateNet
{
    public struct PlayerInput : INetworkInput
    {
        public Vector2 Input;
        public Vector2 YawPitch;
        public bool IsJump;
        public bool IsFire;
        public bool IsInteract;
        public float alpha;
        public Tick remoteFromTick;
    }
}