using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles staircase block spawning. Blocks are placed as steps (forward + up).
/// Character walks forward; player must place blocks to build stairs.
/// </summary>
public class BlockSpawner : MonoBehaviour
{
    [Header("Block Settings")]
    [SerializeField] private float blockHeight = 0.25f;
    [SerializeField] private float blockDepth = 1.5f;
    [SerializeField] private float initialMoveSpeed = 2.0f;
    [SerializeField] private float boundsLimit = 3.5f;
    [SerializeField] private float speedIncrement = 0.07f;
    [SerializeField] private float maxMoveSpeed = 6.0f;
    [SerializeField] private float perfectThreshold = 0.05f;

    [Header("Staircase")]
    [SerializeField] private float stepForwardOffset = 1.4f;

    [Header("References")]
    [SerializeField] private Transform baseBlock;

    [Header("Effects")]
    [SerializeField] private GameObject placeEffectPrefab;
    [SerializeField] private GameObject perfectEffectPrefab;

    [Header("Color Palette")]
    [SerializeField] private Color[] palette = new Color[]
    {
        HexColor("#F75C5C"), // Coral
        HexColor("#5CB8F7"), // Sky
        HexColor("#5CF78E"), // Mint
        HexColor("#F7D95C"), // Amber
        HexColor("#B85CF7"), // Lavender
        HexColor("#F7A55C"), // Peach
    };

    private List<Transform> stack = new List<Transform>();
    private Block currentMovingBlock;
    private float currentSpeed;
    private int colorIndex;
    private int moveDirection = 1;
    private bool isActive;
    private StackCharacter character;

    private void Start()
    {
        GameManager.Instance.OnGameStart.AddListener(OnGameStart);
        GameManager.Instance.OnGameOver.AddListener(OnGameOver);
        GameManager.Instance.OnReturnToMenu.AddListener(OnReturnToMenu);
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStart.RemoveListener(OnGameStart);
            GameManager.Instance.OnGameOver.RemoveListener(OnGameOver);
            GameManager.Instance.OnReturnToMenu.RemoveListener(OnReturnToMenu);
        }
    }

    private void Update()
    {
        if (!isActive) return;

        // Character fell off — game over
        if (character != null && character.IsFalling)
        {
            isActive = false;
            GameManager.Instance.TriggerGameOver();
            return;
        }

        if (currentMovingBlock == null) return;

        bool tapped = (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                      || (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame);
        if (tapped)
        {
            PlaceCurrentBlock();
        }
    }

    /// <summary>
    /// Resets everything and starts the game.
    /// </summary>
    private void OnGameStart()
    {
        ClearStack();
        currentSpeed = initialMoveSpeed;
        colorIndex = 0;
        moveDirection = 1;
        isActive = true;

        if (baseBlock != null)
        {
            stack.Add(baseBlock);
        }

        SpawnCharacter();
        SpawnNextBlock();
    }

    private void OnGameOver()
    {
        if (character != null)
            character.Stop();
        isActive = false;
    }

    private void OnReturnToMenu()
    {
        isActive = false;
        ClearStack();
        DestroyCharacter();
    }

    private void SpawnCharacter()
    {
        DestroyCharacter();
        GameObject charObj = new GameObject("StackCharacter");
        character = charObj.AddComponent<StackCharacter>();
        character.SpawnOnBlock(stack[stack.Count - 1]);
    }

    private void DestroyCharacter()
    {
        if (character != null)
        {
            Destroy(character.gameObject);
            character = null;
        }
    }

    /// <summary>
    /// Places the current block and checks alignment.
    /// </summary>
    private void PlaceCurrentBlock()
    {
        if (currentMovingBlock == null || stack.Count == 0) return;

        Transform previousBlock = stack[stack.Count - 1];
        float remainingWidth = currentMovingBlock.Place(previousBlock, perfectThreshold);

        if (remainingWidth <= 0f)
        {
            GameManager.Instance.TriggerGameOver();
            return;
        }

        if (remainingWidth < GameManager.Instance.MinBlockWidth)
        {
            GameManager.Instance.TriggerGameOver();
            return;
        }

        bool isPerfect = Mathf.Abs(remainingWidth - currentMovingBlock.transform.localScale.x) < 0.001f
                         && Mathf.Abs(currentMovingBlock.transform.position.x - previousBlock.position.x) < perfectThreshold;

        stack.Add(currentMovingBlock.transform);

        // Tell character about the new step
        if (character != null)
        {
            character.OnBlockPlaced(currentMovingBlock.transform);
        }

        if (ScoreManager.Instance != null)
            ScoreManager.Instance.AddPoint();

        if (AudioManager.Instance != null)
        {
            if (isPerfect)
                AudioManager.Instance.PlayPerfect();
            else
                AudioManager.Instance.PlayPlace();
        }

        SpawnPlaceEffect(currentMovingBlock.transform.position, isPerfect);

        currentSpeed = Mathf.Min(currentSpeed + speedIncrement, maxMoveSpeed);
        moveDirection *= -1;

        SpawnNextBlock();
    }

    /// <summary>
    /// Spawns the next block as a stair step: forward in Z, up in Y, oscillating on X.
    /// </summary>
    private void SpawnNextBlock()
    {
        Transform topBlock = stack[stack.Count - 1];

        float spawnY = topBlock.position.y + blockHeight;
        float spawnZ = topBlock.position.z + stepForwardOffset;
        float startX = boundsLimit * moveDirection;

        GameObject blockObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blockObj.transform.position = new Vector3(startX, spawnY, spawnZ);
        blockObj.transform.localScale = new Vector3(topBlock.localScale.x, blockHeight, blockDepth);

        // Apply color
        Renderer rend = blockObj.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetFloat("_Smoothness", 0.7f);
        mat.SetFloat("_Metallic", 0.1f);
        Color blockColor = palette[colorIndex % palette.Length];
        mat.color = blockColor;
        mat.SetColor("_EmissionColor", blockColor * 0.15f);
        mat.EnableKeyword("_EMISSION");
        rend.material = mat;
        colorIndex++;

        Block block = blockObj.AddComponent<Block>();
        block.Initialize(currentSpeed, boundsLimit, moveDirection);

        Rigidbody rb = blockObj.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        blockObj.AddComponent<OutlineEffect>();

        currentMovingBlock = block;
    }

    /// <summary>
    /// Destroys all spawned blocks except base.
    /// </summary>
    private void ClearStack()
    {
        for (int i = stack.Count - 1; i >= 0; i--)
        {
            if (stack[i] != null && stack[i] != baseBlock)
                Destroy(stack[i].gameObject);
        }
        stack.Clear();

        if (currentMovingBlock != null)
        {
            Destroy(currentMovingBlock.gameObject);
            currentMovingBlock = null;
        }
    }

    /// <summary>
    /// Returns the top of the stack for camera following.
    /// </summary>
    public float GetStackTopY()
    {
        if (stack.Count == 0) return 0f;
        return stack[stack.Count - 1].position.y;
    }

    /// <summary>
    /// Returns the Z position of the top block for camera following.
    /// </summary>
    public float GetStackTopZ()
    {
        if (stack.Count == 0) return 0f;
        return stack[stack.Count - 1].position.z;
    }

    private void SpawnPlaceEffect(Vector3 position, bool isPerfect)
    {
        GameObject prefab = isPerfect ? perfectEffectPrefab : placeEffectPrefab;
        if (prefab == null) return;

        GameObject fx = Instantiate(prefab, position, Quaternion.identity);
        foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>())
        {
            var main = ps.main;
            main.simulationSpeed = 3f;
            main.startLifetimeMultiplier = 0.4f;
            var col = main.startColor;
            Color c = col.color;
            c.a *= 0.5f;
            main.startColor = new ParticleSystem.MinMaxGradient(c);
        }
        Destroy(fx, 1f);
    }

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color color);
        return color;
    }
}
