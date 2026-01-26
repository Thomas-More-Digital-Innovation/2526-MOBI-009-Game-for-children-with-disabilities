using System.Collections.Generic;
using UnityEngine;
// MR Utility Kit so we can read the real world surfaces and sizes
using Meta.XR.MRUtilityKit;

public class MRTrackGenerator : MonoBehaviour
{
    public GameObject roadSegmentPrefab;
    public GameObject cornerPrefab;

    public float segmentLength = 0.2f;
    [Range(0.5f, 1f)]
    public float segmentLengthFactor = 0.9f;

    public float cornerGap = 0.05f;
    public Vector3 cornerScale = new Vector3(1f, 1f, 1f);
    public float cornerExtraRotation = 90f;


    public float roadHeightOffset = 0.03f;

    public float trackWidth = 0.4f;
    public float trackLength = 0.8f;

    [Range(0.1f, 0.99f)]
    public float surfaceMargin = 0.85f;

    [Header("Road scale")]
    public float roadWidth = 0.2f;       // X scale
    public float roadThickness = 0.02f;  // Y scale

    [Header("Corner path")]
    [Tooltip("How many extra waypoints to add along each rounded corner curve")]
    [Range(1, 8)]
    public int cornerSubdivisions = 3;

    [Tooltip("Bezier control offset in TRACK LOCAL SPACE (X = right, Y = up, Z = forward). Controls how the corner curve bulges.")]
    public Vector3 cornerBezierOffsetLocal = new Vector3(0f, 0f, 0.05f);

    public MRUKAnchor.SceneLabels surfaceLabels = MRUKAnchor.SceneLabels.TABLE;

    public List<Transform> waypoints = new List<Transform>();

    private Transform waypointsParent;
    private Transform roadParent;

    void Start()
    {
        // start with our own transform as a dumb guess
        Vector3 center = transform.position;
        Vector3 up = Vector3.up;
        Vector3 forward = transform.forward.normalized;

        // Try to snap and scale to the closest MRUK anchor (table / floor / surface)
        TryFitToClosestAnchor(ref center, ref up, ref forward);

        GenerateOnSurface(center, up, forward);
    }

    void TryFitToClosestAnchor(ref Vector3 center, ref Vector3 up, ref Vector3 forward)
    {
        if (MRUK.Instance == null)
        {
            Debug.LogWarning("MRTrackGenerator: MRUK.Instance is null, using default trackWidth/trackLength.");
            return;
        }

        var room = MRUK.Instance.GetCurrentRoom();
        if (room == null)
        {
            Debug.LogWarning("MRTrackGenerator: no current MRUK room, using default track size.");
            return;
        }

        MRUKAnchor bestAnchor = null;
        float bestDist = float.MaxValue;

        // go through all known anchors in the room and find a surface (table, floor, other) closest to our spawn position
        foreach (var anchor in room.Anchors)
        {
            // skip stuff that has no plane (no width or height info)
            if (!anchor.PlaneRect.HasValue)
                continue;

            // skip anchors that are not of the types we care about
            if (!anchor.HasAnyLabel(surfaceLabels))
                continue;

            // where this physical surface lives in world space
            Vector3 anchorCenter = anchor.GetAnchorCenter();
            float dist = Vector3.Distance(transform.position, anchorCenter);

            if (dist < bestDist)
            {
                bestDist = dist;
                bestAnchor = anchor;
            }
        }

        if (bestAnchor == null)
        {
            Debug.LogWarning("MRTrackGenerator: no matching surface anchor found near track, using default size.");
            return;
        }
        var rect = bestAnchor.PlaneRect.Value;
        float surfaceWidth = rect.width;
        float surfaceLength = rect.height;

        // shrink a bit so we do not overflow the edges of the table
        trackWidth = surfaceWidth * surfaceMargin;
        trackLength = surfaceLength * surfaceMargin;

        Vector3 anchorCenterPos = bestAnchor.GetAnchorCenter();
        Vector3 anchorUp = bestAnchor.transform.up.normalized;
        Vector3 worldUp = Vector3.up;

        float dotUp = Vector3.Dot(anchorUp, worldUp);

        center = anchorCenterPos;

        // if the surface is almost horizontal, force world up so table is flat
        if (Mathf.Abs(dotUp) > 0.5f)
        {
            up = dotUp >= 0 ? worldUp : -worldUp;
        }
        else
        {
            // probably wall or weird angle, still force horizontal track
            up = worldUp;
        }

        Vector3 anchorForward = bestAnchor.transform.forward;
        forward = Vector3.ProjectOnPlane(anchorForward, up).normalized;
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.ProjectOnPlane(bestAnchor.transform.right, up).normalized;
        }

        Debug.Log($"MRTrackGenerator: fitted to anchor {bestAnchor.name} " +
                  $"surface {surfaceWidth:F2} x {surfaceLength:F2}, " +
                  $"track {trackWidth:F2} x {trackLength:F2}, dotUp={dotUp:F2}");
    }

    public void GenerateOnSurface(Vector3 hitPoint, Vector3 surfaceNormal, Vector3 viewForward)
    {
        // check if road is assigned
        if (roadSegmentPrefab == null)
        {
            Debug.LogError("MRTrackGenerator needs a roadSegmentPrefab");
            return;
        }

        SetupParents();
        ClearChildren();

        Vector3 up = surfaceNormal.normalized;

        Vector3 forward = Vector3.ProjectOnPlane(viewForward, up).normalized;
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.Cross(up, Vector3.right).normalized;
        }

        Vector3 right = Vector3.Cross(up, forward).normalized;

        Vector3 center = hitPoint + up * roadHeightOffset;

        transform.position = center;
        transform.rotation = Quaternion.LookRotation(right, up);

        // make the road fit within the space by making it half the width and length of the surface
        float halfX = trackWidth * 0.5f;
        float halfZ = trackLength * 0.5f;

        float marginFactor = 0.8f;
        halfX *= marginFactor;
        halfZ *= marginFactor;

        Vector3 c0 = center + (-right * halfX) + (-forward * halfZ);
        Vector3 c1 = center + (right * halfX) + (-forward * halfZ);
        Vector3 c2 = center + (right * halfX) + (forward * halfZ);
        Vector3 c3 = center + (-right * halfX) + (forward * halfZ);

        Vector3[] corners = { c0, c1, c2, c3 };

        // spawn corner prefabs if we have one
        if (cornerPrefab != null)
        {
            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 cornerPos = corners[i];
                Vector3 nextPos = corners[(i + 1) % corners.Length];
                Vector3 dirToNext = (nextPos - cornerPos).normalized;

                Quaternion cornerRot = Quaternion.LookRotation(dirToNext, up);
                cornerRot *= Quaternion.Euler(0f, cornerExtraRotation, 0f);

                GameObject cornerObj = Instantiate(cornerPrefab, cornerPos, cornerRot, roadParent);
                cornerObj.transform.localScale = cornerScale;
            }
        }

        // The car will go around the corners in a loop
        List<Vector3> loopingCorners = new List<Vector3> { c0, c1, c2, c3, c0 };

        waypoints.Clear();

        Transform firstEdgeFirstWp = null;
        Transform prevEdgeLastWp = null;

        // build car path and road segments per edge
        for (int i = 0; i < loopingCorners.Count - 1; i++)
        {
            Vector3 startCorner = loopingCorners[i];
            Vector3 endCorner = loopingCorners[i + 1];
            Vector3 edge = endCorner - startCorner;

            float edgeLength = edge.magnitude;
            Vector3 edgeDir = edge.normalized;

            // usable length between corner gaps
            float usableLength = Mathf.Max(0f, edgeLength - 2f * cornerGap);
            int count = Mathf.Max(1, Mathf.RoundToInt(usableLength / segmentLength));

            List<Transform> edgeWaypoints = new List<Transform>();

            // interior waypoints along the edge
            for (int j = 0; j <= count; j++)
            {
                float t = (float)j / count;
                float distFromStart = cornerGap + t * usableLength;
                Vector3 pos = startCorner + edgeDir * distFromStart;

                GameObject wpObj = new GameObject($"WP_{i}_{j}");
                wpObj.transform.position = pos;
                wpObj.transform.SetParent(waypointsParent);

                edgeWaypoints.Add(wpObj.transform);
            }
            if (i == 0 && edgeWaypoints.Count > 0)
            {
                firstEdgeFirstWp = edgeWaypoints[0];
            }

            // if not the first edge, insert a rounded curve of waypoints between
            // previous edge and this edge, using a Bezier around the corner
            if (i > 0 && prevEdgeLastWp != null && edgeWaypoints.Count > 0)
            {
                Vector3 corner = startCorner;
                AddCornerCurveWaypoints(corner, prevEdgeLastWp, edgeWaypoints[0]);
            }

            // add all edge waypoints to the main car path
            for (int j = 0; j < edgeWaypoints.Count; j++)
            {
                waypoints.Add(edgeWaypoints[j]);
            }

            SpawnRoadSegmentsForEdge(edgeWaypoints, up);

            prevEdgeLastWp = edgeWaypoints[edgeWaypoints.Count - 1];
        }

        if (firstEdgeFirstWp != null && prevEdgeLastWp != null)
        {
            Vector3 corner = c0;
            AddCornerCurveWaypoints(corner, prevEdgeLastWp, firstEdgeFirstWp);
        }

        transform.rotation *= Quaternion.Euler(0, 90f, 0);
    }

    void AddCornerCurveWaypoints(Vector3 corner, Transform pIn, Transform pOut)
    {
        if (cornerSubdivisions <= 0)
            return;
        if (pIn == null || pOut == null)
            return;

        Vector3 control = corner;

        Vector3 localOffsetWorld = transform.TransformDirection(cornerBezierOffsetLocal);
        control += localOffsetWorld;

        Vector3 a = pIn.position;
        Vector3 b = pOut.position;

        for (int k = 1; k <= cornerSubdivisions; k++)
        {
            float t = (float)k / (cornerSubdivisions + 1);

            // quadratic Bezier: B(t) = (1 - t)^2 * A + 2(1 - t)t * P + t^2 * B
            float oneMinusT = 1f - t;
            Vector3 pos =
                oneMinusT * oneMinusT * a +
                2f * oneMinusT * t * control +
                t * t * b;

            GameObject midObj = new GameObject($"CornerCurveWP_{corner.GetHashCode()}_{k}");
            midObj.transform.position = pos;
            midObj.transform.SetParent(waypointsParent);

            waypoints.Add(midObj.transform);
        }
    }

    void SetupParents()
    {
        // Organisation purposes to not clutter scene
        if (waypointsParent == null)
        {
            GameObject wpParentObj = new GameObject("Waypoints");
            wpParentObj.transform.SetParent(transform);
            waypointsParent = wpParentObj.transform;
        }

        if (roadParent == null)
        {
            GameObject rpObj = new GameObject("RoadSegments");
            rpObj.transform.SetParent(transform);
            roadParent = rpObj.transform;
        }
    }

    void ClearChildren()
    {
        waypoints.Clear();

        if (waypointsParent != null)
        {
            for (int i = waypointsParent.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(waypointsParent.GetChild(i).gameObject);
            }
        }

        if (roadParent != null)
        {
            for (int i = roadParent.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(roadParent.GetChild(i).gameObject);
            }
        }
    }

    void SpawnRoadSegmentsForEdge(List<Transform> edgeWaypoints, Vector3 up)
    {
        for (int i = 0; i < edgeWaypoints.Count - 1; i++)
        {
            Vector3 p0 = edgeWaypoints[i].position;
            Vector3 p1 = edgeWaypoints[i + 1].position;

            Vector3 mid = (p0 + p1) * 0.5f;
            Vector3 dir = (p1 - p0).normalized;

            GameObject seg = Instantiate(roadSegmentPrefab, mid, Quaternion.identity, roadParent);
            seg.transform.rotation = Quaternion.LookRotation(dir, up);

            float fullLength = Vector3.Distance(p0, p1);
            float visualLength = fullLength * segmentLengthFactor;

            Vector3 localScale = seg.transform.localScale;

            localScale.x = roadWidth;
            localScale.y = roadThickness;

            localScale.z = visualLength;

            seg.transform.localScale = localScale;
        }
    }

    public void SetRoadVisible(bool visible)
    {
        if (roadParent != null)
        {
            roadParent.gameObject.SetActive(visible);
        }
    }
}
