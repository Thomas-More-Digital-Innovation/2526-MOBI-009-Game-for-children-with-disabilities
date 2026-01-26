using System.Collections.Generic;
using UnityEngine;
// new input system
using UnityEngine.InputSystem;

public class WaypointCarController : MonoBehaviour
{
    [Header("Movement")]
    public List<Transform> waypoints;
    public float speed = 0.25f;
    public float turnSpeed = 4f;
    public float reachDistance = 0.02f;

    // car only moves while this is true
    public bool isDriving = false;

    private int index = 0;

    [Header("Input")]
    // Hold this button to drive
    public InputActionReference driveAction;
    public InputActionReference drive10SecondsAction;

    // Press to honk
    public InputActionReference honkAction;
    // Press to switch car model
    public InputActionReference changeCarModelAction;
    // Press to show / hide road
    public InputActionReference toggleRoadAction;

    [Header("Audio")]
    public AudioSource sfxSource;
    public AudioClip honkClip;
    public AudioClip ignitionClip;
    public AudioClip engineLoopClip;
    public AudioClip brakeClip;

    // NEW: road show/hide sounds
    public AudioClip roadShowClip;
    public AudioClip roadHideClip;

    // NEW: car model change sound
    public AudioClip changeCarClip;

    [Tooltip("Minimum seconds between automatic honks")]
    public float minAutoHonkInterval = 3f;
    [Tooltip("Maximum seconds between automatic honks")]
    public float maxAutoHonkInterval = 4f;

    public bool autoHonkEnabled = true;
    private float nextAutoHonkTime;

    private AudioSource engineLoopSource;

    [Header("Car models")]
    [Tooltip("Different visual models, only one active at a time")]
    public List<GameObject> carModels = new List<GameObject>();
    private int currentModelIndex = 0;

    [Header("Track reference")]
    public MRTrackGenerator trackGenerator;

    private bool roadVisible = true;

    // NEW: 10-second drive state
    [Header("Timed drive")]
    public float timedDriveSeconds = 10f;
    private bool timedDriveActive = false;
    private float timedDriveEndTime = 0f;

    private void Awake()
    {
        // separate audio source for the looping engine so it does not fight with honks
        if (engineLoopClip != null)
        {
            engineLoopSource = gameObject.AddComponent<AudioSource>();
            engineLoopSource.clip = engineLoopClip;
            engineLoopSource.loop = true;
            engineLoopSource.playOnAwake = false;
        }

        // initialise auto honk
        ScheduleNextAutoHonk();

        // make sure only one car model is active at the start
        if (carModels != null && carModels.Count > 0)
        {
            for (int i = 0; i < carModels.Count; i++)
            {
                carModels[i].SetActive(i == currentModelIndex);
            }
        }
    }

    private void OnEnable()
    {
        if (driveAction != null && driveAction.action != null)
        {
            driveAction.action.performed += OnDrivePerformed;
            driveAction.action.canceled += OnDriveCanceled;
            driveAction.action.Enable();
        }

        if (drive10SecondsAction != null && drive10SecondsAction.action != null)
        {
            drive10SecondsAction.action.performed += OnDrive10SecondsPerformed;
            drive10SecondsAction.action.Enable();
        }

        if (honkAction != null && honkAction.action != null)
        {
            honkAction.action.performed += OnHonkPerformed;
            honkAction.action.Enable();
        }

        if (changeCarModelAction != null && changeCarModelAction.action != null)
        {
            changeCarModelAction.action.performed += OnChangeCarModelPerformed;
            changeCarModelAction.action.Enable();
        }

        if (toggleRoadAction != null && toggleRoadAction.action != null)
        {
            toggleRoadAction.action.performed += OnToggleRoadPerformed;
            toggleRoadAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (driveAction != null && driveAction.action != null)
        {
            driveAction.action.performed -= OnDrivePerformed;
            driveAction.action.canceled -= OnDriveCanceled;
            driveAction.action.Disable();
        }

        if (drive10SecondsAction != null && drive10SecondsAction.action != null)
        {
            drive10SecondsAction.action.performed -= OnDrive10SecondsPerformed;
            drive10SecondsAction.action.Disable();
        }

        if (honkAction != null && honkAction.action != null)
        {
            honkAction.action.performed -= OnHonkPerformed;
            honkAction.action.Disable();
        }

        if (changeCarModelAction != null && changeCarModelAction.action != null)
        {
            changeCarModelAction.action.performed -= OnChangeCarModelPerformed;
            changeCarModelAction.action.Disable();
        }

        if (toggleRoadAction != null && toggleRoadAction.action != null)
        {
            toggleRoadAction.action.performed -= OnToggleRoadPerformed;
            toggleRoadAction.action.Disable();
        }
    }

    // drive button pressed (hold)
    private void OnDrivePerformed(InputAction.CallbackContext ctx)
    {
        // if timed drive is active, ignore hold input so it doesn't fight you
        if (timedDriveActive)
            return;

        if (!isDriving)
        {
            StartDriving();
        }
    }

    // drive button released (hold)
    private void OnDriveCanceled(InputAction.CallbackContext ctx)
    {
        // if timed drive is active, ignore hold input so it doesn't fight you
        if (timedDriveActive)
            return;

        if (isDriving)
        {
            StopDriving();
        }
    }

    // NEW: press once, drive for N seconds
    private void OnDrive10SecondsPerformed(InputAction.CallbackContext ctx)
    {
        Debug.Log($"[Drive10] performed, time={Time.time}, isDriving={isDriving}, control={ctx.control?.path}");
        timedDriveActive = true;
        timedDriveEndTime = Time.time + Mathf.Max(0.1f, timedDriveSeconds);

        if (!isDriving)
            StartDriving();
    }

    private void StartDriving()
    {
        isDriving = true;

        // ignition sound once
        PlayOneShot(ignitionClip);

        // engine loop while driving
        if (engineLoopSource != null && engineLoopClip != null && !engineLoopSource.isPlaying)
        {
            engineLoopSource.Play();
        }
    }

    private void StopDriving()
    {
        isDriving = false;

        // stop engine sound
        if (engineLoopSource != null && engineLoopSource.isPlaying)
        {
            engineLoopSource.Stop();
        }

        // brake sound
        PlayOneShot(brakeClip);
    }

    private void OnHonkPerformed(InputAction.CallbackContext ctx)
    {
        // manual honk
        PlayOneShot(honkClip);

        // once user honks, stop the automatic honking
        autoHonkEnabled = false;
    }

    private void OnChangeCarModelPerformed(InputAction.CallbackContext ctx)
    {
        if (carModels == null || carModels.Count == 0)
            return;

        carModels[currentModelIndex].SetActive(false);
        currentModelIndex = (currentModelIndex + 1) % carModels.Count;
        carModels[currentModelIndex].SetActive(true);

        // NEW: sound on every change
        PlayOneShot(changeCarClip);
    }

    private void OnToggleRoadPerformed(InputAction.CallbackContext ctx)
    {
        if (trackGenerator == null)
            return;

        roadVisible = !roadVisible;
        trackGenerator.SetRoadVisible(roadVisible);

        // NEW: different sound for show vs hide
        PlayOneShot(roadVisible ? roadShowClip : roadHideClip);
    }

    private void PlayOneShot(AudioClip clip)
    {
        if (sfxSource != null && clip != null)
        {
            sfxSource.PlayOneShot(clip);
        }
    }

    private void ScheduleNextAutoHonk()
    {
        if (minAutoHonkInterval <= 0f || maxAutoHonkInterval <= 0f)
        {
            nextAutoHonkTime = Time.time + 3f;
        }
        else
        {
            float t = Random.Range(minAutoHonkInterval, maxAutoHonkInterval);
            nextAutoHonkTime = Time.time + t;
        }
    }

    private void HandleAutoHonk()
    {
        if (!autoHonkEnabled)
            return;

        if (honkClip == null || sfxSource == null)
            return;

        if (Time.time >= nextAutoHonkTime)
        {
            PlayOneShot(honkClip);
            ScheduleNextAutoHonk();
        }
    }

    private void Update()
    { 
        if (timedDriveActive && Time.time >= timedDriveEndTime)
        {
            timedDriveActive = false;
            if (isDriving)
                StopDriving();
        }

        // background honk logic
        HandleAutoHonk();

        // If car is not driving, no movement
        if (!isDriving)
            return;

        if (waypoints == null || waypoints.Count == 0)
            return;

        Transform target = waypoints[index];
        Vector3 dir = target.position - transform.position;
        float dist = dir.magnitude;

        if (dist < reachDistance)
        {
            index = (index + 1) % waypoints.Count;
            return;
        }

        dir.Normalize();
        transform.position += dir * speed * Time.deltaTime;

        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, turnSpeed * Time.deltaTime);
        }
    }
    public bool TryGetSpawnPose(out Vector3 pos, out Quaternion rot, float upOffset = 0.02f)
    {
        pos = transform.position;
        rot = transform.rotation;

        if (waypoints == null || waypoints.Count == 0)
            return false;

        Transform start = waypoints[0];
        pos = start.position + Vector3.up * upOffset;

        // If we have at least 2 waypoints, face the next one
        if (waypoints.Count > 1 && waypoints[1] != null)
        {
            Vector3 dir = (waypoints[1].position - start.position);
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
                rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
            else
                rot = start.rotation;
        }
        else
        {
            rot = start.rotation;
        }

        return true;
    }

}
