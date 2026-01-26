using System.Collections;
using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using Meta.XR.MRUtilityKit;

public class CarGrabResetOnAnythingButTable : MonoBehaviour
{
    [SerializeField] private HandGrabInteractable handGrab;
    [SerializeField] private Rigidbody rb;
    [SerializeField] private GameGenerator gameGenerator;

    [SerializeField] private float resetUpOffset = 0.02f;
    [SerializeField] private int resetFixedFrames = 2;
    [SerializeField] private float fallDistance = 0.25f;

    private bool isHeld;
    private bool resetting;

    private void Awake()
    {
        if (!rb) rb = GetComponent<Rigidbody>();
        if (!gameGenerator) gameGenerator = FindFirstObjectByType<GameGenerator>();
    }

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
        if (evt.Type == PointerEventType.Select) isHeld = true;
        if (evt.Type == PointerEventType.Unselect) isHeld = false;
    }

    private bool GeneratorReady()
    {
        return gameGenerator != null &&
               gameGenerator.cellCenters != null &&
               gameGenerator.cellCenters.Length > 0;
    }

    private void FixedUpdate()
    {
        if (resetting) return;
        if (isHeld) return;
        if (!rb) return;
        if (!GeneratorReady()) return;

        float gridY = gameGenerator.cellCenters[0].y + resetUpOffset;

        if (rb.position.y < gridY - fallDistance)
            StartCoroutine(ResetRoutine());
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (resetting) return;
        if (isHeld) return;
        if (!GeneratorReady()) return;

        MRUKAnchor anchor = collision.collider.GetComponentInParent<MRUKAnchor>();
        if (anchor == null) return;

        bool hitFloor = anchor.HasAnyLabel(MRUKAnchor.SceneLabels.FLOOR);
        bool hitWall = anchor.HasAnyLabel(MRUKAnchor.SceneLabels.INNER_WALL_FACE);

        if (!hitFloor && !hitWall) return;

        StartCoroutine(ResetRoutine());
    }

    private IEnumerator ResetRoutine()
    {
        resetting = true;

        int idx = gameGenerator.SelectRandomSpawnSpot(true);
        if (idx < 0)
        {
            resetting = false;
            yield break;
        }

        Vector3 targetPos = gameGenerator.cellCenters[idx] + Vector3.up * resetUpOffset;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.detectCollisions = false;

        for (int i = 0; i < Mathf.Max(1, resetFixedFrames); i++)
            yield return new WaitForFixedUpdate();

        rb.MovePosition(targetPos);
        rb.MoveRotation(Quaternion.Euler(0f, 0f, 0f));

        yield return new WaitForFixedUpdate();

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.detectCollisions = true;

        resetting = false;
    }
}
