using UnityEngine;
using UnityEngine.InputSystem;

public class CarUnfreezeHitStack : MonoBehaviour
{
    [Header("Stack")]
    [SerializeField] private string stackRootTag = "ObstacleStack";
    [SerializeField] private bool keepRotationFrozen = true;

    [Header("Audio")]
    [SerializeField] private AudioSource hitAudioSource;
    [SerializeField] private AudioClip hitClip;
    [SerializeField] private bool crashSoundEnabled = true;

    [Header("Input (New Input System)")]
    [Tooltip("Assign a Button action (ex: X, A, Space). Press to toggle crash sound on/off.")]
    [SerializeField] private InputActionReference toggleCrashSound;

    private void OnEnable()
    {
        if (toggleCrashSound != null)
        {
            toggleCrashSound.action.Enable();
            toggleCrashSound.action.performed += OnToggleCrashSound;
        }
    }

    private void OnDisable()
    {
        if (toggleCrashSound != null)
            toggleCrashSound.action.performed -= OnToggleCrashSound;
    }

    private void OnToggleCrashSound(InputAction.CallbackContext ctx)
    {
        crashSoundEnabled = !crashSoundEnabled;

        // Optional, stop any currently playing sound
        if (!crashSoundEnabled && hitAudioSource != null)
            hitAudioSource.Stop();
    }

    private void OnCollisionEnter(Collision collision)
    {
        Rigidbody hitRb = collision.rigidbody ?? collision.collider.attachedRigidbody;
        if (!hitRb) return;

        Transform stackRoot = hitRb.transform;
        while (stackRoot != null && !stackRoot.CompareTag(stackRootTag))
            stackRoot = stackRoot.parent;

        if (stackRoot == null) return;

        PlayHitSound();
        UnfreezeStack(stackRoot);
    }

    private void PlayHitSound()
    {
        if (!crashSoundEnabled) return;
        if (!hitAudioSource || !hitClip) return;

        hitAudioSource.PlayOneShot(hitClip);
    }

    private void UnfreezeStack(Transform stackRoot)
    {
        Rigidbody[] rbs = stackRoot.GetComponentsInChildren<Rigidbody>(true);

        foreach (var rb in rbs)
        {
            if (!rb) continue;

            rb.isKinematic = false;
            rb.WakeUp();

            rb.constraints &= ~RigidbodyConstraints.FreezePosition;

            if (!keepRotationFrozen)
                rb.constraints = RigidbodyConstraints.None;
        }
    }
}
