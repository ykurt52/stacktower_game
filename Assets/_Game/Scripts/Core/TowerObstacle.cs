using UnityEngine;

/// <summary>
/// Static hazard on a platform. Touching kills the character.
/// Creates a procedural spike visual.
/// </summary>
public class TowerObstacle : MonoBehaviour
{
    private int damage = 2;

    public int Damage => damage;

    public void Init()
    {
        // Kinematic rigidbody for trigger detection
        Rigidbody rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        // Trigger collider for character detection
        BoxCollider col = gameObject.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(0.4f, 0.6f, 0.4f);
        col.center = new Vector3(0, 0.3f, 0);

        CreateVisual();
    }

    private void CreateVisual()
    {
        // Try Synty metal barrel (looks hazardous)
        if (SyntyAssets.Instance != null && SyntyAssets.Instance.BarrelMetalPrefab != null)
        {
            SyntyAssets.Spawn(SyntyAssets.Instance.BarrelMetalPrefab, transform,
                new Vector3(0, 0, 0), 0.45f, 0f);
            return;
        }

        // Fallback: procedural
        Color spikeColor = new Color(0.5f, 0.05f, 0.15f);
        Color baseColor = new Color(0.35f, 0.04f, 0.1f);

        var basePart = CreatePart("Base", PrimitiveType.Cube,
            new Vector3(0, 0.05f, 0), new Vector3(0.3f, 0.1f, 0.3f), baseColor);

        var spike = CreatePart("Spike", PrimitiveType.Cube,
            new Vector3(0, 0.35f, 0), new Vector3(0.2f, 0.35f, 0.2f), spikeColor);
        spike.transform.localRotation = Quaternion.Euler(0, 45f, 0);

        var topSpike = CreatePart("TopSpike", PrimitiveType.Sphere,
            new Vector3(0, 0.55f, 0), new Vector3(0.08f, 0.15f, 0.08f), spikeColor);
    }

    private GameObject CreatePart(string name, PrimitiveType type, Vector3 pos, Vector3 scale, Color color)
    {
        var obj = GameObject.CreatePrimitive(type);
        obj.name = name;
        obj.transform.SetParent(transform, false);
        obj.transform.localPosition = pos;
        obj.transform.localScale = scale;
        var partCol = obj.GetComponent<Collider>();
        if (partCol != null) { partCol.enabled = false; Destroy(partCol); }

        var rend = obj.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        rend.material = mat;

        return obj;
    }
}
