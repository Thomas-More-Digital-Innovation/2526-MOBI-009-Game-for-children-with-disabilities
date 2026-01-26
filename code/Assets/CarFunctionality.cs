using UnityEngine;
using UnityEngine.InputSystem;

public class CarAudioAndModelSwitcher : MonoBehaviour
{
    [Header("Models (set in Inspector)")]
    [Tooltip("Drag your car model GameObjects here. Only one will be active at a time.")]
    [SerializeField] private GameObject[] carModels;

    [Tooltip("Which model should be active at start.")]
    [SerializeField] private int startModelIndex = 0;

    [Header("Input (assign from your Input Action Map)")]
    [Tooltip("Button action that cycles the car model (performed triggers).")]
    [SerializeField] private InputActionReference changeCarModelAction;

    [Tooltip("Button action that honks (performed triggers).")]
    [SerializeField] private InputActionReference honkAction;

    [Header("Speed Source")]
    [Tooltip("Rigidbody used to read speed for engine pitch. If empty, tries GetComponentInParent, then GetComponent.")]
    [SerializeField] private Rigidbody rb;

    [Header("Audio Clips")]
    [SerializeField] private AudioClip honkClip;
    [SerializeField] private AudioClip engineLoopClip;
    [SerializeField] private AudioClip changeModelClip;

    [Header("Audio Sources (optional, auto-created if empty)")]
    [SerializeField] private AudioSource honkSource;
    [SerializeField] private AudioSource engineSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("Engine Pitch (speed-based)")]
    [Tooltip("Speed in m/s where pitch reaches maxPitch.")]
    [SerializeField] private float pitchFullAtSpeed = 20f;

    [Tooltip("Engine pitch at 0 speed.")]
    [SerializeField] private float minPitch = 0.9f;

    [Tooltip("Engine pitch at or above pitchFullAtSpeed.")]
    [SerializeField] private float maxPitch = 1.6f;

    [Tooltip("How fast pitch follows target pitch.")]
    [SerializeField] private float pitchLerpSpeed = 10f;

    [Header("Engine Volume (optional)")]
    [Tooltip("If true, engine volume is 0 when not driving (requires drivingStateSource).")]
    [SerializeField] private bool engineOnlyWhileDriving = false;

    [Tooltip("Optional: assign your PhysicsCarController or WaypointCarController that has public bool isDriving.")]
    [SerializeField] private MonoBehaviour drivingStateSource;

    [Tooltip("Name of the bool field/property on drivingStateSource that indicates driving state.")]
    [SerializeField] private string drivingBoolFieldName = "isDriving";

    private int currentModelIndex;

    private void Awake()
    {
        if (!rb)
        {
            rb = GetComponent<Rigidbody>();
            if (!rb) rb = GetComponentInParent<Rigidbody>();
        }

        EnsureAudioSources();
        SetupEngineLoop();

        if (carModels != null && carModels.Length > 0)
        {
            currentModelIndex = Mathf.Clamp(startModelIndex, 0, carModels.Length - 1);
            ApplyModelActiveState(currentModelIndex);
        }
    }

    private void OnEnable()
    {
        if (changeCarModelAction != null)
        {
            changeCarModelAction.action.performed += OnChangeModelPerformed;
            changeCarModelAction.action.Enable();
        }

        if (honkAction != null)
        {
            honkAction.action.performed += OnHonkPerformed;
            honkAction.action.Enable();
        }

        if (engineSource != null && engineLoopClip != null && !engineSource.isPlaying)
            engineSource.Play();
    }

    private void OnDisable()
    {
        if (changeCarModelAction != null)
        {
            changeCarModelAction.action.performed -= OnChangeModelPerformed;
            changeCarModelAction.action.Disable();
        }

        if (honkAction != null)
        {
            honkAction.action.performed -= OnHonkPerformed;
            honkAction.action.Disable();
        }
    }

    private void Update()
    {
        UpdateEnginePitchWithSpeed();
        UpdateEngineVolumeGate();
    }

    private void OnChangeModelPerformed(InputAction.CallbackContext ctx)
    {
        if (carModels == null || carModels.Length == 0) return;

        currentModelIndex++;
        if (currentModelIndex >= carModels.Length) currentModelIndex = 0;

        ApplyModelActiveState(currentModelIndex);

        if (sfxSource != null && changeModelClip != null)
            sfxSource.PlayOneShot(changeModelClip);
    }

    private void OnHonkPerformed(InputAction.CallbackContext ctx)
    {
        if (honkSource == null || honkClip == null) return;
        honkSource.PlayOneShot(honkClip);
    }

    private void ApplyModelActiveState(int activeIndex)
    {
        for (int i = 0; i < carModels.Length; i++)
        {
            if (carModels[i] == null) continue;
            carModels[i].SetActive(i == activeIndex);
        }
    }

    private void EnsureAudioSources()
    {
        if (honkSource == null)
        {
            honkSource = gameObject.AddComponent<AudioSource>();
            honkSource.playOnAwake = false;
            honkSource.loop = false;
            honkSource.spatialBlend = 1f;
        }

        if (engineSource == null)
        {
            engineSource = gameObject.AddComponent<AudioSource>();
            engineSource.playOnAwake = false;
            engineSource.loop = true;
            engineSource.spatialBlend = 1f;
        }

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
            sfxSource.spatialBlend = 1f;
        }
    }

    private void SetupEngineLoop()
    {
        if (engineSource == null) return;

        engineSource.clip = engineLoopClip;
        engineSource.loop = true;
        engineSource.playOnAwake = false;

        if (engineLoopClip != null && !engineSource.isPlaying)
            engineSource.Play();
    }

    private void UpdateEnginePitchWithSpeed()
    {
        if (engineSource == null) return;

        float speed = 0f;
        if (rb != null) speed = rb.linearVelocity.magnitude;

        float denom = Mathf.Max(0.01f, pitchFullAtSpeed);
        float t = Mathf.Clamp01(speed / denom);

        float targetPitch = Mathf.Lerp(minPitch, maxPitch, t);
        engineSource.pitch = Mathf.Lerp(engineSource.pitch, targetPitch, Time.deltaTime * pitchLerpSpeed);
    }

    private void UpdateEngineVolumeGate()
    {
        if (!engineOnlyWhileDriving || engineSource == null) return;

        bool isDriving = GetIsDriving();
        engineSource.volume = isDriving ? 1f : 0f;
    }

    private bool GetIsDriving()
    {
        if (drivingStateSource == null || string.IsNullOrWhiteSpace(drivingBoolFieldName))
            return false;

        var t = drivingStateSource.GetType();

        var field = t.GetField(drivingBoolFieldName);
        if (field != null && field.FieldType == typeof(bool))
            return (bool)field.GetValue(drivingStateSource);

        var prop = t.GetProperty(drivingBoolFieldName);
        if (prop != null && prop.PropertyType == typeof(bool))
            return (bool)prop.GetValue(drivingStateSource);

        return false;
    }
}
