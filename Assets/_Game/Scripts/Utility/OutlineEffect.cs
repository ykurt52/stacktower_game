using UnityEngine;

/// <summary>
/// Adds a comic-style dark outline to an object using the inverted hull method.
/// </summary>
public class OutlineEffect : MonoBehaviour
{
    [SerializeField] private float outlineWidth = 0.03f;
    [SerializeField] private Color outlineColor = new Color(0.05f, 0.05f, 0.1f, 1f);

    private GameObject outlineObj;
    private static Material outlineMaterial;

    private void Start()
    {
        CreateOutline();
    }

    private void LateUpdate()
    {
        if (outlineObj != null)
        {
            // Follow parent scale changes (for block trimming)
            outlineObj.transform.localScale = Vector3.one * (1f + outlineWidth);
        }
    }

    /// <summary>
    /// Creates the outline mesh as a child object.
    /// </summary>
    private void CreateOutline()
    {
        MeshFilter sourceMF = GetComponent<MeshFilter>();
        if (sourceMF == null || sourceMF.sharedMesh == null) return;

        if (outlineMaterial == null)
        {
            outlineMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            outlineMaterial.color = outlineColor;
            // Render back faces only
            outlineMaterial.SetFloat("_Cull", 1); // 1 = Front face culling (show back faces)
        }

        outlineObj = new GameObject("Outline");
        outlineObj.transform.SetParent(transform, false);
        outlineObj.transform.localPosition = Vector3.zero;
        outlineObj.transform.localRotation = Quaternion.identity;
        outlineObj.transform.localScale = Vector3.one * (1f + outlineWidth);

        MeshFilter mf = outlineObj.AddComponent<MeshFilter>();
        mf.sharedMesh = sourceMF.sharedMesh;

        MeshRenderer mr = outlineObj.AddComponent<MeshRenderer>();
        Material mat = new Material(outlineMaterial);
        mat.color = outlineColor;
        mr.material = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    private void OnDestroy()
    {
        if (outlineObj != null)
            Destroy(outlineObj);
    }
}
