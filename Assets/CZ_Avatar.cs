using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CZ_Avatar : MonoBehaviour
{
    [Header("Skeleton Rig")]
    [SerializeField] Transform spine;
    [SerializeField] Transform cam;
    [SerializeField] Vector3 spineAdjust;

    Quaternion baseRot;

    void Start()
    {
        baseRot = spine.localRotation;
    }

    void Update()
    {
        spine.transform.LookAt(cam.position,cam.forward);
        spine.transform.Rotate(spineAdjust);
        spine.localRotation = Quaternion.Lerp(spine.localRotation,baseRot,0.5f);
    }
}
