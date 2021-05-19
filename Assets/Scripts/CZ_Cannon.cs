using UnityEngine;

public class CZ_Cannon:MonoBehaviour {
    public CannonModes cannonMode;
    public enum CannonModes { client, host, AI }

    [SerializeField] LineRenderer targetLine;
    [SerializeField] Transform turretVertical;
    [SerializeField] Transform firePoint;
    [SerializeField] CZ_CannonBall projectile;
    [SerializeField, Range(0,2)] float forceMult;
    [SerializeField, Range(0,1)] float cannonSpeed;
    Vector2 inputs = Vector2.zero;
    float fireHold;
    float verticalAngle = 45;
    float horizontalAngle = 45;

    [Header("Trajectory")]
    [SerializeField, Range(0,1)] float tLengthMult = 0.04f;
    [SerializeField, Range(0,1)] float tDropMult = 0.01f;
    float tResolution = 0.5f;
    Vector3[] tp;

    [Header("AI")]
    [SerializeField, Range(0,1)] float aiError = 0.1f;
    public AIDirectives AiDirective;
    [SerializeField] Transform target;
    [SerializeField] AudioClip a_cannon;


    public enum AIDirectives { moveHor, moveVert, shooting, loading, waiting };
    Vector3 dirCoords;
    float vertAngle;
    float holdValue;
    float waitValue;
    

    void Start() {
        tp = new Vector3[50];
        if (cannonMode == CannonModes.host) {  }
        else if (cannonMode == CannonModes.AI) { findTargetAngles(); }
    }

    void Update() {
        if (cannonMode == CannonModes.host) { inputSource(); }
        else if (cannonMode == CannonModes.AI) { aiSource(); }
        moveCannon();
        updateTP();
    }

    void inputSource() {
        inputs = Vector3.zero;
        inputs.x -= Input.GetKey(KeyCode.LeftArrow) ? 1 : 0;
        inputs.x += Input.GetKey(KeyCode.RightArrow) ? 1 : 0;
        inputs.y -= Input.GetKey(KeyCode.DownArrow) ? 1 : 0;
        inputs.y += Input.GetKey(KeyCode.UpArrow) ? 1 : 0;

        inputs.x += Input.GetAxis("Horizontal") * 2f;
        inputs.y += Input.GetAxis("Vertical") * 2f;

        if (Input.GetKeyUp(KeyCode.Space) || Input.GetButtonUp("Fire1") || Input.GetButtonUp("Fire2")) {
            fireProjectile();
        }
        if (Input.GetKey(KeyCode.Space) || Input.GetButton("Fire1") || Input.GetButton("Fire2")) {
            fireHold = Mathf.Clamp(fireHold + Time.deltaTime * 2f,1,5);
        }
        else { fireHold = 0; }
    }

    public void aiSource() {
        if (!target) { }
        else if (AiDirective == AIDirectives.moveHor) {
            float a = Vector3.SignedAngle(dirCoords,transform.forward,Vector3.up);
            if (Mathf.Abs(a) > 1) {
                inputs.x = -Mathf.Clamp(a,-1,1);
            }
            else { inputs.x = 0; AiDirective = AIDirectives.moveVert; }
        }
        else if (AiDirective == AIDirectives.moveVert) {
            float a = vertAngle - turretVertical.transform.localRotation.eulerAngles.x;
            if (Mathf.Abs(a) > 1) {
                inputs.y = Mathf.Clamp(-a,-1,1);
            }
            else { inputs.y = 0; AiDirective = AIDirectives.loading; }
        }
        else if (AiDirective == AIDirectives.loading) {
            inputs = Vector2.zero;
            fireHold = Mathf.Clamp(fireHold + Time.deltaTime * 2f,1,5);
            if (fireHold > holdValue || fireHold >= 5) { AiDirective = AIDirectives.shooting; }
        }
        else if (AiDirective == AIDirectives.shooting) {
            fireProjectile();
            findTargetAngles();
            fireHold = 0;
            waitValue = Random.Range(0,3);
            AiDirective = AIDirectives.waiting;
            
        }
        else if (AiDirective == AIDirectives.waiting) {
            waitValue -= Time.deltaTime;
            if (waitValue < 0) { AiDirective = AIDirectives.moveHor; }
        }
    }

    void findTargetAngles() {
        dirCoords = target.position - transform.position;
        dirCoords.y = 0;
        dirCoords.x += Random.Range(-aiError,aiError);
        dirCoords.z += Random.Range(-aiError,aiError);
        dirCoords.Normalize();
        vertAngle = 45f * ( 1 + Random.Range(-aiError,aiError) );
        holdValue = 4f * ( 1 + Random.Range(-aiError,aiError) );
        Debug.DrawRay(transform.position,dirCoords,Color.red,1);
    }

    void moveCannon() {

        verticalAngle = Mathf.Clamp(verticalAngle - inputs.y * cannonSpeed,0,90);
        horizontalAngle = horizontalAngle + inputs.x * cannonSpeed;
        transform.localRotation = Quaternion.Euler(0,horizontalAngle,0);
        turretVertical.localRotation = Quaternion.Euler(verticalAngle,0,0);
    }

    void fireProjectile() {
        AudioSource.PlayClipAtPoint(a_cannon,transform.position);
        projectile.transform.SetPositionAndRotation(firePoint.position,firePoint.rotation);
        projectile.fire(fireHold * forceMult);
        targetLine.enabled = false;
    }

    void updateTP() {
        if (fireHold == 0) { return; }
        targetLine.enabled = true;
        for (int i = 0; i < tp.Length; i++) {
            tp[i] = firePoint.position + ( firePoint.forward * i * fireHold * forceMult * tLengthMult * ( 1 - tResolution ) );
            tp[i] -= Vector3.up * Mathf.Pow(( Physics.gravity.y * tDropMult * i * ( 1 - tResolution ) ),2f);
        }
        updateTargetLine();
    }

    void updateTargetLine() {
        targetLine.positionCount = tp.Length;
        targetLine.SetPositions(tp);
    }

}
