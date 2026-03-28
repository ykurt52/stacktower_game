using UnityEngine;

/// <summary>
/// Top-down camera for Archero-style arena. Portrait orientation.
/// Follows player on XZ plane with smooth tracking.
/// </summary>
public class ArenaCamera : MonoBehaviour
{
    [Header("Follow")]
    [SerializeField] private float followSmooth = 6f;
    [SerializeField] private Vector3 offset = new Vector3(0, 8f, -14f);

    [Header("Shake")]
    [SerializeField] private float defaultShakeIntensity = 0.15f;

    private Transform target;
    private float shakeTimer;
    private float shakeIntensity;
    private Vector3 shakeOffset;

    public void Init(Transform characterTransform)
    {
        target = characterTransform;
        transform.position = target.position + offset;
        transform.rotation = Quaternion.Euler(30f, 0f, 0f);

        var cam = GetComponent<Camera>();
        if (cam != null)
        {
            cam.orthographic = false;
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 80f;
        }
    }

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 desired = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desired, followSmooth * Time.deltaTime);

        // Shake
        if (shakeTimer > 0)
        {
            shakeTimer -= Time.deltaTime;
            shakeOffset = Random.insideUnitSphere * shakeIntensity * (shakeTimer > 0 ? 1f : 0f);
            transform.position += shakeOffset;
        }
    }

    public void Shake(float intensity = -1f, float duration = 0.2f)
    {
        shakeIntensity = intensity < 0 ? defaultShakeIntensity : intensity;
        shakeTimer = duration;
    }
}
