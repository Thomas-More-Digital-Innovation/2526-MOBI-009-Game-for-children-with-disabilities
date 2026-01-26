using UnityEngine;

public class AssignTrackToCar : MonoBehaviour
{
    public MRTrackGenerator generator;
    public WaypointCarController car;

    void Start()
    {
        if (generator != null && car != null)
        {
            car.waypoints = generator.waypoints;

            car.trackGenerator = generator;

            if (generator.waypoints.Count > 1)
            {
                Transform start = generator.waypoints[0];
                Transform next = generator.waypoints[1];

                car.transform.position = start.position;
                Vector3 dir = (next.position - start.position).normalized;
                car.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            }
        }
    }
}
