using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Remplacement des modèles Kenney (style cartoon) par des modèles Poly Haven réalistes.
/// Les appels du générateur gardent les chemins Kenney : LoadModel() bascule sur l'équivalent Poly Haven
/// s'il est présent dans Assets/Models/PolyHaven. Ce qui n'a pas d'équivalent reste en Kenney.
/// "" = l'objet n'est plus placé du tout.
/// </summary>
public static partial class EchoesMapGenerator
{
    static readonly Dictionary<string, string> Realistic = new Dictionary<string, string>
    {
        // Chambres
        [KFurniture + "bedSingle"] = "old_bed_frame",
        [KFurniture + "sideTable"] = "painted_wooden_nightstand",
        [KFurniture + "sideTableDrawers"] = "vintage_wooden_drawer_01",
        [KFurniture + "desk"] = "metal_office_desk",
        [KFurniture + "chairDesk"] = "SchoolChair_01",
        [KFurniture + "bookcaseClosedDoors"] = "painted_wooden_cabinet",
        [KFurniture + "rugRectangle"] = "",
        [KFurniture + "trashcan"] = "metal_trash_can",
        // Mobilier divers
        [KFurniture + "table"] = "WoodenTable_01",
        [KFurniture + "chair"] = "WoodenChair_01",
        [KFurniture + "chairCushion"] = "painted_wooden_chair_01",
        [KFurniture + "bench"] = "painted_wooden_bench",
        [KFurniture + "pottedPlant"] = "planter_pot_clay",
        [KFurniture + "bookcaseOpen"] = "steel_frame_shelves_01",
        [KFurniture + "bookcaseOpenLow"] = "steel_frame_shelves_01",
        [KFurniture + "bookcaseClosedWide"] = "wooden_bookshelf_worn",
        [KFurniture + "cardboardBoxClosed"] = "cardboard_box_01",
        [KFurniture + "cardboardBoxOpen"] = "cardboard_box_01",
        // Sous-sols
        [KSurvival + "box"] = "wooden_crate_01",
        [KSurvival + "box-open"] = "wooden_crate_01",
        [KSurvival + "box-large"] = "wooden_crate_01",
        [KSurvival + "barrel"] = "barrel_03",
        [KSurvival + "bucket"] = "wooden_bucket_01",
        [KSurvival + "tool-shovel"] = "rusted_spade_01",
        [KSurvival + "workbench"] = "WoodenTable_03",
        // Nature / extérieur
        [KSurvival + "rock-a"] = "rock_07",
        [KSurvival + "rock-b"] = "rock_09",
        [KSurvival + "rock-c"] = "stone_01",
        [KSurvival + "rock-flat-grass"] = "stone_01",
        [KSurvival + "resource-stone-large"] = "boulder_01",
        [KSurvival + "tree-trunk"] = "tree_stump_01",
        [KSurvival + "tree-log-small"] = "dead_tree_trunk_02",
        [KGraveyard + "rocks"] = "rock_09",
        [KGraveyard + "rocks-tall"] = "boulder_01",
        [KGraveyard + "debris-wood"] = "dead_tree_trunk_02",
        [KGraveyard + "bench"] = "painted_wooden_bench",
        [KGraveyard + "bench-damaged"] = "painted_wooden_bench",
        [KGraveyard + "iron-fence-border-gate"] = "large_iron_gate",
        // Arbres réalistes (pack AltonGames "3D Nature Assets Pack Lite") — utilisés dans la cour et autour
        [KSurvival + "tree-autumn-tall"] = Foliage + "maple.prefab",
        [KSurvival + "tree-autumn"] = Foliage + "maple1.prefab",
        [KSurvival + "tree-tall"] = Foliage + "lucious tree.prefab",
        [KSurvival + "tree"] = Foliage + "apple.prefab",
        [KGraveyard + "pine"] = Foliage + "Conifer.prefab",
        [KGraveyard + "pine-crooked"] = Foliage + "Conifer.prefab",
        [KGraveyard + "pine-fall"] = Foliage + "Conifer.prefab",
    };

    const string AltonGames = "Assets/AltonGames/3DNatureAssetsPack(Lite)/";
    const string Foliage = AltonGames + "Prefabs/Foliage/";
    const string BushLarge = AltonGames + "Prefabs/Bush/Bush(Large).prefab";
    const string BushSmall = AltonGames + "Prefabs/Bush/Bush(Small).prefab";

    /// Vrai pendant la construction de la forêt lointaine : on y garde des silhouettes légères.
    static bool _skipRealistic;

    /// Correction d'orientation (degrés) quand la face avant du modèle Poly Haven n'est pas dans le même sens que Kenney.
    static readonly Dictionary<string, float> RealisticYaw = new Dictionary<string, float>
    {
    };

    static bool _realisticPrepared;

    /// Chemin réellement chargé pour un chemin Kenney : (asset, idPolyHaven ou null). skip = ne rien placer.
    static GameObject LoadRealistic(string path, out string phId, out bool skip)
    {
        phId = null; skip = false;
        if (_skipRealistic || !Realistic.TryGetValue(path, out var id)) return null;
        if (id == "") { skip = true; return null; }
        if (!_realisticPrepared) { PolyHavenModels.PrepareAll(false); FixFoliageMaterials(); _realisticPrepared = true; }
        bool prefab = id.EndsWith(".prefab");
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(prefab ? id : PolyHavenModels.FbxPath(id));
        if (go != null) phId = prefab ? System.IO.Path.GetFileNameWithoutExtension(id) : id;
        return go;
    }

    /// Feuillages du pack AltonGames : transparence remplacée par une découpe alpha (ombres, tri correct, moins coûteux),
    /// double face, instanciation GPU.
    static void FixFoliageMaterials()
    {
        if (!AssetDatabase.IsValidFolder(AltonGames + "materials")) return;
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { AltonGames + "materials" }))
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (m == null || !m.HasProperty("_BaseMap")) continue;
            m.enableInstancing = true;
            var tex = m.GetTexture("_BaseMap");
            var ti = tex != null ? AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex)) as TextureImporter : null;
            bool leaves = ti != null && ti.DoesSourceTextureHaveAlpha();
            if (leaves)
            {
                m.SetFloat("_Surface", 0f);
                m.SetFloat("_AlphaClip", 1f);
                m.SetFloat("_Cutoff", 0.45f);
                m.SetFloat("_Cull", 0f);
                m.SetFloat("_SrcBlend", 1f); m.SetFloat("_DstBlend", 0f); m.SetFloat("_ZWrite", 1f);
                m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.EnableKeyword("_ALPHATEST_ON");
                m.SetOverrideTag("RenderType", "TransparentCutout");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }
            EditorUtility.SetDirty(m);
        }
    }

    /// Arbre réaliste : disparaît quand il devient minuscule à l'écran, et seul le tronc bloque le joueur.
    static void SetupRealTree(GameObject tree, bool trunkCollider)
    {
        if (tree == null) return;
        var renderers = tree.GetComponentsInChildren<Renderer>();
        var lod = tree.AddComponent<LODGroup>();
        lod.SetLODs(new[] { new LOD(0.015f, renderers) });
        lod.RecalculateBounds();
        foreach (var c in tree.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
        if (trunkCollider && TryBounds(tree, out var b))
        {
            float s = tree.transform.lossyScale.x;
            var cc = tree.AddComponent<CapsuleCollider>();
            cc.radius = 0.35f / s;
            cc.height = b.size.y / s;
            cc.center = tree.transform.InverseTransformPoint(new Vector3(b.center.x, b.center.y, b.center.z));
        }
    }

    /// Place un prefab (chemin complet) mis à l'échelle comme Model().
    static GameObject InstantiateAt(LevelDef L, string prefabPath, Vector3 floorPos, float rotY, Vector3 fit)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (asset == null) return null;
        if (!_realisticPrepared) { PolyHavenModels.PrepareAll(false); FixFoliageMaterials(); _realisticPrepared = true; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, L.props);
        StripVariants(go);
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, rotY, 0f) * asset.transform.localRotation);
        go.transform.localScale = Vector3.one;
        if (!TryBounds(go, out var b)) return go;
        float scale = Mathf.Min(fit.x / Mathf.Max(b.size.x, 0.01f), fit.y / Mathf.Max(b.size.y, 0.01f), fit.z / Mathf.Max(b.size.z, 0.01f));
        go.transform.localScale = Vector3.one * scale;
        Ground(go, floorPos);
        SetLayerRecursive(go, _layerFurniture);
        return go;
    }

    // ───────────────────────── Matelas (le lit Poly Haven est un sommier métallique nu) ─────────────────────────

    static void AddMattress(LevelDef L, GameObject bed)
    {
        if (bed == null || !TryBounds(bed, out var b)) return;
        var m = Mat("Matelas", new Color(0.78f, 0.76f, 0.72f), 0.05f);
        bool alongZ = b.size.z >= b.size.x;
        float len = (alongZ ? b.size.z : b.size.x) - 0.18f, wid = (alongZ ? b.size.x : b.size.z) - 0.12f;
        float top = b.min.y + b.size.y * 0.47f; // le sommier est à mi-hauteur des montants
        var size = alongZ ? new Vector3(wid, 0.16f, len) : new Vector3(len, 0.16f, wid);
        Cube(bed.transform.parent, "Matelas", new Vector3(b.center.x, top + 0.08f, b.center.z), size, Quaternion.identity, m, true, _layerFurniture);
    }

    // ───────────────────────── Clôture en fer forgé (mesh généré, texture métal rouillé) ─────────────────────────

    static readonly Dictionary<int, Mesh> _fenceMeshes = new Dictionary<int, Mesh>();

    static void FenceSegment(LevelDef X, Vector3 center, bool alongX, float length, Material m)
    {
        int key = Mathf.RoundToInt(length * 100f);
        if (!_fenceMeshes.TryGetValue(key, out var mesh) || mesh == null)
        {
            mesh = BuildFenceMesh(length);
            AssetDatabase.AddObjectToAsset(mesh, _boxContainer);
            _fenceMeshes[key] = mesh;
        }
        var go = new GameObject("Cloture");
        go.transform.SetParent(X.props, false);
        go.transform.SetPositionAndRotation(center, Quaternion.Euler(0f, alongX ? 0f : 90f, 0f));
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = m;
        go.layer = _layerFurniture;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
    }

    /// Tronçon centré à l'origine, le long de X : deux poteaux, deux lisses, barreaux à pointe tous les 14 cm.
    static Mesh BuildFenceMesh(float length)
    {
        var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        float h = 1.8f, half = length * 0.5f;
        AddBox(v, n, uv, t, new Vector3(-half, 0f, -0.05f), new Vector3(-half + 0.1f, h + 0.15f, 0.05f));      // poteau
        AddBox(v, n, uv, t, new Vector3(half - 0.1f, 0f, -0.05f), new Vector3(half, h + 0.15f, 0.05f));        // poteau
        AddBox(v, n, uv, t, new Vector3(-half, 0.18f, -0.02f), new Vector3(half, 0.23f, 0.02f));              // lisse basse
        AddBox(v, n, uv, t, new Vector3(-half, h - 0.2f, -0.02f), new Vector3(half, h - 0.15f, 0.02f));        // lisse haute
        for (float x = -half + 0.17f; x < half - 0.12f; x += 0.14f)
        {
            AddBox(v, n, uv, t, new Vector3(x - 0.012f, 0.02f, -0.012f), new Vector3(x + 0.012f, h, 0.012f));  // barreau
            AddBox(v, n, uv, t, new Vector3(x - 0.022f, h, -0.022f), new Vector3(x + 0.022f, h + 0.06f, 0.022f)); // pointe
        }
        var mesh = new Mesh { name = $"Cloture_{length:0.00}m" };
        mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0);
        mesh.RecalculateTangents(); mesh.RecalculateBounds();
        return mesh;
    }

    static void AddBox(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t, Vector3 a, Vector3 b)
    {
        void Face(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 normal, Vector2 s)
        {
            int i = v.Count;
            v.Add(p0); v.Add(p1); v.Add(p2); v.Add(p3);
            for (int k = 0; k < 4; k++) n.Add(normal);
            uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(0, s.y)); uv.Add(new Vector2(s.x, s.y)); uv.Add(new Vector2(s.x, 0));
            t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3);
        }
        Vector3 d = b - a;
        Face(new Vector3(a.x, a.y, a.z), new Vector3(a.x, b.y, a.z), new Vector3(b.x, b.y, a.z), new Vector3(b.x, a.y, a.z), Vector3.back, new Vector2(d.x, d.y));
        Face(new Vector3(b.x, a.y, b.z), new Vector3(b.x, b.y, b.z), new Vector3(a.x, b.y, b.z), new Vector3(a.x, a.y, b.z), Vector3.forward, new Vector2(d.x, d.y));
        Face(new Vector3(a.x, a.y, b.z), new Vector3(a.x, b.y, b.z), new Vector3(a.x, b.y, a.z), new Vector3(a.x, a.y, a.z), Vector3.left, new Vector2(d.z, d.y));
        Face(new Vector3(b.x, a.y, a.z), new Vector3(b.x, b.y, a.z), new Vector3(b.x, b.y, b.z), new Vector3(b.x, a.y, b.z), Vector3.right, new Vector2(d.z, d.y));
        Face(new Vector3(a.x, b.y, a.z), new Vector3(a.x, b.y, b.z), new Vector3(b.x, b.y, b.z), new Vector3(b.x, b.y, a.z), Vector3.up, new Vector2(d.x, d.z));
        Face(new Vector3(a.x, a.y, b.z), new Vector3(a.x, a.y, a.z), new Vector3(b.x, a.y, a.z), new Vector3(b.x, a.y, b.z), Vector3.down, new Vector2(d.x, d.z));
    }
}
