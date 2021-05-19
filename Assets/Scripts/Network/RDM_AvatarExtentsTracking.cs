using UnityEngine;

public class RDM_AvatarExtentsTracking : MonoBehaviour
{
    public Transform trackingTransform;

    public void read() {
        transform.position = trackingTransform.position;
        transform.rotation = trackingTransform.rotation;
    }

}
