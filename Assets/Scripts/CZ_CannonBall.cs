using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CZ_CannonBall : MonoBehaviour
{
    [SerializeField] ParticleSystem fx_explode;
    [SerializeField,Range(0,10f)] float impactTrigger;
    [SerializeField, Range(0,5f)] float radius = 0.1f;
    [SerializeField, Range(1,10f)] float power = 1;
    Rigidbody rb;
    float timer;
    bool exploded = false;
    [SerializeField] AudioClip a_explode;

    void Start()
    {
        fx_explode.transform.parent = null;
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        timer -= Time.deltaTime;
        if(timer < 0 && !exploded) {
            StartCoroutine(explode());
        }
    }

    public void fire(float force){
        rb.velocity = Vector3.zero;
        rb.AddForce(transform.forward * force * rb.mass,ForceMode.Impulse);
        exploded = false;
        timer = 3;
    }

    private void OnCollisionEnter(Collision collision) {
        if(!exploded && collision.relativeVelocity.magnitude > impactTrigger) {
            StartCoroutine(explode());
        }
    }

    IEnumerator explode() {
        exploded = true;
        AudioSource.PlayClipAtPoint(a_explode,transform.position);
        yield return new WaitForFixedUpdate();
        Vector3 explosionPos = transform.position;
        transform.position = Vector3.down * 100;
        Collider[] colliders = Physics.OverlapSphere(explosionPos,radius);
        foreach (Collider hit in colliders) {
            CZ_Brick brick = hit.GetComponent<CZ_Brick>();
            if (brick) {
                float damage = (power * 10f) / Vector3.Distance(brick.transform.position,explosionPos);
                damage = Mathf.Clamp(damage,0,power);
                brick.hit(damage);
            }
        }
        yield return null;
        colliders = Physics.OverlapSphere(explosionPos,radius);
        foreach (Collider hit in colliders) {
            Rigidbody rb = hit.GetComponent<Rigidbody>();
            if (rb) {rb.AddExplosionForce(power * 100,explosionPos,radius,1.5F);}
        }
        fx_explode.transform.position = explosionPos;
        fx_explode.Play();
        Light l = fx_explode.GetComponent<Light>();
        l.enabled = true;
        yield return new WaitForSeconds(0.1f);
        l.enabled = false;

    }

   
}
