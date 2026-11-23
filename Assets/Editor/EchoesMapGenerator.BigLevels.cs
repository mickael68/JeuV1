using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Grands niveaux -1 et -2 (même emprise que le labyrinthe du niveau 0 : 80 × 56 m, nœuds de 4 m) :
/// réseau de couloirs avec boucles, locaux fermés par une porte (caves, laverie… / chaufferie, salle électrique…),
/// et sous le -2, le conduit d'aération vers le -3 : un long labyrinthe où l'on rampe, avec impasses et pièges.
/// </summary>
public static partial class EchoesMapGenerator
{
    enum RoomType { Cave, Laverie, Velos, Poubelles, Stockage, Chaufferie, Electrique, Pompes, Atelier, Reserve }

    class NodeRoom
    {
        public MazeStudio plan;
        public RoomType type;
    }

    // Conduit d'aération (labyrinthe rampant) : cellules de 2 m sous le sud du -2, hors de l'emprise du -3
    const float DuctCell = 2f, DuctH = 1.3f, DuctX0 = MazeX0, DuctZ0 = MazeZ0;
    const int DuctNI = 40, DuctNJ = 9;                 // x ∈ [-40, 40], z ∈ [-30, -12]
    static Vector2Int _ductEntryNode = new Vector2Int(-1, -1); // nœud du -2 où se trouve la trappe
    const float DuctFloorOffset = 3.2f;                 // sol du conduit = sol du -2 - 3,2 m
    static float DuctY => _levels[-2].y - DuctFloorOffset;

    // ───────────────────────── Grands niveaux ─────────────────────────

    static void BuildNodeLevel(LevelDef L)
    {
        L.halfX = -MazeX0;
        L.zMin = MazeZ0;
        L.zMax = MazeZ0 + MazeNZ * MazeP;
        bool technical = L.kind == Kind.Technical;
        var mw = Mat("Mur_" + L.number, L.wall);
        var mf = Mat("Sol_" + L.number, L.floor);
        var mc = Mat("Plafond_" + L.number, L.wall * 0.8f);
        var mp = Mat("Meuble_" + L.number, Color.Lerp(L.wall, L.floor, 0.5f) * 0.9f);
        float y0 = L.y, y1 = L.y + L.h;

        var open = new bool[MazeNX, MazeNZ];
        for (int i = 0; i < MazeNX; i++) for (int j = 0; j < MazeNZ; j++) open[i, j] = true;
        // Cage d'escalier est (porte à x = 20, z = -3) ; gaine de l'ascenseur au-dessus (prudence)
        for (int i = 15; i <= 16; i++) for (int j = 6; j <= 8; j++) open[i, j] = false;
        if (L.number == -1) open[ElevNodeI, ElevNodeJ] = false;
        bool Open(int i, int j) => i >= 0 && i < MazeNX && j >= 0 && j < MazeNZ && open[i, j];

        var east = new bool[MazeNX, MazeNZ];
        var north = new bool[MazeNX, MazeNZ];
        void Link(int i, int j, int ni, int nj)
        {
            if (ni > i) east[i, j] = true; else if (ni < i) east[ni, j] = true;
            else if (nj > j) north[i, j] = true; else north[i, nj] = true;
        }

        // 1) Couloirs : arbre couvrant depuis la porte de l'escalier, puis beaucoup de boucles (réseau, pas labyrinthe)
        var start = new Vector2Int(MazeEastDoorI - 1, MazeDoorJ);
        var visited = new bool[MazeNX, MazeNZ];
        var stack = new Stack<Vector2Int>();
        stack.Push(start); visited[start.x, start.y] = true;
        var dirs = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
        var choices = new List<Vector2Int>();
        while (stack.Count > 0)
        {
            var c = stack.Peek();
            choices.Clear();
            foreach (var d in dirs) { var n = c + d; if (Open(n.x, n.y) && !visited[n.x, n.y]) choices.Add(n); }
            if (choices.Count == 0) { stack.Pop(); continue; }
            var next = choices[_rng.Next(choices.Count)];
            Link(c.x, c.y, next.x, next.y);
            visited[next.x, next.y] = true;
            stack.Push(next);
        }

        // 2) Locaux (fermés par une porte)
        var special = new int[MazeNX, MazeNZ];
        var rooms = new List<RectInt>();
        int wanted = technical ? 11 : 17;
        for (int tries = 0; tries < 3000 && rooms.Count < wanted; tries++)
        {
            int w = 1, h = 1;
            double shape = _rng.NextDouble();
            if (technical) { if (shape < 0.4) { w = 2; h = 2; } else if (shape < 0.7) { w = 2; h = 1; } else { w = 1; h = 2; } }
            else { if (shape < 0.45) { w = 1; h = 1; } else if (shape < 0.65) { w = 2; h = 1; } else if (shape < 0.85) { w = 1; h = 2; } else { w = 2; h = 2; } }
            int i = _rng.Next(MazeNX - w + 1), j = _rng.Next(MazeNZ - h + 1);
            bool ok = true;
            for (int a = i - 1; a <= i + w && ok; a++)
                for (int b = j - 1; b <= j + h && ok; b++)
                {
                    bool inside = a >= i && a < i + w && b >= j && b < j + h;
                    if (inside && !Open(a, b)) ok = false;
                    if (a >= 0 && a < MazeNX && b >= 0 && b < MazeNZ && special[a, b] != 0) ok = false;
                }
            if (ok && Mathf.Abs(i - start.x) + Mathf.Abs(j - start.y) < 2) ok = false;
            if (!ok) continue;
            for (int a = i; a < i + w; a++)
                for (int b = j; b < j + h; b++)
                {
                    special[a, b] = rooms.Count + 2;
                    if (a < i + w - 1) east[a, b] = true;
                    if (b < j + h - 1) north[a, b] = true;
                }
            rooms.Add(new RectInt(i, j, w, h));
        }

        // 3) Boucles : couloirs qui se recoupent
        float loops = technical ? 0.22f : 0.35f;
        for (int i = 0; i < MazeNX; i++)
            for (int j = 0; j < MazeNZ; j++)
            {
                if (!Open(i, j) || special[i, j] != 0) continue;
                if (Open(i + 1, j) && special[i + 1, j] == 0 && !east[i, j] && _rng.NextDouble() < loops) east[i, j] = true;
                if (Open(i, j + 1) && special[i, j + 1] == 0 && !north[i, j] && _rng.NextDouble() < loops) north[i, j] = true;
            }

        // 4) Locaux fermés : une porte chacun, brèches si nécessaire pour ne rien isoler
        var plans = PlanMazeStudios(rooms, east, north, Open, start, L.y);
        var nodeRooms = new List<NodeRoom>();
        foreach (var pl in plans) nodeRooms.Add(new NodeRoom { plan = pl, type = PickRoomType(technical, pl.rect, nodeRooms.Count) });

        // 5) Trappe du conduit d'aération (-2) : impasse au sud, loin de la sortie du conduit
        int Degree(int i, int j) =>
            (east[i, j] ? 1 : 0) + (i > 0 && east[i - 1, j] ? 1 : 0) + (north[i, j] ? 1 : 0) + (j > 0 && north[i, j - 1] ? 1 : 0);
        if (technical)
        {
            _ductEntryNode = new Vector2Int(-1, -1);
            float best = -1f;
            for (int i = 0; i < MazeNX; i++)
                for (int j = 0; j <= 3; j++)
                {
                    if (!Open(i, j) || special[i, j] != 0) continue;
                    float score = Mathf.Abs(i - 12) + (Degree(i, j) == 1 ? 8f : 0f) + (3 - j);
                    if (score > best) { best = score; _ductEntryNode = new Vector2Int(i, j); }
                }
        }

        // 6) Sols et plafonds (bandes), trou de la trappe au -2
        Vector3 Node(int i, int j) => new Vector3(MazeX0 + (i + 0.5f) * MazeP, y0, MazeZ0 + (j + 0.5f) * MazeP);
        for (int j = 0; j < MazeNZ; j++)
        {
            int s = -1;
            for (int i = 0; i <= MazeNX; i++)
            {
                bool trap = technical && i < MazeNX && _ductEntryNode == new Vector2Int(i, j);
                bool o = i < MazeNX && open[i, j] && !trap;
                if (o && s < 0) s = i;
                if (!o && s >= 0)
                {
                    float xa = MazeX0 + s * MazeP, xb = MazeX0 + i * MazeP, za = MazeZ0 + j * MazeP, zb = za + MazeP;
                    Box(L.root, "Sol", new Vector3(xa, y0 - Slab, za), new Vector3(xb, y0, zb), mf, _layerGround);
                    Box(L.root, "Plafond", new Vector3(xa, y1, za), new Vector3(xb, y1 + Slab, zb), mc, _layerWalls);
                    s = -1;
                }
                if (trap)
                {
                    var c = Node(i, j) + new Vector3(1f, 0f, 1f); // centre d'une case du conduit
                    float xa = MazeX0 + i * MazeP, za = MazeZ0 + j * MazeP;
                    SlabWithHole(L.root, "Sol", xa, xa + MazeP, za, za + MazeP, y0 - Slab, y0,
                        new Rect(c.x - 0.6f, c.z - 0.6f, 1.2f, 1.2f), mf, _layerGround);
                    Box(L.root, "Plafond", new Vector3(xa, y1, za), new Vector3(xa + MazeP, y1 + Slab, za + MazeP), mc, _layerWalls);
                }
            }
        }

        // 7) Murs avec ouvertures : porte de l'escalier, portes des locaux
        var holesH = new Dictionary<Vector2Int, Hole>();
        var holesV = new Dictionary<Vector2Int, Hole> { [new Vector2Int(MazeEastDoorI, MazeDoorJ)] = new Hole(DoorZ, DoorW, y0, y0 + DoorH) };
        foreach (var nr in nodeRooms)
        {
            var e = nr.plan.entrance;
            var hole = new Hole(e.alongX ? e.center.x : e.center.z, 1.2f, y0, y0 + DoorH - 0.1f);
            if (e.alongX) holesH[e.wallKey] = hole; else holesV[e.wallKey] = hole;
        }
        NodeWalls(L, open, east, north, holesH, holesV, mw, y0, y1);

        // 8) Locaux : porte métallique + contenu selon le type
        var mDoor = Mat(technical ? "Porte_Technique" : "Porte_Cave", technical ? new Color(0.4f, 0.42f, 0.4f) : new Color(0.5f, 0.45f, 0.38f), 0.4f);
        var lockedRoom = !technical && nodeRooms.Count > 0 ? nodeRooms[nodeRooms.Count / 2] : null; // cave de R. Lefèvre
        foreach (var nr in nodeRooms)
        {
            var e = nr.plan.entrance;
            Vector3 along = e.alongX ? Vector3.right : Vector3.forward;
            var hinge = e.center - along * (0.6f - 0.02f) + Vector3.up * 0.01f;
            var door = BuildDoor(DoorsRoot(L), "Porte_" + nr.type, hinge, e.alongX ? 0f : -90f, 1.16f, DoorH - 0.12f, mDoor, "la porte");
            door.openClip = SndDoorCreak;
            if (nr == lockedRoom)
            {
                door.locked = true;
                door.requiredKeyId = "cave_lefevre";
                door.lockedMessage = "Cave fermée à clé. Étiquette sur la porte : « R. Lefèvre ».";
                door.displayName = "la cave de R. Lefèvre";
                PlaceItem(L, "carnet", RoomCenter(nr.plan.rect, L.y) + new Vector3(0.6f, 0f, -0.4f), Rand(0f, 360f));
            }
            else if (_rng.NextDouble() < 0.5) door.startAngle = (_rng.NextDouble() < 0.5 ? -1f : 1f) * Rand(15f, 80f);
            FurnishNodeRoom(L, nr, mp);
            if (nr != lockedRoom) MaybeBattery(L, RoomCenter(nr.plan.rect, L.y) + new Vector3(Rand(-1f, 1f), 0f, Rand(-1f, 1f)), 0.3);
        }

        // 9) Couloirs : tuyaux au plafond (-2), encombrement, lumières (néons fatigués et défaillants)
        var metal = Mat("Metal_" + L.number, new Color(0.35f, 0.33f, 0.3f));
        string[] clutter = technical
            ? new[] { KSurvival + "barrel", KSurvival + "box-large", KSurvival + "box", KFurniture + "cardboardBoxClosed" }
            : new[] { KFurniture + "cardboardBoxClosed", KFurniture + "cardboardBoxOpen", KFurniture + "trashcan", KSurvival + "box", KFurniture + "chair" };
        var lights = new List<Vector2Int>();
        for (int i = 0; i < MazeNX; i++)
            for (int j = 0; j < MazeNZ; j++)
            {
                if (!open[i, j] || special[i, j] != 0) continue;
                var c = Node(i, j);
                if (technical)
                {
                    if (east[i, j]) Cylinder(L.props, "Tuyau", new Vector3(c.x + MazeP * 0.5f, y1 - 0.35f, c.z + 1.4f), 0.13f, MazeP, Quaternion.Euler(0, 0, 90), metal, false);
                    if (north[i, j]) Cylinder(L.props, "Tuyau", new Vector3(c.x - 1.4f, y1 - 0.55f, c.z + MazeP * 0.5f), 0.1f, MazeP, Quaternion.Euler(90, 0, 0), metal, false);
                }
                if (_ductEntryNode == new Vector2Int(i, j) && technical) continue;
                if (i == start.x && j == start.y) continue;
                if (_rng.NextDouble() < 0.22)
                {
                    var off = new Vector3(Rand(-1.3f, 1.3f), 0, Rand(-1.3f, 1.3f));
                    if (_rng.NextDouble() < 0.4) ScatterBoxes(L, new Rect(c.x + off.x - 0.6f, c.z + off.z - 0.6f, 1.2f, 1.2f), _rng.Next(1, 3));
                    else Model(L, clutter[_rng.Next(clutter.Length)], c + off, Rand(0, 360), new Vector3(0.8f, 1f, 0.8f), mp);
                }
                bool far = true;
                foreach (var o in lights) if (Mathf.Abs(o.x - i) + Mathf.Abs(o.y - j) < 3) { far = false; break; }
                if (far && _rng.NextDouble() < 0.6) lights.Add(new Vector2Int(i, j));
            }
        foreach (var n in lights)
        {
            var c = Node(n.x, n.y);
            float yaw = east[n.x, n.y] || (n.x > 0 && east[n.x - 1, n.y]) ? 0f : 90f;
            double roll = _rng.NextDouble();
            if (roll < 0.62) // néon qui marche (grésille un peu, selon l'étage)
            {
                PointLight(L, new Vector3(c.x, y1 - 0.4f, c.z), technical ? 10f : 9f);
                Cube(L.props, "Neon_Tube", new Vector3(c.x, y1 - 0.06f, c.z), new Vector3(1.15f, 0.04f, 0.06f), Quaternion.Euler(0, yaw, 0), NeonTubeMat(), false, -1);
            }
            else
                Neon(L, new Vector3(c.x, y1, c.z), yaw, roll < 0.85 ? FailingLight.Mode.Failing : roll < 0.95 ? FailingLight.Mode.Dying : FailingLight.Mode.Dead, _rng.NextDouble() < 0.3);
        }
        PointLight(L, new Vector3(Node(start.x, start.y).x, y1 - 0.4f, Node(start.x, start.y).z), 9f); // toujours éclairé à l'arrivée

        // 10) Trappe du conduit (-2)
        if (technical && _ductEntryNode.x >= 0)
        {
            var c = Node(_ductEntryNode.x, _ductEntryNode.y) + new Vector3(1f, 0f, 1f);
            var grate = Cube(L.root, "Trappe_Conduit (E pour retirer)", new Vector3(c.x, y0 + 0.02f, c.z), new Vector3(1.3f, 0.04f, 1.3f),
                Quaternion.identity, Mat("Grille_Metal", new Color(0.18f, 0.18f, 0.2f), 0.5f), true, _layerWalls);
            var vg = grate.AddComponent<VentGrate>();
            vg.floorGrate = true;
            NonStatic(grate);
            Model(L, KSurvival + "barrel", c + new Vector3(-1.4f, 0, 1.2f), 0f, new Vector3(0.8f, 1.1f, 0.8f), mp);
            Marker(L, "Trappe_Conduit_Aeration (vers -3)", c + new Vector3(0, 0, 1.4f), Color.magenta);
        }

        if (L.number == -1 && nodeRooms.Count > 0)
            Marker(L, "Premier_Indice", RoomCenter(nodeRooms[nodeRooms.Count / 2].plan.rect, y0), Color.yellow);
        L.patrol = new List<Vector3> { Node(start.x, start.y), Node(4, 3), Node(10, 11), Node(3, 11), Node(17, 2) };
    }

    static Vector3 RoomCenter(RectInt r, float y) =>
        new Vector3(MazeX0 + (r.x + r.width * 0.5f) * MazeP, y, MazeZ0 + (r.y + r.height * 0.5f) * MazeP);

    static RoomType PickRoomType(bool technical, RectInt r, int index)
    {
        bool big = r.width * r.height >= 4, mid = r.width * r.height == 2;
        if (technical)
        {
            if (index == 0) return RoomType.Chaufferie;   // le générateur en panne
            if (big) return _rng.NextDouble() < 0.5 ? RoomType.Chaufferie : RoomType.Pompes;
            double t = _rng.NextDouble();
            return t < 0.35 ? RoomType.Electrique : t < 0.65 ? RoomType.Atelier : RoomType.Reserve;
        }
        if (big) return _rng.NextDouble() < 0.5 ? RoomType.Laverie : RoomType.Velos;
        if (mid) { double t = _rng.NextDouble(); return t < 0.35 ? RoomType.Stockage : t < 0.6 ? RoomType.Poubelles : t < 0.8 ? RoomType.Laverie : RoomType.Velos; }
        return RoomType.Cave;
    }

    /// Murs du réseau de nœuds (fusionnés en segments), avec ouvertures.
    static void NodeWalls(LevelDef L, bool[,] open, bool[,] east, bool[,] north,
        Dictionary<Vector2Int, Hole> holesH, Dictionary<Vector2Int, Hole> holesV, Material mw, float y0, float y1)
    {
        bool Open(int i, int j) => i >= 0 && i < MazeNX && j >= 0 && j < MazeNZ && open[i, j];
        for (int j = 0; j <= MazeNZ; j++)
        {
            float z = MazeZ0 + j * MazeP;
            int s = -1;
            for (int i = 0; i <= MazeNX; i++)
            {
                bool need = false;
                if (i < MazeNX)
                {
                    bool a = Open(i, j - 1), b = Open(i, j);
                    need = a && b ? !north[i, j - 1] : a || b;
                    if (holesH.TryGetValue(new Vector2Int(i, j), out var hh))
                    {
                        WallAlongX(L.root, "Mur_Porte", z, MazeX0 + i * MazeP - Wall / 2, MazeX0 + (i + 1) * MazeP + Wall / 2, y0, y1, mw, hh);
                        need = false;
                    }
                }
                if (need && s < 0) s = i;
                if (!need && s >= 0)
                {
                    Box(L.root, "Mur", new Vector3(MazeX0 + s * MazeP - Wall / 2, y0, z - Wall / 2), new Vector3(MazeX0 + i * MazeP + Wall / 2, y1, z + Wall / 2), mw, _layerWalls);
                    s = -1;
                }
            }
        }
        for (int i = 0; i <= MazeNX; i++)
        {
            float x = MazeX0 + i * MazeP;
            int s = -1;
            for (int j = 0; j <= MazeNZ; j++)
            {
                bool need = false;
                if (j < MazeNZ)
                {
                    bool a = Open(i - 1, j), b = Open(i, j);
                    need = a && b ? !east[i - 1, j] : a || b;
                    if (holesV.TryGetValue(new Vector2Int(i, j), out var hv))
                    {
                        WallAlongZ(L.root, "Mur_Porte", x, MazeZ0 + j * MazeP, MazeZ0 + (j + 1) * MazeP, y0, y1, mw, hv);
                        need = false;
                    }
                }
                if (need && s < 0) s = j;
                if (!need && s >= 0)
                {
                    Box(L.root, "Mur", new Vector3(x - Wall / 2, y0, MazeZ0 + s * MazeP - Wall / 2), new Vector3(x + Wall / 2, y1, MazeZ0 + j * MazeP + Wall / 2), mw, _layerWalls);
                    s = -1;
                }
            }
        }
    }

    /// Contenu d'un local. Le repère "fond" est le mur opposé à la porte.
    static void FurnishNodeRoom(LevelDef L, NodeRoom nr, Material mp)
    {
        var st = nr.plan;
        var rm = st.rect;
        var center = RoomCenter(rm, L.y);
        Vector3 f = -st.entrance.inward;
        Vector3 r = Quaternion.Euler(0f, 90f, 0f) * f;
        float yawF = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg, yawR = yawF + 90f, yawL = yawF - 90f;
        float sx = rm.width * MazeP, sz = rm.height * MazeP;
        float W = Mathf.Abs(r.x) * sx + Mathf.Abs(r.z) * sz, D = Mathf.Abs(f.x) * sx + Mathf.Abs(f.z) * sz;
        Vector3 P(float u, float d) => center + r * (u - W * 0.5f) + f * (d - D * 0.5f + 0.15f);
        var metal = Mat("Metal_" + L.number, new Color(0.35f, 0.33f, 0.3f));
        float y = L.y;

        switch (nr.type)
        {
            case RoomType.Cave: // cave individuelle : cartons, vieux meubles, vélo
                Model(L, KFurniture + "bookcaseOpen", P(0.35f, D * 0.5f), yawR, new Vector3(0.5f, 2f, 1.8f), mp);
                ScatterBoxes(L, Rect.MinMaxRect(Mathf.Min(P(1f, 0.5f).x, P(W - 0.5f, 2f).x), Mathf.Min(P(1f, 0.5f).z, P(W - 0.5f, 2f).z),
                    Mathf.Max(P(1f, 0.5f).x, P(W - 0.5f, 2f).x), Mathf.Max(P(1f, 0.5f).z, P(W - 0.5f, 2f).z)), _rng.Next(3, 7));
                if (_rng.NextDouble() < 0.4) Model(L, KFurniture + "chair", P(W - 0.7f, D - 1.2f), Rand(0, 360), new Vector3(0.6f, 1f, 0.6f), mp);
                break;

            case RoomType.Laverie:
                for (float u = 0.6f; u < W - 0.4f; u += 0.85f)
                    Model(L, KFurniture + (_rng.NextDouble() < 0.35 ? "dryer" : "washer"), P(u, 0.45f), yawF, new Vector3(0.75f, 0.9f, 0.75f), mp);
                Model(L, KFurniture + "table", P(W * 0.5f, D * 0.6f), yawF, new Vector3(Mathf.Min(2.5f, W - 1f), 0.9f, 1f), mp);
                Model(L, KFurniture + "trashcan", P(W - 0.5f, D - 1f), yawF, new Vector3(0.5f, 0.8f, 0.5f), mp);
                Prop(L, "Panier_Linge", P(0.6f, D * 0.6f) + Vector3.up * 0.25f, new Vector3(0.5f, 0.5f, 0.4f), mp, Rand(0, 90));
                break;

            case RoomType.Velos:
                for (float u = 0.6f; u < W - 0.4f; u += 0.7f)
                    Prop(L, "Velo", P(u, 1.2f) + Vector3.up * 0.5f, new Vector3(0.08f, 1f, 1.8f), metal, yawF);
                if (D > 5f)
                    for (float u = 0.6f; u < W - 0.4f; u += 0.7f)
                        Prop(L, "Velo", P(u, D - 2.5f) + Vector3.up * 0.5f, new Vector3(0.08f, 1f, 1.8f), metal, yawF + Rand(-10f, 10f));
                break;

            case RoomType.Poubelles:
                for (float u = 0.9f; u < W - 0.6f; u += 1.6f)
                    Model(L, KFurniture + "trashcan", P(u, 0.8f), yawF, new Vector3(1.2f, 1.3f, 1.2f), mp);
                ScatterBoxes(L, new Rect(P(W * 0.5f, D * 0.5f).x - 1f, P(W * 0.5f, D * 0.5f).z - 1f, 2f, 2f), 3);
                break;

            case RoomType.Stockage:
            case RoomType.Reserve:
                for (float u = 1.1f; u < W - 0.8f; u += 2.1f)
                    Model(L, KFurniture + "bookcaseOpen", P(u, 0.35f), yawF, new Vector3(2f, 2.2f, 0.5f), mp);
                for (float d = 1.6f; d < D - 2f; d += 2.1f)
                    Model(L, KFurniture + "bookcaseOpen", P(0.3f, d), yawR, new Vector3(0.5f, 2.2f, 2f), mp);
                ScatterBoxes(L, new Rect(P(W * 0.5f, D * 0.5f).x - 1.2f, P(W * 0.5f, D * 0.5f).z - 1.2f, 2.4f, 2.4f), _rng.Next(3, 6));
                break;

            case RoomType.Chaufferie:
            {
                float n = Mathf.Max(1, Mathf.Floor((W - 1f) / 2.8f));
                for (int k = 0; k < n; k++)
                    Cylinder(L.props, "Chaudiere", P(1.5f + k * 2.8f, 1.5f) + Vector3.up * 1.5f, 1.1f, 3f, Quaternion.identity, metal, true);
                Cylinder(L.props, "Tuyau", P(W * 0.5f, 2.8f) + Vector3.up * (L.h - 0.6f), 0.18f, W - 0.4f, Quaternion.Euler(0, yawF + 90f, 90), metal, false);
                if (!_generatorMarked) { Marker(L, "Generateur_Panne", P(W * 0.5f, D - 1.5f), Color.yellow); _generatorMarked = true; }
                Model(L, KSurvival + "barrel", P(W - 0.6f, D - 0.9f), 0f, new Vector3(0.8f, 1.1f, 0.8f), mp);
                break;
            }

            case RoomType.Electrique:
                for (float u = 0.6f; u < W - 0.4f; u += 1.1f)
                    Prop(L, "Armoire_Electrique", P(u, 0.25f) + Vector3.up * 1f, new Vector3(1f, 2f, 0.4f), metal, yawF);
                Model(L, KSurvival + "metal-panel-screws", P(W - 0.2f, D * 0.5f), yawL, new Vector3(0.2f, 2.2f, 1.6f), metal);
                break;

            case RoomType.Pompes:
                for (float u = 1.2f; u < W - 0.8f; u += 2.4f)
                    for (float d = 1.4f; d < D - 1.5f; d += 2.6f)
                        Cylinder(L.props, "Pompe", P(u, d) + Vector3.up * 0.6f, 0.5f, 1.2f, Quaternion.Euler(0, 0, 90), metal, true);
                break;

            case RoomType.Atelier:
                Model(L, KSurvival + "workbench", P(W * 0.5f, 0.45f), yawF, new Vector3(Mathf.Min(2.2f, W - 0.6f), 1f, 0.8f), mp);
                Model(L, KSurvival + "tool-shovel", P(0.3f, 1.5f), yawR, new Vector3(0.3f, 1.2f, 0.3f), mp, false);
                Model(L, KSurvival + "bucket", P(W - 0.5f, 1.2f), 0f, new Vector3(0.4f, 0.4f, 0.4f), mp);
                ScatterBoxes(L, new Rect(P(W * 0.5f, D - 1.5f).x - 0.8f, P(W * 0.5f, D - 1.5f).z - 0.8f, 1.6f, 1.6f), 2);
                break;
        }

        // Un néon par local, souvent fatigué
        double roll = _rng.NextDouble();
        if (roll < 0.5) PointLight(L, P(W * 0.5f, D * 0.5f) + Vector3.up * (L.h - 0.4f), 7f);
        else Neon(L, P(W * 0.5f, D * 0.5f) + Vector3.up * L.h, yawF + 90f, roll < 0.85 ? FailingLight.Mode.Failing : FailingLight.Mode.Dying, _rng.NextDouble() < 0.2);
    }

    static bool _generatorMarked;

    // ───────────────────────── Conduit d'aération : labyrinthe rampant (-2 → -3) ─────────────────────────

    static void BuildVentMaze(Transform root)
    {
        var up = _levels[-2];
        var down = _levels[-3];
        if (_ductEntryNode.x < 0) { Debug.LogWarning("[EchoesMap] Pas de trappe au -2 : conduit non construit."); return; }

        var go = new GameObject("Conduit_Aeration_Labyrinthe (-2 -> -3)").transform;
        go.SetParent(root, false);
        var holder = new LevelDef { number = -2, y = DuctY, h = DuctH, root = go, props = Child(go, "Props"), markers = Child(go, "Marqueurs"),
            lightColor = new Color(1f, 0.35f, 0.25f), lightIntensity = 0.6f };
        var m = Mat("Tunnel_Aeration", new Color(0.32f, 0.32f, 0.34f), 0.35f);
        var rust = Mat("Panneau_Rouille", new Color(0.35f, 0.2f, 0.12f), 0.15f);
        float fy = DuctY, ty = DuctY + DuctH;

        // Entrée sous la trappe, sortie au nord vers le mur sud du -3 (x = 9)
        var nodeC = new Vector3(MazeX0 + (_ductEntryNode.x + 0.5f) * MazeP + 1f, 0, MazeZ0 + (_ductEntryNode.y + 0.5f) * MazeP + 1f);
        var entry = new Vector2Int(Mathf.FloorToInt((nodeC.x - DuctX0) / DuctCell), Mathf.FloorToInt((nodeC.z - DuctZ0) / DuctCell));
        entry.y = Mathf.Clamp(entry.y, 0, DuctNJ - 1);
        float xt = -HalfX + (VentExitI + 0.5f) * Cell;
        var exit = new Vector2Int(Mathf.FloorToInt((xt - DuctX0) / DuctCell), DuctNJ - 1);

        // 1) Labyrinthe parfait (un seul chemin) + très peu de boucles : long, plein d'impasses
        var east = new bool[DuctNI, DuctNJ];
        var north = new bool[DuctNI, DuctNJ];
        var vis = new bool[DuctNI, DuctNJ];
        var stack = new Stack<Vector2Int>();
        stack.Push(entry); vis[entry.x, entry.y] = true;
        var dirs = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
        var ch = new List<Vector2Int>();
        bool In(int i, int j) => i >= 0 && i < DuctNI && j >= 0 && j < DuctNJ;
        while (stack.Count > 0)
        {
            var c = stack.Peek();
            ch.Clear();
            foreach (var d in dirs) { var n = c + d; if (In(n.x, n.y) && !vis[n.x, n.y]) ch.Add(n); }
            if (ch.Count == 0) { stack.Pop(); continue; }
            var nx = ch[_rng.Next(ch.Count)];
            if (nx.x > c.x) east[c.x, c.y] = true; else if (nx.x < c.x) east[nx.x, c.y] = true;
            else if (nx.y > c.y) north[c.x, c.y] = true; else north[c.x, nx.y] = true;
            vis[nx.x, nx.y] = true;
            stack.Push(nx);
        }
        for (int i = 0; i < DuctNI; i++)
            for (int j = 0; j < DuctNJ; j++)
            {
                if (i + 1 < DuctNI && !east[i, j] && _rng.NextDouble() < 0.03) east[i, j] = true;
                if (j + 1 < DuctNJ && !north[i, j] && _rng.NextDouble() < 0.03) north[i, j] = true;
            }

        // 2) Chemin le plus court entrée → sortie (pour placer les pièges)
        var parent = new Dictionary<Vector2Int, Vector2Int>();
        var dist = new int[DuctNI, DuctNJ];
        for (int i = 0; i < DuctNI; i++) for (int j = 0; j < DuctNJ; j++) dist[i, j] = -1;
        var q = new Queue<Vector2Int>();
        q.Enqueue(entry); dist[entry.x, entry.y] = 0;
        while (q.Count > 0)
        {
            var c = q.Dequeue();
            void Go(int i, int j, bool link)
            {
                if (!link || !In(i, j) || dist[i, j] >= 0) return;
                dist[i, j] = dist[c.x, c.y] + 1; parent[new Vector2Int(i, j)] = c; q.Enqueue(new Vector2Int(i, j));
            }
            Go(c.x + 1, c.y, east[c.x, c.y]);
            Go(c.x - 1, c.y, c.x > 0 && east[c.x - 1, c.y]);
            Go(c.x, c.y + 1, north[c.x, c.y]);
            Go(c.x, c.y - 1, c.y > 0 && north[c.x, c.y - 1]);
        }
        var path = new List<Vector2Int> { exit };
        while (path[path.Count - 1] != entry && parent.ContainsKey(path[path.Count - 1])) path.Add(parent[path[path.Count - 1]]);
        path.Reverse();
        var onPath = new HashSet<Vector2Int>(path);

        Vector3 C(int i, int j, float yy) => new Vector3(DuctX0 + (i + 0.5f) * DuctCell, yy, DuctZ0 + (j + 0.5f) * DuctCell);

        // 3) Sols, plafonds (sauf sous la trappe : puits de chute), murs bas
        for (int j = 0; j < DuctNJ; j++)
        {
            Box(go, "Conduit_Sol", new Vector3(DuctX0, fy - 0.15f, DuctZ0 + j * DuctCell), new Vector3(DuctX0 + DuctNI * DuctCell, fy, DuctZ0 + (j + 1) * DuctCell), m, _layerGround);
            if (j == entry.y)
            {
                if (entry.x > 0) Box(go, "Conduit_Plafond", new Vector3(DuctX0, ty, DuctZ0 + j * DuctCell), new Vector3(DuctX0 + entry.x * DuctCell, ty + 0.15f, DuctZ0 + (j + 1) * DuctCell), m, _layerWalls);
                if (entry.x < DuctNI - 1) Box(go, "Conduit_Plafond", new Vector3(DuctX0 + (entry.x + 1) * DuctCell, ty, DuctZ0 + j * DuctCell), new Vector3(DuctX0 + DuctNI * DuctCell, ty + 0.15f, DuctZ0 + (j + 1) * DuctCell), m, _layerWalls);
            }
            else Box(go, "Conduit_Plafond", new Vector3(DuctX0, ty, DuctZ0 + j * DuctCell), new Vector3(DuctX0 + DuctNI * DuctCell, ty + 0.15f, DuctZ0 + (j + 1) * DuctCell), m, _layerWalls);
        }
        const float t = 0.15f;
        for (int j = 0; j <= DuctNJ; j++)
        {
            float z = DuctZ0 + j * DuctCell; int s = -1;
            for (int i = 0; i <= DuctNI; i++)
            {
                bool need = i < DuctNI && (j == 0 || j == DuctNJ || !north[i, j - 1]);
                if (need && j == DuctNJ && i == exit.x) // ouverture vers le -3
                {
                    WallAlongXThin(go, z, DuctX0 + i * DuctCell, DuctX0 + (i + 1) * DuctCell, fy, ty, m, new Hole(C(i, j - 1, 0).x, 1.05f, fy, ty), t);
                    need = false;
                }
                if (need && s < 0) s = i;
                if (!need && s >= 0) { Box(go, "Conduit_Paroi", new Vector3(DuctX0 + s * DuctCell - t / 2, fy, z - t / 2), new Vector3(DuctX0 + i * DuctCell + t / 2, ty, z + t / 2), m, _layerWalls); s = -1; }
            }
        }
        for (int i = 0; i <= DuctNI; i++)
        {
            float x = DuctX0 + i * DuctCell; int s = -1;
            for (int j = 0; j <= DuctNJ; j++)
            {
                bool need = j < DuctNJ && (i == 0 || i == DuctNI || !east[i - 1, j]);
                if (need && s < 0) s = j;
                if (!need && s >= 0) { Box(go, "Conduit_Paroi", new Vector3(x - t / 2, fy, DuctZ0 + s * DuctCell - t / 2), new Vector3(x + t / 2, ty, DuctZ0 + j * DuctCell + t / 2), m, _layerWalls); s = -1; }
            }
        }

        // 4) Puits de chute sous la trappe (on tombe debout, puis on s'accroupit pour avancer)
        var e0 = C(entry.x, entry.y, fy);
        float xa = DuctX0 + entry.x * DuctCell, za = DuctZ0 + entry.y * DuctCell;
        float topShaft = up.y - Slab;
        Box(go, "Puits_Paroi", new Vector3(xa - t, ty, za - t), new Vector3(xa + DuctCell + t, topShaft, za), m, _layerWalls);
        Box(go, "Puits_Paroi", new Vector3(xa - t, ty, za + DuctCell), new Vector3(xa + DuctCell + t, topShaft, za + DuctCell + t), m, _layerWalls);
        Box(go, "Puits_Paroi", new Vector3(xa - t, ty, za), new Vector3(xa, topShaft, za + DuctCell), m, _layerWalls);
        Box(go, "Puits_Paroi", new Vector3(xa + DuctCell, ty, za), new Vector3(xa + DuctCell + t, topShaft, za + DuctCell), m, _layerWalls);
        var respawnStart = new GameObject("Reprise_Entree").transform;
        respawnStart.SetParent(go, false);
        respawnStart.position = e0 + Vector3.up * 0.05f;
        Marker(holder, "Conduit_Arrivee (ramper : accroupi)", e0, Color.magenta);

        // 5) Sortie : court conduit jusqu'au mur sud du -3 (ouverture en hauteur, on se laisse tomber)
        var ex = C(exit.x, exit.y, fy);
        BuildDuct(go, new[] { new Vector3(ex.x, fy, DuctZ0 + DuctNJ * DuctCell - 0.05f), new Vector3(ex.x, fy, GridZMin + 0.08f) }, 1f, DuctH - 0.05f, m);
        Marker(holder, "Conduit_Sortie (vers -3)", ex, Color.magenta);

        // 6) Pièges
        var steamMat = SteamMaterial();
        int nSteam = 0, nFloor = 0;
        // Jets de vapeur sur le bon chemin (on passe en observant le rythme)
        foreach (float frac in new[] { 0.28f, 0.55f, 0.8f })
        {
            int k = Mathf.Clamp(Mathf.RoundToInt(path.Count * frac), 3, path.Count - 2);
            var cell = path[k];
            var back = path[Mathf.Max(0, k - 3)];
            BuildSteamVent(holder, C(cell.x, cell.y, fy), C(back.x, back.y, fy), steamMat, nSteam * 1.3f);
            nSteam++;
        }
        // Panneaux rouillés qui cèdent : dans les fausses pistes (cellules hors du bon chemin, souvent près des impasses)
        var candidates = new List<Vector2Int>();
        for (int i = 0; i < DuctNI; i++)
            for (int j = 0; j < DuctNJ; j++)
            {
                var v = new Vector2Int(i, j);
                if (onPath.Contains(v) || dist[i, j] < 6) continue;
                int deg = (east[i, j] ? 1 : 0) + (i > 0 && east[i - 1, j] ? 1 : 0) + (north[i, j] ? 1 : 0) + (j > 0 && north[i, j - 1] ? 1 : 0);
                if (deg <= 2) candidates.Add(v);
            }
        for (int k = 0; k < 9 && candidates.Count > 0; k++)
        {
            var v = candidates[_rng.Next(candidates.Count)];
            candidates.Remove(v);
            BuildTrapFloor(holder, C(v.x, v.y, fy), respawnStart, rust);
            nFloor++;
        }

        // Ambiance : quelques voyants rouges, un reflet de lumière au bout de fausses pistes
        for (int k = 0; k < 7; k++)
        {
            var v = new Vector2Int(_rng.Next(DuctNI), _rng.Next(DuctNJ));
            var lt = PointLight(holder, C(v.x, v.y, ty - 0.15f), 4f, go);
            lt.intensity = 0.5f;
            if (_rng.NextDouble() < 0.5) lt.gameObject.AddComponent<FailingLight>().mode = FailingLight.Mode.Failing;
        }

        // 7) -3 : grille arrachée au pied de l'ouverture
        Cube(down.props, "Grille_Arrachee", new Vector3(xt + 0.6f, down.y + 0.03f, GridZMin + 0.8f), new Vector3(1f, 0.05f, 1.2f),
            Quaternion.Euler(0, 25f, 0), Mat("Grille_Metal", new Color(0.18f, 0.18f, 0.2f), 0.5f), false, -1);
        Marker(down, "Sortie_Tunnel_Aeration", new Vector3(xt, down.y, GridZMin + 1.2f), Color.magenta);
        Debug.Log($"[EchoesMap] Conduit d'aération : {path.Count} cases sur le bon chemin, {nSteam} jets de vapeur, {nFloor} panneaux piégés.");
    }

    static void WallAlongXThin(Transform p, float z, float a, float b, float y0, float y1, Material m, Hole hole, float thick)
    {
        float h0 = hole.c - hole.w * 0.5f, h1 = hole.c + hole.w * 0.5f;
        if (h0 > a) Box(p, "Conduit_Paroi", new Vector3(a, y0, z - thick / 2), new Vector3(h0, y1, z + thick / 2), m, _layerWalls);
        if (h1 < b) Box(p, "Conduit_Paroi", new Vector3(h1, y0, z - thick / 2), new Vector3(b, y1, z + thick / 2), m, _layerWalls);
    }

    static void BuildSteamVent(LevelDef L, Vector3 floorCenter, Vector3 fallbackPos, Material steamMat, float phase)
    {
        var root = new GameObject("Piege_Vapeur");
        root.transform.SetParent(L.props, false);
        root.transform.position = floorCenter;
        var box = root.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = new Vector3(0f, DuctH * 0.5f, 0f);
        box.size = new Vector3(1.6f, DuctH, 1.6f);

        var metal = Mat("Grille_Metal", new Color(0.18f, 0.18f, 0.2f), 0.5f);
        Cube(root.transform, "Buse", floorCenter + new Vector3(0f, DuctH - 0.12f, 0f), new Vector3(0.35f, 0.12f, 0.35f), Quaternion.identity, metal, false, -1);

        var psGo = new GameObject("Vapeur");
        psGo.transform.SetParent(root.transform, false);
        psGo.transform.position = floorCenter + new Vector3(0f, DuctH - 0.2f, 0f);
        psGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // vers le bas
        var ps = psGo.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.playOnAwake = false;
        main.startLifetime = 0.9f;
        main.startSpeed = 2.6f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
        main.startColor = new Color(0.9f, 0.9f, 0.92f, 0.45f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 200;
        var em = ps.emission; em.rateOverTime = 90f;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 25f; shape.radius = 0.1f;
        var sol = ps.sizeOverLifetime; sol.enabled = true; sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.6f, 1, 1.8f));
        var col = ps.colorOverLifetime; col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(0.5f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;
        var pr = psGo.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = steamMat;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var fb = new GameObject("Repli_Vapeur").transform;
        fb.SetParent(root.transform, false);
        fb.position = fallbackPos + Vector3.up * 0.05f;
        fb.rotation = Quaternion.LookRotation(Flat(floorCenter - fallbackPos), Vector3.up);

        var vent = root.AddComponent<SteamVent>();
        vent.steam = ps;
        vent.fallback = fb;
        vent.phase = phase;
        NonStatic(root);
        Marker(L, "Piege_Vapeur", floorCenter, Color.red);
    }

    static void BuildTrapFloor(LevelDef L, Vector3 floorCenter, Transform respawn, Material rust)
    {
        var root = new GameObject("Piege_Panneau_Rouille");
        root.transform.SetParent(L.props, false);
        root.transform.position = floorCenter;
        var box = root.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = new Vector3(0f, 0.5f, 0f);
        box.size = new Vector3(1.2f, 1f, 1.2f);
        var panel = Cube(root.transform, "Panneau", floorCenter + Vector3.up * 0.015f, new Vector3(1.5f, 0.03f, 1.5f), Quaternion.Euler(0, Rand(-6f, 6f), 0), rust, false, -1);
        var trap = root.AddComponent<VentTrapFloor>();
        trap.panel = panel;
        trap.respawn = respawn;
        NonStatic(root);
        Marker(L, "Piege_Panneau", floorCenter, Color.red);
    }

    static Material SteamMaterial()
    {
        string path = MatFolder + "/Vapeur.mat";
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        else m.shader = shader;
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.5f));
        m.SetTexture("_BaseMap", AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd"));
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        EditorUtility.SetDirty(m);
        return m;
    }
}
