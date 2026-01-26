using Meta.XR.MRUtilityKit;
using UnityEngine;

public class ObjectRotationCenter : MonoBehaviour
{
    [SerializeField] private MRUKAnchor.SceneLabels tableLabel = MRUKAnchor.SceneLabels.TABLE;

    private MRUKAnchor table;

    private void Start()
    {
        Invoke(nameof(TryFindTable), 0.5f);
    }

    private void TryFindTable()
    {
        var room = MRUK.Instance?.GetCurrentRoom();
        if (room == null) return;

        foreach (var anchor in room.Anchors)
        {
            if (anchor.Label == tableLabel)
            {
                table = anchor;
                break;
            }
        }
    }

    private void LateUpdate()
    {
        if (!table) return;
        Vector3 tableCenterWorld =
            table.transform.TransformPoint(
                new Vector3(
                    table.PlaneRect.Value.center.x,
                    table.PlaneRect.Value.center.y,
                    0f
                )
            );
        Vector3 dir = tableCenterWorld - transform.position;

        Vector3 up = table.transform.TransformDirection(Vector3.forward).normalized;

        dir = Vector3.ProjectOnPlane(dir, up);

        if (dir.sqrMagnitude < 0.0001f) return;

        transform.rotation = Quaternion.LookRotation(dir.normalized, up);
    }
}
