using UnityEngine;

/// <summary>
/// Platform in the tower. Can be static, moving, breakable, or icy.
/// </summary>
public class TowerPlatform : MonoBehaviour
{
    public enum PlatformType { Normal, Moving, Breakable, Icy }

    private PlatformType type;
    private float moveSpeed;
    private float moveLeft;
    private float moveRight;
    private int moveDir = 1;
    private float breakTimer;
    private bool breaking;

    public bool Visited { get; private set; }
    public int Floor { get; private set; }
    public PlatformType Type => type;
    public bool IsIcy => type == PlatformType.Icy;

    public void SetFloor(int floor) { Floor = floor; }

    public void Setup(PlatformType platformType, float speed = 0, float left = 0, float right = 0)
    {
        type = platformType;
        moveSpeed = speed;
        moveLeft = left;
        moveRight = right;
        moveDir = Random.value > 0.5f ? 1 : -1;
    }

    public void MarkVisited()
    {
        Visited = true;

        if (type == PlatformType.Breakable && !breaking)
        {
            breaking = true;
            breakTimer = 0.4f;
        }
    }

    private void Update()
    {
        if (type == PlatformType.Moving)
        {
            Vector3 pos = transform.position;
            pos.x += moveSpeed * moveDir * Time.deltaTime;

            if (pos.x >= moveRight) { pos.x = moveRight; moveDir = -1; }
            else if (pos.x <= moveLeft) { pos.x = moveLeft; moveDir = 1; }

            transform.position = pos;
        }

        if (breaking)
        {
            breakTimer -= Time.deltaTime;

            // Shake effect
            float shake = Random.Range(-0.03f, 0.03f);
            Vector3 pos = transform.position;
            pos.x += shake;
            transform.position = pos;

            if (breakTimer <= 0)
            {
                // Fall and destroy
                Rigidbody rb = gameObject.AddComponent<Rigidbody>();
                rb.mass = 2f;
                Destroy(gameObject, 1.5f);
                enabled = false;
            }
        }
    }
}
