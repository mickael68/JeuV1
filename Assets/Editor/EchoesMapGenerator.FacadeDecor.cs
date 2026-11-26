using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Habillage extérieur de la résidence (bâtiment, aile nord, passerelle, cage d'escalier ouest) :
/// soubassement en pierre, bandeaux entre les étages, corniche et acrotère, appuis et linteaux de fenêtres,
/// descentes d'eau pluviale, toit (gravier, climatiseurs, ventilations, réservoir), entrée soignée
/// (marquise sur poteaux, marches, enseigne "RÉSIDENCE DU PARC", appliques, interphone, boîte aux lettres),
/// massifs plantés, local à vélos, conteneurs à ordures.
/// </summary>
public static partial class EchoesMapGenerator
{
    struct Block
    {
        public string name; public Rect r;              // emprise extérieure (faces des façades)
        public List<(char side, float a, float b)> gaps; // parties de côtés à ne pas habiller (jonctions)
        public bool skipN, skipS, skipE, skipW;
    }

    static void DecorateFacades(Transform ext, LevelDef X, float gy, float fy0)
    {
        var parent = Child(ext, "Habillage_Facades");
        var stoneDark = Mat("Facade_Soubassement", new Color(0.45f, 0.44f, 0.42f));
        var stone = Mat("Facade_Bandeau", new Color(0.82f, 0.8f, 0.75f));
        var metal = Mat("Facade_Zinc", new Color(0.32f, 0.33f, 0.35f), 0.45f);

        // Blocs : bâtiment, aile, passerelle, cage d'escalier ouest (habillée de brique)
        Rect shaft = default; bool hasShaft = false;
        foreach (var r in _shaftRects) if (r.xMax <= -HalfX + 0.5f) { shaft = r; hasShaft = true; break; }
        var main = new Block { name = "Batiment", r = Rect.MinMaxRect(-HalfX - 0.3f, CorrS - RoomDepth - 0.3f, HalfX + 0.3f, CorrN + RoomDepth + 0.3f),
            gaps = new List<(char, float, float)> { ('N', WCx0 - 0.1f, WCx1 + 0.1f) } };
        if (hasShaft) main.gaps.Add(('W', shaft.yMin - 0.1f, shaft.yMax + 0.1f));
        var wing = new Block { name = "Aile", r = Rect.MinMaxRect(WX0 - 0.3f, WZ0 - 0.3f, WX1 + 0.3f, WZ1 + 0.3f),
            gaps = new List<(char, float, float)> { ('S', WCx0 - 0.1f, WCx1 + 0.1f) } };
        var bridge = new Block { name = "Passerelle", r = Rect.MinMaxRect(WCx0 - 0.1f, CorrN + RoomDepth + 0.3f, WCx1 + 0.1f, WZ0 - 0.3f),
            gaps = new List<(char, float, float)>(), skipN = true, skipS = true };
        var blocks = new List<Block> { main, wing, bridge };
        if (hasShaft)
        {
            var sr = Rect.MinMaxRect(shaft.xMin - 0.1f, shaft.yMin - 0.1f, shaft.xMax, shaft.yMax + 0.1f);
            CladShaft(parent, sr, fy0, FacadeTop, Mat("Facade_Brique", Color.white));
            blocks.Add(new Block { name = "Cage_Escalier", r = sr, gaps = new List<(char, float, float)>(), skipE = true });
        }

        float top = FacadeTop;
        foreach (var b in blocks)
        {
            // Soubassement (0,6 m au-dessus du sol), bandeaux à chaque plancher, corniche, acrotère + couvertine
            var bs = b;
            if (b.name == "Batiment") // le soubassement s'interrompt devant la porte d'entrée
                bs = new Block { name = b.name, r = b.r, skipN = b.skipN, skipS = b.skipS, skipE = b.skipE, skipW = b.skipW,
                    gaps = new List<(char, float, float)>(b.gaps) { ('E', ExitZ - ExitW * 0.5f - 0.25f, ExitZ + ExitW * 0.5f + 0.25f) } };
            Ring(parent, bs, "Soubassement", fy0 - 0.05f, gy + 0.6f, 0.07f, stoneDark);
            foreach (var n in new[] { 2, 3 })
            {
                float ys = _levels[n].y - Slab;
                Ring(parent, b, "Bandeau", ys, ys + 0.2f, 0.06f, stone);
            }
            Ring(parent, b, "Corniche", top - 0.35f, top, 0.2f, stone);
            Ring(parent, b, "Acrotere", top, top + 0.65f, 0.2f, Mat("Facade_Brique", Color.white));
            Ring(parent, b, "Couvertine", top + 0.65f, top + 0.73f, 0.27f, stone);
        }

        // Toits : revêtement gravillonné + équipements
        var roofMat = Mat("Toit_Gravier", new Color(0.42f, 0.41f, 0.39f));
        foreach (var b in new[] { main, wing, bridge })
            Cube(parent, "Toit_" + b.name, new Vector3(b.r.center.x, top + 0.015f, b.r.center.y), new Vector3(b.r.width - 0.05f, 0.03f, b.r.height - 0.05f), Quaternion.identity, roofMat, false, -1);
        var roofL = new LevelDef { number = 1, y = top, h = 3f, root = parent, props = Child(parent, "Toit_Equipements"), markers = X.markers };
        RoofEquipment(roofL, main.r, top, true);
        RoofEquipment(roofL, wing.r, top, false);

        // Descentes d'eau pluviale aux angles
        foreach (var b in new[] { main, wing })
            foreach (var c in new[] { new Vector2(b.r.xMin, b.r.yMin), new Vector2(b.r.xMax, b.r.yMin), new Vector2(b.r.xMin, b.r.yMax), new Vector2(b.r.xMax, b.r.yMax) })
            {
                if (hasShaft && b.name == "Batiment" && c.x < 0 && Mathf.Abs(c.y - shaft.center.y) < shaft.height) continue;
                var p = new Vector3(c.x + Mathf.Sign(c.x - b.r.center.x) * 0.12f, 0f, c.y + Mathf.Sign(c.y - b.r.center.y) * 0.12f);
                Cylinder(parent, "Descente_Pluviale", new Vector3(p.x, (fy0 + top) * 0.5f, p.z), 0.055f, top - fy0, Quaternion.identity, metal, false);
                Cube(parent, "Dauphin", new Vector3(p.x, gy + 0.35f, p.z), new Vector3(0.15f, 0.7f, 0.15f), Quaternion.identity, metal, false, -1);
            }

        // Appuis (dessous) et linteaux (dessus) de toutes les fenêtres
        foreach (var lv in _levels.Values)
        {
            if (lv.kind != Kind.Dorm) continue;
            foreach (var x in StudioWindowXs(true)) WindowTrim(parent, lv, new Vector3(x, 0f, main.r.yMax), Vector3.forward, stone);
            foreach (var x in StudioWindowXs(false)) WindowTrim(parent, lv, new Vector3(x, 0f, main.r.yMin), Vector3.back, stone);
            foreach (var z in WingWestWinZ) WindowTrim(parent, lv, new Vector3(wing.r.xMin, 0f, z), Vector3.left, stone);
            foreach (var z in WingEastWinZ) WindowTrim(parent, lv, new Vector3(wing.r.xMax, 0f, z), Vector3.right, stone);
            foreach (var x in WingNorthWinX) WindowTrim(parent, lv, new Vector3(x, 0f, wing.r.yMax), Vector3.forward, stone);
            foreach (var x in WingSouthWinX) WindowTrim(parent, lv, new Vector3(x, 0f, wing.r.yMin), Vector3.back, stone);
            if (lv.number >= 2) WindowTrim(parent, lv, new Vector3(main.r.xMax, 0f, ExitZ), Vector3.right, stone); // bout du couloir
        }

        EastCorridorWindows(parent);
        BuildEntrance(parent, X, gy, main.r.xMax, stone, metal);
        FlowerBeds(parent, X, gy, main.r);
        StreetFurniture(parent, X, gy, main.r);
    }

    /// Fenêtres au bout du couloir (façade est, étages 2 et 3), au-dessus de l'entrée.
    static void EastCorridorWindows(Transform p)
    {
        var frame = Mat("Fenetre_Cadre", new Color(0.92f, 0.92f, 0.9f), 0.4f);
        var glass = GlassMat();
        foreach (var n in new[] { 2, 3 })
        {
            var L = _levels[n];
            float y0 = L.y + WinSill, y1 = L.y + WinTop, h = y1 - y0, t = 0.07f, d = 0.12f;
            var c = new Vector3(HalfX + 0.1f, (y0 + y1) * 0.5f, ExitZ);
            Cube(p, "Cadre", c + Vector3.up * (t * 0.5f - h * 0.5f), new Vector3(d, t, WinW), Quaternion.identity, frame, true, _layerWalls);
            Cube(p, "Cadre", c + Vector3.up * (h * 0.5f - t * 0.5f), new Vector3(d, t, WinW), Quaternion.identity, frame, true, _layerWalls);
            Cube(p, "Cadre", c + Vector3.forward * (WinW * 0.5f - t * 0.5f), new Vector3(d, h, t), Quaternion.identity, frame, true, _layerWalls);
            Cube(p, "Cadre", c - Vector3.forward * (WinW * 0.5f - t * 0.5f), new Vector3(d, h, t), Quaternion.identity, frame, true, _layerWalls);
            Cube(p, "Croisillon", c, new Vector3(d * 0.8f, h, 0.05f), Quaternion.identity, frame, false, -1);
            Cube(p, "Vitre", c, new Vector3(0.012f, h - 0.1f, WinW - 0.1f), Quaternion.identity, glass, true, _layerWalls)
                .GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    /// Ceinture de boîtes autour d'un bloc (de y0 à y1, dépassant de "outset" de la façade), en sautant les jonctions.
    static void Ring(Transform p, Block b, string name, float y0, float y1, float outset, Material m)
    {
        float h = y1 - y0, yc = (y0 + y1) * 0.5f, o = outset;
        var r = b.r;
        void Side(char s, float a0, float a1)
        {
            var cuts = new List<(float, float)> { (a0, a1) };
            foreach (var g in b.gaps)
            {
                if (g.side != s) continue;
                var next = new List<(float, float)>();
                foreach (var (u, v) in cuts)
                {
                    if (g.b <= u || g.a >= v) { next.Add((u, v)); continue; }
                    if (g.a > u) next.Add((u, g.a));
                    if (g.b < v) next.Add((g.b, v));
                }
                cuts = next;
            }
            foreach (var (u, v) in cuts)
            {
                if (v - u < 0.05f) continue;
                Vector3 c, size;
                switch (s)
                {
                    case 'N': c = new Vector3((u + v) * 0.5f, yc, r.yMax + o * 0.5f); size = new Vector3(v - u, h, o); break;
                    case 'S': c = new Vector3((u + v) * 0.5f, yc, r.yMin - o * 0.5f); size = new Vector3(v - u, h, o); break;
                    case 'E': c = new Vector3(r.xMax + o * 0.5f, yc, (u + v) * 0.5f); size = new Vector3(o, h, v - u); break;
                    default: c = new Vector3(r.xMin - o * 0.5f, yc, (u + v) * 0.5f); size = new Vector3(o, h, v - u); break;
                }
                Cube(p, name, c, size, Quaternion.identity, m, false, -1);
            }
        }
        if (!b.skipN) Side('N', r.xMin - o, r.xMax + o);
        if (!b.skipS) Side('S', r.xMin - o, r.xMax + o);
        if (!b.skipE) Side('E', r.yMin, r.yMax);
        if (!b.skipW) Side('W', r.yMin, r.yMax);
    }

    /// Appui de fenêtre en pierre (débordant) et linteau au-dessus.
    static void WindowTrim(Transform p, LevelDef L, Vector3 faceCenter, Vector3 outward, Material stone)
    {
        bool alongX = Mathf.Abs(outward.z) > 0.5f;
        var sillSize = alongX ? new Vector3(WinW + 0.16f, 0.07f, 0.14f) : new Vector3(0.14f, 0.07f, WinW + 0.16f);
        var linSize = alongX ? new Vector3(WinW + 0.3f, 0.16f, 0.05f) : new Vector3(0.05f, 0.16f, WinW + 0.3f);
        Cube(p, "Appui", faceCenter + outward * 0.07f + Vector3.up * (L.y + WinSill - 0.035f), sillSize, Quaternion.identity, stone, false, -1);
        Cube(p, "Linteau", faceCenter + outward * 0.025f + Vector3.up * (L.y + WinTop + 0.08f), linSize, Quaternion.identity, stone, false, -1);
    }

    /// Cage d'escalier ouest habillée de brique, avec de hautes vitres sombres à chaque palier.
    static void CladShaft(Transform p, Rect sr, float y0, float y1, Material brick)
    {
        WallAlongX(p, "Cage_Brique_Sud", sr.yMin - 0.1f, sr.xMin - 0.2f, sr.xMax, y0, y1, brick);
        WallAlongX(p, "Cage_Brique_Nord", sr.yMax + 0.1f, sr.xMin - 0.2f, sr.xMax, y0, y1, brick);
        WallAlongZ(p, "Cage_Brique_Ouest", sr.xMin - 0.1f, sr.yMin, sr.yMax, y0, y1, brick);
        Box(p, "Cage_Toit", new Vector3(sr.xMin - 0.2f, y1 - 0.05f, sr.yMin - 0.2f), new Vector3(sr.xMax, y1 + 0.05f, sr.yMax + 0.2f), Mat("Toit_Gravier", new Color(0.42f, 0.41f, 0.39f)), _layerWalls);
        var dark = Mat("Vitre_Sombre", new Color(0.08f, 0.1f, 0.12f), 0.9f);
        var frame = Mat("Fenetre_Cadre", new Color(0.92f, 0.92f, 0.9f), 0.4f);
        foreach (var n in new[] { 1, 2, 3 })
        {
            float yc = _levels[n].y + 1.55f;
            Cube(p, "Vitre_Escalier", new Vector3(sr.xMin - 0.215f, yc, sr.center.y), new Vector3(0.02f, 1.5f, 0.7f), Quaternion.identity, dark, false, -1);
            Cube(p, "Cadre_Escalier", new Vector3(sr.xMin - 0.21f, yc, sr.center.y), new Vector3(0.015f, 1.62f, 0.82f), Quaternion.identity, frame, false, -1);
        }
    }

    /// Climatiseurs, extracteurs, réservoir d'eau et trappe d'accès sur un toit.
    /// Climatiseurs et coffrets techniques sur un toit (modèles Poly Haven).
    /// Hauteur (monde) du bas de la partie « large » d'un objet : on ignore ce qui pend dessous (tuyau, câble…).
    static float LowestWideY(GameObject go, Bounds b)
    {
        var ys = new List<Vector3>();
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            var m = mf.transform.localToWorldMatrix;
            foreach (var v in mf.sharedMesh.vertices) ys.Add(m.MultiplyPoint3x4(v));
        }
        if (ys.Count == 0) return b.min.y;
        const int bins = 50;
        float step = b.size.y / bins;
        for (int i = 0; i < bins; i++)
        {
            float y0 = b.min.y + i * step, y1 = y0 + step;
            float xMin = float.MaxValue, xMax = float.MinValue, zMin = float.MaxValue, zMax = float.MinValue;
            foreach (var p in ys)
                if (p.y >= y0 && p.y < y1) { xMin = Mathf.Min(xMin, p.x); xMax = Mathf.Max(xMax, p.x); zMin = Mathf.Min(zMin, p.z); zMax = Mathf.Max(zMax, p.z); }
            if (xMax - xMin > b.size.x * 0.4f || zMax - zMin > b.size.z * 0.6f) return y0;
        }
        return b.min.y;
    }

    static void RoofEquipment(LevelDef R, Rect r, float top, bool big)
    {
        int n = big ? 5 : 3;
        for (int k = 0; k < n; k++)
        {
            var c = new Vector3(Mathf.Lerp(r.xMin + 4f, r.xMax - 4f, (k + 0.5f) / n), top, r.center.y + (k % 2 == 0 ? -2.2f : 1.8f));
            var ac = CaveProp(R, "exterior_aircon_unit", c, k % 2 == 0 ? 0f : 180f, new Vector3(1.1f, 1.0f, 0.6f));
            // Modèle prévu pour un mur : son tuyau d'évacuation pend sous les équerres. Sur le toit, on le pose
            // sur un socle en béton de la hauteur du tuyau (qui s'y cache), sinon il « flotte » sur son tuyau.
            if (ac != null && TryBounds(ac, out var b))
            {
                float feet = LowestWideY(ac, b);
                float ph = feet - b.min.y;
                if (ph > 0.02f)
                    Cube(R.props, "Socle_Clim", new Vector3(b.center.x, top + ph * 0.5f, b.center.z), new Vector3(b.size.x * 0.95f, ph, b.size.z * 0.9f),
                        Quaternion.identity, Mat("Socle_Beton", new Color(0.55f, 0.54f, 0.52f)), true, _layerWalls);
            }
        }
        if (big) CaveProp(R, "utility_box_02", new Vector3(r.xMax - 5f, top, r.center.y), 90f, new Vector3(0.8f, 1.2f, 0.5f));
    }

    /// Entrée principale (façade est) : marquise sur poteaux, marches, enseigne, appliques, interphone, boîte aux lettres.
    static void BuildEntrance(Transform p, LevelDef X, float gy, float faceX, Material stone, Material metal)
    {
        float zc = ExitZ;
        var dark = Mat("Marquise", new Color(0.14f, 0.15f, 0.17f), 0.5f);
        // Encadrement en pierre de la porte
        Cube(p, "Encadrement_Porte", new Vector3(faceX + 0.04f, gy + DoorH + 0.12f, zc), new Vector3(0.08f, 0.24f, ExitW + 0.5f), Quaternion.identity, stone, false, -1);
        foreach (var s in new[] { -1f, 1f })
        {
            Cube(p, "Encadrement_Porte", new Vector3(faceX + 0.04f, gy + DoorH * 0.5f, zc + s * (ExitW * 0.5f + 0.12f)), new Vector3(0.08f, DoorH, 0.24f), Quaternion.identity, stone, false, -1);
        }
        // Marquise : dalle sombre + bandeau lumineux, deux poteaux
        Cube(p, "Marquise", new Vector3(faceX + 1.1f, gy + 2.75f, zc), new Vector3(2.2f, 0.14f, 3.6f), Quaternion.identity, dark, true, _layerWalls);
        var glow = Mat("Lampe_Allumee", new Color(1f, 0.85f, 0.6f), 0.8f);
        glow.EnableKeyword("_EMISSION");
        glow.SetColor("_EmissionColor", new Color(1f, 0.85f, 0.6f) * 1.5f);
        Cube(p, "Marquise_Spots", new Vector3(faceX + 1.1f, gy + 2.67f, zc), new Vector3(1.6f, 0.02f, 0.1f), Quaternion.identity, glow, false, -1);
        foreach (var s in new[] { -1f, 1f })
            Cylinder(p, "Poteau_Marquise", new Vector3(faceX + 2.05f, gy + 1.34f, zc + s * 1.6f), 0.06f, 2.68f, Quaternion.identity, metal, true);
        // Deux marches devant la porte
        var mPath = Mat("Exterieur_Allee", Color.white);
        Box(p, "Marche", new Vector3(faceX, gy, zc - 1.5f), new Vector3(faceX + 2.2f, gy + 0.1f, zc + 1.5f), mPath, _layerGround);
        Cube(p, "Paillasson", new Vector3(faceX + 0.5f, gy + 0.105f, zc), new Vector3(0.6f, 0.01f, 1.1f), Quaternion.identity, Mat("Paillasson", new Color(0.2f, 0.16f, 0.12f)), false, -1);
        // Enseigne au-dessus de la marquise
        var sign = new LevelDef { number = 1, y = gy, h = 4f, props = p, root = p };
        PlaqueBig(sign, "RÉSIDENCE DU PARC", new Vector3(faceX + 0.08f, gy + 3.0f, zc), Vector3.right, 0.02f, 3.4f, 0.36f); // entre la marquise et le bandeau de l'étage 2
        // Appliques murales de part et d'autre, interphone, boîte aux lettres, numéro
        foreach (var s in new[] { -1f, 1f })
        {
            var at = new Vector3(faceX + 0.1f, gy + 2.1f, zc + s * 1.55f);
            var wl = new LevelDef { number = 1, y = gy, h = 4f, root = p, props = p };
            var lantern = CaveProp(wl, "street_lamp_02", at + Vector3.down * 0.45f + Vector3.right * 0.05f, 90f, new Vector3(0.35f, 0.9f, 0.45f));
            if (lantern == null) Cube(p, "Applique", at, new Vector3(0.14f, 0.3f, 0.18f), Quaternion.identity, glow, false, -1);
            var l = PointLight(X, at + Vector3.right * 0.3f, 6f, p);
            l.intensity = 1.2f;
        }
        Cube(p, "Interphone", new Vector3(faceX + 0.04f, gy + 1.35f, zc + ExitW * 0.5f + 0.45f), new Vector3(0.05f, 0.32f, 0.18f), Quaternion.identity, metal, false, -1);
        PlaqueBig(sign, "12", new Vector3(faceX + 0.02f, gy + 2.25f, zc + ExitW * 0.5f + 0.45f), Vector3.right, 0.03f, 0.35f, 0.3f);
    }

    /// Grande plaque de texte (enseigne) : fond sombre, lettres claires.
    static void PlaqueBig(LevelDef L, string text, Vector3 pos, Vector3 outward, float charSize, float width, float height)
    {
        var rot = Quaternion.LookRotation(-outward, Vector3.up);
        Cube(L.props, "Enseigne", pos - outward * 0.02f, new Vector3(width, height, 0.04f), rot, Mat("Enseigne", new Color(0.1f, 0.12f, 0.16f), 0.5f), false, -1);
        var go = new GameObject("Texte_" + text);
        go.transform.SetParent(L.props, false);
        go.transform.SetPositionAndRotation(pos + outward * 0.006f, rot);
        var tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tm.fontSize = 96;
        tm.characterSize = charSize;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = new Color(0.95f, 0.93f, 0.85f);
        if (tm.font != null) go.GetComponent<MeshRenderer>().sharedMaterial = TextMaterial(tm.font);
    }

    /// Massifs plantés le long des façades sud et nord du bâtiment (bordure + terre + buissons).
    static void FlowerBeds(Transform p, LevelDef X, float gy, Rect main)
    {
        var soil = Mat("Massif_Terre", new Color(0.3f, 0.24f, 0.18f));
        var edge = Mat("Facade_Bandeau", new Color(0.82f, 0.8f, 0.75f));
        foreach (var (z, s) in new[] { (main.yMin, -1f), (main.yMax, 1f) })
        {
            float x0 = main.xMin + 1.5f, x1 = main.xMax - 1.5f;
            var segs = new List<(float, float)>();
            if (s > 0) { segs.Add((x0, WCx0 - 1f)); segs.Add((WCx1 + 1f, x1)); } else segs.Add((x0, x1));
            foreach (var (a, b) in segs)
            {
                var c = new Vector3((a + b) * 0.5f, gy + 0.06f, z + s * 0.75f);
                Cube(p, "Massif", c, new Vector3(b - a, 0.12f, 1.1f), Quaternion.identity, soil, false, -1);
                Cube(p, "Bordure_Massif", c + new Vector3(0f, 0.02f, s * 0.57f), new Vector3(b - a, 0.16f, 0.06f), Quaternion.identity, edge, false, -1);
                if (AssetDatabase.LoadAssetAtPath<GameObject>(BushSmall) == null) continue;
                for (float x = a + 0.8f; x < b - 0.5f; x += Rand(1.6f, 2.6f))
                {
                    float sc = Rand(0.6f, 1.0f);
                    SetupRealTree(InstantiateAt(X, _rng.NextDouble() < 0.7 ? BushSmall : BushLarge, new Vector3(x, gy + 0.1f, c.z + Rand(-0.15f, 0.15f)), Rand(0f, 360f), new Vector3(sc * 1.2f, sc, sc * 1.2f)), false);
                }
            }
        }
    }

    /// Abri à vélos (arceaux sous un toit) et local à conteneurs, près de l'angle sud-est.
    /// Mobilier urbain autour de l'entrée : poubelles (dans le local en brique), sacs, borne incendie,
    /// coffret électrique, regards, banquettes urbaines (modèles Poly Haven).
    static void StreetFurniture(Transform p, LevelDef X, float gy, Rect main)
    {
        // Local à poubelles : muret en brique, poubelles métalliques et sacs
        var bc = new Vector3(main.xMax + 4.5f, gy, main.yMax - 1.5f);
        var brick = Mat("Facade_Brique", Color.white);
        Cube(p, "Muret_Poubelles", bc + new Vector3(0f, 0.65f, 1.0f), new Vector3(3.6f, 1.3f, 0.2f), Quaternion.identity, brick, true, _layerWalls);
        Cube(p, "Muret_Poubelles", bc + new Vector3(1.8f, 0.65f, 0f), new Vector3(0.2f, 1.3f, 2.0f), Quaternion.identity, brick, true, _layerWalls);
        Cube(p, "Muret_Poubelles", bc + new Vector3(-1.8f, 0.65f, 0f), new Vector3(0.2f, 1.3f, 2.0f), Quaternion.identity, brick, true, _layerWalls);
        for (int k = 0; k < 3; k++)
            Model(X, KFurniture + "trashcan", bc + new Vector3(-1.1f + k * 1.1f, 0f, 0.4f), Rand(0f, 360f), new Vector3(0.7f, 1.05f, 0.7f), brick);
        foreach (var dx in new[] { -1.45f, 1.45f })
            CaveProp(X, "trashbag", bc + new Vector3(dx, 0f, 0.55f), Rand(0f, 360f), Vector3.one * Rand(0.6f, 0.75f));
        // Borne incendie et coffret électrique près de l'entrée, regards sur l'allée
        CaveProp(X, "fire_hydrant", new Vector3(main.xMax + 3.2f, gy, ExitZ + 3.2f), Rand(0f, 360f), new Vector3(0.5f, 0.85f, 0.5f), true);
        CaveProp(X, "utility_box_02", new Vector3(main.xMax + 0.45f, gy, main.yMin + 2.5f), 90f, new Vector3(0.8f, 1.2f, 0.45f), true);
        foreach (var x in new[] { main.xMax + 9f, main.xMax + 18f })
            CaveProp(X, "water_manhole_cover", new Vector3(x, gy + 0.035f, ExitZ), 0f, new Vector3(0.7f, 0.04f, 0.7f));
        // (pas de bancs collés à l'entrée : ceux du jardin de devant entourent les massifs — FrontGarden)
    }

}
