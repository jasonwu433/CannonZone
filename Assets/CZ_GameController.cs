using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CZ_GameController : MonoBehaviour
{
    public static CZ_GameController instance;
    void Start()
    {
        instance = this;
        UnityEngine.XR.InputTracking.Recenter();
    }

    
}
