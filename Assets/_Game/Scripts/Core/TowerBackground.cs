using UnityEngine;

/// <summary>
/// Procedural parallax background with building silhouettes and clouds.
/// Layers scroll at different speeds relative to camera.
/// </summary>
public class TowerBackground : MonoBehaviour
{
    [SerializeField] private int buildingCount = 20;
    [SerializeField] private int cloudCount = 8;
    [SerializeField] private float buildingParallax = 0.3f;
    [SerializeField] private float cloudParallax = 0.15f;

    private Transform[] buildings;
    private Transform[] clouds;
    private float[] buildingBaseY;
    private float[] cloudBaseY;
    private float lastCameraY;
    private Camera cam;
    private bool initialized;

    private void Start()
    {
        // Only create visuals at runtime
        if (!Application.isPlaying) return;

        cam = Camera.main;
        if (cam == null) return;
        lastCameraY = cam.transform.position.y;

        CreateBuildings();
        CreateClouds();
        initialized = true;
    }

    private void LateUpdate()
    {
        if (!initialized || cam == null) return;

        float cameraY = cam.transform.position.y;

        // Buildings parallax
        if (buildings != null)
        {
            for (int i = 0; i < buildings.Length; i++)
            {
                if (buildings[i] == null) continue;
                Vector3 p = buildings[i].position;
                p.y = buildingBaseY[i] + cameraY * buildingParallax;
                buildings[i].position = p;
            }
        }

        // Clouds parallax + drift
        if (clouds != null)
        {
            for (int i = 0; i < clouds.Length; i++)
            {
                if (clouds[i] == null) continue;
                Vector3 p = clouds[i].position;
                p.y = cloudBaseY[i] + cameraY * cloudParallax;
                p.x += 0.3f * Time.deltaTime * (i % 2 == 0 ? 1 : -1);
                clouds[i].position = p;
            }
        }
    }

    private static void SafeDestroy(Object obj)
    {
        if (obj == null) return;
#if UNITY_EDITOR
        if (!Application.isPlaying)
            DestroyImmediate(obj);
        else
#endif
            Destroy(obj);
    }

    private void CreateBuildings()
    {
        buildings = new Transform[buildingCount];
        buildingBaseY = new float[buildingCount];

        Color[] buildingColors = new Color[]
        {
            new Color(0.15f, 0.18f, 0.25f, 0.6f),
            new Color(0.2f, 0.22f, 0.3f, 0.5f),
            new Color(0.12f, 0.15f, 0.22f, 0.7f),
        };

        for (int i = 0; i < buildingCount; i++)
        {
            float w = Random.Range(1.5f, 4f);
            float h = Random.Range(5f, 20f);
            float x = Random.Range(-15f, 15f);
            float z = Random.Range(5f, 15f);
            float y = h * 0.5f - 3f + i * 4f;

            GameObject bld = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bld.name = "Building";
            bld.transform.SetParent(transform, false);
            bld.transform.position = new Vector3(x, y, z);
            bld.transform.localScale = new Vector3(w, h, w * 0.8f);
            var bc = bld.GetComponent<Collider>(); if (bc != null) { bc.enabled = false; SafeDestroy(bc); }

            Color c = buildingColors[i % buildingColors.Length];
            var rend = bld.GetComponent<Renderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = c;
            rend.material = mat;

            int windowRows = Mathf.FloorToInt(h / 1.5f);
            for (int row = 0; row < windowRows; row++)
            {
                for (int col = -1; col <= 1; col++)
                {
                    if (Random.value < 0.4f) continue;
                    GameObject win = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    win.name = "Window";
                    win.transform.SetParent(bld.transform, false);
                    win.transform.localPosition = new Vector3(col * 0.2f, -0.4f + row * (1f / windowRows), -0.51f);
                    win.transform.localScale = new Vector3(0.08f, 0.04f, 0.01f);
                    var wc = win.GetComponent<Collider>(); if (wc != null) { wc.enabled = false; SafeDestroy(wc); }
                    var wRend = win.GetComponent<Renderer>();
                    var wMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    wMat.color = new Color(1f, 0.95f, 0.6f, 0.8f);
                    wRend.material = wMat;
                }
            }

            buildings[i] = bld.transform;
            buildingBaseY[i] = y;
        }
    }

    private void CreateClouds()
    {
        clouds = new Transform[cloudCount];
        cloudBaseY = new float[cloudCount];

        for (int i = 0; i < cloudCount; i++)
        {
            float x = Random.Range(-12f, 12f);
            float y = Random.Range(5f, 40f) + i * 8f;
            float z = Random.Range(3f, 8f);

            GameObject cloud = new GameObject("Cloud");
            cloud.transform.SetParent(transform, false);
            cloud.transform.position = new Vector3(x, y, z);

            int puffs = Random.Range(3, 6);
            for (int p = 0; p < puffs; p++)
            {
                var puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                puff.name = "Puff";
                puff.transform.SetParent(cloud.transform, false);
                puff.transform.localPosition = new Vector3(
                    Random.Range(-1f, 1f),
                    Random.Range(-0.2f, 0.3f),
                    Random.Range(-0.3f, 0.3f));
                float s = Random.Range(0.8f, 1.8f);
                puff.transform.localScale = new Vector3(s, s * 0.6f, s * 0.8f);
                var pc = puff.GetComponent<Collider>(); if (pc != null) { pc.enabled = false; SafeDestroy(pc); }

                var rend = puff.GetComponent<Renderer>();
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = new Color(1f, 1f, 1f, 0.4f);
                rend.material = mat;
            }

            clouds[i] = cloud.transform;
            cloudBaseY[i] = y;
        }
    }

    public void Cleanup()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            SafeDestroy(transform.GetChild(i).gameObject);
    }
}
