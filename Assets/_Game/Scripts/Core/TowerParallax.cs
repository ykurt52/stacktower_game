using UnityEngine;

/// <summary>
/// Multi-layer parallax background that scrolls with the camera.
/// Each layer moves at a different speed for depth illusion.
/// Layers tile vertically as the camera moves up.
/// Zone-based color transitions.
/// </summary>
public class TowerParallax : MonoBehaviour
{
    private Camera cam;
    private GameObject[] layers;
    private float[] layerSpeeds;
    private float[] layerDepths;
    private Renderer[][] layerRenderers;
    private float tileHeight = 30f;

    // Zone color sets: [layer0 far, layer1 mid, layer2 near]
    private static readonly float[] zoneHeights = { 0, 15, 35, 55, 80, 120 };
    private static readonly Color[][] zoneColors =
    {
        // Lobby: soft green cityscape
        new[] { new Color(0.25f, 0.4f, 0.55f), new Color(0.2f, 0.35f, 0.25f), new Color(0.15f, 0.25f, 0.18f) },
        // Office: blue city
        new[] { new Color(0.2f, 0.35f, 0.6f), new Color(0.15f, 0.25f, 0.45f), new Color(0.1f, 0.18f, 0.35f) },
        // Construction: warm sunset
        new[] { new Color(0.55f, 0.35f, 0.2f), new Color(0.4f, 0.22f, 0.12f), new Color(0.3f, 0.15f, 0.08f) },
        // Penthouse: purple dusk
        new[] { new Color(0.3f, 0.18f, 0.4f), new Color(0.22f, 0.1f, 0.3f), new Color(0.15f, 0.08f, 0.22f) },
        // Rooftop: dark night
        new[] { new Color(0.12f, 0.1f, 0.2f), new Color(0.08f, 0.06f, 0.15f), new Color(0.05f, 0.04f, 0.1f) },
        // Sky: deep space
        new[] { new Color(0.05f, 0.04f, 0.12f), new Color(0.03f, 0.02f, 0.08f), new Color(0.02f, 0.01f, 0.05f) },
    };

    public void Init()
    {
        cam = Camera.main;
        if (cam == null) return;

        // 3 layers at different Z depths
        layerDepths = new[] { 25f, 18f, 12f };
        layerSpeeds = new[] { 0.1f, 0.25f, 0.45f };

        layers = new GameObject[3];
        layerRenderers = new Renderer[3][];

        for (int i = 0; i < 3; i++)
        {
            layers[i] = new GameObject("ParallaxLayer" + i);
            layers[i].transform.SetParent(transform, false);

            // Each layer: 3 tiles stacked vertically for seamless scrolling
            layerRenderers[i] = new Renderer[3];
            for (int t = 0; t < 3; t++)
            {
                var tile = CreateLayerTile(i, t);
                tile.transform.SetParent(layers[i].transform, false);
                layerRenderers[i][t] = tile.GetComponent<Renderer>();
            }
        }

        UpdatePositions();
        UpdateColors(0);
    }

    private GameObject CreateLayerTile(int layer, int tileIndex)
    {
        var tile = new GameObject("Tile" + tileIndex);

        // Far layers: simple building silhouettes
        // Near layers: darker, taller shapes
        float width = 30f;
        float depth = 0.1f;

        // Create a group of building-like rectangles
        int buildingCount = 4 + layer * 2;
        float spacing = width / buildingCount;

        for (int b = 0; b < buildingCount; b++)
        {
            var building = GameObject.CreatePrimitive(PrimitiveType.Cube);
            building.name = "Building";
            building.transform.SetParent(tile.transform, false);

            float bx = -width / 2f + spacing * b + spacing * 0.5f;
            float bHeight = Random.Range(2f, 6f + layer * 3f);
            float bWidth = spacing * Random.Range(0.5f, 0.9f);

            building.transform.localPosition = new Vector3(bx, bHeight * 0.5f - tileHeight * 0.5f + tileIndex * tileHeight, 0);
            building.transform.localScale = new Vector3(bWidth, bHeight, depth);

            var col = building.GetComponent<Collider>();
            if (col != null) { col.enabled = false; Destroy(col); }

            var rend = building.GetComponent<Renderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            // Slight variation per building
            float v = Random.Range(-0.03f, 0.03f);
            mat.color = new Color(0.15f + v, 0.15f + v, 0.2f + v);
            rend.material = mat;
        }

        // Add some window dots on near layers
        if (layer >= 1)
        {
            int windowCount = 6 + layer * 4;
            for (int w = 0; w < windowCount; w++)
            {
                var window = GameObject.CreatePrimitive(PrimitiveType.Cube);
                window.name = "Window";
                window.transform.SetParent(tile.transform, false);
                float wx = Random.Range(-width * 0.45f, width * 0.45f);
                float wy = Random.Range(-tileHeight * 0.4f, tileHeight * 0.4f) + tileIndex * tileHeight;
                window.transform.localPosition = new Vector3(wx, wy, -0.06f);
                window.transform.localScale = new Vector3(0.15f, 0.2f, 0.01f);
                var wc = window.GetComponent<Collider>();
                if (wc != null) { wc.enabled = false; Destroy(wc); }
                var wr = window.GetComponent<Renderer>();
                var wm = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                float brightness = Random.Range(0.4f, 0.8f);
                wm.color = new Color(brightness, brightness * 0.9f, brightness * 0.5f);
                wr.material = wm;
            }
        }

        return tile;
    }

    public void UpdateParallax(float cameraY)
    {
        if (layers == null) return;
        UpdatePositions();

        // Zone color transition
        UpdateColors(cameraY);
    }

    private void UpdatePositions()
    {
        if (cam == null || layers == null) return;

        float camY = cam.transform.position.y;

        for (int i = 0; i < layers.Length; i++)
        {
            float parallaxY = camY * layerSpeeds[i];

            // Tile wrapping: keep tiles centered around camera view
            float offset = parallaxY % tileHeight;

            layers[i].transform.position = new Vector3(
                cam.transform.position.x,
                camY - offset,
                layerDepths[i]
            );
        }
    }

    private void UpdateColors(float height)
    {
        if (layerRenderers == null) return;

        // Find zone blend
        int zoneA = 0;
        float t = 0;
        for (int i = 0; i < zoneHeights.Length - 1; i++)
        {
            if (height <= zoneHeights[i + 1] || i == zoneHeights.Length - 2)
            {
                zoneA = i;
                t = Mathf.InverseLerp(zoneHeights[i], zoneHeights[i + 1], height);
                break;
            }
        }

        int zoneB = Mathf.Min(zoneA + 1, zoneColors.Length - 1);

        for (int layer = 0; layer < 3; layer++)
        {
            Color baseColor = Color.Lerp(zoneColors[zoneA][layer], zoneColors[zoneB][layer], t);

            if (layerRenderers[layer] == null) continue;
            foreach (var rend in layerRenderers[layer])
            {
                if (rend == null) continue;
                // Update all children building renderers
                foreach (var childRend in rend.GetComponentsInChildren<Renderer>())
                {
                    if (childRend.name == "Window") continue;
                    if (childRend.material != null)
                        childRend.material.color = baseColor + new Color(
                            Random.Range(-0.01f, 0.01f),
                            Random.Range(-0.01f, 0.01f),
                            Random.Range(-0.01f, 0.01f));
                }
            }
        }
    }

    public void Cleanup()
    {
        if (layers != null)
        {
            foreach (var layer in layers)
                if (layer != null) Destroy(layer);
        }
        layers = null;
        layerRenderers = null;
    }
}
