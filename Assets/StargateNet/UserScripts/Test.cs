using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Test : MonoBehaviour
{
    public bool simluate;

    // Start is called before the first frame update
    void Awake()
    {
        Physics.simulationMode = SimulationMode.Script;
    }

    // Update is called once per frame
    void Update()
    {
        if (simluate)
            Physics.Simulate(Time.fixedDeltaTime);
    }
}