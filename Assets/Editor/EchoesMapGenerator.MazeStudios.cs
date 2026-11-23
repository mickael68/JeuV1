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
        public bool shortSide;         // petit côté d'un studio 4 × 8 m
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
        bool shortNS = rm.width < rm.height, shortWE = rm.height < rm.width; // petits côtés d'un studio 4 × 8 m
        for (int i = rm.x; i < rm.x + rm.width; i++)
        {
            int ii = i;
            float cx = MazeX0 + (i + 0.5f) * MazeP;
            int js = rm.y;                    // sud : extérieur (i, y-1)
            list.Add(new MazeEdge { outside = new Vector2Int(i, js - 1), alongX = true, wallKey = new Vector2Int(i, js), shortSide = shortNS,
                center = new Vector3(cx, y, MazeZ0 + js * MazeP), inward = Vector3.forward,
                getLink = () => js - 1 >= 0 && north[ii, js - 1], setLink = v => { if (js - 1 >= 0) north[ii, js - 1] = v; } });
            int jn = rm.y + rm.height - 1;    // nord : extérieur (i, jn+1)
            list.Add(new MazeEdge { outside = new Vector2Int(i, jn + 1), alongX = true, wallKey = new Vector2Int(i, jn + 1), shortSide = shortNS,
                center = new Vector3(cx, y, MazeZ0 + (jn + 1) * MazeP), inward = Vector3.back,
                getLink = () => jn + 1 < north.GetLength(1) && north[ii, jn], setLink = v => { if (jn + 1 < north.GetLength(1)) north[ii, jn] = v; } });
        }
        for (int j = rm.y; j < rm.y + rm.height; j++)
        {
            int jj = j;
            float cz = MazeZ0 + (j + 0.5f) * MazeP;
            int iw = rm.x;                    // ouest : extérieur (x-1, j)
            list.Add(new MazeEdge { outside = new Vector2Int(iw - 1, j), alongX = false, wallKey = new Vector2Int(iw, j), shortSide = shortWE,
                center = new Vector3(MazeX0 + iw * MazeP, y, cz), inward = Vector3.right,
                getLink = () => iw - 1 >= 0 && east[iw - 1, jj], setLink = v => { if (iw - 1 >= 0) east[iw - 1, jj] = v; } });
            int ie = rm.x + rm.width - 1;     // est : extérieur (ie+1, j)
            list.Add(new MazeEdge { outside = new Vector2Int(ie + 1, j), alongX = false, wallKey = new Vector2Int(ie + 1, j), shortSide = shortWE,
                center = new Vector3(MazeX0 + (ie + 1) * MazeP, y, cz), inward = Vector3.left,
                getLink = () => ie + 1 < east.GetLength(0) && east[ie, jj], setLink = v => { if (ie + 1 < east.GetLength(0)) east[ie, jj] = v; } });
        }
        return list;
    }

    /// Referme les salles (une porte chacune) sans jamais couper le labyrinthe.
    /// Les studios de 4 × 8 m ont leur porte sur un petit côté (comme les studios des étages).
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
            bool narrow = rm.width != rm.height;
            var pool = narrow ? edges.FindAll(e => e.shortSide) : edges;
            if (pool.Count == 0) pool = edges;
            var openPool = pool.FindAll(e => e.getLink());
            st.entrance = openPool.Count > 0 ? openPool[_rng.Next(openPool.Count)] : pool[_rng.Next(pool.Count)];
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
        if (false) // (plus de porte arrachée : chaque ouverture a sa porte)
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
        door.openClip = SndDoorCreak; // vieille porte qui grince
        if (roll < 0.6) door.startAngle = (_rng.NextDouble() < 0.5 ? -1f : 1f) * Rand(20f, 85f); // entrouverte
    }

    /// Studio abandonné (8 × 8 m ou 4 × 8 m). "back" = mur opposé à la porte.
    static void FurnishMazeStudio(LevelDef L, MazeStudio st, bool hidingTutorial)
    {
        var mp = Mat("Meuble_" + L.number, Color.Lerp(L.wall, L.floor, 0.5f) * 0.9f);
        var rm = st.rect;
        float xa = MazeX0 + rm.x * MazeP, za = MazeZ0 + rm.y * MazeP;
        var center = new Vector3(xa + rm.width * MazeP * 0.5f, L.y, za + rm.height * MazeP * 0.5f);
        Vector3 f = st.entrance.inward * -1f;                    // du fond vers la porte
        Vector3 r = Quaternion.Euler(0f, 90f, 0f) * f;           // vers la droite (en regardant la porte)
        float yawF = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
        float yawR = yawF + 90f, yawL = yawF - 90f;
        float sizeX = rm.width * MazeP, sizeZ = rm.height * MazeP;
        float W = Mathf.Abs(r.x) * sizeX + Mathf.Abs(r.z) * sizeZ;   // largeur (gauche -> droite)
        float D = Mathf.Abs(f.x) * sizeX + Mathf.Abs(f.z) * sizeZ;   // profondeur (fond -> porte)
        Vector3 P(float u, float d) => center + r * (u - W * 0.5f) + f * (d - D * 0.5f + 0.15f);

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

        bool narrow = W < MazeP * 1.5f; // studio de 4 m de large, comme ceux des étages

        // Lit contre le mur du fond (souvent de travers), table de nuit renversée
        Wreck(Piece(L, MegaBeds, KFurniture + "bedSingle", P(narrow ? 0.85f : 1.2f, 1.1f), yawF, new Vector3(1.6f, 1.1f, 2.15f), mp), 0f);
        Wreck(Piece(L, MegaNightstands, KFurniture + "sideTable", P(narrow ? 1.95f : 2.35f, 0.3f), yawF, new Vector3(0.5f, 0.6f, 0.45f), mp), 0.6f);

        // Bureau et chaise de bureau contre le mur de droite
        Wreck(Piece(L, MegaDesks, KFurniture + "desk", P(W - 0.42f, narrow ? 1.3f : 1.6f), yawL, new Vector3(0.7f, 0.78f, 1.4f), mp), 0.25f);
        Wreck(Piece(L, MegaChairs, KFurniture + "chairDesk", P(W - 1.3f, narrow ? 2.1f : 2.4f), yawR, new Vector3(0.65f, 1.1f, 0.65f), mp), 0.7f);

        // Armoire : mur de gauche (grand studio) ou de droite (petit) ; debout pour la cachette du tutoriel
        var wardrobe = narrow
            ? Piece(L, MegaClosets, KFurniture + "bookcaseClosedDoors", P(W - 0.35f, 3.7f), yawL, new Vector3(0.62f, 2.1f, 1.6f), mp)
            : Piece(L, MegaClosets, KFurniture + "bookcaseClosedDoors", P(0.35f, 4.2f), yawR, new Vector3(0.62f, 2.1f, 1.6f), mp);
        if (hidingTutorial && wardrobe != null)
            Marker(L, "Tuto_Cachette (armoire)", wardrobe.transform.position, Color.yellow);
        else Wreck(wardrobe, 0.45f);
        if (hidingTutorial) // point de vue pour les captures (depuis la porte, vers le fond)
        {
            Marker(L, "Vue_Studio_Abandonne", st.entrance.center + st.entrance.inward * 0.7f, Color.white);
            var mk = L.markers.Find("◆ Vue_Studio_Abandonne");
            if (mk != null) mk.rotation = Quaternion.LookRotation(st.entrance.inward, Vector3.up);
        }

        // Kitchenette côté porte : mur de droite (grand studio) ou de gauche (petit) ; frigo parfois tombé
        float ku = narrow ? 0.35f : W - 0.35f, kyaw = narrow ? yawR : yawL;
        float kd = narrow ? 4.7f : 4.4f;
        Wreck(Piece(L, MegaCabinets, null, P(ku, kd), kyaw, new Vector3(0.62f, 0.9f, 0.62f), mp), 0.1f);
        Wreck(Piece(L, MegaSinks, null, P(ku, kd + 0.65f), kyaw, new Vector3(0.62f, 0.9f, 0.62f), mp), 0.1f);
        Wreck(Piece(L, MegaFridges, null, P(ku + (narrow ? 0.03f : -0.03f), kd + 1.4f), kyaw, new Vector3(0.66f, 1.75f, 0.66f), mp), 0.35f);

        // Objets : la clé de R. Lefèvre dans le studio du tutoriel (près du lit), parfois une pile
        if (hidingTutorial) PlaceItem(L, "cle_cave", P(narrow ? 1.9f : 2.3f, 1.7f), Rand(0f, 360f));
        MaybeBattery(L, P(Rand(1f, W - 1f), Rand(2.5f, D - 2f)), 0.35);

        // Débris : cartons, gravats, dalles de plafond tombées
        float u0 = narrow ? 1.1f : 2f, u1 = narrow ? W - 1.1f : 6f;
        var pa = P(u0, 3f); var pb = P(u1, D - 1.5f);
        ScatterBoxes(L, Rect.MinMaxRect(Mathf.Min(pa.x, pb.x), Mathf.Min(pa.z, pb.z), Mathf.Max(pa.x, pb.x), Mathf.Max(pa.z, pb.z)), _rng.Next(1, narrow ? 3 : 5));
        var mc = Mat("Plafond_" + L.number, L.wall * 0.8f);
        for (int k = 0; k < (narrow ? 2 : 3); k++)
            Cube(L.props, "Dalle_Plafond_Tombee", P(Rand(u0, u1), Rand(2f, D - 1.5f)) + Vector3.up * 0.1f, new Vector3(1.2f, 0.04f, 0.6f),
                Quaternion.Euler(Rand(-25f, 25f), Rand(0, 360), Rand(-10f, 10f)), mc, true, _layerFurniture);
        var c0 = P(Rand(u0, u1), Rand(3f, D - 2f));
        RockPile(L, new Rect(c0.x - 0.5f, c0.z - 0.5f, 1f, 1f), narrow ? 1 : 2, mp);

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

        // Néon au plafond (le plus souvent défaillant)
        double roll = _rng.NextDouble();
        var mode = roll < 0.45 ? FailingLight.Mode.Failing : roll < 0.75 ? FailingLight.Mode.Dying : FailingLight.Mode.Dead;
        Neon(L, P(W * 0.5f, D * 0.45f) + Vector3.up * L.h, yawF + 90f, mode, _rng.NextDouble() < 0.35);
    }

    // ───────────────────────── Néons défaillants ─────────────────────────

    /// Réglette de néon au plafond (ceiling = point du plafond). hanging : décrochée d'un côté, elle pend.
    static void Neon(LevelDef L, Vector3 ceiling, float yaw, FailingLight.Mode mode, bool hanging)
    {
        var root = new GameObject("Neon_" + mode);
        root.transform.SetParent(L.props, false);
        root.transform.SetPositionAndRotation(ceiling, Quaternion.Euler(0f, yaw, 0f));
        var rot = root.transform.rotation;

        var housing = Mat("Neon_Boitier", new Color(0.55f, 0.55f, 0.52f), 0.3f);
        var tubeMat = NeonTubeMat();
        Cube(root.transform, "Boitier", ceiling + Vector3.down * 0.03f, new Vector3(1.25f, 0.05f, 0.16f), rot, housing, false, -1);
        var tube = Cube(root.transform, "Tube", ceiling + Vector3.down * 0.08f, new Vector3(1.15f, 0.04f, 0.04f), rot, tubeMat, false, -1);
        tube.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var lightGo = new GameObject("Lumiere");
        lightGo.transform.SetParent(root.transform, false);
        lightGo.transform.position = ceiling + Vector3.down * 0.25f;
        var l = lightGo.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(0.85f, 0.95f, 1f);
        l.intensity = 1.6f;
        l.range = 7.5f;
        l.shadows = LightShadows.None;
        var fl = lightGo.AddComponent<FailingLight>();
        fl.mode = mode;
        fl.tube = tube.GetComponent<Renderer>();
        if (mode != FailingLight.Mode.Dead) fl.buzz = SndNeonBuzz;

        if (hanging) // décrochée : pivote autour d'une extrémité
        {
            Vector3 end = ceiling + rot * Vector3.right * 0.6f;
            root.transform.RotateAround(end, rot * Vector3.forward, Rand(25f, 55f) * (_rng.NextDouble() < 0.5 ? 1f : -1f));
        }
        NonStatic(root);
    }

    static Material NeonTubeMat()
    {
        var m = Mat("Neon_Tube", new Color(0.9f, 0.95f, 1f), 0.6f);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", new Color(0.85f, 0.95f, 1f) * 2.5f);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        UnityEditor.EditorUtility.SetDirty(m);
        return m;
    }
}
