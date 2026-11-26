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
    static readonly Vector2 LakeC = new Vector2(16f, -35f);   // hors de l'emprise de l'étage 0 (z > -30)
    static readonly Vector2 LakeR = new Vector2(9f, 3.6f);
    const float LakeDepth = 1.45f, LakeWater = 0.25f;
    static readonly Rect PicnicRect = Rect.MinMaxRect(-27f, -34.5f, -9f, -22.5f);
    static Rect LakeBasin => Rect.MinMaxRect(LakeC.x - LakeR.x - 4f, LakeC.y - LakeR.y - 1.5f, LakeC.x + LakeR.x + 4f, LakeC.y + LakeR.y + 1.5f); // rive irrégulière : marge en x

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
            Rect.MinMaxRect(HalfX + 2.6f, ExitZ - 1.1f, LoopX1 - PathW / 2, ExitZ + 1.1f),          // allée d'entrée → boucle
            Rect.MinMaxRect(LoopX1 + PathW / 2, ExitZ - 1.1f, YardRect.xMax, ExitZ + 1.1f),         // boucle → portail
        };
        // Tous les chemins en un seul maillage, texture alignée sur le monde : raccords invisibles aux croisements
        foreach (var r in paths) _parkBusy.Add(Rect.MinMaxRect(r.xMin - 0.6f, r.yMin - 0.6f, r.xMax + 0.6f, r.yMax + 0.6f));
        PavedMesh(root, "Chemins", paths, gy + 0.035f, paving);
        var curb = Mat("Facade_Bandeau", new Color(0.82f, 0.8f, 0.75f));

        // Cour intérieure pavée
        PavedMesh(root, "Cour_Pavee", new List<Rect> { Rect.MinMaxRect(WX0, CorrN + RoomDepth + 0.3f, WX1, WZ0 - 0.3f) }, gy + 0.03f, paving);

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
        FrontGarden(root, P, gy);
        int trees = PlantPark(root, P, gy);

        // Vues de contrôle
        CaveView(X, "Vue_Parc_Lac", new Vector3(LakeC.x - LakeR.x - 2f, gy, LakeC.y + LakeR.y + 2.5f), new Vector3(LakeC.x + 2f, gy - 0.5f, LakeC.y - 1f));
        CaveView(X, "Vue_Parc_Pique_Nique", new Vector3(PicnicRect.xMin + 0.4f, gy, PicnicRect.yMin + 0.4f), new Vector3(PicnicRect.center.x + 2f, gy, PicnicRect.center.y + 1f));
        CaveView(X, "Vue_Parc_Chemin", new Vector3(LoopX0, gy, LoopZ0 - 0.2f), new Vector3(LoopX0, gy, LoopZ1));
        Debug.Log($"[EchoesMap] Parc : boucle de {2 * (LoopX1 - LoopX0) + 2 * (LoopZ1 - LoopZ0):0} m, {benches} bancs, {trees} arbres, mini-lac, aire de pique-nique.");
    }

    /// Dalles (rectangles jointifs) en un seul maillage : dessus + chants, UV en mètres dans le repère du monde.
    static void PavedMesh(Transform root, string name, List<Rect> rects, float top, Material m)
    {
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        float bottom = top - 0.12f;
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d); uv.Add(ua); uv.Add(ub); uv.Add(uc); uv.Add(ud);
            t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3);
        }
        foreach (var r in rects)
        {
            // dessus (sens horaire vu d'en haut)
            Quad(new Vector3(r.xMin, top, r.yMin), new Vector3(r.xMin, top, r.yMax), new Vector3(r.xMax, top, r.yMax), new Vector3(r.xMax, top, r.yMin),
                 new Vector2(r.xMin, r.yMin), new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax), new Vector2(r.xMax, r.yMin));
            // chants (bordure de 3,5 cm visible au-dessus de la pelouse)
            Quad(new Vector3(r.xMin, bottom, r.yMin), new Vector3(r.xMin, top, r.yMin), new Vector3(r.xMax, top, r.yMin), new Vector3(r.xMax, bottom, r.yMin),
                 new Vector2(r.xMin, 0f), new Vector2(r.xMin, 0.12f), new Vector2(r.xMax, 0.12f), new Vector2(r.xMax, 0f));
            Quad(new Vector3(r.xMax, bottom, r.yMax), new Vector3(r.xMax, top, r.yMax), new Vector3(r.xMin, top, r.yMax), new Vector3(r.xMin, bottom, r.yMax),
                 new Vector2(r.xMax, 0f), new Vector2(r.xMax, 0.12f), new Vector2(r.xMin, 0.12f), new Vector2(r.xMin, 0f));
            Quad(new Vector3(r.xMin, bottom, r.yMax), new Vector3(r.xMin, top, r.yMax), new Vector3(r.xMin, top, r.yMin), new Vector3(r.xMin, bottom, r.yMin),
                 new Vector2(r.yMax, 0f), new Vector2(r.yMax, 0.12f), new Vector2(r.yMin, 0.12f), new Vector2(r.yMin, 0f));
            Quad(new Vector3(r.xMax, bottom, r.yMin), new Vector3(r.xMax, top, r.yMin), new Vector3(r.xMax, top, r.yMax), new Vector3(r.xMax, bottom, r.yMax),
                 new Vector2(r.yMin, 0f), new Vector2(r.yMin, 0.12f), new Vector2(r.yMax, 0.12f), new Vector2(r.yMax, 0f));
        }
        var mesh = new Mesh { name = name };
        mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.RecalculateTangents();
        AssetDatabase.AddObjectToAsset(mesh, _boxContainer);
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = m;
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
        go.layer = _layerGround;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
    }

    static bool ParkFreeLine(Vector2 p) => !LakeBasin.Contains(p) && !PicnicRect.Contains(p) && Mathf.Abs(p.x) > 1f;

    // ───────────────────────── Mini-lac ─────────────────────────

    /// Forme de la rive : rayon relatif selon l'angle (anses et avancées), 1 au nord où est le ponton.
    static float LakeShape(float a)
    {
        float Raw(float t) => 1f + 0.12f * Mathf.Sin(2f * t + 0.7f) + 0.08f * Mathf.Sin(3f * t + 2.2f) + 0.05f * Mathf.Sin(5f * t + 0.9f);
        return Raw(a) / Raw(Mathf.PI * 0.5f);
    }

    /// Largeur de la berge en terre (en fraction du rayon) selon l'angle : tantôt une plage, tantôt l'herbe jusqu'à l'eau.
    static float LakeBank(float a) => 0.075f + 0.045f * Mathf.Sin(3f * a + 1.1f) + 0.025f * Mathf.Sin(7f * a + 0.3f);

    static Vector3 LakePoint(float a, float e, float y) =>
        new Vector3(LakeC.x + Mathf.Cos(a) * LakeR.x * e * LakeShape(a), y, LakeC.y + Mathf.Sin(a) * LakeR.y * e * LakeShape(a));

    static float LakeE(float x, float z)
    {
        float a = Mathf.Atan2((z - LakeC.y) / LakeR.y, (x - LakeC.x) / LakeR.x);
        return Mathf.Sqrt(Mathf.Pow((x - LakeC.x) / LakeR.x, 2f) + Mathf.Pow((z - LakeC.y) / LakeR.y, 2f)) / LakeShape(a);
    }

    static void BuildLake(Transform root, LevelDef P, float gy)
    {
        // Cuvette en terre (maillage polaire, bord lisse) jusqu'à e = 1,15, légèrement sous la pelouse au bord
        var r = LakeBasin;
        const int A = 160;
        float[] rings = { 0f, 0.2f, 0.4f, 0.55f, 0.68f, 0.78f, 0.86f, 0.92f, 0.96f, 0.99f, 1.02f, 1.06f, 1.1f, 1.15f }; // > 1 : berge (étirée selon LakeBank)
        int R = rings.Length;
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var mud = new List<int>();
        for (int s = 0; s < A; s++)
        {
            float a = s * Mathf.PI * 2f / A;
            float bank = LakeBank(a);
            foreach (var e0 in rings)
            {
                float e = e0 <= 1f ? e0 : 1f + (e0 - 1f) / 0.15f * (bank + 0.04f);
                float d = e >= 1f ? 0.03f : Mathf.Max(0.03f, LakeDepth * Mathf.Pow(1f - e * e, 0.7f));
                var pt = LakePoint(a, e, gy - d);
                v.Add(pt); uv.Add(new Vector2(pt.x, pt.z));
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
        // Pelouse autour : un seul maillage lisse, de l'ellipse (un peu sur la berge) jusqu'au bord du trou rectangulaire
        var lawn = Mat("Exterieur_Herbe", Color.white);
        const int LA = 160;
        var lv = new List<Vector3>(); var luv = new List<Vector2>(); var lt = new List<int>();
        for (int q = 0; q < LA; q++)
        {
            float a = q * Mathf.PI * 2f / LA, cx = Mathf.Cos(a), cz = Mathf.Sin(a);
            var inner = LakePoint(a, 1f + LakeBank(a), gy + 0.005f);
            // point du bord du rectangle dans la même direction
            float tx = cx > 1e-4f ? (r.xMax - LakeC.x) / cx : cx < -1e-4f ? (r.xMin - LakeC.x) / cx : 1e6f;
            float tz = cz > 1e-4f ? (r.yMax - LakeC.y) / cz : cz < -1e-4f ? (r.yMin - LakeC.y) / cz : 1e6f;
            float t = Mathf.Min(tx, tz);
            var outer = new Vector3(LakeC.x + cx * t, gy, LakeC.y + cz * t);
            lv.Add(inner); lv.Add(outer);
            luv.Add(new Vector2(inner.x, inner.z)); luv.Add(new Vector2(outer.x, outer.z));
        }
        // coins du rectangle (rattachés au secteur le plus proche)
        var corners = new[] { new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax), new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin) };
        for (int q = 0; q < LA; q++)
        {
            int q2 = (q + 1) % LA;
            int i0 = q * 2, o0 = i0 + 1, i1 = q2 * 2, o1 = i1 + 1;
            lt.Add(i0); lt.Add(o0); lt.Add(i1); lt.Add(i1); lt.Add(o0); lt.Add(o1);
        }
        foreach (var c2 in corners)
        {
            float ca = Mathf.Atan2(c2.y - LakeC.y, c2.x - LakeC.x); if (ca < 0f) ca += Mathf.PI * 2f;
            int q = Mathf.FloorToInt(ca / (Mathf.PI * 2f) * LA) % LA, q2 = (q + 1) % LA;
            int ci = lv.Count; lv.Add(new Vector3(c2.x, gy, c2.y)); luv.Add(c2);
            lt.Add(q * 2 + 1); lt.Add(ci); lt.Add(q2 * 2 + 1);
        }
        // Sens des triangles : face vers le haut (on corrige chaque triangle au besoin)
        for (int i = 0; i < lt.Count; i += 3)
            if (Vector3.Cross(lv[lt[i + 1]] - lv[lt[i]], lv[lt[i + 2]] - lv[lt[i]]).y < 0f) (lt[i + 1], lt[i + 2]) = (lt[i + 2], lt[i + 1]);
        var lm = new Mesh { name = "Pelouse_Lac" };
        lm.SetVertices(lv); lm.SetUVs(0, luv); lm.SetTriangles(lt, 0);
        lm.RecalculateNormals(); lm.RecalculateBounds(); lm.RecalculateTangents();
        AssetDatabase.AddObjectToAsset(lm, _boxContainer);
        var lgo = new GameObject("Pelouse_Lac");
        lgo.transform.SetParent(root, false);
        lgo.AddComponent<MeshFilter>().sharedMesh = lm;
        lgo.AddComponent<MeshRenderer>().sharedMaterial = lawn;
        lgo.AddComponent<MeshCollider>().sharedMesh = lm;
        lgo.layer = _layerGround;
        go.layer = _layerGround;

        // Eau
        float k = Mathf.Sqrt(1f - Mathf.Pow(LakeWater / LakeDepth, 1f / 0.7f)); // e où la berge croise la surface
        var wv = new List<Vector3> { new Vector3(LakeC.x, gy - LakeWater, LakeC.y) }; var wt = new List<int>();
        for (int s = 0; s < A; s++) wv.Add(LakePoint(s * Mathf.PI * 2f / A, k + 0.015f, gy - LakeWater)); // juste sous la berge
        for (int s = 0; s < A; s++)
        {
            int i1 = 1 + s, i2 = 1 + (s + 1) % A;
            if (Vector3.Cross(wv[i1] - wv[0], wv[i2] - wv[0]).y < 0f) { wt.Add(0); wt.Add(i1); wt.Add(i2); } else { wt.Add(0); wt.Add(i2); wt.Add(i1); }
        }
        var wm = new Mesh { name = "Eau_Lac" };
        wm.SetVertices(wv); wm.SetTriangles(wt, 0); wm.RecalculateNormals(); wm.RecalculateBounds();
        AssetDatabase.AddObjectToAsset(wm, _boxContainer);
        var water = new GameObject("Eau_Lac");
        water.transform.SetParent(root, false);
        water.AddComponent<MeshFilter>().sharedMesh = wm;
        water.AddComponent<MeshRenderer>().sharedMaterial = WaterMat("Eau_Mini_Lac", new Color(0.55f, 0.62f, 0.6f), new Color(0.03f, 0.07f, 0.08f), 0.7f, 0f, 0.22f, 0.9f, 0.1f);
        water.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        water.AddComponent<Unity.AI.Navigation.NavMeshModifier>().ignoreFromBuild = true;
        WaterVolumeBox(root, "Lac_Patauger", new Vector3(LakeC.x, gy - 0.9f, LakeC.y), new Vector3(LakeR.x * 2.3f * k, 1.5f, LakeR.y * 2.3f * k), Quaternion.identity, 0.55f, gy - LakeWater);
        WaterSound(root, new Vector3(LakeC.x, gy, LakeC.y), SndRiver, 0.12f, 18f);

        // Pierres et roseaux sur la berge, ponton en bois au nord
        int n = 44;
        Physics.SyncTransforms(); // les collisions de la berge viennent d'être créées : on pose tout sur le vrai sol
        for (int s = 0; s < n; s++)
        {
            float a = s * Mathf.PI * 2f / n + Rand(-0.08f, 0.08f);
            float e = Rand(0.92f, 1f + LakeBank(a));
            var p = LakePoint(a, e, gy);
            if (Physics.Raycast(p + Vector3.up * 1f, Vector3.down, out var hit, 4f, 1 << _layerGround, QueryTriggerInteraction.Ignore)) p.y = hit.point.y;
            if (Mathf.Abs(p.x - LakeC.x) < 1.6f && p.z > LakeC.y) continue; // le ponton
            if (s % 2 == 0)
            {
                float sc = Rand(0.5f, 1.1f);
                Model(P, _rng.NextDouble() < 0.5 ? KSurvival + "rock-a" : KSurvival + "rock-b", p + Vector3.down * (0.18f * sc), Rand(0f, 360f), new Vector3(sc, sc * 0.6f, sc), Mat("Lac_Berge", Color.gray), false);
            }
            else if (AssetDatabase.LoadAssetAtPath<GameObject>(Foliage + "grass 1.prefab") != null)
            {
                float sc = Rand(0.7f, 1.2f);
                var reed = InstantiateAt(P, Foliage + "grass 1.prefab", p + Vector3.down * 0.03f, Rand(0f, 360f), new Vector3(sc, sc * 1.2f, sc));
                SetupRealTree(reed, false);
            }
        }
        var wood = Mat("Ponton_Bois", new Color(0.5f, 0.4f, 0.3f));
        float pz0 = LakeC.y + LakeR.y * 0.55f, pz1 = LakeC.y + LakeR.y + 0.7f;
        Box(root, "Ponton", new Vector3(LakeC.x - 0.9f, gy - 0.05f, pz0), new Vector3(LakeC.x + 0.9f, gy + 0.08f, pz1), wood, _layerGround);
        foreach (var dx in new[] { -0.8f, 0.8f })
            foreach (var z in new[] { pz0 + 0.1f, (pz0 + pz1) * 0.5f })
                Cylinder(root, "Pilotis", new Vector3(LakeC.x + dx, gy - 0.6f, z), 0.08f, 1.2f, Quaternion.identity, wood, false);
        // Banc sur l'herbe à côté du ponton, face au lac (on remonte depuis l'eau jusqu'à l'herbe, + 1,2 m)
        float bx = LakeC.x - 3.4f, bz = LakeC.y;
        while (bz < LakeC.y + LakeR.y * 2f && LakeE(bx, bz) < 1f + LakeBank(Mathf.Atan2((bz - LakeC.y) / LakeR.y, (bx - LakeC.x) / LakeR.x)) + 0.02f) bz += 0.05f;
        var bench = Piece(P, MP("Chairs", "Chair39"), KGraveyard + "bench", new Vector3(bx, gy + 0.005f, bz + 1.2f), 180f, F(1.6f, 0.9f, 0.6f, 180f), wood);
        if (bench != null) AddSeat(bench, 0.45f, 180f, Vector3.zero, "S'asseoir face au lac");
        StreetLamp(P, new Vector3(LakeC.x + 1.6f, gy, pz1 + 0.4f));
    }

    // ───────────────────────── Pique-nique ─────────────────────────

    static void BuildPicnic(Transform root, LevelDef P, float gy)
    {
        var r = PicnicRect;
        Box(root, "Aire_Pique_Nique", new Vector3(r.xMin, gy, r.yMin), new Vector3(r.xMax, gy + 0.03f, r.yMax), Mat("Parc_Gravier", Color.white), _layerGround);
        var wood = Mat("Ponton_Bois", new Color(0.5f, 0.4f, 0.3f));
        foreach (var cz in new[] { r.yMin + 3.2f, r.yMax - 3.2f })
            foreach (var cx in new[] { r.xMin + 3.5f, r.center.x, r.xMax - 3.5f })
            {
                var c = new Vector3(cx + Rand(-0.3f, 0.3f), gy, cz + Rand(-0.3f, 0.3f));
                var t = CaveProp(P, "wooden_picnic_table", c, Rand(-8f, 8f), F(2.0f, 0.8f, 1.7f, 0f), true);
                if (t != null) AddSeat(t, 0.45f, 0f, new Vector3(0f, 0f, -0.6f), "S'asseoir à la table");
            }
        Model(P, KFurniture + "trashcan", new Vector3(r.xMax - 0.6f, gy, r.center.y), 0f, new Vector3(0.45f, 0.8f, 0.45f), wood);
        Model(P, KFurniture + "trashcan", new Vector3(r.xMin + 0.6f, gy, r.center.y), 0f, new Vector3(0.45f, 0.8f, 0.45f), wood);
        CaveProp(P, "stone_fire_pit", new Vector3(r.xMin - 2.5f, gy, r.center.y), 0f, new Vector3(1.4f, 0.4f, 1.4f), true); // barbecue en pierre
        StreetLamp(P, new Vector3(r.center.x - 4f, gy, r.center.y));
        StreetLamp(P, new Vector3(r.center.x + 4f, gy, r.center.y));
    }

    // ───────────────────────── Jardin d'entrée ─────────────────────────

    /// Devant la résidence (entre la façade est et le portail) : allée bordée d'arbres et de plates-bandes fleuries,
    /// bacs à fleurs de part et d'autre de l'entrée, bancs face à face, deux pelouses avec massif central et arbustes.
    static void FrontGarden(Transform root, LevelDef P, float gy)
    {
        float x0 = HalfX + 6f, x1 = YardRect.xMax - 3f;
        var soil = Mat("Massif_Terre", new Color(0.3f, 0.24f, 0.18f));
        var stone = Mat("Facade_Bandeau", new Color(0.82f, 0.8f, 0.75f));
        bool hasBush = AssetDatabase.LoadAssetAtPath<GameObject>(BushSmall) != null;
        bool hasFlower = AssetDatabase.LoadAssetAtPath<GameObject>(Foliage + "dandelion.prefab") != null;
        string[] trees = { Foliage + "maple.prefab", Foliage + "lucious tree.prefab", Foliage + "apple.prefab" };
        bool LoopCross(float x) => Mathf.Abs(x - LoopX1) < PathW + 1.5f;

        // Plates-bandes fleuries le long de l'allée, avec arbres d'alignement entre elles
        foreach (var side in new[] { -1f, 1f })
        {
            float zc = ExitZ + side * 2.4f;
            for (float x = x0; x < x1 - 3f; x += 6f)
            {
                if (LoopCross(x + 1.5f) || LoopCross(x)) continue;
                var c = new Vector3(x + 1.5f, gy + 0.06f, zc);
                Cube(root, "Plate_Bande", c, new Vector3(3.6f, 0.12f, 0.9f), Quaternion.identity, soil, false, -1);
                Cube(root, "Bordure_Plate_Bande", c + new Vector3(0f, -0.01f, side * -0.47f), new Vector3(3.6f, 0.14f, 0.06f), Quaternion.identity, stone, false, -1);
                for (int k = 0; k < 7; k++)
                {
                    var f = c + new Vector3(Rand(-1.6f, 1.6f), 0.05f, Rand(-0.3f, 0.3f));
                    if (k < 2 && hasBush) SetupRealTree(InstantiateAt(P, BushSmall, f, Rand(0f, 360f), Vector3.one * Rand(0.6f, 0.85f)), false);
                    else if (hasFlower) SetupRealTree(InstantiateAt(P, Foliage + "dandelion.prefab", f, Rand(0f, 360f), Vector3.one * Rand(0.3f, 0.45f)), false);
                }
                // Arbre d'alignement entre deux plates-bandes, en retrait de l'allée
                var tp = new Vector3(x + 4.5f, gy, ExitZ + side * 3.6f);
                if (!LoopCross(tp.x) && tp.x < x1 - 1f)
                {
                    float h = Rand(7f, 9.5f);
                    var t = InstantiateAt(P, trees[(int)(x / 6f) % trees.Length], tp, Rand(0f, 360f), new Vector3(h * 0.7f, h, h * 0.7f));
                    SetupRealTree(t, true);
                }
            }
            _parkBusy.Add(Rect.MinMaxRect(x0 - 1f, Mathf.Min(ExitZ + side * 1.8f, ExitZ + side * 4.6f), x1, Mathf.Max(ExitZ + side * 1.8f, ExitZ + side * 4.6f)));
        }

        // Bacs à fleurs en bois de part et d'autre du perron
        foreach (var dz in new[] { -2.2f, 2.2f })
            foreach (var dx in new[] { 3.2f, 5.0f })
            {
                var pos = new Vector3(HalfX + dx, gy, ExitZ + dz);
                var box = CaveProp(P, "planter_box_02", pos, 0f, new Vector3(1.1f, 0.6f, 0.6f), true);
                if (box != null && hasBush && TryBounds(box, out var bb))
                    SetupRealTree(InstantiateAt(P, BushSmall, new Vector3(pos.x, bb.max.y - 0.1f, pos.z), Rand(0f, 360f), Vector3.one * 0.7f), false);
            }

        // Deux pelouses de part et d'autre de l'allée : massif rond central, bancs face au massif, arbustes et grands arbres
        foreach (var (za, zb) in new[] { (LoopZ0 + PathW, ExitZ - 5f), (ExitZ + 5f, LoopZ1 - PathW) })
        {
            float cx = (HalfX + 3f + LoopX1 - PathW * 0.5f - 1f) * 0.5f, cz = (za + zb) * 0.5f;
            if (zb - za < 4f) continue;
            float rad = Mathf.Min(2.2f, (zb - za) * 0.35f);
            Cylinder(root, "Massif_Rond", new Vector3(cx, gy + 0.05f, cz), rad, 0.1f, Quaternion.identity, soil, false);
            Cylinder(root, "Bordure_Massif", new Vector3(cx, gy + 0.04f, cz), rad + 0.12f, 0.08f, Quaternion.identity, stone, false);
            for (int k = 0; k < 18; k++)
            {
                float a = Rand(0f, Mathf.PI * 2f), d = Mathf.Sqrt(Rand(0f, 1f)) * (rad - 0.25f);
                var f = new Vector3(cx + Mathf.Cos(a) * d, gy + 0.1f, cz + Mathf.Sin(a) * d);
                if (k < 4 && hasBush) SetupRealTree(InstantiateAt(P, BushSmall, f, Rand(0f, 360f), Vector3.one * Rand(0.7f, 1f)), false);
                else if (hasFlower) SetupRealTree(InstantiateAt(P, Foliage + "dandelion.prefab", f, Rand(0f, 360f), Vector3.one * Rand(0.3f, 0.5f)), false);
            }
            // Deux bancs face à face, de part et d'autre du massif (dans l'axe de la pelouse, loin de la façade)
            foreach (var s in new[] { -1f, 1f })
            {
                float yaw = s < 0 ? 0f : 180f;
                var b = Piece(P, MP("Chairs", "Chair40", "Chair39"), KGraveyard + "bench", new Vector3(cx, gy, cz + s * (rad + 1.3f)), yaw, F(1.7f, 0.9f, 0.65f, yaw), stone);
                if (b != null) AddSeat(b, 0.45f, yaw, Vector3.zero, "S'asseoir sur le banc");
            }
            _parkBusy.Add(Rect.MinMaxRect(cx - rad - 0.8f, cz - rad - 2.2f, cx + rad + 0.8f, cz + rad + 2.2f));
            // Grands arbres aux coins de la pelouse et arbustes le long de la façade
            foreach (var (tx, tz) in new[] { (HalfX + 4.5f, za + 1.2f), (HalfX + 4.5f, zb - 1.2f), (LoopX1 - PathW - 2f, za + 1.2f), (LoopX1 - PathW - 2f, zb - 1.2f) })
            {
                var tp = new Vector2(tx, tz);
                if (!ParkFree(tp, 1.2f)) continue;
                float h = Rand(8f, 11f);
                SetupRealTree(InstantiateAt(P, trees[_rng.Next(trees.Length)], new Vector3(tx, gy, tz), Rand(0f, 360f), new Vector3(h * 0.75f, h, h * 0.75f)), true);
                _parkBusy.Add(new Rect(tx - 1.5f, tz - 1.5f, 3f, 3f));
            }
        }
        StreetLamp(P, new Vector3(HalfX + 6.5f, gy, ExitZ - 1.6f));
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
