using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Spawns a low-poly city environment using imported asset prefabs.
/// Buildings, trees, street props line both sides of the staircase path.
/// Repositions as the camera moves forward.
/// </summary>
public class CityBackground : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] private float sideMinDistance = 25f;
    [SerializeField] private float sideMaxDistance = 60f;
    [SerializeField] private float chunkLength = 80f;
    [SerializeField] private float despawnBehind = 50f;

    [Header("Density")]
    [SerializeField] private int buildingsPerChunk = 8;
    [SerializeField] private int treesPerChunk = 10;
    [SerializeField] private int propsPerChunk = 6;

    [Header("Ground")]
    [SerializeField] private Color groundColor = new Color(0.18f, 0.22f, 0.15f);

    // Loaded prefabs
    private GameObject[] buildingPrefabs;
    private GameObject[] treePrefabs;
    private GameObject[] propPrefabs;
    private GameObject[] roadPrefabs;

    private Transform cam;
    private float nextChunkZ;
    private List<GameObject> chunks = new List<GameObject>();
    private GameObject groundObj;

    private void Start()
    {
        cam = Camera.main.transform;
        LoadPrefabs();
        CreateGround();

        // Spawn initial chunks
        nextChunkZ = -chunkLength * 0.5f;
        for (int i = 0; i < 3; i++)
        {
            SpawnChunk(nextChunkZ);
            nextChunkZ += chunkLength;
        }
    }

    private void LateUpdate()
    {
        if (cam == null) return;

        // Move ground with camera
        if (groundObj != null)
        {
            Vector3 gp = groundObj.transform.position;
            gp.z = cam.position.z;
            groundObj.transform.position = gp;
        }

        // Spawn new chunks ahead
        while (nextChunkZ < cam.position.z + chunkLength * 2f)
        {
            SpawnChunk(nextChunkZ);
            nextChunkZ += chunkLength;
        }

        // Despawn old chunks behind camera
        for (int i = chunks.Count - 1; i >= 0; i--)
        {
            if (chunks[i] == null)
            {
                chunks.RemoveAt(i);
                continue;
            }
            if (chunks[i].transform.position.z < cam.position.z - despawnBehind)
            {
                Destroy(chunks[i]);
                chunks.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Loads prefabs from imported asset packages.
    /// </summary>
    private void LoadPrefabs()
    {
        List<GameObject> buildings = new List<GameObject>();
        List<GameObject> trees = new List<GameObject>();
        List<GameObject> props = new List<GameObject>();
        List<GameObject> roads = new List<GameObject>();

        // SimplePoly City buildings
        TryLoadAll(buildings, "SimplePoly City - Low Poly Assets/Prefab/Buildings",
            "Building_House_01_color01", "Building_House_01_color02",
            "Building_House_01_color03", "Building_House_01_color04",
            "Building_Sky_Big_color01", "Building_Sky_Big_color02",
            "Building_Sky_Small_color01", "Building_Sky_Small_color02",
            "Building_Restaurant", "Building_Coffee Shop",
            "Building_Fast Food", "Building_Super Market",
            "Building_Gas Station", "Building_Bar",
            "Building_Auto Service", "Building_Factory");

        // Cartoon City buildings as fallback
        TryLoadAll(buildings, "ithappy/Cartoon_City_Free/Prefabs/Buildings",
            "Eco_Building_Grid", "Regular_Building_TwistedTower_Large");

        // Trees from multiple packs
        TryLoadAll(trees, "BrokenVector/LowPolyTreePack/Prefabs",
            "TreeType0_1", "TreeType0_2", "TreeType1_1", "TreeType2_1",
            "TreeType3_1", "TreeType4_1", "TreeType5_1", "TreeType6_1");

        TryLoadAll(trees, "SimpleNaturePack/Prefabs",
            "Tree_01", "Tree_02", "Tree_03", "Tree_04", "Tree_05",
            "Bush_01", "Bush_02", "Bush_03");

        // Street props
        TryLoadAll(props, "SimplePoly City - Low Poly Assets/Prefab/Props",
            "Street Light", "Bench", "Bus Stop", "Dustbin",
            "Hydrant", "Traffic Control", "Billboard_Small",
            "Traffic Signs", "Traffic Cone", "Fence");

        TryLoadAll(props, "LowpolyStreetPack/Prefabs/StreetProps/TraficLights",
            "LampPost_A", "LampPost_B", "LampPost_C", "LampPost_D");

        TryLoadAll(props, "LowpolyStreetPack/Prefabs/StreetProps/Bench",
            "Bench_A", "Bench_B");

        // Roads
        TryLoadAll(roads, "SimplePoly City - Low Poly Assets/Prefab/Roads",
            "Road_Lane_01", "Road_Lane_02", "Road_Sidewalk");

        TryLoadAll(roads, "LowpolyStreetPack/Prefabs/Roads/Streets",
            "Road_Streight");

        buildingPrefabs = buildings.Count > 0 ? buildings.ToArray() : null;
        treePrefabs = trees.Count > 0 ? trees.ToArray() : null;
        propPrefabs = props.Count > 0 ? props.ToArray() : null;
        roadPrefabs = roads.Count > 0 ? roads.ToArray() : null;

        Debug.Log($"[CityBackground] Loaded: {buildings.Count} buildings, {trees.Count} trees, {props.Count} props, {roads.Count} roads");
    }

    private void TryLoadAll(List<GameObject> list, string folder, params string[] names)
    {
        foreach (string name in names)
        {
            string path = folder + "/" + name;
            GameObject prefab = Resources.Load<GameObject>(path);
            if (prefab != null)
            {
                list.Add(prefab);
                continue;
            }

            // Try loading from AssetDatabase in editor
#if UNITY_EDITOR
            string assetPath = "Assets/" + folder + "/" + name + ".prefab";
            prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab != null)
            {
                list.Add(prefab);
            }
#endif
        }
    }

    /// <summary>
    /// Spawns a chunk of environment at the given Z position.
    /// </summary>
    private void SpawnChunk(float startZ)
    {
        GameObject chunk = new GameObject("Chunk_" + startZ);
        chunk.transform.SetParent(transform);
        chunk.transform.position = new Vector3(0, 0, startZ);
        chunks.Add(chunk);

        // Buildings on both sides
        if (buildingPrefabs != null)
        {
            for (int i = 0; i < buildingsPerChunk; i++)
            {
                float side = (i % 2 == 0) ? -1f : 1f;
                float x = side * Random.Range(sideMinDistance, sideMaxDistance);
                float z = startZ + Random.Range(0, chunkLength);

                GameObject prefab = buildingPrefabs[Random.Range(0, buildingPrefabs.Length)];
                GameObject building = Instantiate(prefab, chunk.transform);
                building.transform.position = new Vector3(x, 0, z);
                building.transform.rotation = Quaternion.Euler(0, Random.Range(0, 4) * 90f, 0);
                float scale = Random.Range(0.8f, 1.5f);
                building.transform.localScale = Vector3.one * scale;
            }
        }

        // Trees between path and buildings
        if (treePrefabs != null)
        {
            for (int i = 0; i < treesPerChunk; i++)
            {
                float side = (i % 2 == 0) ? -1f : 1f;
                float x = side * Random.Range(12f, 22f);
                float z = startZ + Random.Range(0, chunkLength);

                GameObject prefab = treePrefabs[Random.Range(0, treePrefabs.Length)];
                GameObject tree = Instantiate(prefab, chunk.transform);
                tree.transform.position = new Vector3(x, 0, z);
                tree.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
                float scale = Random.Range(0.8f, 1.4f);
                tree.transform.localScale = Vector3.one * scale;
            }
        }

        // Street props — lamps etc. along sidewalk
        if (propPrefabs != null)
        {
            for (int i = 0; i < propsPerChunk; i++)
            {
                float side = (i % 2 == 0) ? -1f : 1f;
                float x = side * Random.Range(8f, 14f);
                float z = startZ + (i * chunkLength / propsPerChunk) + Random.Range(-2f, 2f);

                GameObject prefab = propPrefabs[Random.Range(0, propPrefabs.Length)];
                GameObject prop = Instantiate(prefab, chunk.transform);
                prop.transform.position = new Vector3(x, 0, z);
                prop.transform.rotation = Quaternion.Euler(0, (side > 0 ? -90f : 90f), 0);
                float scale = Random.Range(0.7f, 1f);
                prop.transform.localScale = Vector3.one * scale;
            }
        }

        // Roads on the sides (not on the stair path)
        if (roadPrefabs != null)
        {
            for (float z = startZ; z < startZ + chunkLength; z += 12f)
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    GameObject prefab = roadPrefabs[Random.Range(0, roadPrefabs.Length)];
                    GameObject road = Instantiate(prefab, chunk.transform);
                    road.transform.position = new Vector3(s * 18f, -0.48f, z);
                    road.transform.rotation = Quaternion.Euler(0, 0, 0);
                }
            }
        }
    }

    private void CreateGround()
    {
        groundObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        groundObj.name = "Ground";
        groundObj.transform.SetParent(transform);
        groundObj.transform.position = new Vector3(0, -0.5f, 0);
        groundObj.transform.localScale = new Vector3(200f, 0.5f, 300f);
        Destroy(groundObj.GetComponent<Collider>());

        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = groundColor;
        mat.SetFloat("_Smoothness", 0.2f);
        groundObj.GetComponent<Renderer>().material = mat;
    }
}
