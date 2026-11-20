using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Ciel (HDRI Poly Haven, un par moment de la journée, branchés sur DayNightCycle), brouillard,
/// et décor lointain : anneau de collines boisées autour de la résidence, petite ville au loin.
/// Rien de tout ça n'a de collision : le joueur reste dans la cour clôturée.
/// </summary>
public static partial class EchoesMapGenerator
{
    const string SkyFolder = PolyHavenFolder + "/Ciels";
    const string SkyMatFolder = "Assets/Materials/Ciel";

    // Collines : anneau de HillR0 (sous la pelouse) à HillR1, ouvert en vallée vers la ville
    const float HillR0 = 60f, HillR1 = 340f;
    const int HillSegments = 180;
    static readonly float[] HillRings = { 60f, 72f, 90f, 115f, 150f, 195f, 250f, 300f, 340f };
    const float TownAngle = 35f, TownHalfWidth = 28f; // direction de la vallée (degrés, 0 = +X, sens trigo vers +Z)

    // ───────────────────────── Ciel + brouillard ─────────────────────────

    static void SetupSky()
    {
        EnsureFolder(SkyMatFolder);
        var morning = SkyMat("Ciel_Matin", "qwantani_sunrise_puresky", 0.8f);
        var day = SkyMat("Ciel_Jour", "kloofendal_48d_partly_cloudy_puresky", 1.0f);
        var evening = SkyMat("Ciel_Soir", "qwantani_dusk_2_puresky", 0.55f);
        // Les HDRI de nuit Poly Haven sont exposés comme en plein jour : on les assombrit fortement
        var night = SkyMat("Ciel_Nuit", "qwantani_night_puresky", 0.07f);

        if (day != null) RenderSettings.skybox = day;
        RenderSettings.ambientMode = AmbientMode.Skybox;

        // Brouillard léger : fond le décor lointain dans le ciel (les étages profonds imposent le leur)
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.0045f;
        RenderSettings.fogColor = new Color(0.62f, 0.68f, 0.74f);

        var sys = GameObject.Find("Systemes");
        var cycle = sys != null ? sys.GetComponent<DayNightCycle>() : null;
        if (cycle != null)
        {
            var so = new SerializedObject(cycle);
            void Set(string prop, Object v) { var p = so.FindProperty(prop); if (p != null) p.objectReferenceValue = v; }
            Set("morningSkybox", morning);
            Set("daySkybox", day);
            Set("eveningSkybox", evening);
            Set("nightSkybox", night);
            Set("sunLight", GameObject.Find("Soleil")?.GetComponent<Light>());
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        var sun = GameObject.Find("Soleil")?.GetComponent<Light>();
        if (sun != null) RenderSettings.sun = sun;
    }

    static Material SkyMat(string name, string hdri, float exposure)
    {
        string texPath = $"{SkyFolder}/{hdri}_2k.hdr";
        var imp = AssetImporter.GetAtPath(texPath) as TextureImporter;
        if (imp == null) { Debug.LogWarning("[EchoesMap] Ciel introuvable : " + texPath); return null; }
        if (imp.mipmapEnabled || imp.wrapModeU != TextureWrapMode.Repeat || imp.textureCompression != TextureImporterCompression.CompressedHQ)
        {
            imp.mipmapEnabled = false;                 // évite la couture verticale du panorama
            imp.wrapModeU = TextureWrapMode.Repeat;
            imp.wrapModeV = TextureWrapMode.Clamp;
            imp.maxTextureSize = 2048;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.SaveAndReimport();
        }
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

        string path = $"{SkyMatFolder}/{name}.mat";
        var shader = Shader.Find("Skybox/Panoramic");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        else m.shader = shader;
        m.SetTexture("_MainTex", tex);
        m.SetFloat("_Exposure", exposure);
        m.SetFloat("_Mapping", 1f);   // latitude-longitude
        m.SetFloat("_ImageType", 0f); // 360°
        m.SetFloat("_Rotation", 0f);
        EditorUtility.SetDirty(m);
        return m;
    }

    // ───────────────────────── Décor lointain ─────────────────────────

    static float HillAmp(float deg)
    {
        float a = deg * Mathf.Deg2Rad;
        float n1 = Mathf.PerlinNoise(Mathf.Cos(a) * 1.6f + 11f, Mathf.Sin(a) * 1.6f + 7f);
        float n2 = Mathf.PerlinNoise(Mathf.Cos(a) * 4.5f + 31f, Mathf.Sin(a) * 4.5f + 3f);
        float amp = 18f + 42f * n1 + 14f * n2;
        // Vallée vers la ville
        float d = Mathf.Abs(Mathf.DeltaAngle(deg, TownAngle));
        float valley = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(TownHalfWidth * 0.5f, TownHalfWidth * 1.6f, d));
        return amp * Mathf.Lerp(0.12f, 1f, valley);
    }

    /// Hauteur (monde) du décor à l'angle deg et au rayon r.
    static float HillHeight(float deg, float r, float gy)
    {
        float t = Mathf.Clamp01((r - HillR0) / (HillR1 - HillR0));
        float shape = Mathf.Pow(t, 1.25f);
        float a = deg * Mathf.Deg2Rad;
        float bump = (Mathf.PerlinNoise(Mathf.Cos(a) * r * 0.04f + 50f, Mathf.Sin(a) * r * 0.04f + 50f) - 0.5f) * 6f * t;
        return gy - 0.08f + HillAmp(deg) * shape + bump;
    }

    static void BuildBackdrop(LevelDef X)
    {
        float gy = X.y;
        var parent = Child(X.root, "Decor_Lointain");
        parent.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true; // hors NavMesh (inaccessible)

        // 1) Anneau de collines (un seul mesh, UV en mètres)
        int nr = HillRings.Length;
        var verts = new Vector3[(HillSegments + 1) * nr];
        var uvs = new Vector2[verts.Length];
        for (int s = 0; s <= HillSegments; s++)
        {
            float deg = 360f * s / HillSegments;
            float a = deg * Mathf.Deg2Rad;
            for (int k = 0; k < nr; k++)
            {
                float r = HillRings[k];
                var p = new Vector3(Mathf.Cos(a) * r, HillHeight(deg, r, gy), Mathf.Sin(a) * r);
                verts[s * nr + k] = p;
                uvs[s * nr + k] = new Vector2(p.x, p.z);
            }
        }
        var tris = new int[HillSegments * (nr - 1) * 6];
        int ti = 0;
        for (int s = 0; s < HillSegments; s++)
            for (int k = 0; k < nr - 1; k++)
            {
                int a0 = s * nr + k, a1 = a0 + 1, b0 = a0 + nr, b1 = b0 + 1;
                tris[ti++] = a0; tris[ti++] = b0; tris[ti++] = a1;  // sens horaire vu du dessus : face vers le haut
                tris[ti++] = a1; tris[ti++] = b0; tris[ti++] = b1;
            }
        var mesh = new Mesh { name = "Collines" };
        mesh.vertices = verts; mesh.uv = uvs; mesh.triangles = tris;
        mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
        AssetDatabase.AddObjectToAsset(mesh, _boxContainer); // stocké avec les autres meshes générés

        var hills = new GameObject("Collines");
        hills.transform.SetParent(parent, false);
        hills.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = hills.AddComponent<MeshRenderer>();
        mr.sharedMaterial = Mat("Decor_Collines", Color.white);
        mr.shadowCastingMode = ShadowCastingMode.Off;
        GameObjectUtility.SetStaticEditorFlags(hills, StaticEditorFlags.BatchingStatic);

        // 2) Forêt sur les collines (sans collision)
        var LDecor = new LevelDef { number = X.number, y = gy, root = parent, props = Child(parent, "Foret"), markers = X.markers };
        string[] trees = { KGraveyard + "pine", KGraveyard + "pine-crooked", KSurvival + "tree-tall", KSurvival + "tree-autumn-tall", KGraveyard + "pine-fall" };
        var fallback = Mat("Decor_Collines", Color.white);
        int placed = 0;
        for (int tries = 0; tries < 1200 && placed < 220; tries++)
        {
            float deg = Rand(0f, 360f), r = Rand(74f, 230f);
            if (Mathf.Abs(Mathf.DeltaAngle(deg, TownAngle)) < TownHalfWidth * 0.8f && r > 150f) continue; // la ville
            float a = deg * Mathf.Deg2Rad;
            var p = new Vector3(Mathf.Cos(a) * r, HillHeight(deg, r, gy) - 0.3f, Mathf.Sin(a) * r);
            float h = Rand(9f, 17f) * Mathf.Lerp(1f, 1.6f, (r - 74f) / 156f);
            var t = Model(LDecor, trees[_rng.Next(trees.Length)], p, Rand(0f, 360f), new Vector3(h * 0.5f, h, h * 0.5f), fallback, false);
            if (t != null) foreach (var rend in t.GetComponentsInChildren<Renderer>()) rend.shadowCastingMode = ShadowCastingMode.Off;
            placed++;
        }

        // 3) Petite ville au fond de la vallée (fenêtres éclairées la nuit)
        var town = Child(parent, "Ville");
        var mTown = TownMaterial();
        var mRoof = Mat("Decor_Toit", new Color(0.12f, 0.12f, 0.13f));
        for (int i = 0; i < 16; i++)
        {
            float deg = TownAngle + Rand(-TownHalfWidth * 0.7f, TownHalfWidth * 0.7f);
            float r = Rand(185f, 290f);
            float a = deg * Mathf.Deg2Rad;
            float w = Rand(14f, 26f), d = Rand(12f, 20f), h = Mathf.Round(Rand(4f, 13f)) * 3f;
            float baseY = HillHeight(deg, r, gy) - 1f;
            var c = new Vector3(Mathf.Cos(a) * r, baseY + h * 0.5f, Mathf.Sin(a) * r);
            var rot = Quaternion.Euler(0f, -deg + 90f + Rand(-15f, 15f), 0f);
            var b = Cube(town, "Immeuble", c, new Vector3(w, h, d), rot, mTown, false, -1);
            b.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            Cube(town, "Toit", c + Vector3.up * (h * 0.5f + 0.4f), new Vector3(w + 0.6f, 0.8f, d + 0.6f), rot, mRoof, false, -1)
                .GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
    }

    /// Façade d'immeuble : texture de fenêtres générée (certaines allumées, émissives).
    static Material TownMaterial()
    {
        string texPath = MatFolder + "/Decor_Fenetres.png";
        if (!File.Exists(texPath))
        {
            const int S = 256, cols = 8, rows = 4; // 8 fenêtres × 4 étages par répétition (12 m)
            var rnd = new System.Random(1234);
            var px = new Color32[S * S];
            var emit = new Color32[S * S];
            var wall = new Color32(58, 56, 54, 255);
            for (int i = 0; i < px.Length; i++) { px[i] = wall; emit[i] = new Color32(0, 0, 0, 255); }
            int cw = S / cols, ch = S / rows;
            for (int cx = 0; cx < cols; cx++)
                for (int cy = 0; cy < rows; cy++)
                {
                    bool lit = rnd.NextDouble() < 0.28;
                    var glass = lit ? new Color32(255, 196, 110, 255) : new Color32(22, 26, 32, 255);
                    for (int x = cx * cw + 7; x < (cx + 1) * cw - 7; x++)
                        for (int y = cy * ch + 18; y < (cy + 1) * ch - 14; y++)
                        {
                            px[y * S + x] = glass;
                            if (lit) emit[y * S + x] = glass;
                        }
                }
            var t = new Texture2D(S, S, TextureFormat.RGBA32, false);
            t.SetPixels32(px); File.WriteAllBytes(texPath, t.EncodeToPNG());
            t.SetPixels32(emit); File.WriteAllBytes(MatFolder + "/Decor_Fenetres_Emission.png", t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(texPath);
            AssetDatabase.ImportAsset(MatFolder + "/Decor_Fenetres_Emission.png");
        }
        var m = Mat("Decor_Immeuble", Color.white);
        var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        var emission = AssetDatabase.LoadAssetAtPath<Texture2D>(MatFolder + "/Decor_Fenetres_Emission.png");
        m.SetTexture("_BaseMap", albedo);
        m.SetTextureScale("_BaseMap", new Vector2(1f / 12f, 1f / 12f));
        m.SetTexture("_EmissionMap", emission);
        m.SetColor("_EmissionColor", new Color(1f, 0.85f, 0.6f) * 1.6f);
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(m);
        return m;
    }
}
