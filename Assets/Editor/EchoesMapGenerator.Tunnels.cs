using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// -3 « Anciens tunnels » : labyrinthe de galeries en brique de 60 × 36 m, plongé dans le noir absolu.
/// Cases de 3 m (galeries de 2,2 m entre des murs épais), labyrinthe avec quelques boucles et beaucoup d'impasses,
/// trois petites salles (campement des disparus, réserve, salle au puits). Le sud (z &lt; -10) reste libre :
/// le conduit d'aération du -2 y passe et débouche dans le mur sud (x = 9).
/// </summary>
public static partial class EchoesMapGenerator
{
    const float TunC = 3f, TunX0 = -20f, TunZ0 = -10f, TunWall = 0.8f;
    const int TunNX = 20, TunNZ = 12;
    const int TunDoorJ = 2;          // case de la porte de l'escalier (z ∈ [-4, -1], porte à z = -3)

    static Vector3 TunCell(LevelDef L, int i, int j) => new Vector3(TunX0 + (i + 0.5f) * TunC, L.y, TunZ0 + (j + 0.5f) * TunC);

    static void BuildTunnelMaze(LevelDef L)
    {
        L.halfX = TunNX * TunC * 0.5f;
        L.xMid = TunX0 + L.halfX;
        L.zMin = TunZ0; L.zMax = TunZ0 + TunNZ * TunC;
        var mw = Mat("Mur_" + L.number, L.wall);
        var mf = Mat("Sol_" + L.number, L.floor);
        var mc = Mat("Plafond_" + L.number, L.wall * 0.8f);
        float y0 = L.y, y1 = L.y + L.h;
        float x1 = TunX0 + TunNX * TunC, z1 = TunZ0 + TunNZ * TunC;

        Box(L.root, "Sol", new Vector3(TunX0 - TunWall / 2, y0 - Slab, TunZ0 - TunWall / 2), new Vector3(x1 + TunWall / 2, y0, z1 + TunWall / 2), mf, _layerGround);
        Box(L.root, "Plafond", new Vector3(TunX0 - TunWall / 2, y1, TunZ0 - TunWall / 2), new Vector3(x1 + TunWall / 2, y1 + Slab, z1 + TunWall / 2), mc, _layerWalls);

        // 1) Labyrinthe parfait depuis la porte de l'escalier
        var east = new bool[TunNX, TunNZ];
        var north = new bool[TunNX, TunNZ];
        var vis = new bool[TunNX, TunNZ];
        void Link(Vector2Int a, Vector2Int b)
        {
            if (b.x > a.x) east[a.x, a.y] = true; else if (b.x < a.x) east[b.x, b.y] = true;
            else if (b.y > a.y) north[a.x, a.y] = true; else north[b.x, b.y] = true;
        }
        var start = new Vector2Int(0, TunDoorJ);
        var stack = new Stack<Vector2Int>();
        stack.Push(start); vis[start.x, start.y] = true;
        var dirs = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
        var next = new List<Vector2Int>();
        while (stack.Count > 0)
        {
            var c = stack.Peek();
            next.Clear();
            foreach (var d in dirs)
            {
                var n = c + d;
                if (n.x >= 0 && n.x < TunNX && n.y >= 0 && n.y < TunNZ && !vis[n.x, n.y]) next.Add(n);
            }
            if (next.Count == 0) { stack.Pop(); continue; }
            var pick = next[_rng.Next(next.Count)];
            Link(c, pick); vis[pick.x, pick.y] = true; stack.Push(pick);
        }
        // 2) Quelques boucles (on peut se perdre en tournant en rond)
        for (int i = 0; i < TunNX; i++)
            for (int j = 0; j < TunNZ; j++)
            {
                if (i < TunNX - 1 && !east[i, j] && _rng.NextDouble() < 0.1) east[i, j] = true;
                if (j < TunNZ - 1 && !north[i, j] && _rng.NextDouble() < 0.1) north[i, j] = true;
            }
        // 3) Trois petites salles de 2 × 2 cases
        var rooms = new List<Vector2Int> { new Vector2Int(9, 4), new Vector2Int(16, 9), new Vector2Int(4, 8) };
        foreach (var r in rooms)
        {
            east[r.x, r.y] = east[r.x, r.y + 1] = true;
            north[r.x, r.y] = north[r.x + 1, r.y] = true;
        }

        // 4) Murs épais entre cases non reliées (segments fusionnés), porte de l'escalier et sortie du conduit
        float ventX = -HalfX + (VentExitI + 0.5f) * Cell;    // la sortie du conduit (x = 9)
        for (int jb = 0; jb <= TunNZ; jb++)                       // murs parallèles à X
        {
            float z = TunZ0 + jb * TunC;
            int s = -1;
            for (int i = 0; i <= TunNX; i++)
            {
                bool need = i < TunNX && (jb == 0 || jb == TunNZ || !north[i, jb - 1]);
                if (need && jb == 0 && ventX > TunX0 + i * TunC && ventX < TunX0 + (i + 1) * TunC)
                {
                    WallThick(L.root, true, z, TunX0 + i * TunC - TunWall / 2, TunX0 + (i + 1) * TunC + TunWall / 2, y0, y1, mw,
                        new Hole(ventX, 1.05f, DuctY, DuctY + DuctH));
                    need = false;
                }
                if (need && s < 0) s = i;
                if (!need && s >= 0) { WallThick(L.root, true, z, TunX0 + s * TunC - TunWall / 2, TunX0 + i * TunC + TunWall / 2, y0, y1, mw); s = -1; }
            }
        }
        for (int ib = 0; ib <= TunNX; ib++)                       // murs parallèles à Z
        {
            float x = TunX0 + ib * TunC;
            int s = -1;
            for (int j = 0; j <= TunNZ; j++)
            {
                bool need = j < TunNZ && (ib == 0 || ib == TunNX || !east[ib - 1, j]);
                if (need && ib == 0 && j == TunDoorJ)
                {
                    WallThick(L.root, false, x, TunZ0 + j * TunC, TunZ0 + (j + 1) * TunC, y0, y1, mw, new Hole(DoorZ, DoorW, y0, y0 + DoorH));
                    need = false;
                }
                if (need && s < 0) s = j;
                if (!need && s >= 0) { WallThick(L.root, false, x, TunZ0 + s * TunC, TunZ0 + j * TunC, y0, y1, mw); s = -1; }
            }
        }
        // Poteaux aux croisements de murs (évitent les trous aux angles)
        for (int ib = 0; ib <= TunNX; ib++)
            for (int jb = 0; jb <= TunNZ; jb++)
            {
                bool inside = ib > 0 && ib < TunNX && jb > 0 && jb < TunNZ;
                if (inside && east[ib - 1, jb - 1] && east[ib - 1, jb] && north[ib - 1, jb - 1] && north[ib, jb - 1]) continue; // centre d'une salle
                var p = new Vector3(TunX0 + ib * TunC, y0, TunZ0 + jb * TunC);
                Box(L.root, "Pilier", p + new Vector3(-TunWall / 2, 0f, -TunWall / 2), p + new Vector3(TunWall / 2, y1 - y0, TunWall / 2), mw, _layerWalls);
            }

        DecorateTunnels(L, east, north, rooms);
        L.patrol = null;
    }

    /// Mur épais (le long de X si alongX, sinon de Z), découpé autour des ouvertures.
    static void WallThick(Transform p, bool alongX, float c, float a, float b, float y0, float y1, Material m, params Hole[] holes)
    {
        var cuts = new List<float> { a, b };
        foreach (var h in holes) { cuts.Add(Mathf.Clamp(h.c - h.w / 2, a, b)); cuts.Add(Mathf.Clamp(h.c + h.w / 2, a, b)); }
        cuts.Sort();
        for (int k = 0; k < cuts.Count - 1; k++)
        {
            float u0 = cuts[k], u1 = cuts[k + 1];
            if (u1 - u0 < 0.01f) continue;
            float um = (u0 + u1) * 0.5f;
            var ys = new List<(float, float)> { (y0, y1) };
            foreach (var h in holes)
                if (um > h.c - h.w / 2 && um < h.c + h.w / 2) ys = new List<(float, float)> { (y0, h.y0), (h.y1, y1) };
            foreach (var (ya, yb) in ys)
            {
                if (yb - ya < 0.01f) continue;
                if (alongX) Box(p, "Mur_Tunnel", new Vector3(u0, ya, c - TunWall / 2), new Vector3(u1, yb, c + TunWall / 2), m, _layerWalls);
                else Box(p, "Mur_Tunnel", new Vector3(c - TunWall / 2, ya, u0), new Vector3(c + TunWall / 2, yb, u1), m, _layerWalls);
            }
        }
    }

    static void DecorateTunnels(LevelDef L, bool[,] east, bool[,] north, List<Vector2Int> rooms)
    {
        var mp = Mat("Meuble_" + L.number, Color.Lerp(L.wall, L.floor, 0.5f) * 0.9f);
        Physics.SyncTransforms(); // le sol vient d'être créé : les ossements s'y posent par rayon
        int Degree(int i, int j) => (i < TunNX - 1 && east[i, j] ? 1 : 0) + (i > 0 && east[i - 1, j] ? 1 : 0) + (j < TunNZ - 1 && north[i, j] ? 1 : 0) + (j > 0 && north[i, j - 1] ? 1 : 0);
        Vector3 RoomCenter(Vector2Int r) => TunCell(L, r.x, r.y) + new Vector3(TunC * 0.5f, 0f, TunC * 0.5f);

        // Campement des étudiants disparus
        var camp = RoomCenter(rooms[0]);
        Prop(L, "Sac_A_Dos", camp + new Vector3(-0.8f, 0.25f, 0.4f), new Vector3(0.4f, 0.5f, 0.3f), mp, 20f);
        Model(L, KSurvival + "bedroll", camp + new Vector3(1.4f, 0, 1f), 80f, new Vector3(0.9f, 0.3f, 2f), mp);
        Model(L, KSurvival + "bedroll-packed", camp + new Vector3(-1.6f, 0, -1.2f), 10f, new Vector3(0.6f, 0.4f, 0.6f), mp);
        Model(L, KSurvival + "bucket", camp + new Vector3(-1.8f, 0, 1.5f), 0f, new Vector3(0.4f, 0.4f, 0.4f), mp);
        CaveProp(L, "small_plastic_torch", camp + new Vector3(0.4f, 0f, -0.6f), Rand(0f, 360f), new Vector3(0.22f, 0.22f, 0.22f));
        CaveProp(L, "wooden_candlestick", camp + new Vector3(0.6f, 0f, 0.6f), 0f, new Vector3(0.12f, 0.3f, 0.12f));
        Marker(L, "Indice_Disparus", camp, Color.yellow);
        // Réserve : caisses, tonneau, pelles
        var store = RoomCenter(rooms[1]);
        ScatterBoxes(L, new Rect(store.x - 2f, store.z - 2f, 4f, 4f), 6);
        Model(L, KSurvival + "barrel", store + new Vector3(2f, 0f, 2f), 0f, new Vector3(0.7f, 1f, 0.7f), mp);
        Model(L, KSurvival + "tool-shovel", store + new Vector3(-2f, 0f, 1.5f), 30f, new Vector3(0.3f, 1.2f, 0.3f), mp, false);
        // Salle au vieux puits (margelle en pierre)
        var well = RoomCenter(rooms[2]);
        var stone = Mat("Mur_" + L.number, L.wall);
        Cylinder(L.props, "Margelle_Puits", well + Vector3.up * 0.45f, 0.9f, 0.9f, Quaternion.identity, stone, true);
        Cylinder(L.props, "Puits_Noir", well + Vector3.up * 0.91f, 0.7f, 0.02f, Quaternion.identity, Mat("Noir", new Color(0.01f, 0.01f, 0.01f)), false);
        Marker(L, "Vieux_Puits", well, Color.magenta);

        // Impasses : piles (la lampe est vitale dans le noir), ossements, gravats
        var deadEnds = new List<Vector2Int>();
        for (int i = 0; i < TunNX; i++)
            for (int j = 0; j < TunNZ; j++)
                if (Degree(i, j) == 1 && !(i == 0 && j == TunDoorJ)) deadEnds.Add(new Vector2Int(i, j));
        for (int k = deadEnds.Count - 1; k > 0; k--) { int r = _rng.Next(k + 1); (deadEnds[k], deadEnds[r]) = (deadEnds[r], deadEnds[k]); }
        for (int k = 0; k < deadEnds.Count; k++)
        {
            var c = TunCell(L, deadEnds[k].x, deadEnds[k].y);
            if (k < 4) PlaceItem(L, "pile", c + new Vector3(Rand(-0.5f, 0.5f), 0f, Rand(-0.5f, 0.5f)), Rand(0f, 360f));
            else if (k < 8) BonePile(L, c);
            else if (_rng.NextDouble() < 0.5)
                Model(L, KGraveyard + (_rng.NextDouble() < 0.5 ? "debris" : "debris-wood"), c + new Vector3(Rand(-0.6f, 0.6f), 0, Rand(-0.6f, 0.6f)), Rand(0, 360), new Vector3(0.9f, 0.5f, 0.9f), mp);
        }
        for (int k = 0; k < 14; k++) // gravats et étais en bois dans les galeries
        {
            int i = _rng.Next(TunNX), j = _rng.Next(TunNZ);
            if (i == 0 && j == TunDoorJ) continue;
            Model(L, KGraveyard + (_rng.NextDouble() < 0.5 ? "debris" : "debris-wood"), TunCell(L, i, j) + new Vector3(Rand(-0.6f, 0.6f), 0, Rand(-0.6f, 0.6f)), Rand(0, 360), new Vector3(0.9f, 0.5f, 0.9f), mp);
        }
        Marker(L, "Ombre_Furtive", TunCell(L, 17, 6), Color.magenta);
        Debug.Log($"[EchoesMap] -3 : labyrinthe de {TunNX * TunC:0} × {TunNZ * TunC:0} m, {deadEnds.Count} impasses, 3 salles.");
    }
}
