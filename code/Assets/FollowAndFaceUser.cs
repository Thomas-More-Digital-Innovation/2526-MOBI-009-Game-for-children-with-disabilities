using UnityEngine;

public class FollowAndFaceUser : MonoBehaviour
{
    public Transform head;
    public float distance = 0.6f;
    public float height = -0.1f;
    public float followSpeed = 8f;

    void LateUpdate()
    {
        if (!head) return;

        
        Vector3 forward = head.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
            forward = head.up;

        forward.Normalize();

        Vector3 targetPos = head.position + forward * distance;
        targetPos.y = head.position.y + height;

        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * followSpeed);

        Vector3 dir = head.position - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(dir);
    }
}
