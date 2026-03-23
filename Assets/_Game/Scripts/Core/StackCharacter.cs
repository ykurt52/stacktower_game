using UnityEngine;

/// <summary>
/// Character that walks forward up a staircase of blocks.
/// Walks toward the next placed block, climbs on top, waits for the next one.
/// </summary>
public class StackCharacter : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 1.8f;
    [SerializeField] private float fallSpeed = 10f;
    [SerializeField] private float edgeWaitMargin = 0.15f;

    [Header("Appearance")]
    [SerializeField] private float bodyHeight = 0.3f;
    [SerializeField] private float bodyRadius = 0.12f;

    private Transform currentBlock;
    private Transform targetBlock;
    private Vector3 targetPos;
    private bool isWalking;
    private bool isFalling;
    private bool isActive;
    private bool waitingForBlock;

    private GameObject bodyObj;
    private GameObject headObj;
    private GameObject leftLeg;
    private GameObject rightLeg;
    private float legTimer;

    public bool IsFalling => isFalling;

    /// <summary>
    /// Spawns character on the given block.
    /// </summary>
    public void SpawnOnBlock(Transform block)
    {
        currentBlock = block;
        isActive = true;
        isFalling = false;
        isWalking = false;
        waitingForBlock = true;

        float y = block.position.y + block.localScale.y / 2f;
        transform.position = new Vector3(block.position.x, y, block.position.z);
        transform.rotation = Quaternion.LookRotation(Vector3.forward);

        CreateBody();
    }

    /// <summary>
    /// Called when a new stair block is placed. Character starts walking to it.
    /// </summary>
    public void OnBlockPlaced(Transform placedBlock)
    {
        if (isFalling) return;

        targetBlock = placedBlock;

        // Target position: center of the new block, on top
        float y = placedBlock.position.y + placedBlock.localScale.y / 2f;
        targetPos = new Vector3(placedBlock.position.x, y, placedBlock.position.z);

        waitingForBlock = false;
        isWalking = true;
    }

    public void Stop()
    {
        isActive = false;
        isWalking = false;
    }

    public void Hide()
    {
        isActive = false;
        isWalking = false;
        isFalling = false;
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!isActive) return;

        if (isFalling)
        {
            transform.position += Vector3.down * fallSpeed * Time.deltaTime;
            transform.Rotate(Vector3.right * 200f * Time.deltaTime);
            return;
        }

        if (waitingForBlock)
        {
            // Idle — wait at current position, slight bounce
            AnimateIdle();
            return;
        }

        if (!isWalking || targetBlock == null) return;

        // Move toward target position
        Vector3 pos = transform.position;
        Vector3 dir = targetPos - pos;
        float distance = dir.magnitude;

        if (distance < 0.05f)
        {
            // Arrived at target block
            transform.position = targetPos;
            currentBlock = targetBlock;
            targetBlock = null;
            isWalking = false;
            waitingForBlock = true;
            return;
        }

        // Move toward target
        Vector3 moveDir = dir.normalized;
        float step = walkSpeed * Time.deltaTime;
        transform.position = Vector3.MoveTowards(pos, targetPos, step);

        // Face movement direction (only horizontal)
        Vector3 lookDir = new Vector3(moveDir.x, 0, moveDir.z);
        if (lookDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(lookDir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 10f * Time.deltaTime);
        }

        AnimateLegs();
    }

    private void AnimateIdle()
    {
        if (bodyObj == null) return;
        float bounce = Mathf.Sin(Time.time * 3f) * 0.01f;
        bodyObj.transform.localPosition = new Vector3(0, bodyHeight / 2f + bounce, 0);
    }

    private void CreateBody()
    {
        Color bodyColor = Color.white;
        Color headColor = new Color(1f, 0.85f, 0.7f);

        bodyObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bodyObj.name = "Body";
        bodyObj.transform.SetParent(transform, false);
        bodyObj.transform.localPosition = new Vector3(0, bodyHeight / 2f, 0);
        bodyObj.transform.localScale = new Vector3(bodyRadius * 2f, bodyHeight, bodyRadius * 1.5f);
        Destroy(bodyObj.GetComponent<Collider>());
        SetMaterial(bodyObj, bodyColor, 0.2f);
        bodyObj.AddComponent<OutlineEffect>();

        headObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        headObj.name = "Head";
        headObj.transform.SetParent(transform, false);
        headObj.transform.localPosition = new Vector3(0, bodyHeight + bodyRadius * 0.8f, 0);
        headObj.transform.localScale = Vector3.one * bodyRadius * 1.8f;
        Destroy(headObj.GetComponent<Collider>());
        SetMaterial(headObj, headColor, 0.3f);
        headObj.AddComponent<OutlineEffect>();

        leftLeg = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leftLeg.name = "LeftLeg";
        leftLeg.transform.SetParent(transform, false);
        leftLeg.transform.localPosition = new Vector3(-bodyRadius * 0.5f, bodyHeight * 0.15f, 0);
        leftLeg.transform.localScale = new Vector3(bodyRadius * 0.6f, bodyHeight * 0.35f, bodyRadius * 0.6f);
        Destroy(leftLeg.GetComponent<Collider>());
        SetMaterial(leftLeg, bodyColor * 0.8f, 0.1f);

        rightLeg = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rightLeg.name = "RightLeg";
        rightLeg.transform.SetParent(transform, false);
        rightLeg.transform.localPosition = new Vector3(bodyRadius * 0.5f, bodyHeight * 0.15f, 0);
        rightLeg.transform.localScale = new Vector3(bodyRadius * 0.6f, bodyHeight * 0.35f, bodyRadius * 0.6f);
        Destroy(rightLeg.GetComponent<Collider>());
        SetMaterial(rightLeg, bodyColor * 0.8f, 0.1f);
    }

    private void AnimateLegs()
    {
        if (leftLeg == null || rightLeg == null) return;
        legTimer += Time.deltaTime * walkSpeed * 12f;
        float swing = Mathf.Sin(legTimer) * 0.04f;

        Vector3 lPos = leftLeg.transform.localPosition;
        lPos.z = swing;
        leftLeg.transform.localPosition = lPos;

        Vector3 rPos = rightLeg.transform.localPosition;
        rPos.z = -swing;
        rightLeg.transform.localPosition = rPos;
    }

    private void SetMaterial(GameObject obj, Color color, float emission)
    {
        Renderer rend = obj.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        mat.SetFloat("_Smoothness", 0.6f);
        if (emission > 0)
        {
            mat.SetColor("_EmissionColor", color * emission);
            mat.EnableKeyword("_EMISSION");
        }
        rend.material = mat;
    }
}
