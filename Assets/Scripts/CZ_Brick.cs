using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CZ_Brick : MonoBehaviour
{
    [SerializeField] float hitpoints = 2;
    [SerializeField] AudioClip[] a_collide;
    [SerializeField] GameObject fx_Debris;
    bool done;
    void Start()
    {
        done = false;
    }

    void Update()
    {
    
    }

    public void hit(float damage) {
        if (done) { return; }
        hitpoints -= damage;
        if (hitpoints < 0) {
            done = true;
            Instantiate(fx_Debris,transform.position,transform.rotation);
            Destroy(gameObject);
        }
    }

    private void OnCollisionEnter(Collision collision) {
        if(collision.relativeVelocity.magnitude > 0.1f) {
            int rClip = Random.Range(0,a_collide.Length);
            AudioSource.PlayClipAtPoint(a_collide[rClip],transform.position,Mathf.Clamp01(collision.relativeVelocity.magnitude));
        }
    }
}
