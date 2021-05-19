using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CZ_ArmorJoint : MonoBehaviour
{
    [SerializeField] Transform target1;
    [SerializeField] Transform target2;
    [SerializeField, Range(0,1)] float dampen = 0;
    [SerializeField] Vector3 rotationCorrection;
    Vector3 dir1;
    Vector3 dir2;
    Vector3 dirAverage;
    Quaternion localRot;
    AudioSource voice;
    float angle;
    float angleHist;
    float angleSpeed;
    float angleSpeedSlow;

    void Start()
    {
        localRot = transform.localRotation;
        voice = GetComponent<AudioSource>();
    }

    // Update is called once per frame
    void Update()
    {
        dir1 = transform.position - target1.position;
        dir2 = transform.position - target2.position;
        dirAverage = (dir1.normalized + dir2.normalized ) / 2f;
        angle = Vector3.Angle(dir1,dir2);
        angleSpeed = angle - angleHist;
        angleHist = angle;
        transform.rotation = Quaternion.LookRotation(dirAverage,Vector3.Cross(dir1, dir2));
        transform.Rotate(rotationCorrection);
        transform.localRotation = Quaternion.Lerp(transform.localRotation,localRot,dampen);
        playHinge(angleSpeed);
    }

    void playHinge(float velocity) {
        if (!voice) { return;}
        if (Mathf.Abs(velocity) < 1) { voice.Stop(); return; }
        angleSpeedSlow = Mathf.Lerp(angleSpeedSlow,Mathf.Clamp(Mathf.Abs(velocity / 5f),0.5f,1.1f),Time.deltaTime * 5f);
        voice.pitch = angleSpeedSlow;
        if (voice.isPlaying) { return; }
        voice.Play();
    }
}
