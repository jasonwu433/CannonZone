using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CZ_Breath : MonoBehaviour
{
    Vector3 startPos;
    Vector3 randPos;
    Quaternion startRot;
    Quaternion randRot;
    float timer;
    void Start()
    {
        startPos = transform.position;
        startRot = transform.rotation;
    }

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer < 0) {
            randPos = Random.insideUnitSphere * 0.05f;
            randRot = Quaternion.Euler(Random.insideUnitSphere * 5f);
            timer = Random.Range(0.5f,2f);
        }
        transform.position = Vector3.Lerp(transform.position,startPos + randPos,Time.deltaTime);
        transform.rotation = Quaternion.Lerp(transform.rotation,startRot * randRot,Time.deltaTime);
    }
}
