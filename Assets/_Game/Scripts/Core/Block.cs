using UnityEngine;

/// <summary>
/// Self-contained block movement and placement logic. Does not know about GameManager or score.
/// </summary>
public class Block : MonoBehaviour
{
    [SerializeField] private float debrisLifetime = 2f;
    [SerializeField] private float debrisImpulse = -1f;

    private float moveSpeed;
    private float boundsLimit;
    private int moveDirection = 1;
    private bool isMoving;

    /// <summary>
    /// Initializes oscillation parameters.
    /// </summary>
    public void Initialize(float speed, float bounds, int direction)
    {
        moveSpeed = speed;
        boundsLimit = bounds;
        moveDirection = direction;
        isMoving = true;
    }

    private void Update()
    {
        if (!isMoving) return;

        Vector3 pos = transform.position;
        pos.x += moveSpeed * moveDirection * Time.deltaTime;

        if (pos.x >= boundsLimit)
        {
            pos.x = boundsLimit;
            moveDirection = -1;
        }
        else if (pos.x <= -boundsLimit)
        {
            pos.x = -boundsLimit;
            moveDirection = 1;
        }

        transform.position = pos;
    }

    /// <summary>
    /// Places this block relative to the previous block. Returns remaining width, or 0 on full miss.
    /// </summary>
    public float Place(Transform previousBlock, float perfectThreshold)
    {
        isMoving = false;

        float prevX = previousBlock.position.x;
        float prevScaleX = previousBlock.localScale.x;
        float prevLeft = prevX - prevScaleX / 2f;
        float prevRight = prevX + prevScaleX / 2f;

        float curX = transform.position.x;
        float curScaleX = transform.localScale.x;
        float curLeft = curX - curScaleX / 2f;
        float curRight = curX + curScaleX / 2f;

        float overlapLeft = Mathf.Max(prevLeft, curLeft);
        float overlapRight = Mathf.Min(prevRight, curRight);
        float overlapWidth = overlapRight - overlapLeft;

        if (overlapWidth <= 0f)
        {
            SpawnDebris(transform.position, transform.localScale, GetComponent<Renderer>().material);
            Destroy(gameObject);
            return 0f;
        }

        float hangingWidth = curScaleX - overlapWidth;

        if (hangingWidth < perfectThreshold)
        {
            Vector3 pos = transform.position;
            pos.x = prevX;
            transform.position = pos;
            return curScaleX;
        }

        float overlapCenterX = (overlapLeft + overlapRight) / 2f;

        Vector3 newPos = transform.position;
        newPos.x = overlapCenterX;
        transform.position = newPos;

        Vector3 newScale = transform.localScale;
        newScale.x = overlapWidth;
        transform.localScale = newScale;

        float debrisCenterX;
        if (curRight > overlapRight)
            debrisCenterX = (overlapRight + curRight) / 2f;
        else
            debrisCenterX = (curLeft + overlapLeft) / 2f;

        Vector3 debrisPos = new Vector3(debrisCenterX, transform.position.y, transform.position.z);
        Vector3 debrisScale = new Vector3(hangingWidth, transform.localScale.y, transform.localScale.z);
        SpawnDebris(debrisPos, debrisScale, GetComponent<Renderer>().material);

        return overlapWidth;
    }

    /// <summary>
    /// Spawns a falling debris piece.
    /// </summary>
    private void SpawnDebris(Vector3 position, Vector3 scale, Material mat)
    {
        GameObject debris = GameObject.CreatePrimitive(PrimitiveType.Cube);
        debris.transform.position = position;
        debris.transform.localScale = scale;

        Renderer rend = debris.GetComponent<Renderer>();
        rend.material = mat;

        debris.AddComponent<OutlineEffect>();

        Rigidbody rb = debris.AddComponent<Rigidbody>();
        rb.AddForce(Vector3.down * Mathf.Abs(debrisImpulse), ForceMode.Impulse);

        Destroy(debris, debrisLifetime);
    }
}
