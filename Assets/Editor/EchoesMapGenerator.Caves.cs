using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Niveau -7 : immense réseau de grottes sous "Le fond".
/// 1) On décrit le vide (salles = ellipsoïdes, tunnels = capsules le long de chemins sinueux, impasses, boyaux, gouffres).
/// 2) Un champ 3D (1 m) dit pour chaque point s'il est dans la roche ou dans le vide, avec du bruit pour une roche irrégulière.
/// 3) La surface est extraite (surface nets) en morceaux de maillage avec collisions ; sols et parois ont leur propre texture.
/// 4) Décor posé par lancers de rayons : lac et rivière, petite forêt sous un puits de lumière, champignons et cristaux
///    lumineux, campements des disparus, ossements, éboulements, fonds de gouffres.
/// Accès : une faille dans le sol du -6, derrière le monolithe, puis une longue pente.
/// </summary>
public static partial class EchoesMapGenerator
{
    // Volume du réseau : x, z ∈ [-90, 90], y ∈ [-82, -33] (le haut affleure le sol du -6)
    const float CaveX0 = -90f, CaveY0 = -82f, CaveZ0 = -90f, CaveStep = 0.5f;
    const int CaveNX = 361, CaveNY = 99, CaveNZ = 361;
    const int CaveChunk = 48;
    const string CaveMeshPath = MatFolder + "/CaveMeshes.asset";

    // Faille au sol du -6 : ouverte dans le champ (vide) et trou un peu plus petit dans la dalle (elle recouvre les bords)
    static readonly Rect CaveOpenRect = Rect.MinMaxRect(5f, 1.5f, 13f, 4.5f);
    public static readonly Rect CaveSlabHole = Rect.MinMaxRect(5.5f, 2f, 12.5f, 4f);

    enum CaveRole { Normal, Entree, Lac, Foret, Camp, Coeur }

    class CaveNode
    {
        public Vector3 c;
        public Vector3 radii;
        public CaveRole role;
    }

    class CaveEdge
    {
        public int a, b;
        public List<Vector3> pts;
        public float r;
        public bool crawl, blocked, river;
    }

    class CaveShape
    {
        public Vector3 A, B;
        public float rA, rB;
        public float down = 1.8f, up = 0.9f;   // aplatissement du sol / du plafond
        public float noise = 0.9f;
        public Vector3 radii;                   // ellipsoïde si radii.x > 0
    }

    static List<CaveShape> _caveShapes;

    // ───────────────────────── Niveau ─────────────────────────

    static void BuildCaveLevel(LevelDef L)
    {
        L.halfX = 90f;
        L.zMin = CaveZ0;
        L.zMax = CaveZ0 + (CaveNZ - 1) * CaveStep;
        _caveShapes = new List<CaveShape>();

        // 1) Salles
        var nodes = new List<CaveNode>();
        nodes.Add(new CaveNode { c = new Vector3(36f, -49.5f, 12f), radii = new Vector3(9f, 7f, 9f), role = CaveRole.Entree });
        for (int tries = 0; tries < 3000 && nodes.Count < 24; tries++)
        {
            var p = new Vector3(Rand(-78f, 78f), Rand(-70f, -52f), Rand(-78f, 78f));
            bool ok = true;
            foreach (var n in nodes) if (Vector2.Distance(new Vector2(p.x, p.z), new Vector2(n.c.x, n.c.z)) < 24f) { ok = false; break; }
            if (!ok) continue;
            float r = Rand(5f, 11f);
            nodes.Add(new CaveNode { c = p, radii = new Vector3(r * Rand(0.9f, 1.4f), r * Rand(0.6f, 0.85f), r * Rand(0.9f, 1.4f)) });
        }

        // Rôles : les plus éloignés de l'entrée = cœur, forêt, lac ; trois campements
        var order = new List<int>();
        for (int i = 1; i < nodes.Count; i++) order.Add(i);
        order.Sort((x, y) => Vector3.Distance(nodes[y].c, nodes[0].c).CompareTo(Vector3.Distance(nodes[x].c, nodes[0].c)));
        if (order.Count >= 6)
        {
            nodes[order[0]].role = CaveRole.Coeur;
            nodes[order[1]].role = CaveRole.Foret;
            nodes[order[1]].radii = new Vector3(17f, 16f, 17f);
            nodes[order[1]].c.y = -66f;
            nodes[order[3]].role = CaveRole.Lac;
            nodes[order[3]].radii = new Vector3(15f, 8f, 13f);
            nodes[order[2]].role = CaveRole.Camp;
            nodes[order[5]].role = CaveRole.Camp;
            nodes[order[order.Count / 2]].role = CaveRole.Camp;
        }

        // 2) Galeries : arbre couvrant (tout est relié) + galeries en plus (boucles)
        var edges = new List<CaveEdge>();
        var inTree = new HashSet<int> { 0 };
        while (inTree.Count < nodes.Count)
        {
            int bi = -1, bj = -1; float bd = float.MaxValue;
            foreach (int i in inTree)
                for (int j = 0; j < nodes.Count; j++)
                {
                    if (inTree.Contains(j)) continue;
                    float d = CaveDist(nodes[i].c, nodes[j].c);
                    if (d < bd) { bd = d; bi = i; bj = j; }
                }
            edges.Add(new CaveEdge { a = bi, b = bj });
            inTree.Add(bj);
        }
        var extra = new List<CaveEdge>();
        for (int i = 0; i < nodes.Count; i++)
            for (int j = i + 1; j < nodes.Count; j++)
            {
                float d = CaveDist(nodes[i].c, nodes[j].c);
                if (d > 58f || edges.Exists(e => (e.a == i && e.b == j) || (e.a == j && e.b == i))) continue;
                if (_rng.NextDouble() < 0.3) extra.Add(new CaveEdge { a = i, b = j });
            }
        // Parmi les galeries en plus : boyaux (ramper) et éboulements (impasses des deux côtés)
        for (int k = 0; k < extra.Count; k++)
        {
            if (k < 3) extra[k].crawl = true;
            else if (k < 6) extra[k].blocked = true;
        }
        edges.AddRange(extra);

        // Tracé sinueux de chaque galerie
        foreach (var e in edges)
        {
            e.r = e.crawl ? 2.4f : Rand(1.9f, 3f);
            e.pts = WindingPath(nodes[e.a].c, nodes[e.b].c, 5f, e.crawl ? 2.5f : 4.5f);
        }
        // La rivière suit une galerie qui part du lac
        var lakeIdx = nodes.FindIndex(n => n.role == CaveRole.Lac);
        var riverEdge = edges.Find(e => !e.crawl && !e.blocked && (e.a == lakeIdx || e.b == lakeIdx));
        if (riverEdge != null) riverEdge.river = true;

        // 3) Formes du vide
        // Faille d'entrée : pente depuis le sol du -6 (y = -33) jusqu'à la première salle
        AddTube(new List<Vector3> { new Vector3(5f, -31.78f, 3f), new Vector3(13f, -36.58f, 3f), new Vector3(24f, -41f, 7f), nodes[0].c + new Vector3(-4f, 0f, -2f) }, 2.2f, 0.5f);
        foreach (var n in nodes)
        {
            _caveShapes.Add(new CaveShape { A = n.c, B = n.c, radii = n.radii, down = n.role == CaveRole.Foret ? 2.2f : 1.6f, up = 1f, noise = n.role == CaveRole.Foret ? 1.4f : 1.1f });
            if (n.role == CaveRole.Lac) // cuvette du lac
                _caveShapes.Add(new CaveShape { A = n.c + Vector3.down * (n.radii.y / 1.6f), B = n.c + Vector3.down * (n.radii.y / 1.6f), radii = new Vector3(n.radii.x * 0.72f, 1.4f, n.radii.z * 0.72f), down = 1f, up = 1f, noise = 0.2f });
        }
        foreach (var e in edges)
        {
            if (e.crawl)
            {
                // Boyau : bas et étroit au milieu, il faut ramper (accroupi)
                int n = e.pts.Count;
                for (int i = 0; i < n - 1; i++)
                {
                    float t = (i + 0.5f) / (n - 1);
                    bool middle = t > 0.2f && t < 0.8f;
                    _caveShapes.Add(new CaveShape { A = e.pts[i], B = e.pts[i + 1], rA = middle ? 0.95f : 2.2f, rB = middle ? 0.95f : 2.2f,
                        down = middle ? 1.35f : 1.8f, up = middle ? 1.35f : 0.9f, noise = middle ? 0.15f : 0.8f });
                }
            }
            else AddTube(e.pts, e.r, 0.85f);
        }
        // Impasses : galeries qui ne mènent nulle part
        int spurs = 0;
        for (int tries = 0; tries < 100 && spurs < 14; tries++)
        {
            var n = nodes[_rng.Next(nodes.Count)];
            if (n.role == CaveRole.Entree) continue;
            float ang = Rand(0f, Mathf.PI * 2f), len = Rand(14f, 34f);
            var end = n.c + new Vector3(Mathf.Cos(ang) * len, Rand(-4f, 3f), Mathf.Sin(ang) * len);
            if (Mathf.Abs(end.x) > 84f || Mathf.Abs(end.z) > 84f || end.y < -77f || end.y > -47f) continue;
            var pts = WindingPath(n.c, end, 4.5f, 3f);
            AddTube(pts, Rand(1.6f, 2.4f), 0.9f);
            _caveShapes.Add(new CaveShape { A = end, B = end, radii = new Vector3(3f, 2.4f, 3f), down = 1.6f, up = 1f, noise = 1f });
            spurs++;
        }
        // Gouffres : puits creusés dans des galeries larges (on passe par les bords)
        var pits = new List<(Vector3 p, Vector3 back)>();
        foreach (var e in edges)
        {
            if (pits.Count >= 4) break;
            if (e.crawl || e.blocked || e.river || e.r < 2.5f || e.pts.Count < 6) continue;
            int k = e.pts.Count / 2;
            var p = e.pts[k];
            float floorY = p.y - e.r / 1.8f;
            _caveShapes.Add(new CaveShape { A = new Vector3(p.x, floorY + 0.3f, p.z), B = new Vector3(p.x, floorY - 13f, p.z), rA = 1.35f, rB = 1.6f, down = 1f, up = 1f, noise = 0.35f });
            pits.Add((p, e.pts[k - 2]));
        }

        // 4) Champ 3D puis surface
        var root = Child(L.root, "Grottes");
        root.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true; // pas de surveillants ici
        var field = BuildCaveField();
        BuildCaveMeshes(root, field, L);
        Physics.SyncTransforms();

        // 5) Décor (posé sur les vraies surfaces)
        var deco = Child(root, "Decor");
        var D = new LevelDef { number = L.number, y = L.y, h = L.h, root = root, props = deco, markers = L.markers,
            lightColor = L.lightColor, lightIntensity = L.lightIntensity };
        var mp = Mat("Meuble_" + L.number, new Color(0.3f, 0.26f, 0.22f));

        ResetCavePropMeshes();
        DecorateCaveEntrance(D);
        foreach (var n in nodes)
        {
            switch (n.role)
            {
                case CaveRole.Lac: DecorateLake(D, n); break;
                case CaveRole.Foret: DecorateForest(D, n); break;
                case CaveRole.Camp: DecorateCamp(D, n, mp); break;
                case CaveRole.Coeur:
                    if (CaveFloor(n.c, out var hf)) Marker(D, "Coeur_du_Reseau", hf.point, Color.red);
                    CrystalCluster(D, n.c, 18, 2.2f);
                    break;
            }
        }
        if (riverEdge != null) DecorateRiver(D, riverEdge);
        foreach (var e in edges)
            if (e.blocked && CaveFloor(e.pts[e.pts.Count / 2], out var hb))
            {
                var c = hb.point;
                RockPile(D, new Rect(c.x - e.r, c.z - e.r, e.r * 2f, e.r * 2f), 14, mp);
                Cube(deco, "Eboulement_Collision", c + Vector3.up * 1.5f, new Vector3(e.r * 2.2f, 3f, e.r * 2.2f), Quaternion.identity, mp, true, _layerWalls)
                    .GetComponent<Renderer>().enabled = false;
            }
        foreach (var (p, back) in pits) BuildPitTrap(D, p, back);

        // Champignons et cristaux le long des parois
        var spots = new List<Vector3>();
        foreach (var n in nodes) spots.Add(n.c);
        foreach (var e in edges) foreach (var p in e.pts) spots.Add(p);
        for (int k = 0; k < 30; k++) MushroomCluster(D, spots[_rng.Next(spots.Count)]);
        for (int k = 0; k < 12; k++) CrystalCluster(D, spots[_rng.Next(spots.Count)], _rng.Next(4, 9), 1f);
        for (int k = 0; k < 6; k++) BonePile(D, spots[_rng.Next(spots.Count)]);
        ScatterBoulders(D, nodes);
        for (int k = 0; k < 4; k++)
            if (CaveFloor(spots[_rng.Next(spots.Count)], out var hbat)) PlaceItem(D, "pile", hbat.point, Rand(0f, 360f));

        Debug.Log($"[EchoesMap] Grottes du -7 : {nodes.Count} salles, {edges.Count} galeries ({extra.Count} en boucle, 3 boyaux, 3 éboulements), {spurs} impasses, {pits.Count} gouffres.");
    }

    static float CaveDist(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z)) + Mathf.Abs(a.y - b.y) * 1.5f;

    /// Chemin sinueux de a à b (points tous les "step" mètres, écart latéral et vertical).
    static List<Vector3> WindingPath(Vector3 a, Vector3 b, float step, float wobble)
    {
        var pts = new List<Vector3> { a };
        float len = Vector3.Distance(a, b);
        int n = Mathf.Max(2, Mathf.CeilToInt(len / step));
        var dir = (b - a).normalized;
        var side = Vector3.Cross(Vector3.up, dir).normalized;
        float s1 = Rand(0f, 100f), s2 = Rand(0f, 100f);
        for (int i = 1; i < n; i++)
        {
            float t = i / (float)n;
            float env = Mathf.Sin(t * Mathf.PI); // pas d'écart aux extrémités
            var p = Vector3.Lerp(a, b, t)
                + side * ((Mathf.PerlinNoise(s1, t * 3f) - 0.5f) * 2f * wobble * env)
                + Vector3.up * ((Mathf.PerlinNoise(s2, t * 2f) - 0.5f) * 2f * wobble * 0.3f * env);
            pts.Add(p);
        }
        pts.Add(b);
        return pts;
    }

    static void AddTube(List<Vector3> pts, float r, float noise)
    {
        for (int i = 0; i < pts.Count - 1; i++)
            _caveShapes.Add(new CaveShape { A = pts[i], B = pts[i + 1], rA = r * Rand(0.9f, 1.1f), rB = r * Rand(0.9f, 1.1f), noise = noise });
    }

    // ───────────────────────── Champ 3D ─────────────────────────

    static int CI(int x, int y, int z) => x + CaveNX * (y + CaveNY * z);
    static Vector3 CP(int x, int y, int z) => new Vector3(CaveX0 + x * CaveStep, CaveY0 + y * CaveStep, CaveZ0 + z * CaveStep);

    static float[] BuildCaveField()
    {
        int total = CaveNX * CaveNY * CaveNZ;
        var air = new float[total];
        var amp = new float[total];
        for (int i = 0; i < total; i++) air[i] = -4f;

        foreach (var s in _caveShapes)
        {
            float rmax = s.radii.x > 0 ? Mathf.Max(s.radii.x, s.radii.y, s.radii.z) : Mathf.Max(s.rA, s.rB);
            var lo = Vector3.Min(s.A, s.B) - Vector3.one * (rmax * 1.8f + 2f);
            var hi = Vector3.Max(s.A, s.B) + Vector3.one * (rmax * 1.8f + 2f);
            int x0 = Mathf.Max(0, Mathf.FloorToInt((lo.x - CaveX0) / CaveStep)), x1 = Mathf.Min(CaveNX - 1, Mathf.CeilToInt((hi.x - CaveX0) / CaveStep));
            int y0 = Mathf.Max(0, Mathf.FloorToInt((lo.y - CaveY0) / CaveStep)), y1 = Mathf.Min(CaveNY - 1, Mathf.CeilToInt((hi.y - CaveY0) / CaveStep));
            int z0 = Mathf.Max(0, Mathf.FloorToInt((lo.z - CaveZ0) / CaveStep)), z1 = Mathf.Min(CaveNZ - 1, Mathf.CeilToInt((hi.z - CaveZ0) / CaveStep));
            var AB = s.B - s.A;
            float ab2 = Mathf.Max(AB.sqrMagnitude, 1e-6f);
            for (int z = z0; z <= z1; z++)
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        var p = CP(x, y, z);
                        float v;
                        if (s.radii.x > 0)
                        {
                            var d = p - s.A;
                            float dy = d.y < 0 ? d.y * s.down : d.y * s.up;
                            var q = new Vector3(d.x / s.radii.x, dy / s.radii.y, d.z / s.radii.z);
                            v = (1f - q.magnitude) * Mathf.Min(s.radii.x, s.radii.y, s.radii.z);
                        }
                        else
                        {
                            float t = Mathf.Clamp01(Vector3.Dot(p - s.A, AB) / ab2);
                            var c = s.A + AB * t;
                            float r = Mathf.Lerp(s.rA, s.rB, t);
                            var d = p - c;
                            d.y = d.y < 0 ? d.y * s.down : d.y * s.up;
                            v = r - d.magnitude;
                        }
                        int i = CI(x, y, z);
                        if (v > air[i]) { air[i] = v; amp[i] = s.noise; }
                    }
        }

        // Bruit (roche irrégulière) près des surfaces, bords du volume fermés sauf la faille
        var f = air;
        for (int z = 0; z < CaveNZ; z++)
            for (int y = 0; y < CaveNY; y++)
                for (int x = 0; x < CaveNX; x++)
                {
                    int i = CI(x, y, z);
                    var p = CP(x, y, z);
                    bool border = x == 0 || z == 0 || y == 0 || x == CaveNX - 1 || z == CaveNZ - 1;
                    bool top = y == CaveNY - 1;
                    if (border || (top && !CaveOpenRect.Contains(new Vector2(p.x, p.z)))) { f[i] = -2f; continue; }
                    if (f[i] < -3f) continue;
                    f[i] += Fbm(p) * amp[i];
                }
        return f;
    }

    static float Hash3(int x, int y, int z)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + z * 1274126177;
            h = (h ^ (h >> 13)) * 1103515245;
            h ^= h >> 16;
            return (h & 0xffffff) / 8388607.5f - 1f;
        }
    }

    static float ValueNoise(Vector3 p)
    {
        int x = Mathf.FloorToInt(p.x), y = Mathf.FloorToInt(p.y), z = Mathf.FloorToInt(p.z);
        float fx = p.x - x, fy = p.y - y, fz = p.z - z;
        fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy); fz = fz * fz * (3 - 2 * fz);
        float L(float a, float b, float t) => a + (b - a) * t;
        return L(L(L(Hash3(x, y, z), Hash3(x + 1, y, z), fx), L(Hash3(x, y + 1, z), Hash3(x + 1, y + 1, z), fx), fy),
                 L(L(Hash3(x, y, z + 1), Hash3(x + 1, y, z + 1), fx), L(Hash3(x, y + 1, z + 1), Hash3(x + 1, y + 1, z + 1), fx), fy), fz);
    }

    static float Fbm(Vector3 p) => ValueNoise(p * 0.17f) * 0.6f + ValueNoise(p * 0.43f + new Vector3(31f, 7f, 13f)) * 0.3f
        + ValueNoise(p * 1.1f + new Vector3(5f, 17f, 29f)) * 0.12f; // dernier octave : petits reliefs (grille de 0,5 m)

    // ───────────────────────── Surface (surface nets) ─────────────────────────

    static void BuildCaveMeshes(Transform parent, float[] F, LevelDef L)
    {
        int cx = CaveNX - 1, cy = CaveNY - 1, cz = CaveNZ - 1;
        var cellVert = new int[cx * cy * cz];
        for (int i = 0; i < cellVert.Length; i++) cellVert[i] = -1;
        var verts = new List<Vector3>();
        int Cell(int x, int y, int z) => x + cx * (y + cy * z);

        // Un sommet par cellule traversée par la surface : moyenne des points de passage sur ses arêtes
        var corner = new Vector3Int[8];
        for (int z = 0; z < cz; z++)
            for (int y = 0; y < cy; y++)
                for (int x = 0; x < cx; x++)
                {
                    int mask = 0;
                    for (int k = 0; k < 8; k++)
                    {
                        int ox = k & 1, oy = (k >> 1) & 1, oz = (k >> 2) & 1;
                        if (F[CI(x + ox, y + oy, z + oz)] > 0f) mask |= 1 << k;
                    }
                    if (mask == 0 || mask == 255) continue;
                    var sum = Vector3.zero; int cnt = 0;
                    for (int k = 0; k < 8; k++)
                        for (int axis = 0; axis < 3; axis++)
                        {
                            int k2 = k | (1 << axis);
                            if (k2 == k) continue;
                            bool a = (mask & (1 << k)) != 0, b = (mask & (1 << k2)) != 0;
                            if (a == b) continue;
                            var pa = new Vector3(k & 1, (k >> 1) & 1, (k >> 2) & 1);
                            var pb = new Vector3(k2 & 1, (k2 >> 1) & 1, (k2 >> 2) & 1);
                            float fa = F[CI(x + (k & 1), y + ((k >> 1) & 1), z + ((k >> 2) & 1))];
                            float fb = F[CI(x + (k2 & 1), y + ((k2 >> 1) & 1), z + ((k2 >> 2) & 1))];
                            float t = fa / (fa - fb);
                            sum += Vector3.Lerp(pa, pb, t); cnt++;
                        }
                    var local = sum / Mathf.Max(1, cnt);
                    cellVert[Cell(x, y, z)] = verts.Count;
                    verts.Add(CP(x, y, z) + local * CaveStep);
                }

        // Quadrilatères : un par arête de la grille qui traverse la surface
        var quads = new List<(int a, int b, int c, int d, int chunk)>();
        var normals = new Vector3[verts.Count];
        // Sens des faces fixé par la grille (pas par la géométrie, qui peut se replier et percer des trous)
        void Quad(int c0, int c1, int c2, int c3, bool flip, int chunk)
        {
            int a = cellVert[c0], b = cellVert[c1], c = cellVert[c2], d = cellVert[c3];
            if (a < 0 || b < 0 || c < 0 || d < 0) return;
            if (flip) (b, d) = (d, b);
            var n = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]) + Vector3.Cross(verts[c] - verts[a], verts[d] - verts[a]);
            normals[a] += n; normals[b] += n; normals[c] += n; normals[d] += n;
            quads.Add((a, b, c, d, chunk));
        }
        int Chunk(int x, int y, int z) => (x / CaveChunk) + 100 * ((y / CaveChunk) + 100 * (z / CaveChunk));
        for (int z = 1; z < cz; z++)
            for (int y = 1; y < cy; y++)
                for (int x = 0; x < cx; x++)
                {
                    bool a = F[CI(x, y, z)] > 0f, b = F[CI(x + 1, y, z)] > 0f;
                    if (a != b) Quad(Cell(x, y - 1, z - 1), Cell(x, y, z - 1), Cell(x, y, z), Cell(x, y - 1, z), !b, Chunk(x, y, z));
                }
        for (int z = 1; z < cz; z++)
            for (int y = 0; y < cy; y++)
                for (int x = 1; x < cx; x++)
                {
                    bool a = F[CI(x, y, z)] > 0f, b = F[CI(x, y + 1, z)] > 0f;
                    if (a != b) Quad(Cell(x - 1, y, z - 1), Cell(x, y, z - 1), Cell(x, y, z), Cell(x - 1, y, z), b, Chunk(x, y, z));
                }
        for (int z = 0; z < cz; z++)
            for (int y = 1; y < cy; y++)
                for (int x = 1; x < cx; x++)
                {
                    bool a = F[CI(x, y, z)] > 0f, b = F[CI(x, y, z + 1)] > 0f;
                    if (a != b) Quad(Cell(x - 1, y - 1, z), Cell(x, y - 1, z), Cell(x, y, z), Cell(x - 1, y, z), !b, Chunk(x, y, z));
                }
        // Lissage (Laplacien, 2 passes) : adoucit les facettes de la grille sans perdre le relief du bruit
        var nb = new List<int>[verts.Count];
        void Link(int u, int w) { (nb[u] ??= new List<int>(6)).Add(w); }
        foreach (var (a, b, c, d, _) in quads) { Link(a, b); Link(b, c); Link(c, d); Link(d, a); Link(b, a); Link(c, b); Link(d, c); Link(a, d); }
        for (int pass = 0; pass < 2; pass++)
        {
            var next = new Vector3[verts.Count];
            for (int i = 0; i < verts.Count; i++)
            {
                if (nb[i] == null) { next[i] = verts[i]; continue; }
                var avg = Vector3.zero;
                foreach (int j in nb[i]) avg += verts[j];
                next[i] = Vector3.Lerp(verts[i], avg / nb[i].Count, 0.5f);
            }
            for (int i = 0; i < verts.Count; i++) verts[i] = next[i];
        }
        for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.zero;
        foreach (var (a, b, c, d, _) in quads)
        {
            var fn = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]) + Vector3.Cross(verts[c] - verts[a], verts[d] - verts[a]);
            normals[a] += fn; normals[b] += fn; normals[c] += fn; normals[d] += fn;
        }
        for (int i = 0; i < normals.Length; i++) normals[i] = normals[i].sqrMagnitude > 1e-8f ? normals[i].normalized : Vector3.up;

        // Morceaux de maillage (sol / parois), stockés dans un fichier de l'éditeur
        AssetDatabase.DeleteAsset(CaveMeshPath);
        var container = new Mesh { name = "CaveMeshes" };
        AssetDatabase.CreateAsset(container, CaveMeshPath);
        var mRock = CaveRockMaterial();

        var byChunk = new Dictionary<int, List<int>>();
        for (int q = 0; q < quads.Count; q++)
        {
            if (!byChunk.TryGetValue(quads[q].chunk, out var list)) byChunk[quads[q].chunk] = list = new List<int>();
            list.Add(q);
        }
        int meshCount = 0, triCount = 0;
        foreach (var kv in byChunk)
        {
            var remap = new Dictionary<int, int>();
            var v = new List<Vector3>(); var nn = new List<Vector3>(); var uv = new List<Vector2>();
            var tris = new List<int>();
            int Local(int g)
            {
                if (remap.TryGetValue(g, out var l)) return l;
                l = v.Count; remap[g] = l;
                var p = verts[g]; var n = normals[g];
                v.Add(p); nn.Add(n);
                float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
                const float tile = 4f;
                uv.Add(ay >= ax && ay >= az ? new Vector2(p.x, p.z) / tile : ax >= az ? new Vector2(p.z, p.y) / tile : new Vector2(p.x, p.y) / tile);
                return l;
            }
            foreach (int q in kv.Value)
            {
                var (a, b, c, d, _) = quads[q];
                int la = Local(a), lb = Local(b), lc = Local(c), ld = Local(d);
                tris.Add(la); tris.Add(lb); tris.Add(lc);
                tris.Add(la); tris.Add(lc); tris.Add(ld);
            }
            var mesh = new Mesh { name = "Grotte_" + meshCount };
            if (v.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(v); mesh.SetNormals(nn); mesh.SetUVs(0, uv);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds(); mesh.RecalculateTangents();
            AssetDatabase.AddObjectToAsset(mesh, container);

            var go = new GameObject(mesh.name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mRock;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            go.layer = _layerGround;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic);
            meshCount++; triCount += tris.Count / 3;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[EchoesMap] Grottes : {meshCount} morceaux, {triCount} triangles.");
    }

    /// Matériau de roche triplanaire (paroi + sol mélangés selon la pente), textures Poly Haven.
    static Material CaveRockMaterial()
    {
        Mat("Grotte_Paroi", new Color(0.35f, 0.3f, 0.27f)); // importe les textures des deux surfaces
        Mat("Grotte_Sol", new Color(0.3f, 0.26f, 0.22f));
        var shader = Shader.Find("TheEchoesBelow/Cave Triplanar");
        if (shader == null) return Mat("Grotte_Paroi", new Color(0.35f, 0.3f, 0.27f));
        string path = $"{MatFolder}/Grotte_Roche.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        m.shader = shader;
        if (_texCache == null) _texCache = new Dictionary<string, Texture2D>();
        string W = Surfaces["Grotte_Paroi"].id, F = Surfaces["Grotte_Sol"].id;
        m.SetTexture("_WallMap", LoadTex($"{PolyHavenFolder}/{W}/{W}_diff_1k.jpg", TextureImporterType.Default, true));
        m.SetTexture("_WallNormal", LoadTex($"{PolyHavenFolder}/{W}/{W}_nor_gl_1k.jpg", TextureImporterType.NormalMap, false));
        m.SetTexture("_FloorMap", LoadTex($"{PolyHavenFolder}/{F}/{F}_diff_1k.jpg", TextureImporterType.Default, true));
        m.SetTexture("_FloorNormal", LoadTex($"{PolyHavenFolder}/{F}/{F}_nor_gl_1k.jpg", TextureImporterType.NormalMap, false));
        m.SetColor("_WallColor", new Color(0.55f, 0.48f, 0.42f));
        m.SetColor("_FloorColor", new Color(0.3f, 0.26f, 0.22f)); // sol plus sombre que la paroi (sinon il paraît enneigé)
        m.SetFloat("_WallScale", 7f);
        m.SetFloat("_FloorScale", 4f);
        EditorUtility.SetDirty(m);
        return m;
    }

    // ───────────────────────── Décor ─────────────────────────

    /// Sol sous un point du vide.
    static bool CaveFloor(Vector3 from, out RaycastHit hit) =>
        Physics.Raycast(from + Vector3.up * 0.3f, Vector3.down, out hit, 40f, 1 << _layerGround, QueryTriggerInteraction.Ignore);

    /// Eau sombre : pas de reflet du ciel (on est sous terre).
    static Material CaveWater()
    {
        var m = Mat("Eau_Grotte", new Color(0.01f, 0.025f, 0.035f), 0.92f);
        m.SetFloat("_EnvironmentReflections", 0f);
        m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        return m;
    }

    /// Paroi la plus proche dans une direction horizontale au hasard.
    static bool CaveWall(Vector3 from, out RaycastHit hit)
    {
        float a = Rand(0f, Mathf.PI * 2f);
        var dir = new Vector3(Mathf.Cos(a), Rand(-0.25f, 0.1f), Mathf.Sin(a)).normalized;
        return Physics.Raycast(from, dir, out hit, 14f, 1 << _layerGround, QueryTriggerInteraction.Ignore);
    }

    static void DecorateCaveEntrance(LevelDef D)
    {
        var lamp = PointLight(D, new Vector3(18f, -38f, 5f), 9f, D.props);
        lamp.color = new Color(0.35f, 0.75f, 1f); lamp.intensity = 0.9f;
        Marker(D, "Faille_vers_les_grottes", new Vector3(8f, -33.5f, 3f), Color.magenta);
        CaveView(D, "Vue_Grotte_Faille", new Vector3(6f, -32.2f, 3f), new Vector3(24f, -41f, 7f));
    }

    static void DecorateLake(LevelDef D, CaveNode n)
    {
        float waterY = n.c.y - n.radii.y / 1.6f - 0.25f;
        var water = Cylinder(D.props, "Lac_Souterrain", new Vector3(n.c.x, waterY, n.c.z), Mathf.Min(n.radii.x, n.radii.z) * 0.78f, 0.04f,
            Quaternion.identity, CaveWater(), false);
        water.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
        water.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        for (int k = 0; k < 4; k++) MushroomCluster(D, n.c + new Vector3(Rand(-8f, 8f), 0f, Rand(-8f, 8f)));
        var l = PointLight(D, n.c + Vector3.up * 2f, 18f, D.props);
        l.color = new Color(0.25f, 0.55f, 0.7f); l.intensity = 0.7f;
        Marker(D, "Lac_Souterrain", new Vector3(n.c.x, waterY, n.c.z), Color.cyan);
        CaveView(D, "Vue_Grotte_Lac", n.c + new Vector3(-n.radii.x * 0.7f, -n.radii.y * 0.3f, 0f), n.c);
    }

    static void DecorateRiver(LevelDef D, CaveEdge e)
    {
        var m = CaveWater();
        var parent = Child(D.props, "Riviere");
        parent.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
        for (int i = 0; i < e.pts.Count - 1; i++)
        {
            var a = e.pts[i]; var b = e.pts[i + 1];
            var mid = (a + b) * 0.5f;
            if (!CaveFloor(mid, out var h)) continue;
            var dir = b - a; dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) continue;
            Cube(parent, "Eau", h.point + Vector3.up * 0.1f, new Vector3(e.r * 0.8f, 0.04f, dir.magnitude + 0.6f),
                Quaternion.LookRotation(dir.normalized, Vector3.up), m, false, -1).GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    static void DecorateForest(LevelDef D, CaveNode n)
    {
        if (!CaveFloor(n.c, out var hc)) return;
        float r = Mathf.Min(n.radii.x, n.radii.z);
        var soil = Cylinder(D.props, "Sol_Foret", hc.point + Vector3.up * 0.06f, r * 0.82f, 0.08f, Quaternion.identity, Mat("Grotte_Foret_Sol", new Color(0.3f, 0.35f, 0.2f)), false);
        soil.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        string[] trees = { KSurvival + "tree-tall", KSurvival + "tree", KSurvival + "tree-autumn", KGraveyard + "pine" };
        for (int k = 0; k < 14; k++)
        {
            float a = Rand(0f, Mathf.PI * 2f), d = Mathf.Sqrt(Rand(0.02f, 1f)) * r * 0.62f;
            var p = n.c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
            if (!CaveFloor(p, out var h)) continue;
            float ht = Rand(7f, 11f);
            var t = Model(D, trees[_rng.Next(trees.Length)], h.point, Rand(0f, 360f), new Vector3(ht * 0.7f, ht, ht * 0.7f), Mat("Grotte_Sol", Color.gray), false);
            SetupRealTree(t, true);
        }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(BushLarge) != null)
            for (int k = 0; k < 12; k++)
            {
                float a = Rand(0f, Mathf.PI * 2f), d = Rand(0.2f, 0.75f) * r;
                if (!CaveFloor(n.c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d), out var h)) continue;
                float s = Rand(0.8f, 1.5f);
                SetupRealTree(InstantiateAt(D, _rng.NextDouble() < 0.5 ? BushLarge : BushSmall, h.point, Rand(0f, 360f), new Vector3(s * 1.4f, s, s * 1.4f)), false);
            }
        for (int k = 0; k < 4; k++)
            if (CaveFloor(n.c + new Vector3(Rand(-r, r) * 0.6f, 0f, Rand(-r, r) * 0.6f), out var h))
                Model(D, KSurvival + "tree-log-small", h.point, Rand(0f, 360f), new Vector3(2.2f, 0.6f, 0.6f), Mat("Grotte_Sol", Color.gray));

        // Puits de lumière : une fissure dans la voûte laisse passer une lueur blafarde
        var top = n.c + Vector3.up * (n.radii.y - 1.5f);
        var go = new GameObject("Puits_de_Lumiere");
        go.transform.SetParent(D.props, false);
        go.transform.SetPositionAndRotation(top, Quaternion.Euler(90f, 0f, 0f));
        var spot = go.AddComponent<Light>();
        spot.type = LightType.Spot; spot.color = new Color(0.72f, 0.85f, 1f); spot.intensity = 5f;
        spot.range = 40f; spot.spotAngle = 75f; spot.shadows = LightShadows.Soft;
        var fill = PointLight(D, hc.point + Vector3.up * 3f, 16f, D.props);
        fill.color = new Color(0.4f, 0.8f, 0.45f); fill.intensity = 0.6f;
        for (int k = 0; k < 5; k++) MushroomCluster(D, n.c + new Vector3(Rand(-r, r) * 0.7f, 0f, Rand(-r, r) * 0.7f));
        DecorateForestGround(D, n, r);
        Marker(D, "Foret_Souterraine", hc.point, Color.green);
        CaveView(D, "Vue_Grotte_Foret", n.c + new Vector3(r * 0.75f, -n.radii.y * 0.25f, 0f), n.c + Vector3.down * 3f);
    }

    static void BuildPitTrap(LevelDef D, Vector3 tunnelPoint, Vector3 backPoint)
    {
        if (!CaveFloor(backPoint, out var hb)) return;
        var respawn = new GameObject("Reprise_Gouffre").transform;
        respawn.SetParent(D.props, false);
        respawn.position = hb.point + Vector3.up * 0.1f;
        var dir = tunnelPoint - backPoint; dir.y = 0f;
        respawn.rotation = Quaternion.LookRotation(dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.forward, Vector3.up);

        float floorY = tunnelPoint.y - 2.6f / 1.8f;
        var trap = new GameObject("Fond_Gouffre");
        trap.transform.SetParent(D.props, false);
        trap.transform.position = new Vector3(tunnelPoint.x, floorY - 8f, tunnelPoint.z);
        var box = trap.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(5f, 8f, 5f);
        trap.AddComponent<FallTrap>().respawn = respawn;
        Marker(D, "Gouffre", new Vector3(tunnelPoint.x, floorY, tunnelPoint.z), Color.red);
    }

    /// Point de vue pour les captures (repère orienté vers une cible).
    static void CaveView(LevelDef D, string name, Vector3 at, Vector3 lookAt)
    {
        Marker(D, name, at, Color.white);
        var mk = D.markers.Find("◆ " + name);
        if (mk != null) mk.rotation = Quaternion.LookRotation((lookAt - at).normalized, Vector3.up);
    }

    static Material GlowMat(string name, Color c, float intensity)
    {
        var m = Mat(name, c * 0.6f, 0.7f);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", c * intensity);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(m);
        return m;
    }

    // ───────────────────────── Notes des grottes ─────────────────────────

    static readonly string[] CaveNoteTexts =
    {
        "On a suivi la rivière. L'eau est tiède, et elle coule vers le haut à certains endroits.\n\nPersonne ne veut en parler.",
        "Il y a des arbres, ici, sous la terre. Des vrais arbres. Une lumière tombe d'une fissure, mais il n'y a pas de ciel au-dessus de nous.\n\nOn ne devrait pas être là.",
        "S. a voulu traverser le boyau. On l'a entendu ramper longtemps, puis plus rien.\n\nCe matin, sa voix nous a appelés depuis le lac.",
        "Les cristaux bourdonnent quand on s'approche du cœur. Plus on avance, plus on entend nos noms.\n\nNe pas descendre plus loin. NE PAS DESCENDRE.",
    };
    static int _caveNotes;

    static string NextCaveNoteId() => "note_grotte_" + (_caveNotes++ % CaveNoteTexts.Length);

    static void EnsureCaveNotes()
    {
        _caveNotes = 0;
        var feuille = AssetDatabase.LoadAssetAtPath<GameObject>(ItemsFolder + "/Prefabs/Feuille.prefab");
        for (int k = 0; k < CaveNoteTexts.Length; k++)
        {
            string text = CaveNoteTexts[k];
            _items["note_grotte_" + k] = SaveItem("note_grotte_" + k, "Page déchirée", "Une page arrachée à un carnet, humide et tachée de boue.", ItemKind.Note, feuille, 0.2f, i => i.noteText = text);
        }
    }
}
