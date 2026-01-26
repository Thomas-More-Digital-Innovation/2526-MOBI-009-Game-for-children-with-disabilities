using UnityEngine;

public class TouchTrackSpawner : MonoBehaviour
{
    public GameObject trackGeneratorPrefab;

    public Camera xrCamera;

    public LayerMask surfaceMask;

    public float spawnHeightOffset = 0.01f;

    public float cooldown = 0.3f;
    private float lastSpawnTime = -999f;

    // helper function so both trigger and collision can share the same logic
    void SpawnTrackAtContact(Vector3 hitPoint, Vector3 hitNormal, Transform surfaceTransform)
    {
        if (Time.time - lastSpawnTime < cooldown)
            return;

        lastSpawnTime = Time.time;

        Vector3 spawnPos = hitPoint + hitNormal.normalized * spawnHeightOffset;

        Vector3 up = hitNormal.normalized;
        Vector3 forward = Vector3.ProjectOnPlane(xrCamera.transform.forward, up).normalized;
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.ProjectOnPlane(transform.forward, up).normalized;
        }

        Quaternion rot = Quaternion.LookRotation(forward, up);

        GameObject trackObj = Instantiate(trackGeneratorPrefab, spawnPos, rot);

        var gen = trackObj.GetComponent<MRTrackGenerator>();

        if (gen != null)
        {
            gen.GenerateOnSurface(spawnPos, up, forward);
        }
        else
        {
            Debug.Log("MRTrackGenerator Does not exist on spawned track prefab");
        }
    }

    // when the hand collider hits another collider that is not trigger
    private void OnCollisionEnter(Collision collision)
    {
        bool isARPlaneName = collision.gameObject.name.Contains("ARPlane");

        bool isInLayerMask = (surfaceMask.value == 0) ||
                             (((1 << collision.gameObject.layer) & surfaceMask.value) != 0);

        if (!isARPlaneName && !isInLayerMask)
            return;

        ContactPoint contact = collision.GetContact(0);
        SpawnTrackAtContact(contact.point, contact.normal, collision.transform);
    }

    private void OnTriggerEnter(Collider other)
    {
        bool isARPlaneName = other.gameObject.name.Contains("ARPlane");
        bool isInLayerMask = (surfaceMask.value == 0) ||
                             (((1 << other.gameObject.layer) & surfaceMask.value) != 0);

        if (!isARPlaneName && !isInLayerMask)
            return;

        Vector3 hitPoint = other.ClosestPoint(transform.position);
        Vector3 hitNormal = (transform.position - hitPoint).normalized;

        SpawnTrackAtContact(hitPoint, hitNormal, other.transform);
    }
}
