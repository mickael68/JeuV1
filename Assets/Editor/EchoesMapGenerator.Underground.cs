using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Habillage des couloirs des grands sous-sols, sans toucher à leur plan :
///  -1 « utilités et béton » : parpaings, tuyaux et gaines au plafond, rangées de casiers rouillés, boîtiers électriques ;
///  -2 « infrastructures défaillantes » : béton sale, gros tuyaux rouillés le long des murs, fuites qui gouttent,
///     flaques, gravats, câbles qui pendent, tuyaux arrachés.
/// </summary>
public static partial class EchoesMapGenerator
{
    static void DressUnderground(LevelDef L, bool technical, bool[,] open, int[,] special, bool[,] east, bool[,] north,
                                 Vector2Int start, Vector2Int trapNode)
    {
        float y0 = L.y, y1 = L.y + L.h;
        Vector3 Node(int i, int j) => new Vector3(MazeX0 + (i + 0.5f) * MazeP, y0, MazeZ0 + (j + 0.5f) * MazeP);
        bool Open(int i, int j) => i >= 0 && i < MazeNX && j >= 0 && j < MazeNZ && open[i, j];
        bool Corridor(int i, int j) => Open(i, j) && special[i, j] == 0;
        bool Linked(int i, int j, Vector2Int d) =>
            d.x > 0 ? east[i, j] : d.x < 0 ? i > 0 && east[i - 1, j] : d.y > 0 ? north[i, j] : j > 0 && north[i, j - 1];

        _pipeKit = null; // recharge les modèles à chaque génération
        var pipeMat = Mat("Metal_" + L.number, new Color(0.35f, 0.33f, 0.3f));
        var thinMat = Mat("Gaine_" + L.number, technical ? new Color(0.2f, 0.18f, 0.16f) : new Color(0.42f, 0.42f, 0.4f), 0.35f);
        var lockerMat = Mat("Casier", new Color(0.45f, 0.5f, 0.52f));
        var darkMat = Mat("Casier_Interieur", new Color(0.03f, 0.03f, 0.03f));
        var boxMat = Mat("Boitier_Electrique", new Color(0.55f, 0.55f, 0.5f), 0.3f);
        foreach (var m in new[] { pipeMat, thinMat, lockerMat, boxMat, Mat("Mur_" + L.number, L.wall), Mat("Sol_" + L.number, L.floor), Mat("Plafond_" + L.number, L.wall * 0.8f) })
            NoSkyReflection(m); // sous terre : le métal ne doit pas refléter le ciel
        var parent = Child(L.props, technical ? "Habillage_Infrastructures" : "Habillage_Beton");
        var sides = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
        int lockers = 0, leaks = 0, puddles = 0;

        // Plafond : un tuyau continu par ligne droite de couloir, qui rentre dans le mur à chaque bout (bride).
        // Hauteurs sous le plafond toutes différentes : les tuyaux en x passent au-dessus de ceux en z sans se toucher.
        float hxBig = 0.25f, hxThin = technical ? 0.1f : 0.08f, hzBig = technical ? 0.6f : 0.5f, hzThin = 0.65f;
        float edge = MazeP * 0.5f - Wall * 0.5f;
        for (int j = 0; j < MazeNZ; j++)
            for (int i = 0; i < MazeNX; i++)
            {
                if (!Corridor(i, j) || !east[i, j] || !Corridor(i + 1, j) || (i > 0 && east[i - 1, j] && Corridor(i - 1, j))) continue;
                int e = i;
                while (e + 1 < MazeNX && east[e, j] && Corridor(e + 1, j)) e++;
                float xa = Node(i, j).x - edge, xb = Node(e, j).x + edge, zc = Node(i, j).z;
                CeilingRun(parent, new Vector3(xa, L.y + L.h - hxBig, zc + 1.35f), Vector3.right, xb - xa, technical ? 0.14f : 0.07f, pipeMat, technical ? PipeStyle.Industrial : PipeStyle.Metal);
                CeilingRun(parent, new Vector3(xa, L.y + L.h - hxThin, zc + 1.0f), Vector3.right, xb - xa, 0.035f, thinMat, technical ? PipeStyle.Thin : PipeStyle.ThinPvc);
            }
        for (int i = 0; i < MazeNX; i++)
            for (int j = 0; j < MazeNZ; j++)
            {
                if (!Corridor(i, j) || !north[i, j] || !Corridor(i, j + 1) || (j > 0 && north[i, j - 1] && Corridor(i, j - 1))) continue;
                int e = j;
                while (e + 1 < MazeNZ && north[i, e] && Corridor(i, e + 1)) e++;
                float za = Node(i, j).z - edge, zb = Node(i, e).z + edge, xc = Node(i, j).x;
                CeilingRun(parent, new Vector3(xc - 1.1f, L.y + L.h - hzBig, za), Vector3.forward, zb - za, technical ? 0.1f : 0.055f, pipeMat, technical ? PipeStyle.Industrial : PipeStyle.Metal);
                if (!technical) CeilingRun(parent, new Vector3(xc - 0.8f, L.y + L.h - hzThin, za), Vector3.forward, zb - za, 0.035f, thinMat, PipeStyle.ThinPvc);
            }

        for (int i = 0; i < MazeNX; i++)
            for (int j = 0; j < MazeNZ; j++)
            {
                if (!Corridor(i, j)) continue;
                var c = Node(i, j);
                bool isStart = i == start.x && j == start.y, isTrap = technical && i == trapNode.x && j == trapNode.y;

                // Murs : tuyaux continus (-2), gaine électrique haute (-1), casiers et boîtiers
                foreach (var d in sides)
                {
                    if (Linked(i, j, d)) continue;
                    var n = new Vector2Int(i + d.x, j + d.y);
                    bool roomBehind = Open(n.x, n.y) && special[n.x, n.y] != 0;   // porte possible
                    bool stairDoor = isStart && d == Vector2Int.right;
                    var outDir = new Vector3(d.x, 0f, d.y);
                    var inward = -outDir;
                    var along = new Vector3(-d.y, 0f, d.x);
                    var face = c + outDir * (MazeP * 0.5f - Wall * 0.5f);
                    // Le mur s'arrête-t-il à un bout (ouverture sur un couloir) ? Alors le tuyau rentre dans le mur.
                    bool WallEnds(int sgn)
                    {
                        var e = new Vector2Int(-d.y * sgn, d.x * sgn);
                        if (!Linked(i, j, e)) return false;                 // coin : le mur perpendiculaire prend le relais
                        int a = i + e.x, b = j + e.y;
                        return !Corridor(a, b) || Linked(a, b, d);
                    }
                    bool endLo = WallEnds(-1), endHi = WallEnds(1);
                    // Coin rentrant : le tuyau s'arrête là où il rejoint celui du mur perpendiculaire
                    bool cornerLo = !Linked(i, j, new Vector2Int(d.y, -d.x)), cornerHi = !Linked(i, j, new Vector2Int(-d.y, d.x));

                    if (technical)
                    {
                        WallPipe(parent, face, inward, along, 0.2f, L.h - 0.95f, 0.12f, endLo, endHi, pipeMat, PipeStyle.Industrial, cornerLo, cornerHi);
                        WallPipe(parent, face, inward, along, 0.12f, L.h - 1.3f, 0.06f, endLo, endHi, pipeMat, PipeStyle.Metal, cornerLo, cornerHi);
                        if (!roomBehind && !stairDoor && _rng.NextDouble() < 0.18) // coulure de rouille sous un raccord
                            Cube(parent, "Coulure_Rouille", face + inward * 0.005f + Vector3.up * (L.h - 1.9f) + along * Rand(-1.2f, 1.2f),
                                new Vector3(Rand(0.15f, 0.35f), Rand(0.9f, 1.6f), 0.01f), Quaternion.LookRotation(inward), Mat("Rouille_Coulure", new Color(0.22f, 0.11f, 0.05f), 0.4f), false, -1);
                    }
                    else
                    {
                        WallPipe(parent, face, inward, along, 0.06f, L.h - 0.9f, 0.04f, endLo, endHi, thinMat, PipeStyle.ThinPvc, cornerLo, cornerHi);
                        if (!roomBehind && !stairDoor && !isStart)
                        {
                            double roll = _rng.NextDouble();
                            if (roll < 0.2) { LockerBank(parent, face, inward, along, _rng.Next(4, 7), lockerMat, darkMat); lockers++; }
                            else if (roll < 0.3) ElectricBox(parent, face, inward, along, boxMat, darkMat);
                        }
                    }
                }

                if (isStart || isTrap) continue;

                // Sol et plafond : fuites, flaques, gravats, câbles
                Vector3 Spot(float m) => c + new Vector3(Rand(-m, m), 0f, Rand(-m, m));
                if (technical)
                {
                    double roll = _rng.NextDouble();
                    if (roll < 0.13) { Leak(L, parent, Spot(1.2f)); leaks++; }
                    else if (roll < 0.17) { BrokenPipe(L, parent, c, pipeMat); leaks++; }
                    else if (roll < 0.33) { Puddle(parent, Spot(1.3f), Rand(0.5f, 1.3f)); puddles++; }
                    if (_rng.NextDouble() < 0.12) Debris(parent, Spot(1.4f), Mat("Mur_" + L.number, L.wall));
                    double cr = _rng.NextDouble();
                    if (cr < 0.45) CableBundle(parent, c, L.h, _rng.Next(1, 4));          // câbles qui pendent en guirlande
                    if (_rng.NextDouble() < 0.3) DanglingCable(parent, c, L.h);           // câble arraché qui pend dans le vide
                }
                else
                {
                    if (_rng.NextDouble() < 0.06) { Puddle(parent, Spot(1.3f), Rand(0.4f, 0.9f)); puddles++; }
                    if (_rng.NextDouble() < 0.15) CableBundle(parent, c, L.h, _rng.Next(1, 3));
                }
            }

        // Vue de contrôle : le plus long couloir droit du niveau
        int best = -1; Vector3 from = Vector3.zero, dir = Vector3.forward;
        for (int i = 0; i < MazeNX; i++)
            for (int j = 0; j < MazeNZ; j++)
            {
                if (!Corridor(i, j)) continue;
                foreach (var d in sides)
                {
                    int len = 0, a = i, b = j;
                    while (Linked(a, b, d) && Corridor(a + d.x, b + d.y)) { a += d.x; b += d.y; len++; }
                    if (len > best) { best = len; from = Node(i, j); dir = new Vector3(d.x, 0f, d.y); }
                }
            }
        if (best > 0) CaveView(L, "Vue_Sous_Sol_" + L.number, from - dir * 1.5f, from + dir * 10f);

        Debug.Log($"[EchoesMap] Habillage du {L.number} : {lockers} rangées de casiers, {leaks} fuites, {puddles} flaques.");
    }

    // ───────────────────────── Tuyaux Poly Haven (modèles modulaires) ─────────────────────────

    enum PipeStyle { None, Industrial, Metal, Thin, ThinPvc }

    struct KitPiece { public Mesh mesh; public Material[] mats; public int axis; public float natLen; }
    static Dictionary<PipeStyle, KitPiece> _pipeKit;

    /// Une pièce droite par style : modular_industrial_pipes_01 (gros tuyaux à brides, -2)
    /// et modular_pipes (tuyaux métal, fins en métal ou en PVC).
    static void LoadPipeKit()
    {
        _pipeKit = new Dictionary<PipeStyle, KitPiece>();
        PolyHavenModels.PrepareAll(false);
        AddKit(PipeStyle.Industrial, "modular_industrial_pipes_01", null);
        AddKit(PipeStyle.Metal, "modular_pipes", "pipe_200cm_metal");
        AddKit(PipeStyle.Thin, "modular_pipes", "pipe_thin_200cm_metal");
        AddKit(PipeStyle.ThinPvc, "modular_pipes", "pipe_thin_200cm_pvc");
        var info = "";
        foreach (var kv in _pipeKit)
        {
            foreach (var m in kv.Value.mats) NoSkyReflection(m);
            info += $" · {kv.Key} = {kv.Value.mesh.name} ({kv.Value.natLen:0.00} m)";
        }
        Debug.Log($"[EchoesMap] Tuyaux Poly Haven : {_pipeKit.Count} styles{info}");
    }

    /// child = nom de la pièce ; null = la plus longue pièce droite du modèle.
    static void AddKit(PipeStyle style, string id, string child)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PolyHavenModels.FbxPath(id));
        if (prefab == null) { Debug.LogWarning("[EchoesMap] Modèle introuvable : " + id); return; }
        KitPiece best = default;
        float bestLen = -1f;
        foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null || (child != null && mf.name != child)) continue;
            var size = mf.sharedMesh.bounds.size;
            var M = mf.transform.localToWorldMatrix;
            var w = new Vector3(M.MultiplyVector(Vector3.right).magnitude * size.x,
                                M.MultiplyVector(Vector3.up).magnitude * size.y,
                                M.MultiplyVector(Vector3.forward).magnitude * size.z);
            int ax = w.x >= w.y && w.x >= w.z ? 0 : w.y >= w.z ? 1 : 2;
            float a = w[(ax + 1) % 3], b = w[(ax + 2) % 3];
            bool straight = Mathf.Max(a, b) / Mathf.Max(0.001f, Mathf.Min(a, b)) < 1.4f && w[ax] > 3f * Mathf.Max(a, b);
            if (child == null && !straight) continue;
            if (w[ax] <= bestLen) continue;
            bestLen = w[ax];
            best = new KitPiece { mesh = mf.sharedMesh, mats = mf.GetComponent<MeshRenderer>().sharedMaterials, axis = ax, natLen = w[ax] };
        }
        if (bestLen > 0f) _pipeKit[style] = best;
        else Debug.LogWarning($"[EchoesMap] Pièce de tuyau introuvable : {id}/{child ?? "(droite)"}");
    }

    static Material KitMaterial(PipeStyle style)
    {
        if (_pipeKit == null) LoadPipeKit();
        return _pipeKit.TryGetValue(style, out var k) && k.mats.Length > 0 ? k.mats[0] : null;
    }

    /// Tuyau droit fait de pièces du modèle mises bout à bout (étirées pour tomber juste).
    static bool KitPipe(Transform p, Vector3 center, Vector3 dir, float length, float radius, PipeStyle style)
    {
        if (_pipeKit == null) LoadPipeKit();
        if (!_pipeKit.TryGetValue(style, out var k)) return false;
        var b = k.mesh.bounds;
        var axVec = k.axis == 0 ? Vector3.right : k.axis == 1 ? Vector3.up : Vector3.forward;
        float meshLen = b.size[k.axis], meshWidth = Mathf.Max(b.size[(k.axis + 1) % 3], b.size[(k.axis + 2) % 3]);
        int n = Mathf.Max(1, Mathf.RoundToInt(length / Mathf.Max(0.3f, k.natLen)));
        float seg = length / n;
        float width = 2f * radius * (style == PipeStyle.Industrial ? 1.45f : 1f); // les brides dépassent du tuyau
        var scale = Vector3.one * (width / Mathf.Max(0.0001f, meshWidth));
        scale[k.axis] = seg / Mathf.Max(0.0001f, meshLen);
        dir = dir.normalized;
        var rot = Quaternion.FromToRotation(axVec, dir);
        for (int i = 0; i < n; i++)
        {
            var c = center + dir * (-length * 0.5f + seg * (i + 0.5f));
            var go = new GameObject("Tuyau_" + style);
            go.transform.SetParent(p, false);
            go.transform.rotation = rot;
            go.transform.localScale = scale;
            go.transform.position = c - rot * Vector3.Scale(b.center, scale);
            go.AddComponent<MeshFilter>().sharedMesh = k.mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = k.mats;
            mr.shadowCastingMode = radius < 0.05f ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
            go.layer = _layerFurniture;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }
        return true;
    }

    // ───────────────────────── Éléments ─────────────────────────

    static void NoSkyReflection(Material m)
    {
        if (m == null || !m.HasProperty("_EnvironmentReflections")) return;
        m.SetFloat("_EnvironmentReflections", 0f);
        m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        EditorUtility.SetDirty(m);
    }

    /// Tuyau le long d'un mur, sur toute la case ; aux bouts où le mur s'arrête, il fait un coude et rentre dans le mur.
    static void WallPipe(Transform p, Vector3 face, Vector3 inward, Vector3 along, float off, float height, float radius, bool endLo, bool endHi, Material m, PipeStyle style,
                         bool cornerLo = false, bool cornerHi = false)
    {
        float cornerT = MazeP * 0.5f - Wall * 0.5f - off;     // axe du tuyau du mur perpendiculaire
        float lo = endLo ? -MazeP * 0.5f + 0.35f : cornerLo ? -cornerT : -MazeP * 0.5f;
        float hi = endHi ? MazeP * 0.5f - 0.35f : cornerHi ? cornerT : MazeP * 0.5f;
        var basePt = face + inward * off + Vector3.up * height;
        Pipe(p, basePt + along * ((lo + hi) * 0.5f), along, hi - lo, radius, m, style);
        m = KitMaterial(style) ?? m; // coude et bride assortis au modèle
        foreach (var (isEnd, t) in new[] { (endLo, lo), (endHi, hi) })
        {
            if (!isEnd) continue;
            var corner = basePt + along * t;
            Pipe(p, corner - inward * (off * 0.5f), inward, off + radius, radius, m);                       // coude vers le mur
            Pipe(p, face + inward * 0.015f + along * t + Vector3.up * height, inward, 0.03f, radius * 1.5f, m); // bride contre le mur
        }
    }

    /// Tuyau de plafond de "start" sur "length" selon "dir", avec une bride contre le mur à chaque bout.
    static void CeilingRun(Transform p, Vector3 start, Vector3 dir, float length, float radius, Material m, PipeStyle style)
    {
        Pipe(p, start + dir * (length * 0.5f), dir, length, radius, m, style);
        var fm = KitMaterial(style) ?? m;
        Pipe(p, start + dir * 0.02f, dir, 0.04f, radius * 1.6f, fm);
        Pipe(p, start + dir * (length - 0.02f), dir, 0.04f, radius * 1.6f, fm);
    }

    /// Tuyau horizontal centré sur "center", orienté selon "axis".
    static void Pipe(Transform p, Vector3 center, Vector3 axis, float length, float radius, Material m, PipeStyle style = PipeStyle.None)
    {
        if (style != PipeStyle.None && KitPipe(p, center, axis, length, radius, style)) return; // vrai modèle Poly Haven
        var go = Cylinder(p, "Tuyau", center, radius, length, Quaternion.FromToRotation(Vector3.up, axis), m, false);
        go.GetComponent<Renderer>().shadowCastingMode = radius < 0.05f ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
    }

    /// Rangée de casiers métalliques contre un mur (quelques portes ouvertes ou arrachées).
    static void LockerBank(Transform p, Vector3 wallFace, Vector3 inward, Vector3 along, int n, Material body, Material dark)
    {
        const float w = 0.42f, depth = 0.45f, h = 1.85f;
        var root = Child(p, "Casiers");
        var rot = Quaternion.LookRotation(inward, Vector3.up);
        float shift = Rand(-0.6f, 0.6f);
        var center = wallFace + inward * (depth * 0.5f + 0.01f) + along * shift;
        Cube(root, "Casiers_Corps", center + Vector3.up * (h * 0.5f), new Vector3(n * w, h, depth), rot, body, true, _layerFurniture);
        for (int k = 0; k < n; k++)
        {
            var lc = center + along * ((k - (n - 1) * 0.5f) * w);
            var front = lc + inward * (depth * 0.5f + 0.012f);
            double roll = _rng.NextDouble();
            if (roll < 0.12) // porte arrachée : intérieur sombre
            {
                Cube(root, "Casier_Vide", front - inward * 0.01f + Vector3.up * (h * 0.5f), new Vector3(w - 0.05f, h - 0.08f, 0.01f), rot, dark, false, -1);
                continue;
            }
            var door = Cube(root, "Casier_Porte", front + Vector3.up * (h * 0.5f), new Vector3(w - 0.03f, h - 0.06f, 0.02f), rot, body, false, -1);
            if (roll < 0.3) // entrouverte : on voit l'intérieur sombre
            {
                Cube(root, "Casier_Vide", front - inward * 0.01f + Vector3.up * (h * 0.5f), new Vector3(w - 0.05f, h - 0.08f, 0.01f), rot, dark, false, -1);
                door.transform.RotateAround(front - along * (w * 0.5f - 0.015f), Vector3.up, -Rand(35f, 110f)); // s'ouvre vers le couloir
                continue;
            }
            for (int s = 0; s < 3; s++) // fentes d'aération
                Cube(root, "Fente", front + inward * 0.012f + Vector3.up * (h - 0.25f - s * 0.05f), new Vector3((w - 0.03f) * 0.55f, 0.014f, 0.004f), rot, dark, false, -1);
        }
    }

    /// Boîtier électrique mural avec gaine qui monte au plafond.
    static void ElectricBox(Transform p, Vector3 wallFace, Vector3 inward, Vector3 along, Material box, Material dark)
    {
        var rot = Quaternion.LookRotation(inward, Vector3.up);
        var at = wallFace + inward * 0.09f + along * Rand(-1.2f, 1.2f) + Vector3.up * 1.5f;
        Cube(p, "Boitier_Electrique", at, new Vector3(0.5f, 0.65f, 0.16f), rot, box, false, -1);
        Cube(p, "Boitier_Voyant", at + inward * 0.085f + Vector3.up * 0.22f + along * 0.15f, new Vector3(0.04f, 0.04f, 0.01f), rot, GlowMat("Voyant_Rouge", new Color(1f, 0.1f, 0.05f), 2f), false, -1);
        Cylinder(p, "Gaine", at + Vector3.up * 1.2f - inward * 0.04f, 0.025f, 1.8f, Quaternion.identity, dark, false);
    }

    /// Flaque : disque sombre et brillant (sans reflet du ciel).
    static void Puddle(Transform p, Vector3 floor, float r)
    {
        var rot = Quaternion.Euler(0f, Rand(0f, 180f), 0f);
        var go = Cylinder(p, "Flaque", floor + Vector3.up * 0.006f, r, 0.004f, rot, WaterPuddle, false);
        float squash = Rand(0.45f, 0.85f);
        var s = go.transform.localScale; s.z *= squash; go.transform.localScale = s;
        go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        WaterVolumeBox(p, "Flaque_Pas_Mouilles", floor + Vector3.up * 0.4f, new Vector3(r * 1.6f, 0.8f, r * 1.6f * squash), rot, 1f); // pas mouillés, sans ralentir
    }

    /// Fuite : tache d'humidité au plafond, gouttes qui tombent, flaque dessous.
    static void Leak(LevelDef L, Transform p, Vector3 floor)
    {
        var top = floor + Vector3.up * (L.h - 0.02f);
        var stain = Cylinder(p, "Tache_Humidite", top, Rand(0.35f, 0.7f), 0.004f, Quaternion.identity, Mat("Tache_Humidite", new Color(0.12f, 0.09f, 0.06f), 0.6f), false);
        stain.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Drips(p, top + Vector3.down * 0.03f, L.h - 0.05f, Rand(1.5f, 5f));
        Puddle(p, floor, Rand(0.5f, 1.1f));
        WaterSound(p, floor + Vector3.up * 0.2f, SndDrip, 0.35f, 9f);
    }

    /// Tuyau arraché qui pend du plafond et goutte par son extrémité.
    static void BrokenPipe(LevelDef L, Transform p, Vector3 c, Material m)
    {
        float yaw = Rand(0f, 360f), tilt = Rand(35f, 60f), len = Rand(1.6f, 2.4f);
        var anchor = c + Vector3.up * (L.h + 0.05f);   // part du plafond (arraché de son support)
        var dirDown = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(90f - tilt, 0f, 0f) * Vector3.forward; // vers le bas, en biais
        var end = anchor + dirDown * len;
        if (end.y < L.y + 0.8f) end = new Vector3(end.x, L.y + 0.8f, end.z);
        var mid = (anchor + end) * 0.5f;
        Cylinder(p, "Support_Arrache", c + Vector3.up * (L.h - 0.03f), 0.2f, 0.06f, Quaternion.identity, KitMaterial(PipeStyle.Industrial) ?? m, false);
        if (!KitPipe(p, mid, (end - anchor).normalized, Vector3.Distance(anchor, end), 0.1f, PipeStyle.Industrial))
            Cylinder(p, "Tuyau_Arrache", mid, 0.1f, Vector3.Distance(anchor, end), Quaternion.FromToRotation(Vector3.up, (end - anchor).normalized), m, false);
        Drips(p, end + Vector3.down * 0.05f, end.y - L.y, Rand(4f, 9f));
        Puddle(p, new Vector3(end.x, L.y, end.z), Rand(0.7f, 1.3f));
        WaterSound(p, new Vector3(end.x, L.y + 0.2f, end.z), SndDrip, 0.45f, 11f);
    }

    /// Gouttes : particules étirées qui tombent sous la gravité, du point "from" jusqu'au sol (hauteur "fall").
    static void Drips(Transform p, Vector3 from, float fall, float rate)
    {
        var go = new GameObject("Gouttes");
        go.transform.SetParent(p, false);
        go.transform.position = from;
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = Mathf.Sqrt(2f * Mathf.Max(0.3f, fall) / 9.81f);
        main.startSpeed = 0f;
        main.gravityModifier = 1f;
        main.startSize = 0.03f;
        main.startColor = new Color(0.75f, 0.82f, 0.9f, 0.75f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 30;
        var em = ps.emission; em.rateOverTime = rate;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.02f;
        var pr = go.GetComponent<ParticleSystemRenderer>();
        pr.renderMode = ParticleSystemRenderMode.Stretch;
        pr.velocityScale = 0.05f;
        pr.lengthScale = 1.5f;
        pr.sharedMaterial = SteamMaterial();
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        NonStatic(go);
    }

    /// Gravats : morceaux de béton tombés du plafond.
    static void Debris(Transform p, Vector3 floor, Material m)
    {
        int n = _rng.Next(4, 9);
        for (int k = 0; k < n; k++)
        {
            float s = Rand(0.08f, 0.32f);
            var at = floor + new Vector3(Rand(-0.6f, 0.6f), s * 0.35f, Rand(-0.6f, 0.6f));
            Cube(p, "Gravat", at, new Vector3(s, s * Rand(0.4f, 0.8f), s * Rand(0.6f, 1.2f)), Quaternion.Euler(Rand(-20f, 20f), Rand(0f, 360f), Rand(-20f, 20f)), m, false, -1);
        }
    }

    /// Câble électrique qui pend entre deux points du plafond.
    static Material[] _cableMats;
    static Material CableMat()
    {
        if (_cableMats == null || _cableMats[0] == null)
            _cableMats = new[] { Mat("Cable_Noir", new Color(0.04f, 0.04f, 0.045f), 0.35f), Mat("Cable_Gris", new Color(0.25f, 0.25f, 0.26f), 0.3f),
                                 Mat("Cable_Rouge", new Color(0.35f, 0.06f, 0.05f), 0.35f), Mat("Cable_Jaune", new Color(0.55f, 0.45f, 0.1f), 0.3f) };
        return _cableMats[_rng.NextDouble() < 0.6 ? 0 : _rng.Next(1, _cableMats.Length)];
    }

    /// Courbe de câble (cylindres bout à bout) passant par les points donnés.
    static void CablePath(Transform p, List<Vector3> pts, float radius, Material m)
    {
        for (int k = 0; k < pts.Count - 1; k++)
        {
            var u = pts[k]; var v = pts[k + 1];
            var go = Cylinder(p, "Cable", (u + v) * 0.5f, radius, Vector3.Distance(u, v) + radius, Quaternion.FromToRotation(Vector3.up, (v - u).normalized), m, false);
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    /// 1 à 3 câbles accrochés au plafond, qui pendent en guirlande (décalés pour ne pas toucher la lampe au centre).
    static void CableBundle(Transform p, Vector3 c, float h, int count)
    {
        float yaw = Rand(0f, 180f);
        var dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        var side = Vector3.Cross(Vector3.up, dir);
        var center = c + side * Rand(0.6f, 1.1f) * (_rng.NextDouble() < 0.5 ? -1f : 1f);
        for (int n = 0; n < count; n++)
        {
            float span = Rand(1.4f, 2.6f), sag = Rand(0.35f, 1.2f), r = Rand(0.012f, 0.024f);
            var off = side * (n * 0.07f) + dir * Rand(-0.3f, 0.3f);
            var a = center + off + Vector3.up * (h - 0.05f) - dir * span * 0.5f;
            var b = center + off + Vector3.up * (h - 0.05f) + dir * span * 0.5f;
            var pts = new List<Vector3>();
            const int segs = 10;
            for (int k = 0; k <= segs; k++) { float t = k / (float)segs; pts.Add(Vector3.Lerp(a, b, t) + Vector3.down * (4f * sag * t * (1f - t))); }
            CablePath(p, pts, r, CableMat());
        }
    }

    /// Câble arraché : il descend du plafond et pend dans le vide, le bout dénudé recourbé.
    static void DanglingCable(Transform p, Vector3 c, float h)
    {
        var top = c + new Vector3(Rand(-1.3f, 1.3f), h - 0.02f, Rand(-1.3f, 1.3f));
        if (new Vector2(top.x - c.x, top.z - c.z).magnitude < 0.6f) top += new Vector3(0.7f, 0f, 0f); // loin de la lampe
        float len = Rand(0.9f, 2.1f);
        var drift = Quaternion.Euler(0f, Rand(0f, 360f), 0f) * Vector3.forward * Rand(0.1f, 0.35f);
        var pts = new List<Vector3>();
        const int segs = 8;
        for (int k = 0; k <= segs; k++)
        {
            float t = k / (float)segs;
            pts.Add(top + Vector3.down * (len * t) + drift * (t * t) + Vector3.Cross(Vector3.up, drift).normalized * (Mathf.Sin(t * 5f) * 0.03f));
        }
        var end = pts[pts.Count - 1];
        pts.Add(end + drift.normalized * 0.12f + Vector3.up * 0.06f);   // bout recourbé
        CablePath(p, pts, Rand(0.014f, 0.022f), CableMat());
        var copper = Mat("Cuivre", new Color(0.75f, 0.42f, 0.2f), 0.7f);
        copper.SetFloat("_Metallic", 1f);
        var tip = pts[pts.Count - 1];
        Cylinder(p, "Cable_Denude", tip + drift.normalized * 0.03f, 0.006f, 0.06f, Quaternion.FromToRotation(Vector3.up, drift.normalized), copper, false);
    }
}
