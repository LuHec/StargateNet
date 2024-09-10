using UnityEngine;

namespace StargateNet
{
    public class StartUI : MonoBehaviour
    {
        private bool isStart = false;

        private void OnGUI()
        {
            if (!isStart && GUI.Button(new Rect(10, 10, 100, 90), "Start"))
            {
                NetworkManager.Singleton.NetworkStart();
                NetworkManager.Singleton.Connect();
                isStart = true;
                Destroy(this);
            }
        }
    }
}