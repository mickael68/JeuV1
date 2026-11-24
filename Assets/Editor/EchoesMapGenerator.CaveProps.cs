using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Objets des grottes du -7 :
///  - vrais modèles Poly Haven : campements (foyer, lanterne, valise, gamelle, thermos, bougeoir, hachette, lampe tombée),
///    rochers, racines et mousse de la forêt ;
///  - maillages fabriqués quand aucun modèle libre n'existe : champignons (profil tourné, chapeau et lamelles),
///    cristaux (prismes hexagonaux à pointe, facettes nettes), ossements (os longs à têtes renflées).
/// Les maillages fabriqués sont rangés dans CaveMeshes.asset avec la roche.
/// </summary>
public static partial class EchoesMapGenerator
{
    static Mesh _mushStem, _mushCapRound, _mushCapCone, _crystalMesh, _boneMesh;

    static void ResetCavePropMeshes() { _mushStem = _mushCapRound = _mushCapCone = _crystalMesh = _boneMesh = null; }

    // ───────────────────────── Modèles Poly Haven ─────────────────────────

    /// Place un modèle Poly Haven posé au sol (sans reflet du ciel), avec une boîte de collision si demandé.
    static GameObject CaveProp(LevelDef D, string id, Vector3 floor, float yaw, Vector3 fit, bool collider = false)
    {
        var go = InstantiateAt(D, PolyHavenModels.FbxPath(id), floor, yaw, fit);
        if (go == null) return null;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
            foreach (var m in r.sharedMaterials) NoSkyReflection(m);
        // Le câble de la radio a un maillage invalide (refusé par le NavMesh) : on le retire
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            if (mf.sharedMesh != null && mf.sharedMesh.name.Contains("cable_curve"))
            {
                if (PrefabUtility.IsPartOfPrefabInstance(go)) PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                Object.DestroyImmediate(mf.gameObject);
            }
        // Petit objet de décor : hors du calcul des chemins (certains maillages Poly Haven y sont refusés)
        if (!collider) go.AddComponent<Unity.AI.Navigation.NavMeshModifier>().ignoreFromBuild = true;
        if (collider && TryBounds(go, out var b))
        {
            var bc = go.AddComponent<BoxCollider>();
            bc.center = go.transform.InverseTransformPoint(b.center);
            var s = go.transform.lossyScale;
            bc.size = new Vector3(b.size.x / s.x, b.size.y / s.y, b.size.z / s.z) * 0.85f;
        }
        return go;
    }

    static void DecorateCamp(LevelDef D, CaveNode n, Material mp)
    {
        if (!CaveFloor(n.c, out var h)) return;
        var c = h.point;
        Vector3 At(float x, float z) => CaveFloor(c + new Vector3(x, 0.5f, z), out var g) ? g.point : c + new Vector3(x, 0f, z);

        CaveProp(D, "stone_fire_pit", c, Rand(0f, 360f), new Vector3(1.1f, 0.35f, 1.1f), true);          // feu éteint
        var lantern = CaveProp(D, "Lantern_01", At(1.1f, 0.5f), Rand(0f, 360f), new Vector3(0.22f, 0.38f, 0.22f));
        CaveProp(D, "vintage_suitcase", At(-1.3f, 0.9f), Rand(0f, 360f), new Vector3(0.7f, 0.45f, 0.7f), true);
        CaveProp(D, "pot_enamel_01", At(0.55f, -0.85f), Rand(0f, 360f), new Vector3(0.25f, 0.16f, 0.25f));
        CaveProp(D, "modified_thermos", At(-0.4f, 1.25f), Rand(0f, 360f), new Vector3(0.12f, 0.32f, 0.12f));
        CaveProp(D, "wooden_candlestick", At(-1.05f, -0.15f), 0f, new Vector3(0.12f, 0.3f, 0.12f));
        CaveProp(D, "small_plastic_torch", At(1.7f, -0.6f), Rand(0f, 360f), new Vector3(0.22f, 0.22f, 0.22f)); // lampe tombée
        CaveProp(D, "hatchet", At(0.9f, 1.4f), Rand(0f, 360f), new Vector3(0.4f, 0.4f, 0.4f));

        var lightPos = lantern != null && TryBounds(lantern, out var lb) ? lb.center : c + new Vector3(1.1f, 0.3f, 0.5f);
        var l = PointLight(D, lightPos, 7f, D.props);
        l.color = new Color(1f, 0.65f, 0.3f); l.intensity = 1.3f;
        l.gameObject.AddComponent<LightFlicker>().strength = 0.35f;

        PlaceItem(D, NextCaveNoteId(), At(-1.2f, -0.7f), Rand(0f, 360f));
        PlaceItem(D, "pile", At(0.9f, -1.1f), Rand(0f, 360f));
        BonePile(D, n.c + new Vector3(Rand(-4f, 4f), 0f, Rand(-4f, 4f)));
        Marker(D, "Campement_Disparus", c, Color.yellow);
        if (D.markers.Find("◆ Vue_Grotte_Campement") == null)
            CaveView(D, "Vue_Grotte_Campement", c + new Vector3(2.6f, 0f, 2.2f), c + Vector3.down * 0.6f);
    }

    /// Mousse au sol et racines qui sortent des parois de la forêt souterraine.
    static void DecorateForestGround(LevelDef D, CaveNode n, float r)
    {
        for (int k = 0; k < 10; k++)
        {
            float a = Rand(0f, Mathf.PI * 2f), d = Rand(0.1f, 0.75f) * r;
            if (CaveFloor(n.c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d), out var f))
                CaveProp(D, "moss_01", f.point, Rand(0f, 360f), new Vector3(Rand(1f, 1.8f), 0.25f, Rand(1f, 1.8f)));
        }
        for (int k = 0; k < 4; k++)
        {
            if (!CaveWall(n.c + Vector3.down * (n.radii.y * 0.4f), out var w)) continue;
            if (CaveFloor(w.point + w.normal * 0.8f + Vector3.up, out var f))
                CaveProp(D, "root_cluster_01", f.point, Rand(0f, 360f), new Vector3(2.4f, 1.6f, 2.4f));
        }
    }

    /// Gros rochers éboulés dans les salles ordinaires (pas dans le lac, la forêt ni les campements).
    static void ScatterBoulders(LevelDef D, List<CaveNode> nodes)
    {
        string[] ids = { "namaqualand_boulder_02", "namaqualand_boulder_05", "boulder_01", "rock_07", "rock_09" };
        int placed = 0;
        foreach (var n in nodes)
        {
            if (n.role != CaveRole.Normal && n.role != CaveRole.Coeur) continue;
            int count = _rng.Next(1, 4);
            for (int k = 0; k < count; k++)
            {
                float rr = Mathf.Min(n.radii.x, n.radii.z) * 0.65f;
                var p = n.c + new Vector3(Rand(-rr, rr), 0f, Rand(-rr, rr));
                if (!CaveFloor(p, out var f) || f.normal.y < 0.7f) continue;
                float s = Rand(0.7f, 2.4f);
                CaveProp(D, ids[_rng.Next(ids.Length)], f.point + Vector3.down * (s * 0.12f), Rand(0f, 360f), new Vector3(s, s * 0.8f, s), s > 1f);
                placed++;
            }
        }
        Debug.Log($"[EchoesMap] Grottes : {placed} rochers posés.");
    }

    // ───────────────────────── Maillages fabriqués ─────────────────────────

    static Mesh SaveCaveMesh(Mesh m)
    {
        m.RecalculateNormals();
        m.RecalculateBounds();
        if (AssetDatabase.LoadAssetAtPath<Object>(CaveMeshPath) != null) AssetDatabase.AddObjectToAsset(m, CaveMeshPath);
        return m;
    }

    /// Surface de révolution autour de Y à partir d'un profil (rayon, hauteur), normales vers l'extérieur.
    static Mesh Lathe(string name, Vector2[] profile, int seg)
    {
        var v = new List<Vector3>();
        var t = new List<int>();
        foreach (var pt in profile)
            for (int s = 0; s < seg; s++)
            {
                float a = s * Mathf.PI * 2f / seg;
                v.Add(new Vector3(Mathf.Cos(a) * pt.x, pt.y, Mathf.Sin(a) * pt.x));
            }
        for (int i = 0; i < profile.Length - 1; i++)
            for (int s = 0; s < seg; s++)
            {
                int a = i * seg + s, b = i * seg + (s + 1) % seg, c = (i + 1) * seg + s, d = (i + 1) * seg + (s + 1) % seg;
                t.Add(a); t.Add(c); t.Add(d);
                t.Add(a); t.Add(d); t.Add(b);
            }
        var m = new Mesh { name = name };
        m.SetVertices(v);
        m.SetTriangles(t, 0);
        return SaveCaveMesh(m);
    }

    static void EnsureCavePropMeshes()
    {
        if (_mushStem != null) return;
        _mushStem = Lathe("Champignon_Pied", new[] {
            new Vector2(0.075f, 0f), new Vector2(0.06f, 0.25f), new Vector2(0.05f, 0.6f), new Vector2(0.055f, 0.9f), new Vector2(0.065f, 1f), new Vector2(0f, 1.01f) }, 10);
        // Chapeau bombé : dessous (lamelles) vers l'extérieur, bord, puis dôme
        _mushCapRound = Lathe("Champignon_Chapeau_Rond", new[] {
            new Vector2(0.06f, -0.01f), new Vector2(0.25f, 0.03f), new Vector2(0.44f, 0.01f), new Vector2(0.49f, 0.06f),
            new Vector2(0.46f, 0.14f), new Vector2(0.37f, 0.22f), new Vector2(0.2f, 0.28f), new Vector2(0f, 0.3f) }, 18);
        // Chapeau conique (mycène)
        _mushCapCone = Lathe("Champignon_Chapeau_Conique", new[] {
            new Vector2(0.05f, 0f), new Vector2(0.26f, 0.02f), new Vector2(0.33f, 0.05f), new Vector2(0.27f, 0.16f),
            new Vector2(0.15f, 0.32f), new Vector2(0.05f, 0.42f), new Vector2(0f, 0.44f) }, 14);
        // Os long : têtes renflées aux deux bouts, diaphyse fine
        _boneMesh = Lathe("Os_Long", new[] {
            new Vector2(0f, -0.02f), new Vector2(0.07f, 0f), new Vector2(0.1f, 0.04f), new Vector2(0.085f, 0.09f), new Vector2(0.045f, 0.16f),
            new Vector2(0.035f, 0.5f), new Vector2(0.045f, 0.84f), new Vector2(0.085f, 0.91f), new Vector2(0.1f, 0.96f), new Vector2(0.07f, 1f), new Vector2(0f, 1.02f) }, 10);
        _crystalMesh = CrystalMesh();
    }

    /// Prisme hexagonal légèrement effilé terminé par une pointe, facettes nettes (sommets non partagés).
    static Mesh CrystalMesh()
    {
        var v = new List<Vector3>();
        var t = new List<int>();
        Vector3 P(float r, float y, int k) { float a = k * Mathf.PI / 3f; return new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r); }
        var apex = new Vector3(0f, 1.45f, 0f);
        void Tri(Vector3 a, Vector3 b, Vector3 c) { int i = v.Count; v.Add(a); v.Add(b); v.Add(c); t.Add(i); t.Add(i + 1); t.Add(i + 2); }
        for (int k = 0; k < 6; k++)
        {
            Vector3 a = P(1f, 0f, k), b = P(1f, 0f, k + 1), c = P(0.86f, 1f, k), d = P(0.86f, 1f, k + 1);
            Tri(a, c, d); Tri(a, d, b);   // flanc
            Tri(c, apex, d);              // pointe
        }
        var m = new Mesh { name = "Cristal" };
        m.SetVertices(v);
        m.SetTriangles(t, 0);
        return SaveCaveMesh(m);
    }

    static GameObject MeshObject(Transform p, string name, Mesh mesh, Material mat, Vector3 pos, Quaternion rot, Vector3 scale, bool shadows)
    {
        var go = new GameObject(name);
        go.transform.SetParent(p, false);
        go.transform.SetPositionAndRotation(pos, rot);
        go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        go.layer = _layerFurniture;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        return go;
    }

    // ───────────────────────── Champignons, cristaux, ossements ─────────────────────────

    static void MushroomCluster(LevelDef D, Vector3 around)
    {
        if (!CaveWall(around, out var w)) return;
        var nearWall = w.point + w.normal * Rand(0.4f, 1f);
        if (!CaveFloor(nearWall + Vector3.up * 0.5f, out var f)) return;
        EnsureCavePropMeshes();
        var stem = Mat("Champignon_Pied", new Color(0.75f, 0.78f, 0.8f), 0.3f);
        var cap = GlowMat("Champignon_Chapeau", new Color(0.25f, 0.85f, 1f), 2.2f);
        var parent = Child(D.props, "Champignons");
        bool cone = _rng.NextDouble() < 0.4; // une touffe = une seule espèce
        int n = _rng.Next(5, 12);
        for (int k = 0; k < n; k++)
        {
            var p = f.point + new Vector3(Rand(-0.8f, 0.8f), -0.01f, Rand(-0.8f, 0.8f));
            float hgt = Rand(0.1f, 0.42f) * (cone ? 1.2f : 1f), capR = hgt * Rand(0.45f, 0.75f);
            var tilt = Quaternion.Euler(Rand(-14f, 14f), Rand(0f, 360f), Rand(-14f, 14f));
            float stemW = capR * Rand(3f, 4.5f);
            MeshObject(parent, "Pied", _mushStem, stem, p, tilt, new Vector3(stemW, hgt, stemW), false); // pied ≈ 1/5 du chapeau
            float cs = capR / 0.49f;
            MeshObject(parent, "Chapeau", cone ? _mushCapCone : _mushCapRound, cap, p + tilt * Vector3.up * hgt * 0.97f, tilt,
                new Vector3(cs, cs * Rand(0.8f, 1.2f), cs), false);
        }
        var l = PointLight(D, f.point + Vector3.up * 0.6f, 5.5f, parent);
        l.color = new Color(0.3f, 0.85f, 1f); l.intensity = 1.3f;
    }

    static void CrystalCluster(LevelDef D, Vector3 around, int count, float scale)
    {
        if (!CaveWall(around, out var w)) return;
        EnsureCavePropMeshes();
        var m = GlowMat("Cristal", new Color(0.65f, 0.35f, 1f), 1.8f);
        var parent = Child(D.props, "Cristaux");
        for (int k = 0; k < count; k++)
        {
            var dir = (w.normal + Random.insideUnitSphere * 0.6f).normalized;
            float len = Rand(0.4f, 1.4f) * scale, th = len * Rand(0.1f, 0.18f);
            var p = w.point + Vector3.ProjectOnPlane(Random.insideUnitSphere * 0.7f * scale, w.normal) - dir * (len * 0.15f); // pied enfoncé dans la roche
            var rot = Quaternion.FromToRotation(Vector3.up, dir) * Quaternion.Euler(0f, Rand(0f, 60f), 0f);
            MeshObject(parent, "Cristal", _crystalMesh, m, p, rot, new Vector3(th * Rand(0.85f, 1.15f), len / 1.45f, th * Rand(0.85f, 1.15f)), false);
        }
        var l = PointLight(D, w.point + w.normal * 1.2f, 6f * scale, parent);
        l.color = new Color(0.6f, 0.35f, 1f); l.intensity = 1.2f;
    }

    static void BonePile(LevelDef D, Vector3 around)
    {
        if (!CaveFloor(around, out var f)) return;
        EnsureCavePropMeshes();
        var bone = Mat("Ossements", new Color(0.78f, 0.74f, 0.64f), 0.25f);
        var parent = Child(D.props, "Ossements");
        int n = _rng.Next(5, 10);
        for (int k = 0; k < n; k++)
        {
            float len = Rand(0.18f, 0.46f);
            bool broken = _rng.NextDouble() < 0.3;
            var rot = Quaternion.Euler(0f, Rand(0f, 360f), 0f) * Quaternion.Euler(90f + Rand(-6f, 6f), 0f, 0f); // couché
            var pos = f.point + new Vector3(Rand(-0.6f, 0.6f), len * 0.09f, Rand(-0.6f, 0.6f)) - rot * Vector3.up * (len * 0.5f);
            MeshObject(parent, broken ? "Os_Brise" : "Os", _boneMesh, bone, pos, rot, new Vector3(len, broken ? len * 0.55f : len, len), false);
        }
    }
}
