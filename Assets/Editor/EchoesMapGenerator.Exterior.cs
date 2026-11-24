using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Portes des studios (battantes, touche E) et extérieur de la résidence, accessible depuis le niveau 1 :
/// porte de sortie au bout du couloir est, pelouse, allée éclairée, arbres, clôture et façade en brique.
/// </summary>
public static partial class EchoesMapGenerator
{
    // Sortie du niveau 1 : mur est, au milieu du couloir
    const float ExitZ = (CorrS + CorrN) * 0.5f, ExitW = 1.4f;

    // Extérieur : pelouse visible jusqu'à ±GroundX, cour clôturée (jouable) dans YardRect
    const float GroundX = 70f, GroundZ0 = -65f, GroundZ1 = 60f;
    static readonly Rect YardRect = Rect.MinMaxRect(-45f, -40f, 45f, 35f);
    const float FacadeTop = 14.0f; // haut du toit (plafond de l'étage 3)

    static List<Rect> _shaftRects;

    // ───────────────────────── Portes ─────────────────────────

    static Transform DoorsRoot(LevelDef L)
    {
        var t = L.root.Find("Portes");
        if (t != null) return t;
        t = Child(L.root, "Portes");
        // Les portes bougent : hors du NavMesh (les rondes restent dans les couloirs)
        t.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
        return t;
    }

    /// Porte battante. hingePos = bas de la charnière ; le battant part vers la droite (+X local) après rotation yaw.
    static Door BuildDoor(Transform parent, string name, Vector3 hingePos, float yaw, float width, float height, Material m, string label)
    {
        var hinge = new GameObject(name);
        hinge.transform.SetParent(parent, false);
        hinge.transform.SetPositionAndRotation(hingePos, Quaternion.Euler(0f, yaw, 0f));
        var rot = hinge.transform.rotation;
        Vector3 right = rot * Vector3.right;

        Cube(hinge.transform, "Battant", hingePos + right * (width * 0.5f) + Vector3.up * (height * 0.5f),
            new Vector3(width, height, 0.05f), rot, m, true, _layerWalls);
        var metal = Mat("Poignee", new Color(0.75f, 0.73f, 0.68f), 0.8f);
        Cube(hinge.transform, "Poignee", hingePos + right * (width - 0.09f) + Vector3.up * 1.0f,
            new Vector3(0.13f, 0.025f, 0.16f), rot, metal, false, -1);

        var rb = hinge.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        var door = hinge.AddComponent<Door>();
        door.displayName = label;
        door.openClip = SndDoorOpen;
        door.closeClip = SndDoorClose;
        NonStatic(hinge);
        return door;
    }

    /// Porte d'un studio (ouverture dans le mur du couloir, plan z = zWall).
    static void BuildStudioDoor(LevelDef L, float cx, float doorW, float zWall, int index, bool north)
    {
        bool inverted = L.kind == Kind.Inverted;
        var m = Mat(inverted ? "Porte_Studio_-5" : "Porte_Studio", inverted ? new Color(0.55f, 0.3f, 0.3f) : new Color(0.75f, 0.6f, 0.45f));
        float w = doorW - 0.04f, h = DoorH - 0.1f - 0.02f;
        var door = BuildDoor(DoorsRoot(L), $"Porte_Studio_{(north ? "N" : "S")}{index + 1}",
            new Vector3(cx - doorW * 0.5f + 0.02f, L.y + 0.01f, zWall), 0f, w, h, m, "la porte du studio");

        // Résidence inversée : certaines portes sont entrouvertes
        if (inverted && _rng.NextDouble() < 0.4)
            door.startAngle = (_rng.NextDouble() < 0.5 ? -1f : 1f) * Rand(15f, 80f);
    }

    /// Porte de sortie du niveau 1 (mur est, x = HalfX), le battant longe +Z.
    static void BuildExitDoor(LevelDef L)
    {
        var m = Mat("Porte_Sortie", new Color(0.45f, 0.33f, 0.25f));
        var door = BuildDoor(DoorsRoot(L), "Porte_Sortie_Exterieur",
            new Vector3(HalfX, L.y + 0.01f, ExitZ - ExitW * 0.5f + 0.02f), -90f, ExitW - 0.04f, DoorH - 0.02f, m, "la porte de sortie");
        door.openAngle = 95f;
        Marker(L, "Sortie_Exterieur", new Vector3(HalfX - 1.2f, L.y, ExitZ), Color.green);
    }

    // ───────────────────────── Extérieur ─────────────────────────

    static void BuildExterior(Transform root)
    {
        var L1 = _levels[1];
        float gy = L1.y; // le dehors est de plain-pied avec le niveau 1

        var ext = Child(root, "Exterieur");
        var X = new LevelDef { number = 1, name = "Exterieur", y = gy, h = 4f, root = ext, props = Child(ext, "Props"), markers = Child(ext, "Marqueurs"),
            lightColor = new Color(1f, 0.82f, 0.6f), lightIntensity = 2.2f };

        // 1) Pelouse autour du bâtiment (trous : emprise du bâtiment + cages d'escalier qui montent jusqu'ici)
        var holes = new List<Rect> { Rect.MinMaxRect(-HalfX - 0.1f, CorrS - RoomDepth - 0.1f, HalfX + 0.1f, CorrN + RoomDepth + 0.1f) };
        holes.AddRange(_shaftRects);
        holes.Add(WingFootprint); // aile nord
        holes.Add(BridgeFootprint); // passerelle (au niveau du sol)
        GroundAround(ext, Rect.MinMaxRect(-GroundX, GroundZ0, GroundX, GroundZ1), holes, gy - Slab, gy, Mat("Exterieur_Herbe", Color.white));

        // 2) Façade en brique (peau extérieure des étages 1 à 3)
        var mf = Mat("Facade_Brique", Color.white);
        float zS = CorrS - RoomDepth - 0.2f, zN = CorrN + RoomDepth + 0.2f, xE = HalfX + 0.2f, xW = -HalfX - 0.2f;
        float fy0 = gy - Slab;
        var winS = new List<Hole>(); var winN = new List<Hole>();
        foreach (var lv in _levels.Values)
        {
            winS.AddRange(StudioWindowHoles(lv, false));
            winN.AddRange(StudioWindowHoles(lv, true));
        }
        WallAlongX(ext, "Facade_Sud", zS, xW - 0.1f, xE + 0.1f, fy0, FacadeTop, mf, winS.ToArray());
        WallAlongX(ext, "Facade_Nord", zN, xW - 0.1f, xE + 0.1f, fy0, FacadeTop, mf, winN.ToArray());
        WallAlongZ(ext, "Facade_Est", xE, zS, zN, fy0, FacadeTop, mf, new Hole(ExitZ, ExitW, gy, gy + DoorH),
            new Hole(ExitZ, WinW, _levels[2].y + WinSill, _levels[2].y + WinTop), new Hole(ExitZ, WinW, _levels[3].y + WinSill, _levels[3].y + WinTop));
        var westHoles = new List<Hole>();
        foreach (var r in _shaftRects)
            if (r.xMax <= -HalfX + 0.5f) westHoles.Add(new Hole(r.center.y, r.height, fy0, FacadeTop));
        WallAlongZ(ext, "Facade_Ouest", xW, zS, zN, fy0, FacadeTop, mf, westHoles.ToArray());
        BuildWingFacade(ext, mf, fy0);

        // Encadrement + perron + applique au-dessus de la porte
        var mPath = Mat("Exterieur_Allee", Color.white);
        Box(ext, "Perron", new Vector3(xE + 0.1f, gy, ExitZ - 1.8f), new Vector3(xE + 2.6f, gy + 0.04f, ExitZ + 1.8f), mPath, _layerGround);
        var lamp = PointLight(X, new Vector3(xE + 1.1f, gy + 2.5f, ExitZ), 9f, ext); // sous la marquise (DecorateFacades)
        lamp.shadows = LightShadows.Soft;

        // 3) Allée vers le portail (est)
        float pz0 = ExitZ - 1.1f, pz1 = ExitZ + 1.1f;
        Box(ext, "Allee", new Vector3(xE + 2.6f, gy, pz0), new Vector3(YardRect.xMax, gy + 0.03f, pz1), mPath, _layerGround);
        for (float x = xE + 7f; x < YardRect.xMax - 2f; x += 9f)
            StreetLamp(X, new Vector3(x, gy, pz1 + 0.6f));
        AddSeat(Model(X, KGraveyard + "bench", new Vector3(xE + 11.5f, gy, pz0 - 0.9f), 0f, new Vector3(1.8f, 0.9f, 0.6f), mPath), 0.45f, 0f, Vector3.zero, "S'asseoir sur le banc");
        AddSeat(Model(X, KGraveyard + "bench-damaged", new Vector3(xE + 20.5f, gy, pz1 + 0.9f), 180f, new Vector3(1.8f, 0.9f, 0.6f), mPath), 0.45f, 180f, Vector3.zero, "S'asseoir sur le banc");

        // 4) Clôture de la cour + portail fermé au bout de l'allée
        var mFence = Mat("Grille_Metal", new Color(0.18f, 0.18f, 0.2f), 0.5f);
        FenceLine(X, new Vector3(YardRect.xMin, gy, YardRect.yMin), new Vector3(YardRect.xMax, gy, YardRect.yMin), mFence, null);
        FenceLine(X, new Vector3(YardRect.xMin, gy, YardRect.yMax), new Vector3(YardRect.xMax, gy, YardRect.yMax), mFence, null);
        FenceLine(X, new Vector3(YardRect.xMin, gy, YardRect.yMin), new Vector3(YardRect.xMin, gy, YardRect.yMax), mFence, null);
        FenceLine(X, new Vector3(YardRect.xMax, gy, YardRect.yMin), new Vector3(YardRect.xMax, gy, YardRect.yMax), mFence, new Vector2(pz0 - 0.6f, pz1 + 0.6f));
        var gate = Model(X, KGraveyard + "iron-fence-border-gate", new Vector3(YardRect.xMax, gy, ExitZ), 90f, new Vector3(0.4f, 2.4f, 3.4f), mFence);
        FitInGap(gate, YardRect.xMax, pz0 - 0.6f, pz1 + 0.6f);
        Marker(X, "Portail_Ferme (sortie de la residence)", new Vector3(YardRect.xMax - 1.5f, gy, ExitZ), Color.yellow);

        // Murs invisibles : on ne sort pas de la cour
        float wh = 4f;
        Box(ext, "Limite", new Vector3(YardRect.xMin - 0.5f, gy, YardRect.yMin - 0.5f), new Vector3(YardRect.xMax + 0.5f, gy + wh, YardRect.yMin), mFence, _layerWalls).GetComponent<Renderer>().enabled = false;
        Box(ext, "Limite", new Vector3(YardRect.xMin - 0.5f, gy, YardRect.yMax), new Vector3(YardRect.xMax + 0.5f, gy + wh, YardRect.yMax + 0.5f), mFence, _layerWalls).GetComponent<Renderer>().enabled = false;
        Box(ext, "Limite", new Vector3(YardRect.xMin - 0.5f, gy, YardRect.yMin), new Vector3(YardRect.xMin, gy + wh, YardRect.yMax), mFence, _layerWalls).GetComponent<Renderer>().enabled = false;
        Box(ext, "Limite", new Vector3(YardRect.xMax, gy, YardRect.yMin), new Vector3(YardRect.xMax + 0.5f, gy + wh, YardRect.yMax), mFence, _layerWalls).GetComponent<Renderer>().enabled = false;

        // 5) Arbres : bois dense hors de la cour, quelques arbres dans la cour
        string[] trees = { KSurvival + "tree-autumn-tall", KSurvival + "tree-autumn", KSurvival + "tree-tall", KSurvival + "tree", KGraveyard + "pine", KGraveyard + "pine-crooked" };
        var keepOut = Rect.MinMaxRect(-HalfX - 8f, CorrS - RoomDepth - 4f, HalfX + 4f, CorrN + RoomDepth + 4f);
        var wingOut = Rect.MinMaxRect(WX0 - 4f, 5f, WX1 + 4f, WZ1 + 4f); // aile + cour
        int placed = 0;
        for (int tries = 0; tries < 600 && placed < 45; tries++) // arbres réalistes : coûteux, on en met moins
        {
            var p = new Vector2(Rand(-GroundX + 3f, GroundX - 3f), Rand(GroundZ0 + 3f, GroundZ1 - 3f));
            bool inYard = YardRect.Contains(p);
            if (inYard && (placed % 6 != 0)) continue;                   // ~1 sur 6 dans la cour
            if (wingOut.Contains(p)) continue;
            if (inYard && (keepOut.Contains(p) || (p.x > HalfX && Mathf.Abs(p.y - ExitZ) < 4f))) continue;
            if (!inYard && Mathf.Abs(p.x - YardRect.xMax) < 2.5f && Mathf.Abs(p.y - ExitZ) < 3f) continue;
            if (Mathf.Abs(p.x - YardRect.xMin) < 1.5f || Mathf.Abs(p.x - YardRect.xMax) < 1.5f ||
                Mathf.Abs(p.y - YardRect.yMin) < 1.5f || Mathf.Abs(p.y - YardRect.yMax) < 1.5f) continue; // pas sur la clôture
            float hgt = Rand(6f, 11f);
            var tree = Model(X, trees[_rng.Next(trees.Length)], new Vector3(p.x, gy, p.y), Rand(0f, 360f), new Vector3(hgt * 0.8f, hgt, hgt * 0.8f), mFence, false);
            SetupRealTree(tree, inYard);
            placed++;
        }

        // Buissons le long des façades et de la clôture
        if (AssetDatabase.LoadAssetAtPath<GameObject>(BushLarge) != null)
        {
            for (int i = 0; i < 34; i++)
            {
                Vector2 p;
                if (i < 16) // pied de façade (sauf devant la porte)
                {
                    float t = Rand(0f, 1f);
                    int side = i % 4;
                    p = side == 0 ? new Vector2(Mathf.Lerp(-HalfX, HalfX, t), CorrS - RoomDepth - 1.2f)
                      : side == 1 ? new Vector2(Mathf.Lerp(-HalfX, HalfX, t), CorrN + RoomDepth + 1.2f)
                      : side == 2 ? new Vector2(HalfX + 1.2f, Mathf.Lerp(CorrS - RoomDepth, CorrN + RoomDepth, t))
                                  : new Vector2(-HalfX - 1.2f, Mathf.Lerp(CorrN + 3f, CorrN + RoomDepth, t));
                    if (side == 2 && Mathf.Abs(p.y - ExitZ) < 3f) continue;
                    if (side == 1 && p.x > WX0 - 1f && p.x < WX1 + 1f) continue; // devant l'aile nord
                }
                else // le long de la clôture, côté cour
                {
                    int side = i % 3;
                    p = side == 0 ? new Vector2(Rand(YardRect.xMin + 2f, YardRect.xMax - 2f), YardRect.yMin + 1.2f)
                      : side == 1 ? new Vector2(Rand(YardRect.xMin + 2f, YardRect.xMax - 2f), YardRect.yMax - 1.2f)
                                  : new Vector2(YardRect.xMin + 1.2f, Rand(YardRect.yMin + 2f, YardRect.yMax - 2f));
                }
                float bs = Rand(1.0f, 1.8f);
                var bush = InstantiateAt(X, _rng.NextDouble() < 0.5 ? BushLarge : BushSmall, new Vector3(p.x, gy, p.y), Rand(0f, 360f), new Vector3(bs * 1.4f, bs, bs * 1.4f));
                SetupRealTree(bush, false);
            }
        }
        // Rochers et souches pour casser la pelouse
        string[] rocks = { KSurvival + "rock-a", KSurvival + "rock-b", KSurvival + "rock-flat-grass", KSurvival + "tree-log-small", KSurvival + "tree-trunk" };
        for (int i = 0; i < 18; i++)
        {
            var p = new Vector2(Rand(YardRect.xMin + 3f, YardRect.xMax - 3f), Rand(YardRect.yMin + 3f, YardRect.yMax - 3f));
            if (keepOut.Contains(p) || wingOut.Contains(p) || (p.x > HalfX && Mathf.Abs(p.y - ExitZ) < 3f)) continue;
            float s = Rand(0.6f, 1.4f);
            Model(X, rocks[_rng.Next(rocks.Length)], new Vector3(p.x, gy, p.y), Rand(0f, 360f), new Vector3(s, s * 0.7f, s), mFence);
        }
        // 6) Habillage des façades, toits, entrée, massifs, abri à vélos
        DecorateFacades(ext, X, gy, fy0);

        // 7) Décor lointain : collines boisées, petite ville
        BuildBackdrop(X);
    }

    /// Étire et recentre le portail pour qu'il bouche exactement l'ouverture [z0, z1] de la clôture (ligne x).
    static void FitInGap(GameObject gate, float x, float z0, float z1)
    {
        if (gate == null || !TryBounds(gate, out var b) || b.size.z < 0.01f) return;
        var t = gate.transform;
        var local = t.InverseTransformDirection(Vector3.forward); // axe local qui donne la largeur en z
        int axis = Mathf.Abs(local.x) > Mathf.Abs(local.y) ? (Mathf.Abs(local.x) > Mathf.Abs(local.z) ? 0 : 2) : (Mathf.Abs(local.y) > Mathf.Abs(local.z) ? 1 : 2);
        var s = t.localScale; s[axis] *= (z1 - z0) / b.size.z; t.localScale = s;
        if (!TryBounds(gate, out b)) return;
        t.position += new Vector3(x - b.center.x, 0f, (z0 + z1) * 0.5f - b.center.z);
    }

    /// Lampadaire : poteau, tête, lumière chaude.
    static void StreetLamp(LevelDef X, Vector3 foot)
    {
        var m = Mat("Grille_Metal", new Color(0.18f, 0.18f, 0.2f), 0.5f);
        Cylinder(X.props, "Lampadaire", foot + Vector3.up * 1.75f, 0.06f, 3.5f, Quaternion.identity, m, true);
        Cube(X.props, "Lampadaire_Tete", foot + Vector3.up * 3.55f, new Vector3(0.35f, 0.12f, 0.35f), Quaternion.identity, Mat("Lampe_Allumee", new Color(1f, 0.85f, 0.6f), 0.8f), false, -1);
        var l = PointLight(X, foot + Vector3.up * 3.3f, 11f, X.props);
        l.intensity = 2.5f;
    }

    /// Clôture en fer forgé de a à b (tronçons de 3 m), avec une ouverture éventuelle (plage de z).
    static void FenceLine(LevelDef X, Vector3 a, Vector3 b, Material fallback, Vector2? gap)
    {
        // Ouverture exacte (le portail la remplit) : deux tronçons de part et d'autre
        if (gap.HasValue && Mathf.Abs(b.x - a.x) < Mathf.Abs(b.z - a.z))
        {
            FenceLine(X, a, new Vector3(a.x, a.y, gap.Value.x), fallback, null);
            FenceLine(X, new Vector3(a.x, a.y, gap.Value.y), b, fallback, null);
            return;
        }
        const float seg = 3f;
        bool alongX = Mathf.Abs(b.x - a.x) > Mathf.Abs(b.z - a.z);
        float len = alongX ? b.x - a.x : b.z - a.z;
        int n = Mathf.Max(1, Mathf.RoundToInt(len / seg));
        float step = len / n;
        for (int i = 0; i < n; i++)
        {
            float c = (alongX ? a.x : a.z) + (i + 0.5f) * step;
            if (gap.HasValue && !alongX && c > gap.Value.x - step * 0.5f && c < gap.Value.y + step * 0.5f) continue;
            var pos = alongX ? new Vector3(c, a.y, a.z) : new Vector3(a.x, a.y, c);
            FenceSegment(X, pos, alongX, step, fallback); // grille en fer forgé (mesh généré)
        }
    }

    /// Dalle de sol sur "area", sauf dans les rectangles "holes" (découpage en grille, cellules fusionnées par ligne).
    static void GroundAround(Transform p, Rect area, List<Rect> holes, float y0, float y1, Material m)
    {
        var xs = new SortedSet<float> { area.xMin, area.xMax };
        var zs = new SortedSet<float> { area.yMin, area.yMax };
        foreach (var h in holes)
        {
            xs.Add(Mathf.Clamp(h.xMin, area.xMin, area.xMax)); xs.Add(Mathf.Clamp(h.xMax, area.xMin, area.xMax));
            zs.Add(Mathf.Clamp(h.yMin, area.yMin, area.yMax)); zs.Add(Mathf.Clamp(h.yMax, area.yMin, area.yMax));
        }
        var X = new List<float>(xs); var Z = new List<float>(zs);
        for (int j = 0; j < Z.Count - 1; j++)
        {
            float za = Z[j], zb = Z[j + 1];
            if (zb - za < 0.01f) continue;
            int i = 0;
            while (i < X.Count - 1)
            {
                // début d'une suite de cellules pleines
                if (Covered(holes, (X[i] + X[i + 1]) * 0.5f, (za + zb) * 0.5f)) { i++; continue; }
                int k = i;
                while (k < X.Count - 1 && !Covered(holes, (X[k] + X[k + 1]) * 0.5f, (za + zb) * 0.5f)) k++;
                if (X[k] - X[i] > 0.01f)
                    Box(p, "Pelouse", new Vector3(X[i], y0, za), new Vector3(X[k], y1, zb), m, _layerGround);
                i = k;
            }
        }
    }

    static bool Covered(List<Rect> holes, float x, float z)
    {
        foreach (var h in holes) if (h.Contains(new Vector2(x, z))) return true;
        return false;
    }
}
