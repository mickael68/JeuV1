using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Parc de la résidence (dans la cour clôturée) : chemin pavé qui fait tout le tour des bâtiments,
/// allées vers la cour intérieure, le lac et l'aire de pique-nique ; bancs, lampadaires, poubelles ;
/// mini-lac creusé (berges, pierres, roseaux, ponton) ; aire de pique-nique avec parasols ;
/// arbres, haies, massifs fleuris.
/// </summary>
public static partial class EchoesMapGenerator
{
    // Boucle du chemin (axes) et largeur
    const float LoopX0 = -32f, LoopX1 = 32f, LoopZ0 = -18f, LoopZ1 = 31f, PathW = 2.4f;
    // Mini-lac (ellipse) et aire de pique-nique
    static readonly Vector2 LakeC = new Vector2(16f, -29f);
    static readonly Vector2 LakeR = new Vector2(8.5f, 5.5f);
    const float LakeDepth = 2.0f, LakeWater = 0.25f;
    static readonly Rect PicnicRect = Rect.MinMaxRect(-27f, -34.5f, -9f, -22.5f);
    static Rect LakeBasin => Rect.MinMaxRect(LakeC.x - LakeR.x - 1.5f, LakeC.y - LakeR.y - 1.5f, LakeC.x + LakeR.x + 1.5f, LakeC.y + LakeR.y + 1.5f);

    static List<Rect> _parkBusy;

    static bool ParkFree(Vector2 p, float margin)
    {
        if (!YardRect.Contains(p) || p.x < YardRect.xMin + 1.5f || p.x > YardRect.xMax - 1.5f || p.y < YardRect.yMin + 1.5f || p.y > YardRect.yMax - 1.5f) return false;
        foreach (var r in _parkBusy)
            if (p.x > r.xMin - margin && p.x < r.xMax + margin && p.y > r.yMin - margin && p.y < r.yMax + margin) return false;
        return true;
    }

    static void BuildPark(Transform ext, LevelDef X, float gy)
    {
        var root = Child(ext, "Parc");
        var P = new LevelDef { number = 1, y = gy, h = 4f, root = root, props = Child(root, "Props"), markers = X.markers, lightColor = X.lightColor, lightIntensity = X.lightIntensity };
        var paving = Mat("Parc_Allee", Color.white);

        // Zones occupées (bâtiments, cages, entrée, parkings…) : rien n'y pousse
        _parkBusy = new List<Rect>
        {
            Rect.MinMaxRect(-HalfX - 2f, CorrS - RoomDepth - 2.5f, HalfX + 2f, CorrN + RoomDepth + 2.5f),
            Rect.MinMaxRect(WX0 - 2f, 4f, WX1 + 2f, WZ1 + 2f),
            Rect.MinMaxRect(HalfX, ExitZ - 2.5f, YardRect.xMax, ExitZ + 2.5f),           // allée d'entrée
            Rect.MinMaxRect(HalfX + 2f, CorrS - RoomDepth, HalfX + 7f, CorrS - RoomDepth + 3.5f), // vélos
            Rect.MinMaxRect(HalfX + 2f, CorrN + RoomDepth - 3.5f, HalfX + 7f, CorrN + RoomDepth),  // poubelles
            LakeBasin, PicnicRect,
        };
        foreach (var r in _shaftRects) _parkBusy.Add(Rect.MinMaxRect(r.xMin - 2f, r.yMin - 2f, r.xMax + 2f, r.yMax + 2f));

        // 1) Chemins : boucle + allées
        var paths = new List<Rect>
        {
            Rect.MinMaxRect(LoopX0 - PathW / 2, LoopZ0 - PathW / 2, LoopX1 + PathW / 2, LoopZ0 + PathW / 2),
            Rect.MinMaxRect(LoopX0 - PathW / 2, LoopZ1 - PathW / 2, LoopX1 + PathW / 2, LoopZ1 + PathW / 2),
            Rect.MinMaxRect(LoopX0 - PathW / 2, LoopZ0 + PathW / 2, LoopX0 + PathW / 2, LoopZ1 - PathW / 2),
            Rect.MinMaxRect(LoopX1 - PathW / 2, LoopZ0 + PathW / 2, LoopX1 + PathW / 2, LoopZ1 - PathW / 2),
            Rect.MinMaxRect(WX1 + 0.4f, 7.2f, LoopX1 - PathW / 2, 9.4f),                          // cour intérieure → boucle est
            Rect.MinMaxRect(LakeC.x - 1f, LakeC.y + LakeR.y + 0.6f, LakeC.x + 1f, LoopZ0 - PathW / 2),     // boucle → lac
            Rect.MinMaxRect(PicnicRect.center.x - 1f, PicnicRect.yMax, PicnicRect.center.x + 1f, LoopZ0 - PathW / 2), // boucle → pique-nique
            Rect.MinMaxRect(HalfX + 2.4f, CorrS - RoomDepth - 1.6f, HalfX + 3.6f, ExitZ - 1.1f),  // perron → vélos
        };
        foreach (var r in paths)
        {
            Box(root, "Chemin", new Vector3(r.xMin, gy, r.yMin), new Vector3(r.xMax, gy + 0.035f, r.yMax), paving, _layerGround);
            _parkBusy.Add(Rect.MinMaxRect(r.xMin - 0.6f, r.yMin - 0.6f, r.xMax + 0.6f, r.yMax + 0.6f));
        }
        // Bordures claires le long de la boucle
        var curb = Mat("Facade_Bandeau", new Color(0.82f, 0.8f, 0.75f));
        foreach (var s in new[] { -1f, 1f })
        {
            Cube(root, "Bordure", new Vector3(0f, gy + 0.03f, LoopZ0 + s * (PathW / 2 + 0.06f)), new Vector3(LoopX1 - LoopX0 + PathW, 0.06f, 0.1f), Quaternion.identity, curb, false, -1);
            Cube(root, "Bordure", new Vector3(0f, gy + 0.03f, LoopZ1 + s * (PathW / 2 + 0.06f)), new Vector3(LoopX1 - LoopX0 + PathW, 0.06f, 0.1f), Quaternion.identity, curb, false, -1);
        }
        // Cour intérieure pavée
        Box(root, "Cour_Pavee", new Vector3(WX0, gy, CorrN + RoomDepth + 0.3f), new Vector3(WX1, gy + 0.03f, WZ0 - 0.3f), paving, _layerGround);

        // 2) Bancs, lampadaires, poubelles le long de la boucle (côté extérieur)
        int benches = 0;
        void BenchAt(Vector3 p, float yaw)
        {
            var b = Piece(P, MP("Chairs", "Chair40", "Chair39"), KGraveyard + "bench", p, yaw, F(1.7f, 0.9f, 0.65f, yaw), Mat("Exterieur_Allee", Color.white));
            if (b != null) { AddSeat(b, 0.45f, yaw, Vector3.zero, "S'asseoir sur le banc"); benches++; }
        }
        float o = PathW / 2 + 0.75f;
        for (float x = LoopX0 + 7f; x < LoopX1 - 4f; x += 12f)
        {
            if (ParkFreeLine(new Vector2(x, LoopZ0 - o))) BenchAt(new Vector3(x, gy, LoopZ0 - o), 0f);
            if (ParkFreeLine(new Vector2(x + 6f, LoopZ1 + o))) BenchAt(new Vector3(x + 6f, gy, LoopZ1 + o), 180f);
        }
        for (float z = LoopZ0 + 8f; z < LoopZ1 - 4f; z += 11f)
        {
            if (Mathf.Abs(z - ExitZ) > 4f) BenchAt(new Vector3(LoopX1 + o, gy, z), -90f);
            BenchAt(new Vector3(LoopX0 - o, gy, z + 4f), 90f);
        }
        foreach (var c in new[] { new Vector2(LoopX0, LoopZ0), new Vector2(LoopX1, LoopZ0), new Vector2(LoopX0, LoopZ1), new Vector2(LoopX1, LoopZ1) })
            StreetLamp(P, new Vector3(c.x + Mathf.Sign(c.x) * 1.8f, gy, c.y + Mathf.Sign(c.y) * 1.8f));
        for (float x = LoopX0 + 13f; x < LoopX1 - 8f; x += 16f)
        {
            StreetLamp(P, new Vector3(x, gy, LoopZ0 + PathW / 2 + 0.6f));
            StreetLamp(P, new Vector3(x + 4f, gy, LoopZ1 - PathW / 2 - 0.6f));
        }
        foreach (var z in new[] { 0f, 18f })
        {
            StreetLamp(P, new Vector3(LoopX0 + PathW / 2 + 0.6f, gy, z));
            if (Mathf.Abs(z - ExitZ) > 3f) StreetLamp(P, new Vector3(LoopX1 - PathW / 2 - 0.6f, gy, z));
        }
        foreach (var p in new[] { new Vector3(LoopX0 + 3f, 0f, LoopZ0 - o), new Vector3(LoopX1 - 3f, 0f, LoopZ1 + o), new Vector3(LoopX1 + o, 0f, LoopZ0 + 4f) })
            Model(P, KFurniture + "trashcan", new Vector3(p.x, gy, p.z), 0f, new Vector3(0.45f, 0.8f, 0.45f), curb);

        // 3) Mini-lac, 4) pique-nique
        BuildLake(root, P, gy);
        BuildPicnic(root, P, gy);

        // 5) Végétation : haies, massifs fleuris, arbres
        int trees = PlantPark(root, P, gy);

        // Vues de contrôle
        CaveView(X, "Vue_Parc_Lac", new Vector3(LakeC.x - LakeR.x - 2f, gy, LakeC.y + LakeR.y + 2.5f), new Vector3(LakeC.x + 2f, gy - 0.5f, LakeC.y - 1f));
        CaveView(X, "Vue_Parc_Pique_Nique", new Vector3(PicnicRect.xMin + 0.4f, gy, PicnicRect.yMin + 0.4f), new Vector3(PicnicRect.center.x + 2f, gy, PicnicRect.center.y + 1f));
        CaveView(X, "Vue_Parc_Chemin", new Vector3(LoopX0, gy, LoopZ0 - 0.2f), new Vector3(LoopX0, gy, LoopZ1));
        Debug.Log($"[EchoesMap] Parc : boucle de {2 * (LoopX1 - LoopX0) + 2 * (LoopZ1 - LoopZ0):0} m, {benches} bancs, {trees} arbres, mini-lac, aire de pique-nique.");
    }

    static bool ParkFreeLine(Vector2 p) => !LakeBasin.Contains(p) && !PicnicRect.Contains(p) && Mathf.Abs(p.x) > 1f;

    // ───────────────────────── Mini-lac ─────────────────────────

    static float LakeE(float x, float z) => Mathf.Sqrt(Mathf.Pow((x - LakeC.x) / LakeR.x, 2f) + Mathf.Pow((z - LakeC.y) / LakeR.y, 2f));

    static void BuildLake(Transform root, LevelDef P, float gy)
    {
        // Cuvette en terre (maillage polaire, bord lisse) jusqu'à e = 1,15, légèrement sous la pelouse au bord
        var r = LakeBasin;
        const int A = 160;
        float[] rings = { 0f, 0.2f, 0.4f, 0.55f, 0.68f, 0.78f, 0.86f, 0.92f, 0.96f, 0.99f, 1.02f, 1.06f, 1.1f, 1.15f };
        int R = rings.Length;
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var mud = new List<int>();
        for (int s = 0; s < A; s++)
        {
            float a = s * Mathf.PI * 2f / A;
            foreach (var e in rings)
            {
                float x = LakeC.x + Mathf.Cos(a) * LakeR.x * e, z = LakeC.y + Mathf.Sin(a) * LakeR.y * e;
                float d = e >= 1f ? 0.03f : Mathf.Max(0.03f, LakeDepth * Mathf.Pow(1f - e * e, 0.7f));
                v.Add(new Vector3(x, gy - d, z)); uv.Add(new Vector2(x, z));
            }
        }
        for (int s = 0; s < A; s++)
        {
            int s2 = (s + 1) % A;
            for (int k2 = 0; k2 < R - 1; k2++)
            {
                int a0 = s * R + k2, a1 = a0 + 1, b0 = s2 * R + k2, b1 = b0 + 1;
                mud.Add(a0); mud.Add(b0); mud.Add(a1); mud.Add(b0); mud.Add(b1); mud.Add(a1); // sens horaire vu du dessus
            }
        }
        var mesh = new Mesh { name = "Lac_Cuvette" };
        mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(mud, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.RecalculateTangents();
        AssetDatabase.AddObjectToAsset(mesh, _boxContainer);
        var go = new GameObject("Lac_Cuvette");
        go.transform.SetParent(root, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = Mat("Lac_Berge", new Color(0.32f, 0.27f, 0.2f));
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
        // Pelouse autour (dans le trou rectangulaire) : fines bandes qui épousent l'ellipse et recouvrent un peu la berge
        var lawn = Mat("Exterieur_Herbe", Color.white);
        const float band = 0.15f, E = 1.12f;
        for (float z0 = r.yMin; z0 < r.yMax - 0.001f; z0 += band)
        {
            float z1 = Mathf.Min(r.yMax, z0 + band);
            float dz = Mathf.Min(Mathf.Abs(z0 - LakeC.y), Mathf.Abs(z1 - LakeC.y));
            if (z0 < LakeC.y && z1 > LakeC.y) dz = 0f;
            float q = dz / (E * LakeR.y);
            if (q >= 1f) { Box(root, "Pelouse_Lac", new Vector3(r.xMin, gy - Slab, z0), new Vector3(r.xMax, gy, z1), lawn, _layerGround); continue; }
            float hx = E * LakeR.x * Mathf.Sqrt(1f - q * q);
            Box(root, "Pelouse_Lac", new Vector3(r.xMin, gy - Slab, z0), new Vector3(LakeC.x - hx, gy, z1), lawn, _layerGround);
            Box(root, "Pelouse_Lac", new Vector3(LakeC.x + hx, gy - Slab, z0), new Vector3(r.xMax, gy, z1), lawn, _layerGround);
        }
        go.layer = _layerGround;

        // Eau
        float k = Mathf.Sqrt(1f - Mathf.Pow(LakeWater / LakeDepth, 1f / 0.7f)); // e où la berge croise la surface
        var water = Cylinder(root, "Eau_Lac", new Vector3(LakeC.x, gy - LakeWater, LakeC.y), 1f, 0.02f, Quaternion.identity, WaterMat("Eau_Mini_Lac", new Color(0.55f, 0.62f, 0.6f), new Color(0.03f, 0.07f, 0.08f), 0.7f, 0f, 0.22f, 0.9f, 0.1f), false);
        water.transform.localScale = new Vector3(LakeR.x * 2f * k, 0.01f, LakeR.y * 2f * k);
        water.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        water.AddComponent<Unity.AI.Navigation.NavMeshModifier>().ignoreFromBuild = true;
        WaterVolumeBox(root, "Lac_Patauger", new Vector3(LakeC.x, gy - 0.5f, LakeC.y), new Vector3(LakeR.x * 1.6f * k, 1.2f, LakeR.y * 1.6f * k), Quaternion.identity, 0.55f);
        WaterSound(root, new Vector3(LakeC.x, gy, LakeC.y), SndRiver, 0.12f, 18f);

        // Pierres et roseaux sur la berge, ponton en bois au nord
        int n = 44;
        for (int s = 0; s < n; s++)
        {
            float a = s * Mathf.PI * 2f / n + Rand(-0.08f, 0.08f);
            float e = Rand(0.95f, 1.12f);
            var p = new Vector3(LakeC.x + Mathf.Cos(a) * LakeR.x * e, gy, LakeC.y + Mathf.Sin(a) * LakeR.y * e);
            if (Mathf.Abs(p.x - LakeC.x) < 1.6f && p.z > LakeC.y) continue; // le ponton
            if (s % 2 == 0)
            {
                float sc = Rand(0.5f, 1.1f);
                Model(P, _rng.NextDouble() < 0.5 ? KSurvival + "rock-a" : KSurvival + "rock-b", p + Vector3.down * 0.15f, Rand(0f, 360f), new Vector3(sc, sc * 0.6f, sc), Mat("Lac_Berge", Color.gray), false);
            }
            else if (AssetDatabase.LoadAssetAtPath<GameObject>(Foliage + "grass 1.prefab") != null)
            {
                float sc = Rand(0.7f, 1.2f);
                var reed = InstantiateAt(P, Foliage + "grass 1.prefab", p, Rand(0f, 360f), new Vector3(sc, sc * 1.2f, sc));
                SetupRealTree(reed, false);
            }
        }
        var wood = Mat("Ponton_Bois", new Color(0.5f, 0.4f, 0.3f));
        float pz0 = LakeC.y + LakeR.y * 0.55f, pz1 = LakeC.y + LakeR.y + 0.7f;
        Box(root, "Ponton", new Vector3(LakeC.x - 0.9f, gy - 0.05f, pz0), new Vector3(LakeC.x + 0.9f, gy + 0.08f, pz1), wood, _layerGround);
        foreach (var dx in new[] { -0.8f, 0.8f })
            foreach (var z in new[] { pz0 + 0.1f, (pz0 + pz1) * 0.5f })
                Cylinder(root, "Pilotis", new Vector3(LakeC.x + dx, gy - 0.6f, z), 0.08f, 1.2f, Quaternion.identity, wood, false);
        var bench = Piece(P, MP("Chairs", "Chair39"), KGraveyard + "bench", new Vector3(LakeC.x, gy + 0.08f, pz0 + 0.9f), 180f, F(1.6f, 0.9f, 0.6f, 180f), wood);
        if (bench != null) AddSeat(bench, 0.45f, 180f, Vector3.zero, "S'asseoir face au lac");
        StreetLamp(P, new Vector3(LakeC.x + 1.6f, gy, pz1 + 0.4f));
    }

    // ───────────────────────── Pique-nique ─────────────────────────

    static void BuildPicnic(Transform root, LevelDef P, float gy)
    {
        var r = PicnicRect;
        Box(root, "Aire_Pique_Nique", new Vector3(r.xMin, gy, r.yMin), new Vector3(r.xMax, gy + 0.03f, r.yMax), Mat("Parc_Gravier", Color.white), _layerGround);
        var wood = Mat("Ponton_Bois", new Color(0.5f, 0.4f, 0.3f));
        var canvas = new[] { Mat("Parasol_Rouge", new Color(0.65f, 0.15f, 0.12f), 0.2f), Mat("Parasol_Creme", new Color(0.85f, 0.8f, 0.68f), 0.2f) };
        int k = 0;
        foreach (var cz in new[] { r.yMin + 3.2f, r.yMax - 3.2f })
            foreach (var cx in new[] { r.xMin + 3.5f, r.center.x, r.xMax - 3.5f })
            {
                var c = new Vector3(cx + Rand(-0.3f, 0.3f), gy, cz + Rand(-0.3f, 0.3f));
                Piece(P, MP("Tables", "Table46", "Table45"), KFurniture + "table", c, 0f, F(1.9f, 0.76f, 0.85f, 0f), wood);
                foreach (var s in new[] { -1f, 1f })
                {
                    var b = Piece(P, MP("Chairs", "Chair39"), KGraveyard + "bench", c + new Vector3(0f, 0f, s * 0.95f), s < 0 ? 0f : 180f, F(1.8f, 0.85f, 0.55f, 0f), wood);
                    if (b != null) AddSeat(b, 0.45f, s < 0 ? 0f : 180f, Vector3.zero, "S'asseoir à la table");
                }
                // Parasol
                Cylinder(root, "Parasol_Mat", c + Vector3.up * 1.2f, 0.03f, 2.4f, Quaternion.identity, Mat("Grille_Metal", new Color(0.18f, 0.18f, 0.2f), 0.5f), false);
                var top = Cylinder(root, "Parasol_Toile", c + Vector3.up * 2.35f, 1.3f, 0.08f, Quaternion.identity, canvas[k++ % 2], false);
                top.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
            }
        Model(P, KFurniture + "trashcan", new Vector3(r.xMax - 0.6f, gy, r.center.y), 0f, new Vector3(0.45f, 0.8f, 0.45f), wood);
        Model(P, KFurniture + "trashcan", new Vector3(r.xMin + 0.6f, gy, r.center.y), 0f, new Vector3(0.45f, 0.8f, 0.45f), wood);
        CaveProp(P, "stone_fire_pit", new Vector3(r.xMin - 2.5f, gy, r.center.y), 0f, new Vector3(1.4f, 0.4f, 1.4f), true); // barbecue en pierre
        StreetLamp(P, new Vector3(r.center.x - 4f, gy, r.center.y));
        StreetLamp(P, new Vector3(r.center.x + 4f, gy, r.center.y));
    }

    // ───────────────────────── Végétation ─────────────────────────

    static int PlantPark(Transform root, LevelDef P, float gy)
    {
        bool hasBush = AssetDatabase.LoadAssetAtPath<GameObject>(BushLarge) != null;
        // Haies le long de l'intérieur de la clôture (nord, ouest, sud)
        if (hasBush)
        {
            void Hedge(Vector2 a, Vector2 b)
            {
                float len = Vector2.Distance(a, b);
                for (float t = 0f; t < len; t += 1.3f)
                {
                    var p = Vector2.Lerp(a, b, t / len);
                    if (!ParkFree(p, 0.3f)) continue;
                    float s = Rand(1.0f, 1.4f);
                    SetupRealTree(InstantiateAt(P, BushLarge, new Vector3(p.x, gy, p.y), Rand(0f, 360f), new Vector3(s * 1.6f, s * 1.1f, s * 1.6f)), false);
                }
            }
            Hedge(new Vector2(YardRect.xMin + 2f, YardRect.yMax - 2f), new Vector2(YardRect.xMax - 2f, YardRect.yMax - 2f));
            Hedge(new Vector2(YardRect.xMin + 2f, YardRect.yMin + 2f), new Vector2(YardRect.xMin + 2f, YardRect.yMax - 2f));
            Hedge(new Vector2(YardRect.xMin + 2f, YardRect.yMin + 2f), new Vector2(YardRect.xMax - 2f, YardRect.yMin + 2f));
        }

        // Massifs fleuris ronds aux coins de la boucle et au bord des allées
        var soil = Mat("Massif_Terre", new Color(0.3f, 0.24f, 0.18f));
        var stone = Mat("Facade_Bandeau", new Color(0.82f, 0.8f, 0.75f));
        bool hasFlower = AssetDatabase.LoadAssetAtPath<GameObject>(Foliage + "dandelion.prefab") != null;
        foreach (var c in new[] { new Vector2(LoopX0 + 4.5f, LoopZ0 + 4.5f), new Vector2(LoopX1 - 4.5f, LoopZ0 + 4.5f), new Vector2(LoopX0 + 4.5f, LoopZ1 - 4.5f),
                                  new Vector2(LoopX1 - 4.5f, LoopZ1 - 4.5f), new Vector2(-24f, 14f), new Vector2(26f, 18f), new Vector2(-26f, -12.5f) })
        {
            if (!ParkFree(c, 1.6f)) continue;
            float rad = Rand(1.4f, 2.0f);
            Cylinder(root, "Massif_Rond", new Vector3(c.x, gy + 0.05f, c.y), rad, 0.1f, Quaternion.identity, soil, false);
            Cylinder(root, "Bordure_Massif", new Vector3(c.x, gy + 0.04f, c.y), rad + 0.12f, 0.08f, Quaternion.identity, stone, false);
            _parkBusy.Add(new Rect(c.x - rad, c.y - rad, rad * 2f, rad * 2f));
            for (int i = 0; i < 16; i++)
            {
                float a = Rand(0f, Mathf.PI * 2f), d = Mathf.Sqrt(Rand(0f, 1f)) * (rad - 0.25f);
                var p = new Vector3(c.x + Mathf.Cos(a) * d, gy + 0.1f, c.y + Mathf.Sin(a) * d);
                if (i < 3 && hasBush) SetupRealTree(InstantiateAt(P, BushSmall, p, Rand(0f, 360f), Vector3.one * Rand(0.6f, 0.9f)), false);
                else if (hasFlower) SetupRealTree(InstantiateAt(P, Foliage + "dandelion.prefab", p, Rand(0f, 360f), Vector3.one * Rand(0.25f, 0.45f)), false);
            }
        }

        // Arbres : alignés le long de la boucle, puis en bosquets sur les pelouses
        string[] trees = { Foliage + "maple.prefab", Foliage + "apple.prefab", Foliage + "lucious tree.prefab", Foliage + "maple1.prefab", Foliage + "Conifer.prefab" };
        int placed = 0;
        void Tree(Vector2 p, float minH, float maxH)
        {
            if (!ParkFree(p, 1.2f)) return;
            string t = trees[_rng.Next(trees.Length)];
            if (AssetDatabase.LoadAssetAtPath<GameObject>(t) == null) return;
            float h = Rand(minH, maxH);
            var go = InstantiateAt(P, t, new Vector3(p.x, gy, p.y), Rand(0f, 360f), new Vector3(h * 0.75f, h, h * 0.75f));
            SetupRealTree(go, true);
            _parkBusy.Add(new Rect(p.x - 1.5f, p.y - 1.5f, 3f, 3f));
            placed++;
            if (hasBush && _rng.NextDouble() < 0.5)
            {
                var b = p + new Vector2(Rand(-1.8f, 1.8f), Rand(-1.8f, 1.8f));
                if (ParkFree(b, 0.5f)) SetupRealTree(InstantiateAt(P, _rng.NextDouble() < 0.5 ? BushSmall : BushLarge, new Vector3(b.x, gy, b.y), Rand(0f, 360f), Vector3.one * Rand(0.8f, 1.3f)), false);
            }
        }
        for (float x = LoopX0 + 3f; x < LoopX1 - 2f; x += 7f)
        {
            Tree(new Vector2(x + Rand(-0.5f, 0.5f), LoopZ0 - PathW / 2 - 2.6f), 7f, 10f);
            Tree(new Vector2(x + 3.5f + Rand(-0.5f, 0.5f), LoopZ1 - PathW / 2 - 2.4f), 6f, 9f);
        }
        for (float z = LoopZ0 + 3f; z < LoopZ1 - 2f; z += 7f)
        {
            Tree(new Vector2(LoopX0 - PathW / 2 - 2.6f, z + Rand(-0.5f, 0.5f)), 7f, 11f);
            Tree(new Vector2(LoopX1 + PathW / 2 + 2.6f, z + 3.5f + Rand(-0.5f, 0.5f)), 7f, 11f);
        }
        for (int tries = 0; tries < 400 && placed < 70; tries++)
            Tree(new Vector2(Rand(YardRect.xMin + 3f, YardRect.xMax - 3f), Rand(YardRect.yMin + 3f, YardRect.yMax - 3f)), 6f, 12f);
        return placed;
    }
}
