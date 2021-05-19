using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent (typeof(Light))]
public class LightBehaviour : MonoBehaviour {

    public float minIntensity = 1f;
    public float maxIntensity = 4f;
    public float moveSpeed = 1f;
    public float lightFlickerFrequency = 2f;
    public Vector3 posOffset;

    private Light lightSource;
    private Vector3 moveTo;
    private Vector3 startPos;
    private Transform ownTransform;
    private float random;

    private void Start() {
        ownTransform = GetComponent<Transform>();
        startPos = ownTransform.position;
        lightSource = GetComponent<Light>();
        random = Random.Range(0.0f, 10f);
        moveTo = startPos;
    }

    private void Update() {
        CheckDistance();
        ownTransform.position = Move();
        lightSource.intensity = SetLightIntensity();       
    }

    private void CheckDistance() {
        if (Vector3.Distance(ownTransform.position, moveTo) <= 0.01f) {
            moveTo = GetNewPos();
        }
    }

    private Vector3 Move() {
        Vector3 newPos = Vector3.Lerp(ownTransform.position, moveTo, Time.deltaTime * moveSpeed);
        return newPos;
    }

    private float SetLightIntensity() {
        float noise = Mathf.PerlinNoise(random, Time.time * lightFlickerFrequency);
        float newIntensity = Mathf.Lerp(minIntensity, maxIntensity, noise);
        return newIntensity;
    }

    private Vector3 GetNewPos() {
        Vector3 newPos = new Vector3(
            Random.Range(-posOffset.x, posOffset.x), 
            Random.Range(-posOffset.y, posOffset.y),
            Random.Range(-posOffset.z, posOffset.z));
        return startPos + newPos;
    }
}
