using UnityEngine;

namespace StargateNet
{
    public class StartUI : MonoBehaviour
    {
        private bool isStart = false;

        private void OnGUI()
        {
            if (!isStart && GUI.Button(new Rect(10, 10, 100, 90), "Server"))
            {
                NetworkManager.Singleton.NetworkStart();
                NetworkManager.Singleton.Connect();
                isStart = true;
                Destroy(this);
            }
            
            if (!isStart && GUI.Button(new Rect(10, 120, 100, 90), "Server"))
            {
                NetworkManager.Singleton.NetworkStart();
                NetworkManager.Singleton.Connect();
                isStart = true;
                Destroy(this);
            }
        }
    }
}