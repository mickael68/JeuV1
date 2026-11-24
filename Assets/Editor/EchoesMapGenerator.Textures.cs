using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Textures Poly Haven (CC0, 1K) appliquées aux matériaux de la map, et cubes avec UV en mètres
/// pour que les textures se répètent au lieu de s'étirer sur les grands murs.
/// Les textures sont dans Assets/Textures/PolyHaven/&lt;id&gt;/ (diff, nor_gl, rough).
/// </summary>
public static partial class EchoesMapGenerator
{
    const string PolyHavenFolder = "Assets/Textures/PolyHaven";
    const string BoxMeshPath = MatFolder + "/BoxMeshes.asset";

    struct Surface
    {
        public string id;     // dossier Poly Haven
        public float tile;    // taille d'une répétition, en mètres
        public Color tint;    // teinte multipliée à la texture
        public float metal;   // 0 = mat, ~0.6 = métal
        public Surface(string id, float tile, Color tint, float metal = 0f) { this.id = id; this.tile = tile; this.tint = tint; this.metal = metal; }
    }

    static Color G(float v) => new Color(v, v, v);
    static Color T(float r, float g, float b) => new Color(r, g, b);

    /// Matériau → texture. Les murs / sols / plafonds sont nommés "Mur_<étage>", "Sol_<étage>", "Plafond_<étage>".
    static readonly Dictionary<string, Surface> Surfaces = new Dictionary<string, Surface>
    {
        // Résidence (3, 2, 1) : plâtre peint, stratifié, plafond intérieur
        ["Mur_3"] = new Surface("painted_plaster_wall", 2.5f, T(1f, 0.97f, 0.92f)),
        ["Mur_2"] = new Surface("painted_plaster_wall", 2.5f, T(1f, 0.97f, 0.92f)),
        ["Mur_1"] = new Surface("painted_plaster_wall", 2.5f, T(1f, 0.97f, 0.92f)),
        ["Sol_3"] = new Surface("laminate_floor_02", 2f, G(1f)),
        ["Sol_2"] = new Surface("laminate_floor_02", 2f, G(1f)),
        ["Sol_1"] = new Surface("laminate_floor_02", 2f, G(1f)),
        ["Plafond_3"] = new Surface("ceiling_interior", 2.5f, G(1f)),
        ["Plafond_2"] = new Surface("ceiling_interior", 2.5f, G(1f)),
        ["Plafond_1"] = new Surface("ceiling_interior", 2.5f, G(1f)),

        // 0 : labyrinthe de béton abandonné
        ["Mur_0"] = new Surface("concrete_wall_008", 3f, G(0.85f)),
        ["Sol_0"] = new Surface("concrete_floor_worn_001", 3f, G(0.8f)),
        ["Plafond_0"] = new Surface("brushed_concrete", 3f, G(0.6f)),

        // -1 : sous-sol ordinaire (peinture écaillée, carrelage usé)
        ["Mur_-1"] = new Surface("peeling_painted_wall", 2.5f, G(0.9f)),
        ["Sol_-1"] = new Surface("worn_tile_floor", 2f, G(0.85f)),
        ["Plafond_-1"] = new Surface("brushed_concrete", 3f, G(0.7f)),

        // -2 : locaux techniques
        ["Mur_-2"] = new Surface("factory_wall", 3f, G(0.8f)),
        ["Sol_-2"] = new Surface("garage_floor", 3f, G(0.75f)),
        ["Plafond_-2"] = new Surface("brushed_concrete", 3f, G(0.55f)),
        ["Metal_-2"] = new Surface("rusty_metal_02", 1.5f, G(0.9f), 0.6f),

        // -3 : anciens tunnels de brique
        ["Mur_-3"] = new Surface("worn_brick_wall", 2.5f, G(0.7f)),
        ["Sol_-3"] = new Surface("dirt_floor", 3f, G(0.7f)),
        ["Plafond_-3"] = new Surface("worn_brick_wall", 2.5f, G(0.55f)),

        // -4 : archives inondées (papier peint pourri, planches)
        ["Mur_-4"] = new Surface("decrepit_wallpaper", 2.5f, T(0.55f, 0.6f, 0.7f)),
        ["Sol_-4"] = new Surface("worn_planks", 2.5f, T(0.5f, 0.55f, 0.6f)),
        ["Plafond_-4"] = new Surface("brushed_concrete", 3f, G(0.45f)),

        // -5 : résidence inversée (mêmes textures que les chambres, teintées de rouge)
        ["Mur_-5"] = new Surface("painted_plaster_wall", 2.5f, T(0.75f, 0.45f, 0.45f)),
        ["Sol_-5"] = new Surface("laminate_floor_02", 2f, T(0.6f, 0.35f, 0.35f)),
        ["Plafond_-5"] = new Surface("ceiling_interior", 2.5f, T(0.6f, 0.4f, 0.4f)),

        // -6 : le fond (roche)
        ["Mur_-6"] = new Surface("rock_wall_08", 4f, T(0.45f, 0.35f, 0.35f)),
        ["Sol_-6"] = new Surface("rocky_terrain_02", 4f, T(0.4f, 0.32f, 0.32f)),
        ["Plafond_-6"] = new Surface("rock_wall_08", 4f, T(0.35f, 0.28f, 0.28f)),

        // Structures communes
        ["Escalier_Mur"] = new Surface("concrete_wall_008", 3f, G(0.8f)),
        ["Escalier_Marche"] = new Surface("concrete_floor_worn_001", 1.5f, G(0.8f)),
        ["Ascenseur_Mur"] = new Surface("metal_plate", 2f, G(0.9f), 0.6f),
        ["Ascenseur_Porte"] = new Surface("metal_plate", 1.5f, G(1f), 0.6f),
        ["Ascenseur_Cabine"] = new Surface("metal_plate", 1.5f, T(0.85f, 0.75f, 0.65f), 0.6f),
        ["Grille_Metal"] = new Surface("rusty_metal_02", 1f, G(0.6f), 0.5f),
        ["Tunnel_Aeration"] = new Surface("rusty_metal_02", 1.5f, G(0.8f), 0.5f),

        // Portes et extérieur
        ["Porte_Studio"] = new Surface("wood_table_001", 1.2f, T(0.95f, 0.85f, 0.75f)),
        ["Porte_Escalier"] = new Surface("metal_plate", 1.5f, G(0.75f), 0.5f),
        ["Porte_Cave"] = new Surface("metal_plate", 1.5f, T(0.75f, 0.68f, 0.58f), 0.5f),
        ["Porte_Technique"] = new Surface("rusty_metal_02", 1.5f, G(0.7f), 0.5f),
        ["Porte_Studio_-5"] = new Surface("wood_table_001", 1.2f, T(0.7f, 0.35f, 0.35f)),
        ["Porte_Sortie"] = new Surface("wood_table_001", 1.2f, T(0.6f, 0.45f, 0.35f)),
        ["Exterieur_Herbe"] = new Surface("leafy_grass", 3f, G(0.9f)),
        ["Exterieur_Allee"] = new Surface("concrete_pavement", 2f, G(0.85f)),
        ["Facade_Brique"] = new Surface("red_brick_03", 2.5f, G(0.9f)),
        ["Grotte_Paroi"] = new Surface("cliff_side", 6f, T(0.55f, 0.48f, 0.42f)),
        ["Grotte_Sol"] = new Surface("rocky_terrain_02", 4f, T(0.5f, 0.45f, 0.4f)),
        ["Grotte_Foret_Sol"] = new Surface("forest_leaves_04", 3f, T(0.55f, 0.65f, 0.45f)),
        ["Decor_Collines"] = new Surface("forest_leaves_04", 6f, T(0.45f, 0.55f, 0.35f)),
    };

    static Dictionary<string, Texture2D> _texCache;

    /// Applique la texture Poly Haven du matériau (ou retire les cartes s'il n'en a pas).
    static void ApplySurface(Material m, string name)
    {
        if (!m.HasProperty("_BaseMap")) return; // pas URP
        if (_texCache == null) _texCache = new Dictionary<string, Texture2D>();

        if (!Surfaces.TryGetValue(name, out var s) || !Directory.Exists($"{PolyHavenFolder}/{s.id}"))
        {
            m.SetTexture("_BaseMap", null);
            m.SetTexture("_BumpMap", null);
            m.SetTexture("_MetallicGlossMap", null);
            m.DisableKeyword("_NORMALMAP");
            m.DisableKeyword("_METALLICSPECGLOSSMAP");
            m.SetTextureScale("_BaseMap", Vector2.one);
            return;
        }

        var albedo = LoadTex($"{PolyHavenFolder}/{s.id}/{s.id}_diff_1k.jpg", TextureImporterType.Default, true);
        var normal = LoadTex($"{PolyHavenFolder}/{s.id}/{s.id}_nor_gl_1k.jpg", TextureImporterType.NormalMap, false);
        var mask = MaskFromRoughness(s.id, s.metal);

        m.SetTexture("_BaseMap", albedo);
        m.SetColor("_BaseColor", s.tint);
        float scale = 1f / s.tile;
        m.SetTextureScale("_BaseMap", new Vector2(scale, scale));

        if (normal != null)
        {
            m.SetTexture("_BumpMap", normal);
            m.SetFloat("_BumpScale", 1f);
            m.EnableKeyword("_NORMALMAP");
        }
        if (mask != null)
        {
            m.SetTexture("_MetallicGlossMap", mask);
            m.SetFloat("_Smoothness", 1f);            // multiplie l'alpha de la carte
            m.SetFloat("_SmoothnessTextureChannel", 0f);
            m.EnableKeyword("_METALLICSPECGLOSSMAP");
        }
    }

    static Texture2D LoadTex(string path, TextureImporterType type, bool srgb)
    {
        if (_texCache.TryGetValue(path, out var t)) return t;
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) { Debug.LogWarning("[EchoesMap] Texture introuvable : " + path); return null; }
        if (imp.textureType != type || imp.sRGBTexture != srgb)
        {
            imp.textureType = type;
            imp.sRGBTexture = srgb;
            imp.SaveAndReimport();
        }
        t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        _texCache[path] = t;
        return t;
    }

    /// URP Lit lit le métal dans R et la brillance (smoothness = 1 - roughness) dans A : on fabrique cette carte une fois.
    static Texture2D MaskFromRoughness(string id, float metal)
    {
        string path = $"{PolyHavenFolder}/{id}/{id}_mask_m{Mathf.RoundToInt(metal * 100)}.png";
        if (_texCache.TryGetValue(path, out var cached)) return cached;

        if (!File.Exists(path))
        {
            string rough = $"{PolyHavenFolder}/{id}/{id}_rough_1k.jpg";
            if (!File.Exists(rough)) return null;
            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            src.LoadImage(File.ReadAllBytes(rough));
            var px = src.GetPixels32();
            byte r = (byte)Mathf.RoundToInt(metal * 255f);
            for (int i = 0; i < px.Length; i++)
                px[i] = new Color32(r, 0, 0, (byte)(255 - px[i].r));
            var dst = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false, true);
            dst.SetPixels32(px);
            File.WriteAllBytes(path, dst.EncodeToPNG());
            Object.DestroyImmediate(src);
            Object.DestroyImmediate(dst);
            AssetDatabase.ImportAsset(path);
        }
        return LoadTex(path, TextureImporterType.Default, false);
    }

    // ───────────────────────── Cubes avec UV en mètres ─────────────────────────

    static Dictionary<Vector3Int, Mesh> _boxMeshes;
    static Mesh _boxContainer;

    static void ResetBoxMeshes()
    {
        _texCache = null;
        _boxMeshes = new Dictionary<Vector3Int, Mesh>();
        AssetDatabase.DeleteAsset(BoxMeshPath);
        _boxContainer = new Mesh { name = "BoxMeshes" };
        AssetDatabase.CreateAsset(_boxContainer, BoxMeshPath);
    }

    /// Cube unité dont les UV sont exprimées en mètres pour la taille donnée (projection par face).
    static Mesh BoxMesh(Vector3 size)
    {
        if (_boxMeshes == null) ResetBoxMeshes();
        var key = new Vector3Int(Mathf.RoundToInt(size.x * 100), Mathf.RoundToInt(size.y * 100), Mathf.RoundToInt(size.z * 100));
        if (_boxMeshes.TryGetValue(key, out var mesh)) return mesh;

        var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        var v = cube.vertices;
        var n = cube.normals;
        var uv = new Vector2[v.Length];
        for (int i = 0; i < v.Length; i++)
        {
            Vector3 a = new Vector3(Mathf.Abs(n[i].x), Mathf.Abs(n[i].y), Mathf.Abs(n[i].z));
            if (a.x > a.y && a.x > a.z) uv[i] = new Vector2(v[i].z * size.z * Mathf.Sign(n[i].x), v[i].y * size.y);
            else if (a.y > a.z) uv[i] = new Vector2(v[i].x * size.x, v[i].z * size.z * Mathf.Sign(n[i].y));
            else uv[i] = new Vector2(-v[i].x * size.x * Mathf.Sign(n[i].z), v[i].y * size.y);
        }

        mesh = new Mesh { name = $"Box_{key.x}x{key.y}x{key.z}" };
        mesh.vertices = v;
        mesh.normals = n;
        mesh.uv = uv;
        mesh.triangles = cube.triangles;
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        AssetDatabase.AddObjectToAsset(mesh, _boxContainer);
        _boxMeshes[key] = mesh;
        return mesh;
    }
}
