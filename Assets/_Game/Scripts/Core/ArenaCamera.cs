using UnityEngine;

/// <summary>
/// Archero-style arena camera. Portrait orientation.
/// Locked to arena center on X axis; follows player only on Z with gentle smoothing.
/// </summary>
public class ArenaCamera : MonoBehaviour
{
    [Header("Follow")]
    [SerializeField] private float followSmooth = 4f;
    [SerializeField] private float zFollowStrength = 0.35f;

    [Header("Shake")]
    [SerializeField] private float defaultShakeIntensity = 0.15f;

    private const float CAMERA_PITCH = 50f;
    private const float CAMERA_HEIGHT = 12f;
    private const float CAMERA_Z_OFFSET = -9f;
    private const float ARENA_VIEW_MARGIN = 0.5f;
    private const float MAX_FOV = 60f;

    private Transform _target;
    private float _arenaCenterX;
    private float _shakeTimer;
    private float _shakeIntensity;

    public void Init(Transform characterTransform, float arenaFullWidth)
    {
        _target = characterTransform;
        _arenaCenterX = 0f;

        Vector3 startPos = new Vector3(
            _arenaCenterX,
            CAMERA_HEIGHT,
            _target.position.z + CAMERA_Z_OFFSET
        );
        transform.position = startPos;
        transform.rotation = Quaternion.Euler(CAMERA_PITCH, 0f, 0f);

        var cam = GetComponent<Camera>();
        if (cam != null)
        {
            cam.orthographic = false;
            cam.fieldOfView = CalculateFOV(cam, arenaFullWidth + ARENA_VIEW_MARGIN * 2f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 80f;
        }
    }

    private float CalculateFOV(Camera cam, float targetWidth)
    {
        float distToGround = CAMERA_HEIGHT / Mathf.Sin(CAMERA_PITCH * Mathf.Deg2Rad);
        float halfHFovRad = Mathf.Atan(targetWidth * 0.5f / distToGround);
        float aspect = (float)Screen.width / Screen.height;
        float vFovRad = 2f * Mathf.Atan(Mathf.Tan(halfHFovRad) / aspect);
        return Mathf.Min(vFovRad * Mathf.Rad2Deg, MAX_FOV);
    }

    private void LateUpdate()
    {
        if (_target == null) return;

        Vector3 desired = new Vector3(
            _target.position.x,
            CAMERA_HEIGHT,
            _target.position.z + CAMERA_Z_OFFSET
        );
        transform.position = Vector3.Lerp(transform.position, desired, followSmooth * Time.deltaTime);

        // Shake
        if (_shakeTimer > 0)
        {
            _shakeTimer -= Time.deltaTime;
            Vector3 shakeOffset = Random.insideUnitSphere * _shakeIntensity * (_shakeTimer > 0 ? 1f : 0f);
            transform.position += shakeOffset;
        }
    }

    public void Shake(float intensity = -1f, float duration = 0.2f)
    {
        _shakeIntensity = intensity < 0 ? defaultShakeIntensity : intensity;
        _shakeTimer = duration;
    }
}
