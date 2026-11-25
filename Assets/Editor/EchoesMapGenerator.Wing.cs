using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Aile nord de la résidence (étages 1 à 3). Elle est séparée du bâtiment par une cour (les studios nord gardent
/// leurs fenêtres sur la cour) et reliée au couloir par une passerelle vitrée partant de l'ancien studio nord n°6.
///   étage 1 : cuisine + cafétéria · toilettes garçons · toilettes filles · salle de repos du personnel
///   étage 2 : salle d'étude · toilettes garçons · toilettes filles · deux studios du personnel
///   étage 3 : espace détente / jeux · toilettes garçons · toilettes filles · deux studios du personnel
/// Entrée (étage 1, près de la sortie) : accueil vitré avec une surveillante au comptoir (studio sud n°10)
/// et poste de surveillance (studio nord n°10) : vraies caméras, images sur des télés plates.
/// Les locaux du personnel sont fermés : le passe est sur le bureau du poste de surveillance.
/// </summary>
public static partial class EchoesMapGenerator
{
    const int WingRoomIndex = 5;          // studio nord qui devient le départ de la passerelle
    const int SurveillanceRoomIndex = 9;  // studio nord de l'étage 1 près de la sortie
    const int ReceptionRoomIndex = 9;     // studio sud de l'étage 1 près de la sortie : accueil
    const float WX0 = -12f, WX1 = 16f;    // emprise de l'aile en x
    const float WZ0 = 11.3f, WZ1 = 27f;   // de la cour au fond de l'aile
    const float WCx0 = 0.5f, WCx1 = 3.5f; // couloir de l'aile et passerelle
    const float BridgeZ0 = 5.3f;          // la passerelle part de la façade nord
    const float KitchenX = -7f;           // mur entre la cuisine et la salle (étage 1)
    const float BoysZ1 = WZ0 + 3.1f, GirlsZ1 = WZ0 + 6.2f, StaffSplit = 22.25f;
    const string StaffKey = "passe_personnel";

    static readonly float[] WingWestWinZ = { 13f, 17f, 21f, 25f };
    static readonly float[] WingEastWinZ = { 12.85f, 15.95f, 19.9f, 24.6f };
    static readonly float[] WingNorthWinX = { -9.5f, -5f, -1.5f, 2f, 6f, 10f, 14f };
    static readonly float[] WingSouthWinX = { -9.5f, -5f, -1.5f, 6f, 10f, 14f };

    static readonly string[] StaffNames = { "M. Garnier — surveillant de nuit", "Mme Roussel — surveillante", "M. Ferrand — gardien", "Mme Okafor — intendante" };

    static readonly List<(Camera cam, string label)> _cctvCams = new List<(Camera, string)>();
    static CctvSystem _cctvSystem;

    static string[] MP(string cat, params string[] names)
    {
        var r = new string[names.Length];
        for (int i = 0; i < names.Length; i++) r[i] = cat + "/" + names[i];
        return r;
    }

    /// Taille d'un objet (largeur, hauteur, profondeur) exprimée dans les axes du monde selon son orientation.
    static Vector3 F(float w, float h, float d, float yaw) =>
        Mathf.Abs(Mathf.Sin(yaw * Mathf.Deg2Rad)) > 0.7f ? new Vector3(d, h, w) : new Vector3(w, h, d);

    // ───────────────────────── Structure ─────────────────────────

    static void BuildWing(LevelDef L)
    {
        if (L.kind != Kind.Dorm) return;
        var mw = Mat("Mur_" + L.number, L.wall);
        var mf = Mat("Sol_" + L.number, L.floor);
        var mc = Mat("Plafond_" + L.number, L.wall * 0.8f);
        float y0 = L.y, y1 = L.y + L.h;
        var root = Child(L.root, "Aile_Nord");

        // Dalles de l'aile et de la passerelle
        Box(root, "Sol_Aile", new Vector3(WX0 - 0.1f, y0 - Slab, WZ0 - 0.1f), new Vector3(WX1 + 0.1f, y0, WZ1 + 0.1f), mf, _layerGround);
        Box(root, "Plafond_Aile", new Vector3(WX0 - 0.1f, y1, WZ0 - 0.1f), new Vector3(WX1 + 0.1f, y1 + Slab, WZ1 + 0.1f), mc, _layerWalls);
        Box(root, "Sol_Passerelle", new Vector3(WCx0 - 0.1f, y0 - Slab, 5.0f), new Vector3(WCx1 + 0.1f, y0, WZ0), mf, _layerGround);
        Box(root, "Plafond_Passerelle", new Vector3(WCx0 - 0.1f, y1, 5.0f), new Vector3(WCx1 + 0.1f, y1 + Slab, WZ0), mc, _layerWalls);

        // Passerelle vitrée au-dessus de la cour
        var brick = Mat("Facade_Brique", Color.white);
        var bridgeWin = new Hole((BridgeZ0 + WZ0) * 0.5f, WZ0 - BridgeZ0 - 1.0f, y0 + 0.85f, y0 + 2.5f);
        WallAlongZ(root, "Passerelle_Ouest", WCx0, BridgeZ0, WZ0, y0, y1, brick, bridgeWin);
        WallAlongZ(root, "Passerelle_Est", WCx1, BridgeZ0, WZ0, y0, y1, brick, bridgeWin);
        var glass = GlassMat();
        foreach (var x in new[] { WCx0, WCx1 })
            Cube(root, "Vitre_Passerelle", new Vector3(x, y0 + 1.675f, bridgeWin.c), new Vector3(0.02f, 1.65f, bridgeWin.w), Quaternion.identity, glass, true, _layerWalls)
                .GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        CeilingLight(L, root, new Vector3(2f, y1, 8.3f), 6f);

        // Murs extérieurs de l'aile (côté intérieur), avec fenêtres
        WallAlongZ(root, "Mur_Aile_Ouest", WX0, WZ0, WZ1, y0, y1, mw, WingWinHoles(L, WingWestWinZ));
        WallAlongZ(root, "Mur_Aile_Est", WX1, WZ0, WZ1, y0, y1, mw, WingWinHoles(L, WingEastWinZ));
        WallAlongX(root, "Mur_Aile_Nord", WZ1, WX0, WX1, y0, y1, mw, WingWinHoles(L, WingNorthWinX));
        var southHoles = new List<Hole>(WingWinHoles(L, WingSouthWinX)) { new Hole(2f, WCx1 - WCx0 - 0.2f, y0, y0 + DoorH + 0.2f) };
        WallAlongX(root, "Mur_Aile_Sud", WZ0, WX0, WX1, y0, y1, mw, southHoles.ToArray());
        BuildWingWindows(L, root);

        // Couloir de l'aile : grande salle à l'ouest (deux ouvertures), toilettes et personnel à l'est
        WallAlongZ(root, "Mur_Couloir_Aile_O", WCx0, WZ0, WZ1, y0, y1, mw,
            new Hole(13.5f, 2.2f, y0, y0 + DoorH + 0.2f), new Hole(21.5f, 2.2f, y0, y0 + DoorH + 0.2f));
        float boysDoor = (WZ0 + BoysZ1) * 0.5f, girlsDoor = (BoysZ1 + GirlsZ1) * 0.5f;
        var eastDoors = new List<Hole> { new Hole(boysDoor, 1.1f, y0, y0 + DoorH - 0.1f), new Hole(girlsDoor, 1.1f, y0, y0 + DoorH - 0.1f) };
        float[] staffDoors = L.number == 1 ? new[] { StaffSplit } : new[] { (GirlsZ1 + StaffSplit) * 0.5f, (StaffSplit + WZ1) * 0.5f };
        foreach (var z in staffDoors) eastDoors.Add(new Hole(z, 1.1f, y0, y0 + DoorH - 0.1f));
        WallAlongZ(root, "Mur_Couloir_Aile_E", WCx1, WZ0, WZ1, y0, y1, mw, eastDoors.ToArray());
        WallAlongX(root, "Cloison_Toilettes", BoysZ1, WCx1, WX1, y0, y1, mw);
        WallAlongX(root, "Cloison_Toilettes", GirlsZ1, WCx1, WX1, y0, y1, mw);
        if (L.number != 1) WallAlongX(root, "Cloison_Studios", StaffSplit, WCx1, WX1, y0, y1, mw);

        // Portes
        var doorMat = Mat("Porte_Studio", new Color(0.75f, 0.6f, 0.45f));
        WingDoor(L, boysDoor, doorMat, "Porte_Toilettes_Garcons", "les toilettes des garçons", false, null);
        WingDoor(L, girlsDoor, doorMat, "Porte_Toilettes_Filles", "les toilettes des filles", false, null);
        if (L.number == 1)
            WingDoor(L, StaffSplit, doorMat, "Porte_Salle_Repos", "la salle de repos du personnel", true, "Salle de repos — réservé au personnel.");
        else
        {
            int s = (L.number - 2) * 2;
            for (int k = 0; k < 2; k++)
                WingDoor(L, staffDoors[k], doorMat, "Porte_Studio_Personnel", "le studio de " + StaffNames[s + k].Split('—')[0].Trim(), true, "Studio privé : " + StaffNames[s + k] + ".");
        }

        // Plaques
        float px = WCx1 - Wall * 0.5f - 0.02f;
        Plaque(L, "GARÇONS", new Vector3(px, y0 + 2.35f, boysDoor), Vector3.left);
        Plaque(L, "FILLES", new Vector3(px, y0 + 2.35f, girlsDoor), Vector3.left);
        foreach (var z in staffDoors) Plaque(L, "PERSONNEL", new Vector3(px, y0 + 2.35f, z), Vector3.left);
        string west = L.number == 1 ? "CAFÉTÉRIA" : L.number == 2 ? "SALLE D'ÉTUDE" : "ESPACE DÉTENTE";
        Plaque(L, west, new Vector3(WCx0 + Wall * 0.5f + 0.02f, y0 + 2.6f, 13.5f), Vector3.right);
        Plaque(L, west, new Vector3(WCx0 + Wall * 0.5f + 0.02f, y0 + 2.6f, 21.5f), Vector3.right);
        Plaque(L, west + " · TOILETTES", new Vector3(2f, y0 + 2.6f, CorrN + RoomDepth - Wall * 0.5f - 0.02f), Vector3.back);

        // Éclairage
        foreach (var z in new[] { 13f, 17.5f, 22f, 25.8f }) CeilingLight(L, root, new Vector3(2f, y1, z), 7f);

        // Contenu
        var westRoom = Rect.MinMaxRect(WX0, WZ0, WCx0, WZ1);
        if (L.number == 1) FurnishCafeteria(L, westRoom);
        else if (L.number == 2) FurnishStudyRoom(L, westRoom);
        else FurnishGameRoom(L, westRoom);
        FurnishToilets(L, Rect.MinMaxRect(WCx1, WZ0, WX1, BoysZ1), true);
        FurnishToilets(L, Rect.MinMaxRect(WCx1, BoysZ1, WX1, GirlsZ1), false);
        if (L.number == 1) FurnishStaffRestRoom(L, Rect.MinMaxRect(WCx1, GirlsZ1, WX1, WZ1));
        else
        {
            int s = (L.number - 2) * 2;
            FurnishStaffStudio(L, Rect.MinMaxRect(WCx1, GirlsZ1, WX1, StaffSplit), StaffNames[s]);
            FurnishStaffStudio(L, Rect.MinMaxRect(WCx1, StaffSplit, WX1, WZ1), StaffNames[s + 1]);
        }
        if (L.number == 1) DecorateCourtyard(L);

        // Vues de contrôle (captures)
        if (L.number == 1)
        {
            CaveView(L, "Vue_Cafeteria", new Vector3(-0.6f, y0, WZ1 - 0.7f), new Vector3(-8f, y0, 14f));
            CaveView(L, "Vue_Cuisine", new Vector3(-7.6f, y0, 26.3f), new Vector3(-11f, y0, 15f));
            CaveView(L, "Vue_Toilettes", new Vector3(4.4f, y0, boysDoor + 0.3f), new Vector3(15f, y0, WZ0 + 0.5f));
            CaveView(L, "Vue_Salle_Repos", new Vector3(4.4f, y0, GirlsZ1 + 0.7f), new Vector3(12f, y0, WZ1));
            CaveView(L, "Vue_Aile_Exterieur", new Vector3(26f, y0, 40f), new Vector3(2f, y0 + 4f, 16f));
            CaveView(L, "Vue_Cour", new Vector3(14f, y0, 6.5f), new Vector3(-6f, y0 + 2f, 10f));
        }
        else if (L.number == 2)
        {
            CaveView(L, "Vue_Salle_Etude", new Vector3(-1f, y0, WZ0 + 0.7f), new Vector3(-6f, y0, 24f));
            CaveView(L, "Vue_Studio_Personnel", new Vector3(4.6f, y0, 20.6f), new Vector3(15f, y0, 19.5f));
        }
        else CaveView(L, "Vue_Espace_Detente", new Vector3(-0.8f, y0, WZ1 - 0.7f), new Vector3(-9f, y0, 15f));

        // Extincteur et caméras
        CaveProp(L, "korean_fire_extinguisher_01", new Vector3(WCx0 + 0.25f, y0, 18f), 90f, new Vector3(0.3f, 0.6f, 0.3f));
        CctvCamera(L, new Vector3(WCx1 - 0.2f, y1 - 0.25f, WZ1 - 0.3f), new Vector3(2f, y0 + 1f, 13f), $"Aile nord — étage {L.number}");
        if (L.number == 1) CctvCamera(L, new Vector3(-0.2f, y1 - 0.25f, WZ1 - 0.4f), new Vector3(-5f, y0 + 0.8f, 14f), "Cafétéria");
        CctvCamera(L, new Vector3(-HalfX + 0.4f, y1 - 0.25f, CorrN - 0.2f), new Vector3(8f, y0 + 1f, (CorrS + CorrN) * 0.5f), $"Couloir — étage {L.number}");
        if (L.number == 1) CctvCamera(L, new Vector3(HalfX - 0.4f, y1 - 0.25f, CorrN - 0.2f), new Vector3(-6f, y0 + 1f, (CorrS + CorrN) * 0.5f), "Entrée");
    }

    static Hole[] WingWinHoles(LevelDef L, float[] centers)
    {
        var h = new Hole[centers.Length];
        for (int i = 0; i < centers.Length; i++) h[i] = new Hole(centers[i], WinW, L.y + WinSill, L.y + WinTop);
        return h;
    }

    /// Cadres et vitres des fenêtres de l'aile (entre le mur intérieur et la façade).
    static void BuildWingWindows(LevelDef L, Transform root)
    {
        var frame = Mat("Fenetre_Cadre", new Color(0.92f, 0.92f, 0.9f), 0.4f);
        var glass = GlassMat();
        var parent = Child(root, "Fenetres_Aile");
        float y0 = L.y + WinSill, y1 = L.y + WinTop, h = y1 - y0, t = 0.07f, d = 0.12f;
        void Win(Vector3 c, bool alongX)
        {
            var rot = alongX ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);
            Vector3 a = rot * Vector3.right;
            Cube(parent, "Cadre", c + Vector3.up * (t * 0.5f - h * 0.5f), new Vector3(WinW, t, d), rot, frame, true, _layerWalls);
            Cube(parent, "Cadre", c + Vector3.up * (h * 0.5f - t * 0.5f), new Vector3(WinW, t, d), rot, frame, true, _layerWalls);
            Cube(parent, "Cadre", c - a * (WinW * 0.5f - t * 0.5f), new Vector3(t, h, d), rot, frame, true, _layerWalls);
            Cube(parent, "Cadre", c + a * (WinW * 0.5f - t * 0.5f), new Vector3(t, h, d), rot, frame, true, _layerWalls);
            Cube(parent, "Croisillon", c, new Vector3(0.05f, h, d * 0.8f), rot, frame, false, -1);
            Cube(parent, "Vitre", c, new Vector3(WinW - 0.1f, h - 0.1f, 0.012f), rot, glass, true, _layerWalls)
                .GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        float ym = (y0 + y1) * 0.5f;
        foreach (var z in WingWestWinZ) Win(new Vector3(WX0 - 0.2f, ym, z), false);
        foreach (var z in WingEastWinZ) Win(new Vector3(WX1 + 0.2f, ym, z), false);
        foreach (var x in WingNorthWinX) Win(new Vector3(x, ym, WZ1 + 0.2f), true);
        foreach (var x in WingSouthWinX) Win(new Vector3(x, ym, WZ0 - 0.2f), true);
    }

    /// Porte dans le mur est du couloir de l'aile (x = WCx1), centrée en z.
    static void WingDoor(LevelDef L, float z, Material m, string name, string label, bool staff, string lockedMsg)
    {
        var door = BuildDoor(DoorsRoot(L), name, new Vector3(WCx1, L.y + 0.01f, z - 0.55f + 0.02f), -90f, 1.06f, DoorH - 0.12f, m, label);
        door.displayName = label;
        door.openClip = SndDoorOpen;
        if (!staff) return;
        door.locked = true;
        door.requiredKeyId = StaffKey;
        door.lockedMessage = lockedMsg + " Il faudrait le passe du personnel.";
    }

    /// Plaque de texte murale ; "outward" = direction vers laquelle la plaque regarde.
    static void Plaque(LevelDef L, string text, Vector3 pos, Vector3 outward)
    {
        var rot = Quaternion.LookRotation(-outward, Vector3.up); // le texte se lit face à -Z local
        var parent = Child(L.props, "Plaques");
        float w = Mathf.Max(0.6f, text.Length * 0.13f + 0.1f);
        Cube(parent, "Plaque", pos - outward * 0.01f, new Vector3(w, 0.24f, 0.015f), rot, Mat("Plaque", new Color(0.12f, 0.16f, 0.22f), 0.5f), false, -1);
        var go = new GameObject("Texte_" + text);
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(pos + outward * 0.006f, rot);
        var tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tm.fontSize = 64;
        tm.characterSize = 0.022f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = new Color(0.92f, 0.92f, 0.88f);
        if (tm.font != null) go.GetComponent<MeshRenderer>().sharedMaterial = TextMaterial(tm.font);
    }

    /// Matériau de texte qui ne traverse pas les murs (le matériau de police par défaut s'affiche par-dessus tout).
    static Material TextMaterial(Font font)
    {
        var shader = Shader.Find("TheEchoesBelow/Text3D");
        if (shader == null) return font.material;
        string path = MatFolder + "/Texte_Plaque.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        m.shader = shader;
        m.mainTexture = font.material.mainTexture;
        EditorUtility.SetDirty(m);
        return m;
    }

    /// Plafonnier : dalle lumineuse + lumière chaude.
    static void CeilingLight(LevelDef L, Transform p, Vector3 ceiling, float range, float intensity = -1f)
    {
        var m = Mat("Plafonnier", new Color(1f, 0.95f, 0.85f), 0.3f);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", new Color(1f, 0.92f, 0.78f) * 1.6f);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        Cube(p, "Plafonnier", ceiling + Vector3.down * 0.02f, new Vector3(0.6f, 0.03f, 0.6f), Quaternion.identity, m, false, -1)
            .GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var l = PointLight(L, ceiling + Vector3.down * 0.35f, range, p);
        if (intensity > 0f) l.intensity = intensity;
    }

    /// Sol carrelé posé sur la dalle (cuisine, toilettes, salles de bains).
    static void TileFloor(LevelDef L, Rect r)
    {
        var m = Mat("Sol_Carrelage", new Color(0.8f, 0.8f, 0.78f));
        Cube(L.props, "Carrelage", new Vector3(r.center.x, L.y + 0.004f, r.center.y), new Vector3(r.width - 0.02f, 0.008f, r.height - 0.02f), Quaternion.identity, m, false, -1);
    }

    static GameObject MakeSeat(GameObject chair, float yaw, string label = "S'asseoir")
    {
        AddSeat(chair, 0.46f, yaw, Vector3.zero, label);
        return chair;
    }

    /// Table et chaises de part et d'autre (table orientée selon x).
    static void TableWithChairs(LevelDef L, Vector3 c, string[] table, float tw, float th, float td, string[] chairs, int perSide, Material mp)
    {
        Piece(L, table, KFurniture + "table", c, 0f, F(tw, th, td, 0f), mp);
        float depth = td * 0.5f + 0.38f;
        for (int k = 0; k < perSide; k++)
        {
            float x = c.x - tw * 0.5f + tw * (k + 0.5f) / perSide;
            MakeSeat(Piece(L, chairs, KFurniture + "chair", new Vector3(x, L.y, c.z - depth + Rand(-0.05f, 0.05f)), Rand(-8f, 8f), F(0.5f, 0.95f, 0.5f, 0f), mp), 0f);
            MakeSeat(Piece(L, chairs, KFurniture + "chair", new Vector3(x, L.y, c.z + depth + Rand(-0.05f, 0.05f)), 180f + Rand(-8f, 8f), F(0.5f, 0.95f, 0.5f, 0f), mp), 180f);
        }
    }

    /// Télé plate moderne fixée au mur ; renvoie l'écran (pour y afficher une caméra).
    static Renderer FlatTv(LevelDef L, Vector3 center, Vector3 outward, float width, Material screen)
    {
        var rot = Quaternion.LookRotation(-outward, Vector3.up);
        float h = width * 0.58f;
        var frame = Mat("TV_Cadre", new Color(0.03f, 0.03f, 0.035f), 0.75f);
        var parent = Child(L.props, "Televisions");
        Cube(parent, "TV_Support", center - outward * 0.06f, new Vector3(0.3f, 0.25f, 0.06f), rot, frame, false, -1);
        Cube(parent, "TV_Cadre", center - outward * 0.025f, new Vector3(width, h, 0.035f), rot, frame, false, -1);
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "TV_Ecran";
        quad.transform.SetParent(parent, false);
        quad.transform.SetPositionAndRotation(center - outward * 0.006f, rot);
        quad.transform.localScale = new Vector3(width * 0.965f, h * 0.94f, 1f);
        Object.DestroyImmediate(quad.GetComponent<Collider>());
        var r = quad.GetComponent<Renderer>();
        r.sharedMaterial = screen;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return r;
    }

    static Material TvOffMat()
    {
        var m = Mat("TV_Ecran_Eteint", new Color(0.01f, 0.012f, 0.015f), 0.95f);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", new Color(0.02f, 0.04f, 0.07f));
        return m;
    }

    /// Centre d'un meuble de largeur w posé contre un mur entre a et b, qui ne cache aucune fenêtre (centres "wins"),
    /// le plus près possible de "preferred".
    static float FreeSpot(float a, float b, float w, float[] wins, float preferred)
    {
        float best = preferred, bestD = float.MaxValue;
        for (float c = a + w * 0.5f; c <= b - w * 0.5f + 1e-3f; c += 0.05f)
        {
            bool ok = true;
            foreach (var x in wins) if (Mathf.Abs(c - x) < (w + WinW) * 0.5f + 0.1f) { ok = false; break; }
            if (ok && Mathf.Abs(c - preferred) < bestD) { bestD = Mathf.Abs(c - preferred); best = c; }
        }
        return best;
    }

    /// Centres d'autant de meubles de largeur w que possible contre un mur [a, b], entre les fenêtres.
    static List<float> WallSpans(float a, float b, float w, float[] wins)
    {
        var res = new List<float>();
        var cuts = new List<float> { a };
        var sorted = new List<float>(wins); sorted.Sort();
        foreach (var x in sorted) { cuts.Add(x - WinW * 0.5f - 0.1f); cuts.Add(x + WinW * 0.5f + 0.1f); }
        cuts.Add(b);
        for (int k = 0; k + 1 < cuts.Count; k += 2)
        {
            float u = Mathf.Max(a, cuts[k]), v = Mathf.Min(b, cuts[k + 1]);
            int n = Mathf.FloorToInt((v - u + 0.05f) / (w + 0.1f));
            for (int i = 0; i < n; i++) res.Add(u + (v - u) * (i + 0.5f) / n);
        }
        return res;
    }

    // ───────────────────────── Salles communes ─────────────────────────

    static void FurnishCafeteria(LevelDef L, Rect r)
    {
        var mp = Mat("Meuble_" + L.number, Color.Lerp(L.wall, L.floor, 0.5f) * 0.9f);
        var mw = Mat("Mur_" + L.number, L.wall);
        float y = L.y, y1 = L.y + L.h;

        // Cuisine (x < KitchenX) séparée par un mur avec passe-plats et porte
        float hatchC = 18.75f, hatchW = 9.5f;
        WallAlongZ(L.root, "Mur_Cuisine", KitchenX, r.yMin, r.yMax, y, y1, mw,
            new Hole(hatchC, hatchW, y + 0.95f, y + 2.1f), new Hole(25.6f, 1.0f, y, y + DoorH - 0.1f));
        var kdoor = BuildDoor(DoorsRoot(L), "Porte_Cuisine", new Vector3(KitchenX, y + 0.01f, 25.6f - 0.48f), -90f, 0.96f, DoorH - 0.12f, Mat("Porte_Studio", new Color(0.75f, 0.6f, 0.45f)), "la porte de la cuisine");
        kdoor.displayName = "la cuisine";
        var inox = Mat("Inox", new Color(0.72f, 0.73f, 0.75f), 0.75f);
        inox.SetFloat("_Metallic", 0.85f);
        TileFloor(L, Rect.MinMaxRect(r.xMin, r.yMin, KitchenX, r.yMax));
        // Comptoir de service dans le passe-plats, avec vitre anti-postillons
        Cube(L.props, "Comptoir_Service", new Vector3(KitchenX, y + 0.47f, hatchC), new Vector3(0.75f, 0.94f, hatchW), Quaternion.identity, inox, true, _layerFurniture);
        Cube(L.props, "Vitre_Comptoir", new Vector3(KitchenX + 0.25f, y + 1.25f, hatchC), new Vector3(0.02f, 0.5f, hatchW), Quaternion.Euler(0f, 0f, -12f), GlassMat(), false, -1);
        for (float z = hatchC - hatchW * 0.5f + 0.6f; z < hatchC + hatchW * 0.5f - 0.5f; z += 1.1f) // bacs gastronormes
            Cube(L.props, "Bac_Service", new Vector3(KitchenX - 0.05f, y + 0.955f, z), new Vector3(0.32f, 0.04f, 0.5f), Quaternion.identity, inox, false, -1);

        // Cuisine : rangée d'appareils contre le mur ouest, îlot central inox, étagères
        string[] row = { "Kitchen/Refrigerator02", "Kitchen/Refrigerator02", "Kitchen/CabinetC_Sink", "Kitchen/CabinetC01", "Kitchen/GasStove01",
                         "Kitchen/KitchenOven01", "Kitchen/GasStove01", "Kitchen/CabinetC02", "Kitchen/KitchenOven01", "Kitchen/GasStove01",
                         "Kitchen/CabinetC_Sink", "Kitchen/CabinetC01", "Kitchen/CabinetC02", "Kitchen/Refrigerator02" };
        float z0 = r.yMin + 0.5f;
        foreach (var piece in row)
        {
            bool fridge = piece.Contains("Refrigerator");
            var fit = fridge ? F(0.8f, 2.0f, 0.75f, 90f) : F(0.65f, 0.92f, 0.65f, 90f);
            var go = Piece(L, new[] { piece }, KFurniture + "kitchenCabinet", new Vector3(r.xMin + 0.42f, y, z0 + 0.35f), 90f, fit, mp);
            float wz = go != null && TryBounds(go, out var b) ? Mathf.Max(0.6f, b.size.z) : 0.7f;
            if (piece.Contains("GasStove"))
                Piece(L, MP("Kitchen", "KitchenExhaust01"), null, new Vector3(r.xMin + 0.35f, y + 1.75f, z0 + wz * 0.5f), 90f, F(0.8f, 0.7f, 0.55f, 90f), mp, false);
            if (piece == "Kitchen/CabinetC02")
                Piece(L, MP("Kitchen", "MicrowaveOven02"), null, new Vector3(r.xMin + 0.35f, y + 0.93f, z0 + wz * 0.5f), 90f, F(0.5f, 0.32f, 0.4f, 90f), mp, false);
            z0 += wz + 0.02f;
            if (z0 > r.yMax - 0.9f) break;
        }
        Cube(L.props, "Ilot_Inox", new Vector3(-9.6f, y + 0.46f, 19f), new Vector3(1.0f, 0.92f, 6f), Quaternion.identity, inox, true, _layerFurniture);
        Cube(L.props, "Ilot_Etagere", new Vector3(-9.6f, y + 0.15f, 19f), new Vector3(0.9f, 0.03f, 5.8f), Quaternion.identity, inox, false, -1);
        foreach (var z in new[] { 16.8f, 18.4f, 20.2f, 21.6f })
            CaveProp(L, "pot_enamel_01", new Vector3(-9.6f + Rand(-0.25f, 0.25f), y + 0.92f, z), Rand(0f, 360f), new Vector3(0.3f, 0.2f, 0.3f));
        CaveProp(L, "steel_frame_shelves_02", new Vector3(FreeSpot(r.xMin + 0.2f, KitchenX - 0.2f, 1.6f, WingNorthWinX, -8.2f), y, r.yMax - 0.45f), 180f, F(1.6f, 2.0f, 0.5f, 180f), true);
        CaveProp(L, "steel_frame_shelves_02", new Vector3(FreeSpot(r.xMin + 0.2f, KitchenX - 0.2f, 1.6f, WingSouthWinX, -8.2f), y, r.yMin + 0.45f), 0f, F(1.6f, 2.0f, 0.5f, 0f), true);
        CeilingLight(L, L.props, new Vector3(-9.5f, y1, 15f), 7f);
        CeilingLight(L, L.props, new Vector3(-9.5f, y1, 22.5f), 7f);

        // Salle : 2 × 5 tables de 4 places
        foreach (var tz in new[] { 13.3f, 16.3f, 19.3f, 22.3f, 25.3f })
            foreach (var tx in new[] { -4.6f, -1.6f })
                TableWithChairs(L, new Vector3(tx, y, tz), MP("Tables", "Table10"), 1.6f, 0.78f, 0.8f, MP("Chairs", "Chair29", "Chair27"), 2, mp);
        CaveProp(L, "standing_chalkboard_01", new Vector3(-6.3f, y, r.yMin + 0.8f), 30f, F(0.7f, 1.4f, 0.6f, 30f));
        CaveProp(L, "CoffeeCart_01", new Vector3(-0.3f, y, r.yMin + 0.5f), 0f, F(1.1f, 1.1f, 0.6f, 0f), true);
        Model(L, KFurniture + "trashcan", new Vector3(-6.4f, y, 26.4f), 0f, new Vector3(0.5f, 0.8f, 0.5f), mp);
        CaveProp(L, "potted_plant_01", new Vector3(-0.2f, y, r.yMax - 0.5f), 0f, new Vector3(0.8f, 1.4f, 0.8f));
        CaveProp(L, "wall_clock", new Vector3(-3f, y + 2.35f, r.yMin + 0.12f), 0f, new Vector3(0.35f, 0.35f, 0.12f));
        foreach (var p in new[] { new Vector3(-4.5f, 0f, 15f), new Vector3(-2f, 0f, 15f), new Vector3(-4.5f, 0f, 23f), new Vector3(-2f, 0f, 23f) })
            CeilingLight(L, L.props, new Vector3(p.x, y1, p.z), 8f);
        Marker(L, "Cafeteria", new Vector3(-3f, y, 19f), Color.yellow);
    }

    static void FurnishStudyRoom(LevelDef L, Rect r)
    {
        var mp = Mat("Meuble_" + L.number, Color.Lerp(L.wall, L.floor, 0.5f) * 0.9f);
        float y = L.y, y1 = L.y + L.h;
        foreach (var tz in new[] { 13.2f, 15.7f, 18.2f, 20.7f, 23.2f })
            foreach (var tx in new[] { -8.6f, -5.6f, -2.6f })
            {
                Piece(L, MP("Tables", "Table43", "Table44"), KFurniture + "desk", new Vector3(tx, y, tz), 0f, F(1.4f, 0.78f, 0.7f, 0f), mp);
                MakeSeat(Piece(L, MP("Chairs", "Chair04", "Chair31"), KFurniture + "chairDesk", new Vector3(tx, y, tz - 0.62f), 0f, F(0.58f, 1.05f, 0.58f, 0f), mp), 0f);
                double roll = _rng.NextDouble();
                if (roll < 0.3) CaveProp(L, "classic_laptop", new Vector3(tx + Rand(-0.3f, 0.3f), y + 0.78f, tz + 0.05f), Rand(160f, 200f), F(0.38f, 0.3f, 0.3f, 180f));
                else if (roll < 0.55) CaveProp(L, "book_encyclopedia_set_01", new Vector3(tx + Rand(-0.4f, 0.4f), y + 0.78f, tz + 0.12f), Rand(0f, 360f), new Vector3(0.4f, 0.3f, 0.4f));
            }
        CaveProp(L, "standing_chalkboard_01", new Vector3(FreeSpot(r.xMin + 0.5f, r.xMax - 0.5f, 1.9f, WingNorthWinX, -5.6f), y, r.yMax - 0.7f), 180f, F(1.8f, 2.0f, 0.8f, 180f));
        foreach (var z in WallSpans(r.yMin + 0.2f, r.yMax - 0.2f, 1.9f, WingWestWinZ)) // bibliothèques contre le mur ouest, entre les fenêtres
            Piece(L, MP("Closets", "Closet04", "Closet09"), KFurniture + "bookcaseClosedWide", new Vector3(r.xMin + 0.33f, y, z), 90f, F(1.9f, 2.2f, 0.55f, 90f), mp);
        CaveProp(L, "wall_clock", new Vector3(-3f, y + 2.4f, r.yMax - 0.1f), 180f, new Vector3(0.35f, 0.35f, 0.12f));
        CaveProp(L, "potted_plant_02", new Vector3(-0.2f, y, r.yMax - 0.5f), 0f, new Vector3(0.7f, 1.3f, 0.7f));
        CaveProp(L, "potted_plant_01", new Vector3(-0.2f, y, r.yMin + 0.5f), 0f, new Vector3(0.8f, 1.4f, 0.8f));
        foreach (var p in new[] { new Vector3(-8f, 0, 15f), new Vector3(-3.5f, 0, 15f), new Vector3(-8f, 0, 22.5f), new Vector3(-3.5f, 0, 22.5f) })
            CeilingLight(L, L.props, new Vector3(p.x, y1, p.z), 9f);
        Marker(L, "Salle_Etude", new Vector3(-5f, y, 19f), Color.yellow);
    }

    static void FurnishGameRoom(LevelDef L, Rect r)
    {
        var mp = Mat("Meuble_" + L.number, Color.Lerp(L.wall, L.floor, 0.5f) * 0.9f);
        float y = L.y, y1 = L.y + L.h;
        // Coin télé : grande télé plate au mur ouest, meuble bas + console, canapés en L, table basse
        float tvz = FreeSpot(r.yMin + 1f, r.yMax - 3f, 1.9f, WingWestWinZ, 15f);   // entre deux fenêtres
        FlatTv(L, new Vector3(r.xMin + 0.1f + 0.06f, y + 1.5f, tvz), Vector3.right, 1.6f, TvOffMat());
        var stand = Piece(L, MP("Drawers", "Drawer31", "Drawer39"), KFurniture + "tableCoffee", new Vector3(r.xMin + 0.35f, y, tvz), 90f, F(1.8f, 0.6f, 0.5f, 90f), mp);
        float top = stand != null && TryBounds(stand, out var sb) ? sb.max.y : y + 0.5f;
        CaveProp(L, "gaming_console", new Vector3(r.xMin + 0.4f, top, tvz + 0.4f), 90f, F(0.42f, 0.12f, 0.35f, 90f));
        MakeSeat(Piece(L, MP("Sofas", "Sofa34", "Sofa36"), KFurniture + "loungeSofa", new Vector3(r.xMin + 4.2f, y, tvz), -90f, F(2.3f, 0.9f, 1.0f, -90f), mp), -90f, "S'asseoir sur le canapé");
        MakeSeat(Piece(L, MP("Sofas", "Sofa12", "Sofa16"), KFurniture + "loungeSofa", new Vector3(r.xMin + 2.3f, y, tvz - 2.2f), 0f, F(2.0f, 0.9f, 0.95f, 0f), mp), 0f, "S'asseoir sur le canapé");
        Piece(L, MP("Tables", "Table33"), KFurniture + "tableCoffee", new Vector3(r.xMin + 2.4f, y, tvz), 90f, F(1.2f, 0.45f, 0.7f, 90f), mp);
        CaveProp(L, "gamepad", new Vector3(r.xMin + 2.3f, y + 0.46f, tvz + 0.1f), Rand(0f, 360f), new Vector3(0.18f, 0.07f, 0.13f));
        foreach (var p in new[] { new Vector3(-9.8f, 0, 19.4f), new Vector3(-8.3f, 0, 19.8f), new Vector3(-6.9f, 0, 19.2f) })
            MakeSeat(Piece(L, MP("Chairs", "Chair48"), null, new Vector3(p.x, y, p.z), Rand(160f, 220f), new Vector3(0.85f, 0.6f, 0.85f), mp), 180f, "S'affaler sur le pouf");
        // Tables de jeux (échecs, cartes) et table haute
        TableWithChairs(L, new Vector3(-3.6f, y, 13.8f), MP("Tables", "Table41"), 1.0f, 0.75f, 1.0f, MP("Chairs", "Chair29"), 1, mp);
        CaveProp(L, "chess_set", new Vector3(-3.6f, y + 0.76f, 13.8f), Rand(0f, 90f), new Vector3(0.45f, 0.14f, 0.45f));
        TableWithChairs(L, new Vector3(-3.6f, y, 17.5f), MP("Tables", "Table41"), 1.0f, 0.75f, 1.0f, MP("Chairs", "Chair29"), 1, mp);
        TableWithChairs(L, new Vector3(-3.6f, y, 22.6f), MP("Tables", "Table27"), 1.0f, 1.0f, 1.0f, MP("Chairs", "Chair24"), 1, mp);
        // Fléchettes, musique, second canapé
        CaveProp(L, "dartboard", new Vector3(-6f, y + 1.6f, r.yMax - 0.12f), 180f, new Vector3(0.55f, 0.55f, 0.15f));
        CaveProp(L, "boombox", new Vector3(-9.5f, y, r.yMax - 0.5f), 160f, F(0.7f, 0.4f, 0.3f, 160f));
        MakeSeat(Piece(L, MP("Sofas", "Sofa34", "Sofa36"), KFurniture + "loungeSofa", new Vector3(r.xMin + 0.55f, y, 23.5f), 90f, F(2.3f, 0.9f, 1.0f, 90f), mp), 90f, "S'asseoir sur le canapé");
        CaveProp(L, "potted_plant_01", new Vector3(-0.3f, y, r.yMin + 0.5f), 0f, new Vector3(0.8f, 1.4f, 0.8f));
        CaveProp(L, "potted_plant_02", new Vector3(r.xMin + 0.6f, y, r.yMax - 0.6f), 0f, new Vector3(0.7f, 1.3f, 0.7f));
        foreach (var p in new[] { new Vector3(-8f, 0, 15f), new Vector3(-3.5f, 0, 15f), new Vector3(-8f, 0, 22.5f), new Vector3(-3.5f, 0, 22.5f) })
            CeilingLight(L, L.props, new Vector3(p.x, y1, p.z), 9f);
        Marker(L, "Espace_Detente", new Vector3(-5f, y, 19f), Color.yellow);
    }

    /// Toilettes garçons (cabines + urinoirs) ou filles (cabines). Porte sur le couloir (x = r.xMin).
    static void FurnishToilets(LevelDef L, Rect r, bool boys)
    {
        var mp = Mat("Meuble_" + L.number, Color.Lerp(L.wall, L.floor, 0.5f) * 0.9f);
        var part = Mat("Cloison_WC", new Color(0.55f, 0.6f, 0.62f), 0.4f);
        var ceramic = Mat("Ceramique", new Color(0.95f, 0.95f, 0.94f), 0.85f);
        float y = L.y;
        TileFloor(L, r);
        // Cabines contre le mur du fond (côté opposé à la porte d'entrée de la pièce, le long de z)
        bool farIsMax = boys;                          // garçons : cabines au sud ; filles : au nord
        float wallZ = farIsMax ? r.yMin : r.yMax, inz = farIsMax ? 1f : -1f;
        int n = boys ? 3 : 5;
        const float cw = 1.5f, cd = 1.5f, ph = 2.0f, lift = 0.15f;
        float xs = r.xMax - n * cw;
        float zf = wallZ + inz * cd;                   // façade des cabines
        for (int k = 0; k <= n; k++)
            Cube(L.props, "Cloison_WC", new Vector3(xs + k * cw, y + lift + ph * 0.5f, wallZ + inz * cd * 0.5f), new Vector3(0.04f, ph, cd), Quaternion.identity, part, true, _layerWalls);
        for (int k = 0; k < n; k++)
        {
            float cx = xs + (k + 0.5f) * cw;
            Cube(L.props, "Facade_WC", new Vector3(cx - 0.55f, y + lift + ph * 0.5f, zf), new Vector3(0.4f, ph, 0.04f), Quaternion.identity, part, true, _layerWalls);
            Cube(L.props, "Facade_WC", new Vector3(cx + 0.55f, y + lift + ph * 0.5f, zf), new Vector3(0.4f, ph, 0.04f), Quaternion.identity, part, true, _layerWalls);
            var door = BuildDoor(DoorsRoot(L), "Porte_WC", new Vector3(cx - 0.35f, y + lift, zf), 0f, 0.7f, ph - 0.05f, part, "la cabine");
            door.displayName = "la cabine";
            if (_rng.NextDouble() < 0.35) door.startAngle = -inz * Rand(20f, 70f);
            float faceYaw = farIsMax ? 0f : 180f;      // dos au mur du fond
            Piece(L, MP("Bathroom", "Toilet01", "Toilet05"), KFurniture + "toilet", new Vector3(cx, y, wallZ + inz * 0.4f), faceYaw, F(0.42f, 0.8f, 0.68f, faceYaw), mp);
            Piece(L, MP("Bathroom", "TissueHanger01"), null, new Vector3(cx + 0.6f, y + 0.7f, wallZ + inz * 0.75f), -90f, F(0.15f, 0.12f, 0.12f, -90f), mp, false);
        }
        // Lavabos et miroirs le long du mur opposé aux cabines, près de l'entrée
        var mirror = Mat("Miroir", new Color(0.7f, 0.75f, 0.78f), 1f);
        mirror.SetFloat("_Metallic", 1f);
        float sinkWall = farIsMax ? r.yMax : r.yMin, sinkIn = -inz;
        float sinkYaw = farIsMax ? 180f : 0f;
        for (int k = 0; k < 3; k++)
        {
            float x = r.xMin + 1.2f + k * 1.0f;
            Piece(L, MP("Bathroom", "WashBasin07"), KFurniture + "bathroomSink", new Vector3(x, y, sinkWall + sinkIn * 0.35f), sinkYaw, F(0.6f, 0.95f, 0.5f, sinkYaw), mp);
            Cube(L.props, "Miroir", new Vector3(x, y + 1.55f, sinkWall + sinkIn * 0.12f), new Vector3(0.6f, 0.8f, 0.02f), Quaternion.identity, mirror, false, -1);
        }
        // Urinoirs (garçons), entre les lavabos et les cabines
        if (boys)
            for (int k = 0; k < 3; k++)
            {
                float x = r.xMin + 5.2f + k * 0.9f;
                Cube(L.props, "Urinoir", new Vector3(x, y + 0.75f, sinkWall + sinkIn * 0.17f), new Vector3(0.38f, 0.6f, 0.3f), Quaternion.identity, ceramic, true, _layerFurniture);
                if (k < 2) Cube(L.props, "Separation_Urinoir", new Vector3(x + 0.45f, y + 1.0f, sinkWall + sinkIn * 0.3f), new Vector3(0.03f, 0.9f, 0.5f), Quaternion.identity, part, false, -1);
            }
        Model(L, KFurniture + "trashcan", new Vector3(r.xMin + 0.45f, y, sinkWall + sinkIn * 0.4f), 0f, new Vector3(0.4f, 0.6f, 0.4f), mp);
        CeilingLight(L, L.props, new Vector3(r.center.x - 2f, L.y + L.h, r.center.y), 6f);
        CeilingLight(L, L.props, new Vector3(r.center.x + 3f, L.y + L.h, r.center.y), 6f);
    }

    // ───────────────────────── Personnel ─────────────────────────

    static void FurnishStaffRestRoom(LevelDef L, Rect r)
    {
        var mp = Mat("Meuble_" + L.number, Color.Lerp(L.wall, L.floor, 0.5f) * 0.9f);
        float y = L.y, y1 = L.y + L.h;
        // Kitchenette contre le mur est
        float z = r.yMin + 0.5f;
        foreach (var piece in new[] { "Kitchen/Refrigerator02", "Kitchen/CabinetC_Sink", "Kitchen/CabinetC01", "Kitchen/CabinetC02" })
        {
            bool fridge = piece.Contains("Refrigerator");
            var go = Piece(L, new[] { piece }, KFurniture + "kitchenCabinet", new Vector3(r.xMax - 0.42f, y, z + 0.35f), -90f,
                fridge ? F(0.8f, 2.0f, 0.75f, -90f) : F(0.65f, 0.92f, 0.65f, -90f), mp);
            float wz = go != null && TryBounds(go, out var b) ? Mathf.Max(0.6f, b.size.z) : 0.7f;
            if (piece == "Kitchen/CabinetC02")
                Piece(L, MP("Kitchen", "MicrowaveOven02"), null, new Vector3(r.xMax - 0.35f, y + 0.93f, z + wz * 0.5f), -90f, F(0.5f, 0.32f, 0.4f, -90f), mp, false);
            z += wz + 0.02f;
        }
        CaveProp(L, "CoffeeCart_01", new Vector3(r.xMax - 0.5f, y, z + 0.8f), -90f, F(1.1f, 1.1f, 0.6f, -90f), true);
        // Coin télé : télé plate au mur nord, canapé, fauteuil, table basse
        FlatTv(L, new Vector3(FreeSpot(r.xMin + 1f, r.xMax - 2f, 1.5f, WingNorthWinX, 8f), y + 1.5f, r.yMax - 0.1f - 0.06f), Vector3.back, 1.4f, TvOffMat());
        MakeSeat(Piece(L, MP("Sofas", "Sofa36", "Sofa34"), KFurniture + "loungeSofa", new Vector3(7.5f, y, r.yMax - 3.6f), 0f, F(2.2f, 0.9f, 1.0f, 0f), mp), 0f, "S'asseoir sur le canapé");
        MakeSeat(Piece(L, MP("Chairs", "Chair16"), KFurniture + "loungeChair", new Vector3(5.2f, y, r.yMax - 2.2f), 60f, F(0.95f, 0.9f, 0.95f, 60f), mp), 60f, "S'asseoir dans le fauteuil");
        Piece(L, MP("Tables", "Table33"), KFurniture + "tableCoffee", new Vector3(7.5f, y, r.yMax - 2.1f), 0f, F(1.2f, 0.45f, 0.7f, 0f), mp);
        // Table du personnel, casiers
        TableWithChairs(L, new Vector3(11.5f, y, r.center.y - 0.5f), MP("Tables", "Table27"), 1.1f, 0.78f, 1.1f, MP("Chairs", "Chair29"), 1, mp);
        CaveProp(L, "clipboard", new Vector3(11.4f, y + 0.79f, r.center.y - 0.4f), Rand(0f, 360f), new Vector3(0.25f, 0.04f, 0.33f));
        LockerBank(L.props, new Vector3(7.5f, y, r.yMin + Wall * 0.5f), Vector3.forward, Vector3.right, 5, Mat("Casier", new Color(0.45f, 0.5f, 0.52f)), Mat("Casier_Interieur", new Color(0.03f, 0.03f, 0.03f)));
        CaveProp(L, "boombox", new Vector3(r.xMax - 0.4f, y + 0.93f, r.yMin + 1.8f), -90f, F(0.5f, 0.3f, 0.22f, -90f));
        CaveProp(L, "wall_clock", new Vector3(11.5f, y + 2.35f, r.yMax - 0.1f), 180f, new Vector3(0.35f, 0.35f, 0.12f));
        CaveProp(L, "potted_plant_02", new Vector3(r.xMin + 0.5f, y, r.yMax - 0.5f), 0f, new Vector3(0.7f, 1.3f, 0.7f));
        CeilingLight(L, L.props, new Vector3(7.5f, y1, r.center.y + 1.5f), 8f);
        CeilingLight(L, L.props, new Vector3(12.5f, y1, r.center.y - 1f), 8f);
        Marker(L, "Salle_Repos_Personnel", new Vector3(r.center.x, y, r.center.y), Color.yellow);
    }

    static void FurnishStaffStudio(LevelDef L, Rect r, string owner)
    {
        var mp = Mat("Meuble_" + L.number, Color.Lerp(L.wall, L.floor, 0.5f) * 0.9f);
        float y = L.y, y1 = L.y + L.h;
        // Salle de bains dans le coin sud-ouest, porte vers le nord
        var bath = Rect.MinMaxRect(r.xMin, r.yMin, r.xMin + 2.8f, r.yMin + 1.75f);
        SmallBathroom(L, bath, Vector3.back, -0.55f, false, true, false, true);
        // Lit double tête contre le mur est, chevets, armoire, bureau, fauteuil
        float bedZ = FreeSpot(r.yMin + 1.9f, r.yMax - 0.1f, 1.7f, WingEastWinZ, r.center.y);
        var bed = Piece(L, MegaBeds, KFurniture + "bedDouble", new Vector3(r.xMax - 1.15f, y, bedZ), -90f, F(1.6f, 1.1f, 2.15f, -90f), mp);
        if (bed != null) AddSeat(bed, 0.5f, 90f, Vector3.zero, "S'asseoir sur le lit");
        Piece(L, MegaNightstands, KFurniture + "sideTable", new Vector3(r.xMax - 0.3f, y, r.yMax - 0.45f), -90f, F(0.5f, 0.6f, 0.45f, -90f), mp);
                // Mur nord : fenêtres seulement pour le studio du fond (contre la façade)
        float[] northWins = r.yMax > WZ1 - 0.5f ? WingNorthWinX : new float[0];
        float deskX = FreeSpot(r.xMin + 2.9f, r.xMax - 2.6f, 1.5f, northWins, r.xMin + 3.4f);
        float closetX = FreeSpot(r.xMin + 2.9f, r.xMax - 2.6f, 1.6f, northWins, r.center.x + 1.2f);
        if (Mathf.Abs(closetX - deskX) < 1.6f) closetX = FreeSpot(deskX + 1.6f, r.xMax - 2.6f, 1.6f, northWins, deskX + 1.7f);
        Piece(L, MegaClosets, KFurniture + "bookcaseClosedDoors", new Vector3(closetX, y, r.yMax - 0.36f), 180f, F(1.5f, 2.1f, 0.62f, 180f), mp);
        var desk = Piece(L, MegaDesks, KFurniture + "desk", new Vector3(deskX, y, r.yMax - 0.45f), 180f, F(1.4f, 0.78f, 0.7f, 180f), mp);
        MakeSeat(Piece(L, MP("Chairs", "Chair31", "Chair33"), KFurniture + "chairDesk", new Vector3(deskX, y, r.yMax - 1.15f), 0f, F(0.62f, 1.1f, 0.62f, 0f), mp), 0f);
        float top = desk != null && TryBounds(desk, out var db) ? db.max.y : y + 0.78f;
        CaveProp(L, "classic_laptop", new Vector3(deskX - 0.1f, top, r.yMax - 0.5f), 180f, F(0.38f, 0.3f, 0.3f, 180f));
        CaveProp(L, "vintage_radio_transceiver", new Vector3(deskX + 0.45f, top, r.yMax - 0.4f), 170f, F(0.4f, 0.24f, 0.28f, 170f));
        MakeSeat(Piece(L, MegaArmchairs, KFurniture + "loungeChair", new Vector3(r.center.x - 0.5f, y, r.yMin + 1.0f), 20f, F(0.9f, 0.95f, 0.9f, 20f), mp), 20f, "S'asseoir dans le fauteuil");
        CaveProp(L, "wall_clock", new Vector3(r.center.x + 2f, y + 2.3f, r.yMin + 0.12f), 0f, new Vector3(0.32f, 0.32f, 0.1f));
        CaveProp(L, "potted_plant_02", new Vector3(r.xMax - 0.5f, y, r.yMin + 0.5f), 0f, new Vector3(0.6f, 1.2f, 0.6f));
        CeilingLight(L, L.props, new Vector3(r.center.x + 1.5f, y1, r.center.y), 8f);
        Marker(L, "Studio_Personnel (" + owner + ")", new Vector3(r.center.x, y, r.center.y), Color.cyan);
    }

    // ───────────────────────── Salle de bains (studios) ─────────────────────────

    /// Salle d'eau en longueur : la porte est sur un grand côté ; en face, le long du mur du fond, on trouve
    /// à un bout la douche vitrée (toute la profondeur), au milieu le WC, à l'autre bout le lavabo avec son miroir.
    /// b : rectangle sur les axes des murs ; inward : du mur de la porte vers le fond ;
    /// doorOffset : décalage de la porte le long de ce mur (vers -right = côté lavabo) ; build* : murs à construire.
    static void SmallBathroom(LevelDef L, Rect b, Vector3 inward, float doorOffset, bool buildXMin, bool buildXMax, bool buildZMin, bool buildZMax)
    {
        var mw = Mat("Mur_" + L.number, L.wall);
        var mp = Mat("Meuble_" + L.number, Color.Lerp(L.wall, L.floor, 0.5f) * 0.9f);
        float y = L.y, y1 = L.y + L.h;
        var c = new Vector3(b.center.x, y, b.center.y);
        var right = Vector3.Cross(Vector3.up, inward);
        bool fx = Mathf.Abs(inward.x) > 0.5f;
        float halfF = (fx ? b.width : b.height) * 0.5f - Wall * 0.5f;   // demi-profondeur utile (porte → fond)
        float halfR = (fx ? b.height : b.width) * 0.5f - Wall * 0.5f;   // demi-longueur utile
        const float showerLen = 0.85f, wcLen = 0.68f;
        doorOffset = ((-halfR + wcLen) + (halfR - showerLen)) * 0.5f;      // milieu de l'espace libre (WC d'un côté, douche de l'autre)
        var doorMid = c - inward * (halfF + Wall * 0.5f) + right * doorOffset;
        float doorAlong = fx ? doorMid.z : doorMid.x;

        Hole[] DoorHole(bool isDoorWall) => isDoorWall ? new[] { new Hole(doorAlong, 0.76f, y, y + 2.05f) } : new Hole[0];
        bool dXMin = fx && inward.x > 0, dXMax = fx && inward.x < 0, dZMin = !fx && inward.z > 0, dZMax = !fx && inward.z < 0;
        if (buildXMin) WallAlongZ(L.root, "Cloison_SdB", b.xMin, b.yMin, b.yMax, y, y1, mw, DoorHole(dXMin));
        if (buildXMax) WallAlongZ(L.root, "Cloison_SdB", b.xMax, b.yMin, b.yMax, y, y1, mw, DoorHole(dXMax));
        if (buildZMin) WallAlongX(L.root, "Cloison_SdB", b.yMin, b.xMin, b.xMax, y, y1, mw, DoorHole(dZMin));
        if (buildZMax) WallAlongX(L.root, "Cloison_SdB", b.yMax, b.xMin, b.xMax, y, y1, mw, DoorHole(dZMax));
        // La porte s'ouvre vers l'intérieur, en pivotant côté douche (elle se rabat contre le mur de la porte)
        float doorYaw = Mathf.Atan2(right.z, -right.x) * Mathf.Rad2Deg;   // battant le long de -right
        var door = BuildDoor(DoorsRoot(L), "Porte_Salle_De_Bains", doorMid + right * 0.36f + Vector3.up * 0.01f, doorYaw, 0.72f, 2.0f,
            Mat("Porte_Studio", new Color(0.75f, 0.6f, 0.45f)), "la salle de bains");
        door.displayName = "la salle de bains";
        // La porte s'ouvre toujours vers l'extérieur : à l'intérieur, son battant bloquerait le passage
        door.openTowards = Vector3.Dot(door.transform.rotation * Vector3.forward, inward) > 0f ? 1 : -1;

        TileFloor(L, Rect.MinMaxRect(b.xMin + 0.1f, b.yMin + 0.1f, b.xMax - 0.1f, b.yMax - 0.1f));
        var ceramic = Mat("Ceramique", new Color(0.95f, 0.95f, 0.94f), 0.85f);
        var glass = GlassMat();
        Vector3 Along(float r, float f) => c + right * r + inward * f;
        float faceBack = Mathf.Atan2(-inward.x, -inward.z) * Mathf.Rad2Deg; // dos au mur du fond, face à la porte

        // Douche à l'italienne au bout +right : receveur sur toute la profondeur, colonne au fond, paroi vitrée à moitié
        float sr = halfR - showerLen * 0.5f;
        var tray = Along(sr, 0f);
        Cube(L.props, "Receveur_Douche", tray + Vector3.up * 0.025f, new Vector3(showerLen, 0.05f, halfF * 2f - 0.02f), Quaternion.LookRotation(inward), ceramic, false, -1);
        // Pomme de douche fixée en hauteur (le modèle n'est que la tête et son bras) + barre verticale jusqu'au mitigeur
        var head = Piece(L, MP("Bathroom", "ShowerSystem01"), null, Along(sr, halfF - 0.08f), faceBack, new Vector3(0.3f, 0.5f, 0.45f), mp, false);
        if (head != null && TryBounds(head, out var hb)) head.transform.position += Vector3.up * (y + 2.15f - hb.max.y);
        var chrome = Mat("Chrome", new Color(0.8f, 0.8f, 0.82f), 0.9f);
        chrome.SetFloat("_Metallic", 1f);
        Cylinder(L.props, "Barre_Douche", Along(sr, halfF - 0.04f) + Vector3.up * 1.45f, 0.015f, 1.1f, Quaternion.identity, chrome, false);
        Cube(L.props, "Mitigeur_Douche", Along(sr, halfF - 0.05f) + Vector3.up * 1.05f, new Vector3(0.18f, 0.07f, 0.06f), Quaternion.LookRotation(inward), chrome, false, -1);
        Cube(L.props, "Paroi_Douche", Along(halfR - showerLen, halfF * 0.45f) + Vector3.up * 1.0f, new Vector3(0.02f, 1.9f, halfF * 1.1f),
            Quaternion.LookRotation(inward), glass, false, -1);

        // WC au bout -right, de profil (dos au petit mur), pour laisser le passage devant la porte
        float wcYaw = Mathf.Atan2(right.x, right.z) * Mathf.Rad2Deg;
        Piece(L, MP("Bathroom", "Toilet05", "Toilet01"), KFurniture + "toilet", Along(-halfR + 0.34f, halfF * 0.3f), wcYaw, F(0.42f, 0.78f, 0.62f, wcYaw), mp);
        Piece(L, MP("Bathroom", "TissueHanger01"), null, Along(-halfR + 0.08f, -halfF * 0.25f) + Vector3.up * 0.7f, wcYaw, new Vector3(0.12f, 0.12f, 0.15f), mp, false);

        // Petit lavabo contre le mur du fond, en face de la porte, avec miroir
        float lr = doorOffset;
        Piece(L, MP("Bathroom", "WashBasin06", "WashBasin07"), KFurniture + "bathroomSink", Along(lr, halfF - 0.18f), faceBack, F(0.42f, 0.85f, 0.32f, faceBack), mp);
        var mirror = Mat("Miroir", new Color(0.7f, 0.75f, 0.78f), 1f);
        Cube(L.props, "Miroir", Along(lr, halfF - 0.015f) + Vector3.up * 1.5f, new Vector3(0.42f, 0.6f, 0.02f), Quaternion.LookRotation(inward), mirror, false, -1);
        var l = PointLight(L, c + Vector3.up * (L.h - 0.3f), 3f, L.props);
        l.intensity = 0.7f;
        if (L.number == 1 && L.markers.Find("◆ Vue_Salle_De_Bains") == null) // vue de contrôle (une seule)
            CaveView(L, "Vue_Salle_De_Bains", Along(-halfR + 0.15f, -halfF + 0.1f), Along(halfR, halfF) + Vector3.down * 0.4f);
    }

    // ───────────────────────── Entrée : accueil et poste de surveillance ─────────────────────────

    /// Accueil (studio sud n°10 de l'étage 1) : comptoir entouré d'une vitre, surveillante assise derrière.
    static void FurnishReception(LevelDef L, Rect r)
    {
        var mp = Mat("Meuble_" + L.number, Color.Lerp(L.wall, L.floor, 0.5f) * 0.9f);
        var wood = Mat("Comptoir_Bois", new Color(0.45f, 0.33f, 0.24f), 0.35f);
        var top = Mat("Comptoir_Plateau", new Color(0.85f, 0.84f, 0.8f), 0.6f);
        var glass = GlassMat();
        float y = L.y, y1 = L.y + L.h;
        float cz = CorrS - 0.75f;                     // le comptoir, à 0,75 m du couloir
        float x0 = r.xMin + 0.15f, x1 = r.xMax - 0.85f;
        Cube(L.props, "Comptoir_Accueil", new Vector3((x0 + x1) * 0.5f, y + 0.52f, cz), new Vector3(x1 - x0, 1.04f, 0.6f), Quaternion.identity, wood, true, _layerFurniture);
        Cube(L.props, "Comptoir_Plateau", new Vector3((x0 + x1) * 0.5f, y + 1.06f, cz + 0.05f), new Vector3(x1 - x0 + 0.04f, 0.04f, 0.72f), Quaternion.identity, top, true, _layerFurniture);
        // Vitre au-dessus du comptoir, avec passe-documents au milieu
        float mid = (x0 + x1) * 0.5f, gap = 0.5f, gy0 = y + 1.08f, gy1 = y + 2.2f;
        Cube(L.props, "Vitre_Accueil", new Vector3((x0 + mid - gap * 0.5f) * 0.5f, (gy0 + gy1) * 0.5f, cz), new Vector3(mid - gap * 0.5f - x0, gy1 - gy0, 0.02f), Quaternion.identity, glass, true, _layerWalls);
        Cube(L.props, "Vitre_Accueil", new Vector3((mid + gap * 0.5f + x1) * 0.5f, (gy0 + gy1) * 0.5f, cz), new Vector3(x1 - mid - gap * 0.5f, gy1 - gy0, 0.02f), Quaternion.identity, glass, true, _layerWalls);
        Cube(L.props, "Vitre_Accueil", new Vector3(mid, (gy0 + 0.25f + gy1) * 0.5f, cz), new Vector3(gap, gy1 - gy0 - 0.25f, 0.02f), Quaternion.identity, glass, true, _layerWalls);
        // Porte vitrée du personnel au bout du comptoir (fermée par le passe)
        var gdoor = BuildDoor(DoorsRoot(L), "Porte_Accueil", new Vector3(x1 + 0.02f, y + 0.01f, cz), 0f, r.xMax - 0.1f - x1 - 0.04f, 2.15f, glass, "la porte de l'accueil");
        gdoor.displayName = "la porte de l'accueil";
        gdoor.locked = true;
        gdoor.requiredKeyId = StaffKey;
        gdoor.lockedMessage = "Accueil — réservé au personnel.";
        // Derrière : fauteuil, ordinateur, surveillante, étagères, casiers à courrier, tableau des clés
        var chair = Piece(L, MP("Chairs", "Chair31", "Chair33"), KFurniture + "chairDesk", new Vector3(mid, y, cz - 0.85f), 0f, F(0.62f, 1.1f, 0.62f, 0f), mp);
        CaveProp(L, "classic_laptop", new Vector3(mid + 0.5f, y + 1.08f, cz - 0.15f), 180f, F(0.38f, 0.3f, 0.3f, 180f));
        CaveProp(L, "clipboard", new Vector3(mid - 0.6f, y + 1.08f, cz - 0.1f), Rand(160f, 200f), new Vector3(0.25f, 0.04f, 0.33f));
        BuildReceptionist(L, new Vector3(mid, y, cz - 0.85f));
        Piece(L, MP("Closets", "Closet04", "Closet09"), KFurniture + "bookcaseClosedWide", new Vector3(r.center.x, y, r.yMin + 0.35f), 0f, F(1.8f, 2.1f, 0.55f, 0f), mp);
        var box = Mat("Casier_Courrier", new Color(0.55f, 0.5f, 0.42f), 0.4f);
        for (int i = 0; i < 6; i++)                    // casiers à courrier (mur ouest)
            for (int j = 0; j < 4; j++)
                Cube(L.props, "Casier_Courrier", new Vector3(r.xMin + 0.18f, y + 1.1f + j * 0.28f, r.yMin + 1.6f + i * 0.34f), new Vector3(0.14f, 0.26f, 0.32f), Quaternion.identity, box, false, -1);
        Cube(L.props, "Tableau_Cles", new Vector3(r.xMax - 0.14f, y + 1.6f, r.center.y - 0.5f), new Vector3(0.06f, 0.6f, 0.5f), Quaternion.identity, Mat("Bois_Clair", new Color(0.62f, 0.5f, 0.36f), 0.3f), false, -1);
        CaveProp(L, "wall_clock", new Vector3(r.center.x, y + 2.45f, r.yMin + 0.12f), 0f, new Vector3(0.3f, 0.3f, 0.1f));
        CaveProp(L, "potted_plant_01", new Vector3(r.xMin + 0.5f, y, cz - 0.9f), 0f, new Vector3(0.6f, 1.2f, 0.6f));
        CeilingLight(L, L.props, new Vector3(r.center.x, y1, cz - 1.2f), 7f);
        Plaque(L, "ACCUEIL", new Vector3(r.center.x, y + 2.45f, CorrS + Wall * 0.5f + 0.02f), Vector3.forward);
        CctvCamera(L, new Vector3(r.xMax - 0.3f, y1 - 0.25f, r.yMin + 0.4f), new Vector3(mid, y + 1f, cz + 1.5f), "Accueil");
        CaveView(L, "Vue_Accueil", new Vector3(r.center.x - 0.5f, y, CorrN - 0.4f), new Vector3(r.center.x, y + 0.9f, cz - 1f));
        Marker(L, "Accueil", new Vector3(r.center.x, y, cz - 1f), Color.cyan);
    }

    /// Surveillante assise derrière le comptoir (même silhouette que les surveillants en ronde).
    static void BuildReceptionist(LevelDef L, Vector3 seat)
    {
        var go = new GameObject("Surveillante_Accueil");
        go.transform.SetParent(L.props, false);
        go.transform.position = seat;
        go.layer = _layerNpc;
        var body = Mat("Surveillant", new Color(0.1f, 0.12f, 0.25f));
        var torso = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        torso.name = "Corps";
        torso.layer = _layerNpc;
        torso.transform.SetParent(go.transform, false);
        torso.transform.localPosition = new Vector3(0f, 0.95f, 0f);
        torso.transform.localScale = new Vector3(0.62f, 0.5f, 0.55f);
        torso.GetComponent<Renderer>().sharedMaterial = body;
        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Tete";
        head.layer = _layerNpc;
        head.transform.SetParent(go.transform, false);
        head.transform.localPosition = new Vector3(0f, 1.62f, 0.02f);
        head.transform.localScale = Vector3.one * 0.24f;
        Object.DestroyImmediate(head.GetComponent<Collider>());
        head.GetComponent<Renderer>().sharedMaterial = Mat("Peau", new Color(0.78f, 0.62f, 0.5f), 0.3f);
        NonStatic(go);
    }

    /// Poste de surveillance (studio nord n°10 de l'étage 1) : six télés plates au mur affichent les caméras.
    static void FurnishSurveillance(LevelDef L, Rect r)
    {
        var mp = Mat("Meuble_" + L.number, Color.Lerp(L.wall, L.floor, 0.5f) * 0.9f);
        float y = L.y;
        var desk = Piece(L, MP("Tables", "Table48", "Table49"), KFurniture + "desk", new Vector3(r.xMax - 1.5f, y, r.center.y + 0.6f), -90f, F(1.8f, 0.78f, 0.75f, -90f), mp);
        MakeSeat(Piece(L, MP("Chairs", "Chair31", "Chair33"), KFurniture + "chairDesk", new Vector3(r.xMax - 2.35f, y, r.center.y + 0.2f), 90f, F(0.62f, 1.1f, 0.62f, 90f), mp), 90f);
        MakeSeat(Piece(L, MP("Chairs", "Chair31", "Chair33"), KFurniture + "chairDesk", new Vector3(r.xMax - 2.35f, y, r.center.y + 1.2f), 75f, F(0.62f, 1.1f, 0.62f, 75f), mp), 75f);
        float deskTop = desk != null && TryBounds(desk, out var db) ? db.max.y : y + 0.78f;
        CaveProp(L, "vintage_radio_transceiver", new Vector3(r.xMax - 1.5f, deskTop, r.center.y + 1.25f), -100f, F(0.4f, 0.24f, 0.28f, -100f));
        CaveProp(L, "clipboard", new Vector3(r.xMax - 1.6f, deskTop, r.center.y - 0.1f), Rand(-100f, -80f), new Vector3(0.25f, 0.04f, 0.33f));
        PlaceItem(L, StaffKey, new Vector3(r.xMax - 1.4f, deskTop, r.center.y + 0.5f), Rand(0f, 360f));
        CaveProp(L, "wall_clock", new Vector3(r.center.x, y + 2.45f, r.yMax - 0.12f), 180f, new Vector3(0.3f, 0.3f, 0.1f));

        // Mur d'images : 3 × 2 télés plates de 1 m sur le mur est
        var screens = new List<Renderer>();
        var screenMat = CctvScreenMat();
        float wx = r.xMax - Wall * 0.5f - 0.06f;
        foreach (var ty in new[] { y + 1.25f, y + 1.9f })
            for (int k = 0; k < 3; k++)
                screens.Add(FlatTv(L, new Vector3(wx, ty, r.center.y + 0.6f + (k - 1) * 1.06f), Vector3.left, 1.0f, screenMat));
        var l = PointLight(L, new Vector3(r.xMax - 1.2f, y + 1.5f, r.center.y + 0.6f), 4f);
        l.color = new Color(0.55f, 0.75f, 1f); l.intensity = 0.6f;

        var sys = new GameObject("Systeme_CCTV");
        sys.transform.SetParent(L.props, false);
        sys.transform.position = new Vector3(r.center.x, y + 1f, r.center.y);
        var cctv = sys.AddComponent<CctvSystem>();
        cctv.screens = screens.ToArray();
        _cctvSystem = cctv;
        Plaque(L, "POSTE DE SURVEILLANCE", new Vector3(r.center.x, y + 2.45f, CorrN - Wall * 0.5f - 0.02f), Vector3.back);
        CaveView(L, "Vue_Surveillance", new Vector3(r.xMin + 0.5f, y, r.yMin + 0.6f), new Vector3(r.xMax, y + 1.2f, r.center.y + 0.6f));
        Marker(L, "Poste_de_Surveillance", new Vector3(r.center.x, y, r.center.y), Color.cyan);
    }

    static Material CctvScreenMat()
    {
        string path = MatFolder + "/Ecran_CCTV.mat";
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        m.shader = shader;
        m.SetColor("_BaseColor", new Color(0.78f, 0.95f, 0.85f));
        EditorUtility.SetDirty(m);
        return m;
    }

    /// Caméra de surveillance murale (modèle Poly Haven) avec une vraie caméra qui filme pour le poste.
    static void CctvCamera(LevelDef L, Vector3 pos, Vector3 lookAt, string label)
    {
        var dir = (lookAt - pos).normalized;
        var yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        var model = CaveProp(L, "security_camera_01", pos + Vector3.down * 0.15f, yaw + 180f, new Vector3(0.3f, 0.3f, 0.3f));
        if (model != null && TryBounds(model, out var b)) model.transform.position += Vector3.up * (pos.y + 0.12f - b.max.y);
        var go = new GameObject("Camera_CCTV (" + label + ")");
        go.transform.SetParent(L.props, false);
        go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(dir, Vector3.up));
        var cam = go.AddComponent<Camera>();
        cam.enabled = false;            // rendue à la demande par le poste de surveillance
        cam.fieldOfView = 75f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 40f;
        cam.depth = -10;
        _cctvCams.Add((cam, label));
    }

    /// Relie les caméras posées sur tous les étages aux écrans du poste (appelé après la construction des étages).
    static void ConnectCctv()
    {
        if (_cctvSystem == null) { _cctvCams.Clear(); return; }
        var cams = new List<Camera>(); var labels = new List<string>();
        foreach (var (cam, label) in _cctvCams) { cams.Add(cam); labels.Add(label); }
        _cctvSystem.cameras = cams.ToArray();
        _cctvSystem.labels = labels.ToArray();
        Debug.Log($"[EchoesMap] Poste de surveillance : {cams.Count} caméras, {_cctvSystem.screens.Length} écrans.");
        _cctvCams.Clear();
        _cctvSystem = null;
    }

    // ───────────────────────── Extérieur : cour et façades de l'aile ─────────────────────────

    /// Cour entre le bâtiment et l'aile : pelouse, bancs, buissons, lampadaire.
    static void DecorateCourtyard(LevelDef L)
    {
        float y = L.y, zc = (5.5f + WZ0) * 0.5f;
        var bench = KGraveyard + "bench";
        AddSeat(Model(L, bench, new Vector3(-6f, y, zc + 1.2f), 180f, new Vector3(1.8f, 0.9f, 0.6f), Mat("Exterieur_Allee", Color.white)), 0.45f, 180f, Vector3.zero, "S'asseoir sur le banc");
        AddSeat(Model(L, bench, new Vector3(9.5f, y, zc + 1.2f), 180f, new Vector3(1.8f, 0.9f, 0.6f), Mat("Exterieur_Allee", Color.white)), 0.45f, 180f, Vector3.zero, "S'asseoir sur le banc");
        if (AssetDatabase.LoadAssetAtPath<GameObject>(BushLarge) != null)
            foreach (var x in new[] { -10.5f, -3f, 6.5f, 13f })
            {
                float s = Rand(1f, 1.5f);
                SetupRealTree(InstantiateAt(L, _rng.NextDouble() < 0.5 ? BushLarge : BushSmall, new Vector3(x, y, WZ0 - 1f), Rand(0f, 360f), new Vector3(s * 1.4f, s, s * 1.4f)), false);
            }
        var lamp = PointLight(L, new Vector3(-1.5f, y + 3.2f, zc), 9f, L.props);
        lamp.intensity = 1.4f;
        Cylinder(L.props, "Lampadaire_Cour", new Vector3(-1.5f, y + 1.7f, zc), 0.06f, 3.4f, Quaternion.identity, Mat("Grille_Metal", new Color(0.18f, 0.18f, 0.2f), 0.5f), true);
    }

    static void BuildWingFacade(Transform ext, Material brick, float fy0)
    {
        var west = new List<Hole>(); var east = new List<Hole>(); var north = new List<Hole>(); var south = new List<Hole>();
        foreach (var lv in _levels.Values)
        {
            if (lv.kind != Kind.Dorm) continue;
            west.AddRange(WingWinHoles(lv, WingWestWinZ));
            east.AddRange(WingWinHoles(lv, WingEastWinZ));
            north.AddRange(WingWinHoles(lv, WingNorthWinX));
            south.AddRange(WingWinHoles(lv, WingSouthWinX));
        }
        south.Add(new Hole(2f, WCx1 - WCx0 + 0.2f, fy0, FacadeTop)); // la passerelle arrive dans l'aile
        WallAlongZ(ext, "Facade_Aile_Ouest", WX0 - 0.2f, WZ0 - 0.3f, WZ1 + 0.3f, fy0, FacadeTop, brick, west.ToArray());
        WallAlongZ(ext, "Facade_Aile_Est", WX1 + 0.2f, WZ0 - 0.3f, WZ1 + 0.3f, fy0, FacadeTop, brick, east.ToArray());
        WallAlongX(ext, "Facade_Aile_Nord", WZ1 + 0.2f, WX0 - 0.3f, WX1 + 0.3f, fy0, FacadeTop, brick, north.ToArray());
        WallAlongX(ext, "Facade_Aile_Sud", WZ0 - 0.2f, WX0 - 0.3f, WX1 + 0.3f, fy0, FacadeTop, brick, south.ToArray());
    }

    static Rect WingFootprint => Rect.MinMaxRect(WX0 - 0.4f, WZ0 - 0.4f, WX1 + 0.4f, WZ1 + 0.4f);
    static Rect BridgeFootprint => Rect.MinMaxRect(WCx0 - 0.2f, BridgeZ0 - 0.2f, WCx1 + 0.2f, WZ0);
}
