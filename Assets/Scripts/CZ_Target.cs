using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CZ_Target : MonoBehaviour
{

    [SerializeField, Range(0,1f)] float radius = 0.5f;
    [SerializeField, Range(1,10f)] float power = 5;
    [SerializeField] CZ_Cannon cannon;
    bool exploded = false;

    void Start()
    {
        
    }

    void Update()
    {
        
    }
    private void OnTriggerEnter(Collider other) {
        if (!exploded && other.GetComponent<CZ_CannonBall>()) {
            StartCoroutine(explode());
        }
    }

    IEnumerator explode() {
        exploded = true;
        yield return new WaitForFixedUpdate();
        Vector3 explosionPos = transform.position;
        
        Collider[] colliders = Physics.OverlapSphere(explosionPos,radius);
        foreach (Collider hit in colliders) {
            Rigidbody rb = hit.GetComponent<Rigidbody>();

            if (rb != null)
                rb.AddExplosionForce(power * 100,explosionPos,radius,0.5F);
        }
        Destroy(cannon.gameObject);
        Destroy(gameObject);
    }

}
