using UnityEngine;

/// <summary>
/// Smoothly follows the staircase upward and forward from behind.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private float smoothSpeed = 5.0f;
    [SerializeField] private float yOffset = 3.0f;
    [SerializeField] private float zOffset = -5.0f;
    [SerializeField] private BlockSpawner blockSpawner;

    private void LateUpdate()
    {
        if (blockSpawner == null) return;

        float targetY = blockSpawner.GetStackTopY() + yOffset;
        float targetZ = blockSpawner.GetStackTopZ() + zOffset;

        Vector3 pos = transform.position;
        pos.y = Mathf.Lerp(pos.y, targetY, smoothSpeed * Time.deltaTime);
        pos.z = Mathf.Lerp(pos.z, targetZ, smoothSpeed * Time.deltaTime);
        transform.position = pos;
    }
}
