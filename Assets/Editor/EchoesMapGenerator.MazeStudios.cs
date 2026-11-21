using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Niveau 0 : les grandes salles du labyrinthe deviennent des studios abandonnés.
/// Chaque salle est refermée avec une seule porte (entrouverte, fermée ou arrachée) ; si refermer un mur
/// coupait le labyrinthe en deux, ce mur reste ouvert et devient une brèche (gravats).
/// Dedans : les mêmes meubles que les studios des étages, mais renversés, déplacés, couverts de débris.
/// </summary>
public static partial class EchoesMapGenerator
{
    class MazeEdge
    {
        public Vector2Int outside;     // nœud du labyrinthe de l'autre côté
        public bool alongX;            // mur parallèle à X (sinon à Z)
        public Vector2Int wallKey;     // clé holesH (i, ligne j) ou holesV (colonne i, j)
        public Vector3 center;         // milieu du mur, au sol
        public Vector3 inward;         // direction vers l'intérieur de la salle
        public System.Action<bool> setLink;
        public System.Func<bool> getLink;
    }

    class MazeStudio
    {
        public RectInt rect;
        public MazeEdge entrance;
        public readonly List<MazeEdge> breaches = new List<MazeEdge>();
    }

    static List<MazeEdge> MazeRoomEdges(RectInt rm, bool[,] east, bool[,] north, float y)
    {
        var list = new List<MazeEdge>();
        for (int i = rm.x; i < rm.x + 2; i++)
        {
            int ii = i;
            float cx = MazeX0 + (i + 0.5f) * MazeP;
            // sud : intérieur (i, y), extérieur (i, y-1)
            int js = rm.y;
            list.Add(new MazeEdge { outside = new Vector2Int(i, js - 1), alongX = true, wallKey = new Vector2Int(i, js),
                center = new Vector3(cx, y, MazeZ0 + js * MazeP), inward = Vector3.forward,
                getLink = () => js - 1 >= 0 && north[ii, js - 1], setLink = v => { if (js - 1 >= 0) north[ii, js - 1] = v; } });
            // nord : intérieur (i, y+1), extérieur (i, y+2)
            int jn = rm.y + 1;
            list.Add(new MazeEdge { outside = new Vector2Int(i, jn + 1), alongX = true, wallKey = new Vector2Int(i, jn + 1),
                center = new Vector3(cx, y, MazeZ0 + (jn + 1) * MazeP), inward = Vector3.back,
                getLink = () => north[ii, jn], setLink = v => north[ii, jn] = v });
        }
        for (int j = rm.y; j < rm.y + 2; j++)
        {
            int jj = j;
            float cz = MazeZ0 + (j + 0.5f) * MazeP;
            int iw = rm.x;
            list.Add(new MazeEdge { outside = new Vector2Int(iw - 1, j), alongX = false, wallKey = new Vector2Int(iw, j),
                center = new Vector3(MazeX0 + iw * MazeP, y, cz), inward = Vector3.right,
                getLink = () => iw - 1 >= 0 && east[iw - 1, jj], setLink = v => { if (iw - 1 >= 0) east[iw - 1, jj] = v; } });
            int ie = rm.x + 1;
            list.Add(new MazeEdge { outside = new Vector2Int(ie + 1, j), alongX = false, wallKey = new Vector2Int(ie + 1, j),
                center = new Vector3(MazeX0 + (ie + 1) * MazeP, y, cz), inward = Vector3.left,
                getLink = () => east[ie, jj], setLink = v => east[ie, jj] = v });
        }
        return list;
    }

    /// Referme les salles (une porte chacune) sans jamais couper le labyrinthe.
    static List<MazeStudio> PlanMazeStudios(List<RectInt> rooms, bool[,] east, bool[,] north,
        System.Func<int, int, bool> carvable, Vector2Int start, float y)
    {
        var result = new List<MazeStudio>();
        foreach (var rm in rooms)
        {
            var st = new MazeStudio { rect = rm };
            var edges = MazeRoomEdges(rm, east, north, y).FindAll(e => carvable(e.outside.x, e.outside.y));
            if (edges.Count == 0) continue;
            var openEdges = edges.FindAll(e => e.getLink());
            st.entrance = openEdges.Count > 0 ? openEdges[_rng.Next(openEdges.Count)] : edges[_rng.Next(edges.Count)];
            foreach (var e in edges) e.setLink(e == st.entrance);
            result.Add(st);

            // Refermer ne doit rien isoler : sinon on rouvre des murs (brèches) jusqu'à ce que tout soit relié
            var closed = openEdges.FindAll(e => e != st.entrance);
            while (!MazeConnected(east, north, carvable, start) && closed.Count > 0)
            {
                var e = closed[_rng.Next(closed.Count)];
                closed.Remove(e);
                e.setLink(true);
                st.breaches.Add(e);
            }
        }
        return result;
    }

    static bool MazeConnected(bool[,] east, bool[,] north, System.Func<int, int, bool> carvable, Vector2Int start)
    {
        int nx = east.GetLength(0), nz = east.GetLength(1);
        var seen = new bool[nx, nz];
        var q = new Queue<Vector2Int>();
        q.Enqueue(start); seen[start.x, start.y] = true;
        int count = 1;
        while (q.Count > 0)
        {
            var c = q.Dequeue();
            void Try(int i, int j, bool link)
            {
                if (!link || i < 0 || j < 0 || i >= nx || j >= nz || seen[i, j] || !carvable(i, j)) return;
                seen[i, j] = true; count++; q.Enqueue(new Vector2Int(i, j));
            }
            Try(c.x + 1, c.y, east[c.x, c.y]);
            Try(c.x - 1, c.y, c.x > 0 && east[c.x - 1, c.y]);
            Try(c.x, c.y + 1, north[c.x, c.y]);
            Try(c.x, c.y - 1, c.y > 0 && north[c.x, c.y - 1]);
        }
        int total = 0;
        for (int i = 0; i < nx; i++) for (int j = 0; j < nz; j++) if (carvable(i, j)) total++;
        return count >= total;
    }

    /// Porte de studio abandonnée dans l'ouverture d'entrée.
    static void BuildMazeStudioDoor(LevelDef L, MazeEdge e)
    {
        const float w = 1.1f;
        var m = Mat("Porte_Studio_0", new Color(0.45f, 0.38f, 0.32f));
        double roll = _rng.NextDouble();
        if (roll < 0.2)
        {
            // Porte arrachée, couchée par terre dans la salle
            var c = e.center + e.inward * 1.4f + Vector3.up * 0.03f;
            Cube(L.props, "Porte_Arrachee", c, new Vector3(w - 0.04f, 0.05f, DoorH - 0.12f),
                Quaternion.LookRotation(e.inward, Vector3.up) * Quaternion.Euler(0, Rand(-20f, 20f), 0), m, true, _layerFurniture);
            return;
        }
        Vector3 along = e.alongX ? Vector3.right : Vector3.forward;
        var hinge = e.center - along * (w * 0.5f - 0.02f) + Vector3.up * 0.01f;
        var door = BuildDoor(DoorsRoot(L), "Porte_Studio_Abandonne", hinge, e.alongX ? 0f : -90f, w - 0.04f, DoorH - 0.12f, m, "la porte du studio");
        if (roll < 0.75) door.startAngle = (_rng.NextDouble() < 0.5 ? -1f : 1f) * Rand(20f, 85f); // entrouverte
    }

    /// Studio abandonné de 8 × 8 m. "back" = mur opposé à la porte.
    static void FurnishMazeStudio(LevelDef L, MazeStudio st, bool hidingTutorial)
    {
        var mp = Mat("Meuble_" + L.number, Color.Lerp(L.wall, L.floor, 0.5f) * 0.9f);
        float xa = MazeX0 + st.rect.x * MazeP, za = MazeZ0 + st.rect.y * MazeP;
        var center = new Vector3(xa + MazeP, L.y, za + MazeP);
        Vector3 f = st.entrance.inward * -1f;                    // du fond vers la porte
        Vector3 r = Quaternion.Euler(0f, 90f, 0f) * f;           // vers la droite (en regardant la porte)
        float yawF = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
        float yawR = yawF + 90f, yawL = yawF - 90f;
        const float size = 2f * MazeP;
        Vector3 P(float u, float d) => center + r * (u - size * 0.5f) + f * (d - size * 0.5f + 0.15f);

        // Saccage : rotation aléatoire, ou meuble renversé
        GameObject Wreck(GameObject go, float topple)
        {
            if (go == null) return null;
            go.transform.Rotate(0f, Rand(-18f, 18f), 0f, Space.World);
            if (_rng.NextDouble() < topple && TryBounds(go, out var b))
            {
                var axis = _rng.NextDouble() < 0.5 ? r : f;
                go.transform.RotateAround(new Vector3(b.center.x, b.min.y, b.center.z), axis, (_rng.NextDouble() < 0.5 ? 1 : -1) * 90f);
                Ground(go, new Vector3(b.center.x, L.y, b.center.z));
            }
            return go;
        }

        // Lit contre le mur du fond (souvent de travers), table de nuit renversée
        Wreck(Piece(L, MegaBeds, KFurniture + "bedSingle", P(1.2f, 1.1f), yawF, new Vector3(1.6f, 1.1f, 2.15f), mp), 0f);
        Wreck(Piece(L, MegaNightstands, KFurniture + "sideTable", P(2.35f, 0.3f), yawF, new Vector3(0.5f, 0.6f, 0.45f), mp), 0.6f);

        // Armoire contre le mur de gauche : reste debout dans la salle du tutoriel (cachette)
        var wardrobe = Piece(L, MegaClosets, KFurniture + "bookcaseClosedDoors", P(0.35f, 4.2f), yawR, new Vector3(0.62f, 2.1f, 1.6f), mp);
        if (hidingTutorial && wardrobe != null)
            Marker(L, "Tuto_Cachette (armoire)", wardrobe.transform.position, Color.yellow);
        if (hidingTutorial) // point de vue pour les captures (depuis la porte, vers le fond)
        {
            Marker(L, "Vue_Studio_Abandonne", st.entrance.center + st.entrance.inward * 0.7f, Color.white);
            var mk = L.markers.Find("◆ Vue_Studio_Abandonne");
            if (mk != null) mk.rotation = Quaternion.LookRotation(st.entrance.inward, Vector3.up);
        }
        else Wreck(wardrobe, 0.45f);

        // Bureau et chaise de bureau contre le mur de droite
        Wreck(Piece(L, MegaDesks, KFurniture + "desk", P(size - 0.4f, 1.6f), yawL, new Vector3(0.7f, 0.78f, 1.4f), mp), 0.25f);
        Wreck(Piece(L, MegaChairs, KFurniture + "chairDesk", P(size - 1.5f, 2.4f), yawR, new Vector3(0.65f, 1.1f, 0.65f), mp), 0.7f);

        // Kitchenette contre le mur de droite, côté porte (frigo parfois tombé)
        Wreck(Piece(L, MegaCabinets, null, P(size - 0.35f, 4.4f), yawL, new Vector3(0.62f, 0.9f, 0.62f), mp), 0.1f);
        Wreck(Piece(L, MegaSinks, null, P(size - 0.35f, 5.05f), yawL, new Vector3(0.62f, 0.9f, 0.62f), mp), 0.1f);
        Wreck(Piece(L, MegaFridges, null, P(size - 0.38f, 5.8f), yawL, new Vector3(0.66f, 1.75f, 0.66f), mp), 0.35f);

        // Débris : cartons, gravats, dalles de plafond tombées
        ScatterBoxes(L, Rect.MinMaxRect(Mathf.Min(P(2f, 3f).x, P(6f, 6.5f).x), Mathf.Min(P(2f, 3f).z, P(6f, 6.5f).z),
                                        Mathf.Max(P(2f, 3f).x, P(6f, 6.5f).x), Mathf.Max(P(2f, 3f).z, P(6f, 6.5f).z)), _rng.Next(2, 5));
        var mc = Mat("Plafond_" + L.number, L.wall * 0.8f);
        for (int k = 0; k < 3; k++)
            Cube(L.props, "Dalle_Plafond_Tombee", P(Rand(1.5f, 6.5f), Rand(2f, 6.5f)) + Vector3.up * 0.1f, new Vector3(1.2f, 0.04f, 0.6f),
                Quaternion.Euler(Rand(-25f, 25f), Rand(0, 360), Rand(-10f, 10f)), mc, true, _layerFurniture);
        var c0 = P(Rand(2.5f, 5.5f), Rand(3f, 6f));
        RockPile(L, new Rect(c0.x - 0.6f, c0.z - 0.6f, 1.2f, 1.2f), 2, mp);

        // Brèches : gravats de part et d'autre du mur effondré (le passage reste libre au milieu)
        foreach (var e in st.breaches)
        {
            Vector3 along = e.alongX ? Vector3.right : Vector3.forward;
            foreach (var side in new[] { -1.55f, 1.55f })
            {
                var p = e.center + along * side + e.inward * 0.4f;
                RockPile(L, new Rect(p.x - 0.35f, p.z - 0.35f, 0.7f, 0.7f), 2, mp);
            }
        }
    }
}
