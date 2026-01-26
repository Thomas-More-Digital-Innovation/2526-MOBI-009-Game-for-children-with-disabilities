using System.Collections;
using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;

public class CarGrabHooks : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private HandGrabInteractable handGrab;
    [SerializeField] private Rigidbody rb;
    [SerializeField] private WaypointCarController car;

    [Header("Throw + reset")]
    [SerializeField] private bool enableGravityOnRelease = true;

    [Tooltip("Lift the car slightly when resetting so it never starts inside the floor/mesh.")]
    [SerializeField] private float resetUpOffset = 0.02f; // 2cm

    [Tooltip("How many FixedUpdate frames to keep collisions off during reset.")]
    [SerializeField] private int resetFixedFrames = 2;

    [Header("Physics")]
    [Tooltip("Collision detection mode to use while the car is flying after release.")]
    [SerializeField] private CollisionDetectionMode releasedCollisionMode = CollisionDetectionMode.ContinuousDynamic;

    private Vector3 homePos;
    private Quaternion homeRot;
    private bool hasHomePose;

    private bool isHeld;
    private bool wasReleased;
    private bool resetting;

    private void OnEnable()
    {
        if (handGrab != null)
            handGrab.WhenPointerEventRaised += OnPointerEvent;
    }

    private void OnDisable()
    {
        if (handGrab != null)
            handGrab.WhenPointerEventRaised -= OnPointerEvent;
    }

    private void OnPointerEvent(PointerEvent evt)
    {
        if (evt.Type == PointerEventType.Select) OnGrabbed();
        else if (evt.Type == PointerEventType.Unselect) OnReleased();
    }

    private void CacheHomePoseFromTrack()
    {
        if (car != null && car.TryGetSpawnPose(out var p, out var r, resetUpOffset))
        {
            homePos = p;
            homeRot = r;
            hasHomePose = true;
            return;
        }

        if (rb != null)
        {
            homePos = rb.position + Vector3.up * resetUpOffset;
            homeRot = rb.rotation;
            hasHomePose = true;
        }
    }

    private void OnGrabbed()
    {
        if (rb == null) return;

        CacheHomePoseFromTrack();

        isHeld = true;
        wasReleased = false;
        resetting = false;

        if (car != null) car.isDriving = false;

        rb.useGravity = false;
        rb.isKinematic = false;
        rb.detectCollisions = true;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
    }

    private void OnReleased()
    {
        if (rb == null) return;

        isHeld = false;
        wasReleased = true;

        if (enableGravityOnRelease)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.detectCollisions = true;
        }

        rb.collisionDetectionMode = releasedCollisionMode;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (rb == null) return;
        if (resetting) return;

        if (!wasReleased || isHeld)
            return;

        CacheHomePoseFromTrack();

        if (!hasHomePose)
            return;

        StartCoroutine(ResetRoutine());
    }

    private IEnumerator ResetRoutine()
    {
        resetting = true;
        wasReleased = false;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        rb.detectCollisions = false;

        for (int i = 0; i < Mathf.Max(1, resetFixedFrames); i++)
            yield return new WaitForFixedUpdate();

        rb.MovePosition(homePos);
        rb.MoveRotation(homeRot);

        yield return new WaitForFixedUpdate();

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        rb.useGravity = false;
        rb.isKinematic = false;

        rb.detectCollisions = true;
        rb.collisionDetectionMode = CollisionDetectionMode.Discrete;

        resetting = false;
    }
}
