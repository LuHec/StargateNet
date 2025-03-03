using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AutoDes : MonoBehaviour
{
    public float desTime = 1.0f;
    private float timer = 0.0f;

    // Update is called once per frame
    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= desTime)
        {
            Destroy(gameObject);
        }   
    }
}
