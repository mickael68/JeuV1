using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Ciel (HDRI Poly Haven, un par moment de la journée, branchés sur DayNightCycle), brouillard,
/// et décor lointain : terrain avec collines boisées, montagnes enneigées et petite ville dans la vallée.
/// Rien de tout ça n'a de collision : le joueur reste dans la cour clôturée.
/// </summary>
public static partial class EchoesMapGenerator
{
    const string SkyFolder = PolyHavenFolder + "/Ciels";
    const string SkyMatFolder = "Assets/Materials/Ciel";

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
        RenderSettings.fogDensity = 0.0006f; // assez léger pour voir les montagnes au loin
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
            Set("skyBlendShader", Shader.Find("TheEchoesBelow/Skybox Blend")); // référencé : inclus dans les builds
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
    // Vrai terrain Unity de 2,4 km autour de la résidence : la cour reste plate, puis collines boisées,
    // vallée vers la petite ville, et chaîne de montagnes rocheuses aux sommets enneigés au fond.
    // Forêt proche : vrais arbres (AltonGames) placés comme arbres du terrain.
    // Forêt lointaine : "imposteurs" (deux plans croisés avec une image des mêmes arbres), très légers.

    const float TerrSize = 2400f, TerrHeight = 760f, TerrBase = -4f;
    const int TerrRes = 513, TerrSplatRes = 512;
    const string TerrDataPath = MatFolder + "/Terrain_Decor.asset";
    const string TerrLayerFolder = MatFolder + "/Terrain_Couches";
    const string ImpostorPath = MatFolder + "/Imposteurs_Arbres.png";
    static readonly string[] ForestPrefabs = { Foliage + "Conifer.prefab", Foliage + "maple.prefab", Foliage + "lucious tree.prefab", Foliage + "maple1.prefab" };

    /// Distance (m) hors de la grande pelouse autour de la résidence.
    static float YardDistance(float x, float z)
    {
        float dx = Mathf.Max(0f, Mathf.Max(-GroundX - x, x - GroundX));
        float dz = Mathf.Max(0f, Mathf.Max(GroundZ0 - z, z - GroundZ1));
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    /// 1 hors de la vallée, 0 au fond de la vallée qui descend vers la ville.
    static float ValleyFactor(float x, float z)
    {
        float deg = Mathf.Atan2(z, x) * Mathf.Rad2Deg;
        float d = Mathf.Abs(Mathf.DeltaAngle(deg, TownAngle));
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(TownHalfWidth * 0.6f, TownHalfWidth * 1.8f, d));
    }

    static float Perlin(float x, float z, float f, float ox, float oz) => Mathf.PerlinNoise(x * f + ox, z * f + oz);

    /// Hauteur du décor (m, relative au sol de la cour) en (x, z).
    static float BackdropHeight(float x, float z)
    {
        float dist = YardDistance(x, z);
        if (dist <= 0f) return YardRect.Contains(new Vector2(x, z)) ? -0.12f : -0.12f; // la cour elle-même est percée (trou du terrain), voir BuildTerrain
        float valley = ValleyFactor(x, z);
        // Collines : relief doux qui monte à partir de la clôture
        float n = Perlin(x, z, 0.0042f, 100f, 300f) * 0.65f + Perlin(x, z, 0.011f, 40f, 20f) * 0.35f;
        float hills = Mathf.Pow(Mathf.SmoothStep(0f, 1f, dist / 380f), 1.2f) * (18f + 75f * n) * Mathf.Lerp(0.12f, 1f, valley);
        float bumps = (Perlin(x, z, 0.03f, 7f, 9f) - 0.5f) * 5f * Mathf.Clamp01(dist / 50f);
        // Montagnes : crêtes marquées au-delà de 450 m
        float m = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(500f, 950f, dist)); // les montagnes commencent plus loin
        // Crêtes arrondies (pas d'aiguilles) : bruit lissé, pentes adoucies
        float ridge = Mathf.SmoothStep(0f, 1f, 1f - Mathf.Abs(2f * Perlin(x, z, 0.0019f, 500f, 800f) - 1f));
        float ridge2 = Mathf.SmoothStep(0f, 1f, 1f - Mathf.Abs(2f * Perlin(x, z, 0.0055f, 210f, 90f) - 1f));
        float massif = Perlin(x, z, 0.0012f, 70f, 40f);
        float mountains = m * (120f + 180f * massif + 220f * Mathf.Pow(ridge, 1.6f) + 55f * ridge2) * Mathf.Lerp(0.55f, 1f, valley);
        return -0.12f + hills + bumps + mountains;
    }

    /// Densité de forêt (0 = prairie, 1 = forêt) : grandes masses boisées, pas dans le fond de vallée ni sur les sommets.
    static float ForestDensity(float x, float z, float h)
    {
        float f = Perlin(x, z, 0.0055f, 13f, 77f) * 0.7f + Perlin(x, z, 0.02f, 3f, 5f) * 0.3f;
        float d = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.34f, 0.52f, f));
        d *= Mathf.Lerp(0.25f, 1f, ValleyFactor(x, z));                // vallée plus dégagée
        d *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(150f, 210f, h)); // limite des arbres en altitude
        d *= Mathf.Clamp01(YardDistance(x, z) / 6f);                      // rien sur la pelouse
        return d;
    }

    static float BackdropSlope(float x, float z)
    {
        const float e = 3f;
        float hx = BackdropHeight(x + e, z) - BackdropHeight(x - e, z);
        float hz = BackdropHeight(x, z + e) - BackdropHeight(x, z - e);
        return Mathf.Atan(Mathf.Sqrt(hx * hx + hz * hz) / (2f * e)) * Mathf.Rad2Deg;
    }

    static void BuildBackdrop(LevelDef X)
    {
        float gy = X.y;
        var parent = Child(X.root, "Decor_Lointain");
        parent.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true; // hors NavMesh (inaccessible)
        if (!_realisticPrepared) { PolyHavenModels.PrepareAll(false); FixFoliageMaterials(); _realisticPrepared = true; }

        var terrain = BuildTerrain(parent, gy);
        var near = PlaceTerrainForest(terrain, gy);
        int far = BuildImpostorForest(parent, gy);
        Debug.Log($"[EchoesMap] Décor : terrain {TerrSize:0} m, {near} arbres proches, {far} arbres lointains (imposteurs).");

        // La caméra doit voir jusqu'aux montagnes
        var cam = GameObject.Find("Joueur")?.GetComponentInChildren<Camera>(true);
        if (cam != null && cam.farClipPlane < 2600f) { cam.farClipPlane = 2600f; EditorUtility.SetDirty(cam); }

        // Petite ville au fond de la vallée (fenêtres éclairées la nuit)
        var town = Child(parent, "Ville");
        var mTown = TownMaterial();
        var mRoof = Mat("Decor_Toit", new Color(0.12f, 0.12f, 0.13f));
        for (int i = 0; i < 22; i++)
        {
            float deg = TownAngle + Rand(-TownHalfWidth * 0.6f, TownHalfWidth * 0.6f);
            float r = Rand(230f, 420f);
            float a = deg * Mathf.Deg2Rad;
            float w = Rand(14f, 26f), d = Rand(12f, 20f), h = Mathf.Round(Rand(4f, 13f)) * 3f;
            float px = Mathf.Cos(a) * r, pz = Mathf.Sin(a) * r;
            float baseY = gy + BackdropHeight(px, pz) - 1.5f;
            var c = new Vector3(px, baseY + h * 0.5f, pz);
            var rot = Quaternion.Euler(0f, -deg + 90f + Rand(-15f, 15f), 0f);
            var b = Cube(town, "Immeuble", c, new Vector3(w, h, d), rot, mTown, false, -1);
            b.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            Cube(town, "Toit", c + Vector3.up * (h * 0.5f + 0.4f), new Vector3(w + 0.6f, 0.8f, d + 0.6f), rot, mRoof, false, -1)
                .GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
    }

    // ── Terrain ──

    static Terrain BuildTerrain(Transform parent, float gy)
    {
        var td = new TerrainData { heightmapResolution = TerrRes, alphamapResolution = TerrSplatRes, baseMapResolution = 512 };
        td.size = new Vector3(TerrSize, TerrHeight, TerrSize);
        // Enregistré AVANT de peindre : sinon les textures de mélange (sous-assets) ne sont pas sauvegardées
        // et le terrain rechargé n'affiche que la première couche (herbe partout)
        AssetDatabase.DeleteAsset(TerrDataPath);
        AssetDatabase.CreateAsset(td, TerrDataPath);
        float x0 = -TerrSize * 0.5f, z0 = -TerrSize * 0.5f;

        var heights = new float[TerrRes, TerrRes];
        for (int j = 0; j < TerrRes; j++)
            for (int i = 0; i < TerrRes; i++)
            {
                float x = x0 + TerrSize * i / (TerrRes - 1), z = z0 + TerrSize * j / (TerrRes - 1);
                heights[j, i] = Mathf.Clamp01((BackdropHeight(x, z) - TerrBase) / TerrHeight);
            }
        td.SetHeights(0, 0, heights);
        // Trou dans le terrain sous toute la cour : il ne doit jamais traverser les sous-sols (étage 0, -1…)
        int hr = td.holesResolution;
        var solid = new bool[hr, hr];
        for (int j = 0; j < hr; j++)
            for (int i = 0; i < hr; i++)
            {
                float x = x0 + TerrSize * (i + 0.5f) / hr, z = z0 + TerrSize * (j + 0.5f) / hr;
                solid[j, i] = !Rect.MinMaxRect(YardRect.xMin - 1f, YardRect.yMin - 1f, YardRect.xMax + 1f, YardRect.yMax + 1f).Contains(new Vector2(x, z));
            }
        td.SetHoles(0, 0, solid);

        // Couches : herbe, sous-bois, rocher, neige, alpage (herbe et cailloux)
        EnsureFolder(TerrLayerFolder);
        td.terrainLayers = new[]
        {
            TerrainLayerFor("Herbe", "leafy_grass", 5f, new Color(0.62f, 0.85f, 0.5f)),
            TerrainLayerFor("Sous_Bois", "forest_ground_04", 6f, Color.white),
            TerrainLayerFor("Rocher", "aerial_rocks_02", 28f, Color.white),
            TerrainLayerFor("Neige", "snow_02", 18f, Color.white),
            TerrainLayerFor("Alpage", "aerial_grass_rock", 22f, Color.white),
        };
        var splat = new float[TerrSplatRes, TerrSplatRes, 5];
        for (int j = 0; j < TerrSplatRes; j++)
            for (int i = 0; i < TerrSplatRes; i++)
            {
                float x = x0 + TerrSize * (i + 0.5f) / TerrSplatRes, z = z0 + TerrSize * (j + 0.5f) / TerrSplatRes;
                float h = BackdropHeight(x, z), s = BackdropSlope(x, z);
                float rock = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(17f, 27f, s));
                float snow = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(185f, 260f, h + (Perlin(x, z, 0.02f, 9f, 4f) - 0.5f) * 50f)) * (1f - rock * 0.5f);
                float alp = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(80f, 160f, h)) * (1f - snow);
                float forest = ForestDensity(x, z, h);
                float w0 = 1f, w1 = forest * 1.4f, w2 = rock * 2.2f, w3 = snow * 3f, w4 = alp * 1.6f;
                float sum = w0 + w1 + w2 + w3 + w4;
                // L'herbe s'efface quand une autre couche domine
                w0 = Mathf.Max(0f, 1f - (w1 + w2 + w3 + w4)); sum = w0 + w1 + w2 + w3 + w4;
                splat[j, i, 0] = w0 / sum; splat[j, i, 1] = w1 / sum; splat[j, i, 2] = w2 / sum; splat[j, i, 3] = w3 / sum; splat[j, i, 4] = w4 / sum;
            }
        td.SetAlphamaps(0, 0, splat);
        foreach (var tex in td.alphamapTextures)
            if (tex != null && !AssetDatabase.Contains(tex)) AssetDatabase.AddObjectToAsset(tex, td);
        EditorUtility.SetDirty(td);
        AssetDatabase.SaveAssets();

        var go = Terrain.CreateTerrainGameObject(td);
        go.name = "Terrain_Decor";
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(x0, gy + TerrBase, z0);
        var t = go.GetComponent<Terrain>();
        var shader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
        if (shader != null)
        {
            string mp = MatFolder + "/Terrain_Decor.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, mp); }
            m.shader = shader;
            t.materialTemplate = m;
        }
        t.heightmapPixelError = 6f;
        t.basemapDistance = 350f;
        t.drawInstanced = true;
        t.treeDistance = 190f;
        t.treeBillboardDistance = 190f;
        t.detailObjectDistance = 0f;
        t.shadowCastingMode = ShadowCastingMode.TwoSided;
        return t;
    }

    static TerrainLayer TerrainLayerFor(string name, string texId, float tile, Color tint)
    {
        if (_texCache == null) _texCache = new System.Collections.Generic.Dictionary<string, Texture2D>();
        string path = $"{TerrLayerFolder}/{name}.terrainlayer";
        var l = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
        if (l == null) { l = new TerrainLayer(); AssetDatabase.CreateAsset(l, path); }
        l.diffuseTexture = LoadTex($"{PolyHavenFolder}/{texId}/{texId}_diff_1k.jpg", TextureImporterType.Default, true);
        l.normalMapTexture = LoadTex($"{PolyHavenFolder}/{texId}/{texId}_nor_gl_1k.jpg", TextureImporterType.NormalMap, false);
        l.tileSize = new Vector2(tile, tile);
        l.diffuseRemapMax = new Vector4(tint.r, tint.g, tint.b, 1f);
        l.smoothness = 0f;
        EditorUtility.SetDirty(l);
        return l;
    }

    // ── Forêt proche : arbres du terrain ──

    static int PlaceTerrainForest(Terrain t, float gy)
    {
        var td = t.terrainData;
        var protos = new System.Collections.Generic.List<TreePrototype>();
        var natural = new System.Collections.Generic.List<float>();  // hauteur native de chaque prefab
        foreach (var path in new[] { ForestPrefabs[0], ForestPrefabs[1], ForestPrefabs[2], ForestPrefabs[3], BushLarge, BushSmall })
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (src == null) continue;
            // Le terrain exige un LODGroup (ou un MeshRenderer) à la racine : on enveloppe le prefab
            var tmp = (GameObject)PrefabUtility.InstantiatePrefab(src);
            float h = TryBounds(tmp, out var b) ? Mathf.Max(0.5f, b.size.y) : 10f;
            var lod = tmp.AddComponent<LODGroup>();
            lod.SetLODs(new[] { new LOD(0.008f, tmp.GetComponentsInChildren<Renderer>()) });
            lod.RecalculateBounds();
            EnsureFolder(MatFolder + "/Arbres_Terrain");
            var prefab = PrefabUtility.SaveAsPrefabAsset(tmp, $"{MatFolder}/Arbres_Terrain/{System.IO.Path.GetFileNameWithoutExtension(path)}.prefab");
            Object.DestroyImmediate(tmp);
            if (prefab == null) continue;
            protos.Add(new TreePrototype { prefab = prefab });
            natural.Add(h);
        }
        if (protos.Count == 0) return 0;
        td.treePrototypes = protos.ToArray();
        int nTrees = Mathf.Min(4, protos.Count), bushStart = nTrees;
        var inst = new System.Collections.Generic.List<TreeInstance>();
        float x0 = -TerrSize * 0.5f, z0 = -TerrSize * 0.5f;
        for (int tries = 0; tries < 30000 && inst.Count < 1700; tries++)
        {
            float x = Rand(-GroundX - 170f, GroundX + 170f), z = Rand(GroundZ0 - 170f, GroundZ1 + 170f);
            float dist = YardDistance(x, z);
            if (dist < 4f || dist > 165f) continue;
            float h = BackdropHeight(x, z);
            float dens = ForestDensity(x, z, h);
            bool bush = _rng.NextDouble() < 0.22 && protos.Count > bushStart;
            if (_rng.NextDouble() > (bush ? 0.35f + dens * 0.4f : dens * 0.9f + 0.03f)) continue;
            if (BackdropSlope(x, z) > 32f) continue;
            int k = bush ? bushStart + _rng.Next(protos.Count - bushStart) : (_rng.NextDouble() < 0.45 ? 0 : _rng.Next(nTrees));
            float want = bush ? Rand(1.0f, 2.2f) : Rand(9f, 17f);
            float sc = want / natural[k];
            inst.Add(new TreeInstance
            {
                prototypeIndex = k,
                position = new Vector3((x - x0) / TerrSize, (h - TerrBase) / TerrHeight, (z - z0) / TerrSize),
                widthScale = sc * Rand(0.85f, 1.15f), heightScale = sc,
                rotation = Rand(0f, Mathf.PI * 2f),
                color = Color.Lerp(Color.white, new Color(0.8f, 0.85f, 0.75f), Rand(0f, 1f)),
                lightmapColor = Color.white,
            });
        }
        td.SetTreeInstances(inst.ToArray(), true);
        return inst.Count;
    }

    // ── Forêt lointaine : imposteurs (plans croisés texturés) ──

    static int BuildImpostorForest(Transform parent, float gy)
    {
        var atlas = ImpostorAtlas(out int tiles);
        if (atlas == null) return 0;
        var m = Mat("Decor_Imposteurs", Color.white, 0f);
        m.SetTexture("_BaseMap", atlas);
        m.SetTextureScale("_BaseMap", Vector2.one);
        m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.45f); m.EnableKeyword("_ALPHATEST_ON");
        m.SetFloat("_Cull", 0f);
        m.SetTexture("_BumpMap", null); m.DisableKeyword("_NORMALMAP");
        m.SetTexture("_MetallicGlossMap", null); m.DisableKeyword("_METALLICSPECGLOSSMAP");
        m.SetFloat("_Smoothness", 0f);
        m.SetFloat("_EnvironmentReflections", 0f); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        m.SetOverrideTag("RenderType", "TransparentCutout");
        m.renderQueue = (int)RenderQueue.AlphaTest;
        EditorUtility.SetDirty(m);

        var root = Child(parent, "Foret_Lointaine");
        const float cell = 600f;
        var chunks = new System.Collections.Generic.Dictionary<Vector2Int, (System.Collections.Generic.List<Vector3> v, System.Collections.Generic.List<Vector2> uv, System.Collections.Generic.List<int> t)>();
        int count = 0;
        for (int tries = 0; tries < 120000 && count < 14000; tries++)
        {
            float x = Rand(-TerrSize * 0.5f + 20f, TerrSize * 0.5f - 20f), z = Rand(-TerrSize * 0.5f + 20f, TerrSize * 0.5f - 20f);
            float dist = YardDistance(x, z);
            if (dist < 150f) continue;
            float h = BackdropHeight(x, z);
            float dens = ForestDensity(x, z, h);
            if (_rng.NextDouble() > dens * 0.95f) continue;
            if (BackdropSlope(x, z) > 36f) continue;
            var key = new Vector2Int(Mathf.FloorToInt(x / cell), Mathf.FloorToInt(z / cell));
            if (!chunks.TryGetValue(key, out var c)) chunks[key] = c = (new System.Collections.Generic.List<Vector3>(), new System.Collections.Generic.List<Vector2>(), new System.Collections.Generic.List<int>());
            int tile = h > 150f || _rng.NextDouble() < 0.5 ? 0 : _rng.Next(tiles); // les conifères dominent en altitude
            float th = Rand(10f, 19f) * Mathf.Lerp(1f, 1.35f, Mathf.Clamp01((dist - 150f) / 800f)), tw = th * 0.5f;
            var basePt = new Vector3(x, gy + h - 0.6f, z);
            float yaw = Rand(0f, Mathf.PI);
            for (int q = 0; q < 2; q++)
            {
                float a = yaw + q * Mathf.PI * 0.5f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (tw * 0.5f);
                int i0 = c.v.Count;
                c.v.Add(basePt - dir); c.v.Add(basePt + dir); c.v.Add(basePt + dir + Vector3.up * th); c.v.Add(basePt - dir + Vector3.up * th);
                float u0 = (float)tile / tiles, u1 = (float)(tile + 1) / tiles;
                c.uv.Add(new Vector2(u0, 0f)); c.uv.Add(new Vector2(u1, 0f)); c.uv.Add(new Vector2(u1, 1f)); c.uv.Add(new Vector2(u0, 1f));
                c.t.Add(i0); c.t.Add(i0 + 2); c.t.Add(i0 + 1); c.t.Add(i0); c.t.Add(i0 + 3); c.t.Add(i0 + 2);
            }
            count++;
        }
        foreach (var kv in chunks)
        {
            var mesh = new Mesh { name = $"Foret_Lointaine_{kv.Key.x}_{kv.Key.y}" };
            if (kv.Value.v.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(kv.Value.v); mesh.SetUVs(0, kv.Value.uv); mesh.SetTriangles(kv.Value.t, 0);
            var normals = new Vector3[kv.Value.v.Count];
            for (int i = 0; i < normals.Length; i++) normals[i] = new Vector3(0.25f, 1f, 0.15f).normalized; // éclairées comme un couvert vu de loin
            mesh.normals = normals;
            mesh.RecalculateBounds();
            AssetDatabase.AddObjectToAsset(mesh, _boxContainer);
            var go = new GameObject(mesh.name);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = m;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }
        return count;
    }

    /// Photographie chaque arbre de la forêt (fond magenta rendu transparent) dans une bande de vignettes 256 × 512.
    static Texture2D ImpostorAtlas(out int tiles)
    {
        tiles = 0;
        var prefabs = new System.Collections.Generic.List<GameObject>();
        foreach (var p in ForestPrefabs) { var g = AssetDatabase.LoadAssetAtPath<GameObject>(p); if (g != null) prefabs.Add(g); }
        if (prefabs.Count == 0) return null;
        tiles = prefabs.Count;
        const int W = 256, H = 512;
        var atlas = new Texture2D(W * tiles, H, TextureFormat.RGBA32, false);
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
        var camGo = new GameObject("Camera_Imposteurs");
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(1f, 0f, 1f, 1f);
        cam.cullingMask = 1 << 31; cam.targetTexture = rt; cam.allowHDR = false; cam.allowMSAA = false;
        var lightGo = new GameObject("Lumiere_Imposteurs");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional; light.intensity = 1.1f; light.cullingMask = 1 << 31;
        lightGo.transform.rotation = Quaternion.Euler(40f, -25f, 0f);
        bool fog = RenderSettings.fog; RenderSettings.fog = false;
        for (int k = 0; k < tiles; k++)
        {
            var go = (GameObject)Object.Instantiate(prefabs[k]);
            go.transform.position = new Vector3(0f, -4000f, 0f);
            foreach (var tr in go.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = 31;
            if (!TryBounds(go, out var b)) { Object.DestroyImmediate(go); continue; }
            float aspect = (float)W / H;
            cam.orthographicSize = Mathf.Max(b.extents.y, b.extents.x / aspect, b.extents.z / aspect) * 1.04f;
            camGo.transform.SetPositionAndRotation(new Vector3(b.center.x, b.center.y, b.center.z - 60f), Quaternion.identity);
            cam.nearClipPlane = 1f; cam.farClipPlane = 200f;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            var px = tex.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                bool key = c.r > 200 && c.b > 200 && c.g < 70;          // fond magenta
                bool fringe = c.r > 150 && c.b > 150 && c.g < 110;
                px[i] = key ? new Color32(30, 40, 25, 0) : fringe ? new Color32((byte)(c.g), (byte)(c.g + 10), (byte)(c.g), 120) : new Color32(c.r, c.g, c.b, 255);
            }
            atlas.SetPixels32(k * W, 0, W, H, px);
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(go);
        }
        RenderSettings.fog = fog;
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(camGo);
        Object.DestroyImmediate(lightGo);
        atlas.Apply();
        File.WriteAllBytes(ImpostorPath, atlas.EncodeToPNG());
        Object.DestroyImmediate(atlas);
        AssetDatabase.ImportAsset(ImpostorPath);
        if (AssetImporter.GetAtPath(ImpostorPath) is TextureImporter ti)
        {
            ti.alphaIsTransparency = true; ti.mipmapEnabled = true; ti.wrapMode = TextureWrapMode.Clamp; ti.sRGBTexture = true;
            ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(ImpostorPath);
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
