using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Aile nord de la résidence (étages 1 à 3), reliée au couloir par l'ancien studio nord n°6 :
///   étage 1 : cafétéria · toilettes · salle de repos du personnel
///   étage 2 : salle d'étude · toilettes · deux studios du personnel
///   étage 3 : espace détente / jeux · toilettes · deux studios du personnel
/// + poste de surveillance à l'entrée (studio nord n°10 de l'étage 1, à côté de la porte de sortie),
///   avec de vraies caméras dont les images passent sur les écrans.
/// Les locaux du personnel sont fermés : le passe se trouve sur le bureau du poste de surveillance.
/// </summary>
public static partial class EchoesMapGenerator
{
    const int WingRoomIndex = 5;          // studio nord qui devient le passage vers l'aile
    const int SurveillanceRoomIndex = 9;  // studio nord de l'étage 1 près de la sortie
    const float WX0 = -12f, WX1 = 16f;    // emprise en x
    const float WZ0 = 5.3f, WZ1 = 21f;    // de la façade nord au fond de l'aile
    const float WCx0 = 0.5f, WCx1 = 3.5f; // couloir de l'aile
    const float WSplit = 10.5f, WSplit2 = 15.75f;
    const string StaffKey = "passe_personnel";

    static readonly float[] WingWestWinZ = { 8f, 12f, 16f, 19.5f };
    static readonly float[] WingEastWinZ = { 7.9f, 13.1f, 18.4f };
    static readonly float[] WingNorthWinX = { -9f, -5f, -1.5f, 2f, 6f, 10f, 14f };

    static readonly string[] StaffNames = { "M. Garnier — surveillant de nuit", "Mme Roussel — surveillante", "M. Ferrand — gardien", "Mme Okafor — intendante" };

    static readonly List<(Camera cam, string label)> _cctvCams = new List<(Camera, string)>();

    static string[] MP(string cat, params string[] names)
    {
        var r = new string[names.Length];
        for (int i = 0; i < names.Length; i++) r[i] = cat + "/" + names[i];
        return r;
    }

    // ───────────────────────── Structure ─────────────────────────

    static void BuildWing(LevelDef L)
    {
        if (L.kind != Kind.Dorm) return;
        var mw = Mat("Mur_" + L.number, L.wall);
        var mf = Mat("Sol_" + L.number, L.floor);
        var mc = Mat("Plafond_" + L.number, L.wall * 0.8f);
        float y0 = L.y, y1 = L.y + L.h;
        var root = Child(L.root, "Aile_Nord");

        // Dalles (le sol déborde sous la façade pour rejoindre le couloir)
        Box(root, "Sol_Aile", new Vector3(WX0 - 0.1f, y0 - Slab, 5.0f), new Vector3(WX1 + 0.1f, y0, WZ1 + 0.1f), mf, _layerGround);
        Box(root, "Plafond_Aile", new Vector3(WX0 - 0.1f, y1, 5.0f), new Vector3(WX1 + 0.1f, y1 + Slab, WZ1 + 0.1f), mc, _layerWalls);

        // Murs extérieurs (côté intérieur), avec fenêtres
        WallAlongZ(root, "Mur_Aile_Ouest", WX0, WZ0, WZ1, y0, y1, mw, WingWinHoles(L, WingWestWinZ));
        WallAlongZ(root, "Mur_Aile_Est", WX1, WZ0, WZ1, y0, y1, mw, WingWinHoles(L, WingEastWinZ));
        WallAlongX(root, "Mur_Aile_Nord", WZ1, WX0, WX1, y0, y1, mw, WingWinHoles(L, WingNorthWinX));
        BuildWingWindows(L, root);
        // Doublage de la façade nord dans les salles (on ne voit pas la brique de l'intérieur)
        Cube(root, "Doublage", new Vector3((WX0 + WCx0) * 0.5f, (y0 + y1) * 0.5f, WZ0 + 0.03f), new Vector3(WCx0 - WX0, L.h, 0.06f), Quaternion.identity, mw, true, _layerWalls);
        Cube(root, "Doublage", new Vector3((WCx1 + WX1) * 0.5f, (y0 + y1) * 0.5f, WZ0 + 0.03f), new Vector3(WX1 - WCx1, L.h, 0.06f), Quaternion.identity, mw, true, _layerWalls);

        // Couloir de l'aile : grande salle à l'ouest (deux grandes ouvertures), toilettes et locaux du personnel à l'est
        WallAlongZ(root, "Mur_Couloir_Aile_O", WCx0, WZ0, WZ1, y0, y1, mw,
            new Hole(8.5f, 2.2f, y0, y0 + DoorH + 0.2f), new Hole(17.5f, 2.2f, y0, y0 + DoorH + 0.2f));
        var eastDoors = new List<Hole> { new Hole(8f, 1.1f, y0, y0 + DoorH - 0.1f) };
        if (L.number == 1) eastDoors.Add(new Hole(15.75f, 1.1f, y0, y0 + DoorH - 0.1f));
        else { eastDoors.Add(new Hole(13.1f, 1.1f, y0, y0 + DoorH - 0.1f)); eastDoors.Add(new Hole(18.4f, 1.1f, y0, y0 + DoorH - 0.1f)); }
        WallAlongZ(root, "Mur_Couloir_Aile_E", WCx1, WZ0, WZ1, y0, y1, mw, eastDoors.ToArray());
        WallAlongX(root, "Cloison_Toilettes", WSplit, WCx1, WX1, y0, y1, mw);
        if (L.number != 1) WallAlongX(root, "Cloison_Studios", WSplit2, WCx1, WX1, y0, y1, mw);

        // Portes : toilettes (libre), personnel (fermé à clé)
        var doorMat = Mat("Porte_Studio", new Color(0.75f, 0.6f, 0.45f));
        WingDoor(L, 8f, doorMat, "Porte_Toilettes", "la porte des toilettes", false, null);
        if (L.number == 1)
            WingDoor(L, 15.75f, doorMat, "Porte_Salle_Repos", "la salle de repos du personnel", true, "Salle de repos — réservé au personnel.");
        else
        {
            int s = (L.number - 2) * 2;
            WingDoor(L, 13.1f, doorMat, "Porte_Studio_Personnel", "le studio de " + StaffNames[s].Split('—')[0].Trim(), true, "Studio privé : " + StaffNames[s] + ".");
            WingDoor(L, 18.4f, doorMat, "Porte_Studio_Personnel", "le studio de " + StaffNames[s + 1].Split('—')[0].Trim(), true, "Studio privé : " + StaffNames[s + 1] + ".");
        }

        // Plaques
        Plaque(L, "TOILETTES", new Vector3(WCx1 - Wall * 0.5f - 0.02f, y0 + 2.35f, 8f), Vector3.left);
        string west = L.number == 1 ? "CAFÉTÉRIA" : L.number == 2 ? "SALLE D'ÉTUDE" : "ESPACE DÉTENTE";
        Plaque(L, west, new Vector3(WCx0 + Wall * 0.5f + 0.02f, y0 + 2.6f, 8.5f), Vector3.right);
        Plaque(L, west, new Vector3(WCx0 + Wall * 0.5f + 0.02f, y0 + 2.6f, 17.5f), Vector3.right);
        if (L.number == 1) Plaque(L, "PERSONNEL", new Vector3(WCx1 - Wall * 0.5f - 0.02f, y0 + 2.35f, 15.75f), Vector3.left);
        else
        {
            Plaque(L, "PERSONNEL", new Vector3(WCx1 - Wall * 0.5f - 0.02f, y0 + 2.35f, 13.1f), Vector3.left);
            Plaque(L, "PERSONNEL", new Vector3(WCx1 - Wall * 0.5f - 0.02f, y0 + 2.35f, 18.4f), Vector3.left);
        }
        Plaque(L, west + " · TOILETTES", new Vector3(2f, y0 + 2.6f, CorrN + RoomDepth - Wall * 0.5f - 0.02f), Vector3.back); // au-dessus du passage, côté couloir

        // Éclairage
        foreach (var z in new[] { 7f, 12f, 17f, 20f }) CeilingLight(L, root, new Vector3(2f, y1, z), 7f);
        foreach (var p in new[] { new Vector3(-8f, 0, 9f), new Vector3(-3f, 0, 9f), new Vector3(-8f, 0, 17f), new Vector3(-3f, 0, 17f) })
            CeilingLight(L, root, new Vector3(p.x, y1, p.z), 9f);
        CeilingLight(L, root, new Vector3(9.5f, y1, 7.9f), 7f);
        if (L.number == 1) { CeilingLight(L, root, new Vector3(7f, y1, 15.5f), 8f); CeilingLight(L, root, new Vector3(13f, y1, 15.5f), 8f); }
        else { CeilingLight(L, root, new Vector3(9.5f, y1, 13.1f), 8f); CeilingLight(L, root, new Vector3(9.5f, y1, 18.4f), 8f); }

        // Contenu
        var westRoom = Rect.MinMaxRect(WX0, WZ0, WCx0, WZ1);
        if (L.number == 1) FurnishCafeteria(L, westRoom);
        else if (L.number == 2) FurnishStudyRoom(L, westRoom);
        else FurnishGameRoom(L, westRoom);
        FurnishToilets(L, Rect.MinMaxRect(WCx1, WZ0, WX1, WSplit));
        if (L.number == 1) FurnishStaffRestRoom(L, Rect.MinMaxRect(WCx1, WSplit, WX1, WZ1));
        else
        {
            int s = (L.number - 2) * 2;
            FurnishStaffStudio(L, Rect.MinMaxRect(WCx1, WSplit, WX1, WSplit2), StaffNames[s]);
            FurnishStaffStudio(L, Rect.MinMaxRect(WCx1, WSplit2, WX1, WZ1), StaffNames[s + 1]);
        }

        // Vues de contrôle (captures)
        float yv = L.y;
        if (L.number == 1)
        {
            CaveView(L, "Vue_Cafeteria", new Vector3(-0.6f, yv, 20.2f), new Vector3(-8f, yv, 8f));
            CaveView(L, "Vue_Toilettes", new Vector3(4.3f, yv, 5.9f), new Vector3(14f, yv, 9.6f));
            CaveView(L, "Vue_Salle_Repos", new Vector3(4.3f, yv, 11.1f), new Vector3(14f, yv, 19f));
            CaveView(L, "Vue_Aile_Exterieur", new Vector3(26f, yv, 36f), new Vector3(2f, yv + 4f, 12f));
        }
        else if (L.number == 2)
        {
            CaveView(L, "Vue_Salle_Etude", new Vector3(-1f, yv, 6.2f), new Vector3(-6f, yv, 18f));
            CaveView(L, "Vue_Studio_Personnel", new Vector3(4.3f, yv, 11.1f), new Vector3(14f, yv, 14f));
        }
        else CaveView(L, "Vue_Espace_Detente", new Vector3(-0.8f, yv, 20.2f), new Vector3(-8f, yv, 8f));

        // Extincteur et caméra dans le couloir de l'aile
        CaveProp(L, "korean_fire_extinguisher_01", new Vector3(WCx0 + 0.25f, y0, 13f), 90f, new Vector3(0.3f, 0.6f, 0.3f));
        CctvCamera(L, new Vector3(WCx1 - 0.2f, y1 - 0.25f, WZ1 - 0.3f), new Vector3(2f, y0 + 1f, 7f), $"Aile nord — étage {L.number}");
        if (L.number == 1) CctvCamera(L, new Vector3(WCx0 + 0.3f, y1 - 0.25f, WZ1 - 0.4f), new Vector3(-6f, y0 + 0.8f, 9f), "Cafétéria");
        CctvCamera(L, new Vector3(-HalfX + 0.4f, y1 - 0.25f, CorrN - 0.2f), new Vector3(8f, y0 + 1f, (CorrS + CorrN) * 0.5f), $"Couloir — étage {L.number}");
        if (L.number == 1) CctvCamera(L, new Vector3(HalfX - 0.4f, y1 - 0.25f, CorrS + 0.2f), new Vector3(-6f, y0 + 1f, (CorrS + CorrN) * 0.5f), "Entrée");
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
    static void CeilingLight(LevelDef L, Transform p, Vector3 ceiling, float range)
    {
        var m = Mat("Plafonnier", new Color(1f, 0.95f, 0.85f), 0.3f);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", new Color(1f, 0.92f, 0.78f) * 1.6f);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        Cube(p, "Plafonnier", ceiling + Vector3.down * 0.02f, new Vector3(0.6f, 0.03f, 0.6f), Quaternion.identity, m, false, -1)
            .GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        PointLight(L, ceiling + Vector3.down * 0.35f, range, p);
    }

    /// Sol carrelé posé sur la dalle (cafétéria, toilettes).
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

    /// Table et chaises tout autour (yaw de la table : 0 = longueur selon x).
    static void TableWithChairs(LevelDef L, Vector3 c, string[] table, Vector3 tableFit, string[] chairs, int perSide, Material mp, bool seats = true)
    {
        Piece(L, table, KFurniture + "table", c, 0f, tableFit, mp);
        float half = tableFit.x * 0.5f, depth = tableFit.z * 0.5f + 0.38f;
        for (int k = 0; k < perSide; k++)
        {
            float x = c.x - half + tableFit.x * (k + 0.5f) / perSide;
            var a = Piece(L, chairs, KFurniture + "chair", new Vector3(x, L.y, c.z - depth + Rand(-0.05f, 0.05f)), Rand(-8f, 8f), new Vector3(0.5f, 1f, 0.5f), mp);
            var b = Piece(L, chairs, KFurniture + "chair", new Vector3(x, L.y, c.z + depth + Rand(-0.05f, 0.05f)), 180f + Rand(-8f, 8f), new Vector3(0.5f, 1f, 0.5f), mp);
            if (seats) { MakeSeat(a, 0f); MakeSeat(b, 180f); }
        }
    }

    // ───────────────────────── Salles ─────────────────────────

    static void FurnishCafeteria(LevelDef L, Rect r)
    {
        var mp = Mat("Meuble_" + L.number, Color.Lerp(L.wall, L.floor, 0.5f) * 0.9f);
        float y = L.y;
        TileFloor(L, r);
        // Comptoir et cuisine le long du mur ouest (face à la salle, +X)
        string[] row = { "Kitchen/Refrigerator02", "Kitchen/CabinetC01", "Kitchen/CabinetC_Sink", "Kitchen/CabinetC02", "Kitchen/GasStove01", "Kitchen/CabinetC01", "Kitchen/KitchenOven01", "Kitchen/CabinetC02", "Kitchen/Refrigerator02" };
        float z = WZ0 + 1.0f;
        foreach (var piece in row)
        {
            bool fridge = piece.Contains("Refrigerator");
            var go = Piece(L, new[] { piece }, KFurniture + "kitchenCabinet", new Vector3(r.xMin + 0.4f, y, z), 90f, fridge ? new Vector3(0.75f, 1.9f, 0.75f) : new Vector3(0.62f, 0.95f, 0.62f), mp);
            if (go != null && TryBounds(go, out var b)) z += Mathf.Max(0.55f, b.size.z) + 0.02f; else z += 0.65f;
            if (piece == "Kitchen/CabinetC02" && z < 12f)
                Piece(L, MP("Kitchen", "MicrowaveOven02"), null, new Vector3(r.xMin + 0.35f, y + 0.92f, z - 0.35f), 90f, new Vector3(0.5f, 0.32f, 0.4f), mp, false);
        }
        CaveProp(L, "standing_chalkboard_01", new Vector3(r.xMin + 1.6f, y, z + 0.8f), 120f, new Vector3(0.7f, 1.4f, 0.7f));
        CaveProp(L, "CoffeeCart_01", new Vector3(r.xMin + 1.2f, y, r.yMax - 1.2f), 90f, new Vector3(1.2f, 1.1f, 0.7f), true);
        // Salle : 3 × 3 tables de 4 places
        foreach (var tz in new[] { 9.2f, 13.2f, 17.2f })
            foreach (var tx in new[] { -6.8f, -3.9f, -1.0f })
                TableWithChairs(L, new Vector3(tx, y, tz), MP("Tables", "Table10"), new Vector3(1.6f, 0.78f, 0.8f), MP("Chairs", "Chair29", "Chair27"), 2, mp);
        Model(L, KFurniture + "trashcan", new Vector3(-0.2f, y, WZ0 + 0.6f), 0f, new Vector3(0.5f, 0.8f, 0.5f), mp);
        Model(L, KFurniture + "trashcan", new Vector3(-0.2f, y, WZ1 - 0.6f), 0f, new Vector3(0.5f, 0.8f, 0.5f), mp);
        CaveProp(L, "potted_plant_01", new Vector3(-9.2f, y, WZ1 - 0.7f), 0f, new Vector3(0.8f, 1.4f, 0.8f));
        CaveProp(L, "wall_clock", new Vector3(-4f, y + 2.3f, WZ0 + 0.08f), 0f, new Vector3(0.35f, 0.35f, 0.12f));
        Marker(L, "Cafeteria", new Vector3(-4f, y, 13f), Color.yellow);
    }

    static void FurnishStudyRoom(LevelDef L, Rect r)
    {
        var mp = Mat("Meuble_" + L.number, Color.Lerp(L.wall, L.floor, 0.5f) * 0.9f);
        float y = L.y;
        // Rangées de bureaux tournés vers le tableau (au nord)
        foreach (var tz in new[] { 8.2f, 10.6f, 13f, 15.4f })
            foreach (var tx in new[] { -9.2f, -6.2f, -3.2f, -0.6f })
            {
                Piece(L, MP("Tables", "Table43", "Table44"), KFurniture + "desk", new Vector3(tx, y, tz), 0f, new Vector3(1.3f, 0.78f, 0.65f), mp);
                MakeSeat(Piece(L, MP("Chairs", "Chair04", "Chair31"), KFurniture + "chairDesk", new Vector3(tx, y, tz - 0.6f), 0f, new Vector3(0.55f, 1f, 0.55f), mp), 0f);
                if (_rng.NextDouble() < 0.3) CaveProp(L, "classic_laptop", new Vector3(tx + Rand(-0.3f, 0.3f), y + 0.78f, tz + 0.05f), Rand(160f, 200f), new Vector3(0.36f, 0.28f, 0.28f));
                else if (_rng.NextDouble() < 0.3) CaveProp(L, "book_encyclopedia_set_01", new Vector3(tx + Rand(-0.4f, 0.4f), y + 0.78f, tz + 0.1f), Rand(0f, 360f), new Vector3(0.35f, 0.3f, 0.3f));
            }
        CaveProp(L, "standing_chalkboard_01", new Vector3(-5f, y, 19.2f), 180f, new Vector3(1.6f, 1.8f, 0.8f));
        // Étagères de livres le long du mur ouest
        for (float z = 7f; z < 20f; z += 2.2f)
        {
            Piece(L, MP("Closets", "Closet04", "Closet09"), KFurniture + "bookcaseClosedWide", new Vector3(r.xMin + 0.35f, y, z), 90f, new Vector3(1.8f, 2.1f, 0.5f), mp);
        }
        CaveProp(L, "wall_clock", new Vector3(-4f, y + 2.4f, WZ1 - 0.08f), 180f, new Vector3(0.35f, 0.35f, 0.12f));
        CaveProp(L, "potted_plant_02", new Vector3(-0.2f, y, WZ1 - 0.6f), 0f, new Vector3(0.7f, 1.2f, 0.7f));
        Marker(L, "Salle_Etude", new Vector3(-5f, y, 13f), Color.yellow);
    }

    static void FurnishGameRoom(LevelDef L, Rect r)
    {
        var mp = Mat("Meuble_" + L.number, Color.Lerp(L.wall, L.floor, 0.5f) * 0.9f);
        float y = L.y;
        // Coin télé : canapés en U autour d'une table basse, télé et console contre le mur ouest
        var tvStand = Piece(L, MP("Tables", "Table35"), KFurniture + "tableCoffee", new Vector3(r.xMin + 0.5f, y, 9f), 90f, new Vector3(1.4f, 0.5f, 0.5f), mp);
        float top = tvStand != null && TryBounds(tvStand, out var tb) ? tb.max.y : y + 0.5f;
        CaveProp(L, "Television_01", new Vector3(r.xMin + 0.45f, top, 9f), 90f, new Vector3(0.9f, 0.7f, 0.5f));
        CaveProp(L, "gaming_console", new Vector3(r.xMin + 0.5f, top, 9.75f), 90f, new Vector3(0.35f, 0.1f, 0.3f));
        MakeSeat(Piece(L, MP("Sofas", "Sofa34", "Sofa36"), KFurniture + "loungeSofa", new Vector3(r.xMin + 3.8f, y, 9f), -90f, new Vector3(0.95f, 0.9f, 2.2f), mp), -90f, "S'asseoir sur le canapé");
        MakeSeat(Piece(L, MP("Sofas", "Sofa12", "Sofa16"), KFurniture + "loungeSofa", new Vector3(r.xMin + 2.3f, y, 7.1f), 0f, new Vector3(2f, 0.9f, 0.95f), mp), 0f, "S'asseoir sur le canapé");
        Piece(L, MP("Tables", "Table33"), KFurniture + "tableCoffee", new Vector3(r.xMin + 2.3f, y, 9f), 90f, new Vector3(1.2f, 0.45f, 0.7f), mp);
        CaveProp(L, "gamepad", new Vector3(r.xMin + 2.2f, y + 0.46f, 9.1f), Rand(0f, 360f), new Vector3(0.16f, 0.06f, 0.12f));
        foreach (var p in new[] { new Vector3(-9.8f, 0, 12f), new Vector3(-8.4f, 0, 12.4f), new Vector3(-7.2f, 0, 11.6f) })
            MakeSeat(Piece(L, MP("Chairs", "Chair48"), null, new Vector3(p.x, y, p.z), Rand(160f, 220f), new Vector3(0.8f, 0.6f, 0.8f), mp), 180f, "S'affaler sur le pouf");
        // Tables de jeux (échecs, cartes)
        TableWithChairs(L, new Vector3(-3.5f, y, 9.5f), MP("Tables", "Table41"), new Vector3(1.0f, 0.75f, 1.0f), MP("Chairs", "Chair29"), 1, mp);
        CaveProp(L, "chess_set", new Vector3(-3.5f, y + 0.76f, 9.5f), Rand(0f, 90f), new Vector3(0.42f, 0.12f, 0.42f));
        TableWithChairs(L, new Vector3(-3.5f, y, 13.5f), MP("Tables", "Table41"), new Vector3(1.0f, 0.75f, 1.0f), MP("Chairs", "Chair29"), 1, mp);
        // Coin fléchettes et musique
        CaveProp(L, "dartboard", new Vector3(-6f, y + 1.55f, WZ1 - 0.1f), 180f, new Vector3(0.5f, 0.5f, 0.12f));
        CaveProp(L, "boombox", new Vector3(-9f, y, WZ1 - 0.6f), 160f, new Vector3(0.6f, 0.35f, 0.25f));
        TableWithChairs(L, new Vector3(-4.5f, y, 17.5f), MP("Tables", "Table27"), new Vector3(1.0f, 0.78f, 1.0f), MP("Chairs", "Chair24"), 1, mp);
        MakeSeat(Piece(L, MP("Sofas", "Sofa34", "Sofa36"), KFurniture + "loungeSofa", new Vector3(-9.6f, y, 17f), 90f, new Vector3(0.95f, 0.9f, 2.2f), mp), 90f, "S'asseoir sur le canapé");
        CaveProp(L, "potted_plant_01", new Vector3(-0.3f, y, WZ0 + 0.6f), 0f, new Vector3(0.8f, 1.4f, 0.8f));
        CaveProp(L, "potted_plant_02", new Vector3(r.xMin + 0.6f, y, WZ1 - 0.6f), 0f, new Vector3(0.7f, 1.2f, 0.7f));
        Marker(L, "Espace_Detente", new Vector3(-5f, y, 13f), Color.yellow);
    }

    static void FurnishToilets(LevelDef L, Rect r)
    {
        var mp = Mat("Meuble_" + L.number, Color.Lerp(L.wall, L.floor, 0.5f) * 0.9f);
        var part = Mat("Cloison_WC", new Color(0.55f, 0.6f, 0.62f), 0.4f);
        float y = L.y;
        TileFloor(L, r);
        // Quatre cabines contre le mur du fond (z = r.yMax), portes battantes
        const float cw = 1.5f, cd = 1.6f, ph = 2.0f, lift = 0.15f;
        float zf = r.yMax - cd;                         // façade des cabines
        float xs = r.xMax - 4 * cw;
        for (int k = 0; k <= 4; k++)                     // séparations
            Cube(L.props, "Cloison_WC", new Vector3(xs + k * cw, y + lift + ph * 0.5f, zf + cd * 0.5f), new Vector3(0.04f, ph, cd), Quaternion.identity, part, true, _layerWalls);
        for (int k = 0; k < 4; k++)
        {
            float cx = xs + (k + 0.5f) * cw;
            // Façade : deux montants autour d'une porte de 0,7 m
            Cube(L.props, "Facade_WC", new Vector3(cx - 0.55f, y + lift + ph * 0.5f, zf), new Vector3(0.4f, ph, 0.04f), Quaternion.identity, part, true, _layerWalls);
            Cube(L.props, "Facade_WC", new Vector3(cx + 0.55f, y + lift + ph * 0.5f, zf), new Vector3(0.4f, ph, 0.04f), Quaternion.identity, part, true, _layerWalls);
            var door = BuildDoor(DoorsRoot(L), "Porte_WC", new Vector3(cx - 0.35f, y + lift, zf), 0f, 0.7f, ph - 0.05f, part, "la cabine");
            door.displayName = "la cabine";
            if (_rng.NextDouble() < 0.35) door.startAngle = -Rand(20f, 70f);
            Piece(L, MP("Bathroom", "Toilet01", "Toilet05"), KFurniture + "toilet", new Vector3(cx, y, r.yMax - 0.4f), 180f, new Vector3(0.45f, 0.8f, 0.7f), mp);
            Piece(L, MP("Bathroom", "TissueHanger01"), null, new Vector3(cx + 0.6f, y + 0.7f, r.yMax - 0.7f), -90f, new Vector3(0.12f, 0.12f, 0.15f), mp, false);
        }
        // Lavabos et miroir contre la façade (z = r.yMin)
        var mirror = Mat("Miroir", new Color(0.7f, 0.75f, 0.78f), 1f);
        mirror.SetFloat("_Metallic", 1f);
        for (int k = 0; k < 3; k++)
        {
            float x = r.xMax - 1.2f - k * 1.3f;
            Piece(L, MP("Bathroom", "WashBasin07"), KFurniture + "bathroomSink", new Vector3(x, y, r.yMin + 0.35f), 0f, new Vector3(0.6f, 0.95f, 0.5f), mp);
            Cube(L.props, "Miroir", new Vector3(x, y + 1.55f, r.yMin + 0.07f), new Vector3(0.6f, 0.8f, 0.02f), Quaternion.identity, mirror, false, -1);
        }
        Model(L, KFurniture + "trashcan", new Vector3(r.xMin + 0.5f, y, r.yMin + 0.5f), 0f, new Vector3(0.4f, 0.6f, 0.4f), mp);
    }

    static void FurnishStaffRestRoom(LevelDef L, Rect r)
    {
        var mp = Mat("Meuble_" + L.number, Color.Lerp(L.wall, L.floor, 0.5f) * 0.9f);
        float y = L.y;
        // Kitchenette contre le mur est
        float z = r.yMin + 0.8f;
        foreach (var piece in new[] { "Kitchen/Refrigerator02", "Kitchen/CabinetC_Sink", "Kitchen/CabinetC01" })
        {
            var go = Piece(L, new[] { piece }, KFurniture + "kitchenCabinet", new Vector3(r.xMax - 0.4f, y, z), -90f,
                piece.Contains("Refrigerator") ? new Vector3(0.75f, 1.9f, 0.75f) : new Vector3(0.62f, 0.95f, 0.62f), mp);
            z += go != null && TryBounds(go, out var b) ? Mathf.Max(0.55f, b.size.z) + 0.02f : 0.65f;
        }
        Piece(L, MP("Kitchen", "MicrowaveOven02"), null, new Vector3(r.xMax - 0.35f, y + 0.92f, z - 0.35f), -90f, new Vector3(0.5f, 0.32f, 0.4f), mp, false);
        CaveProp(L, "CoffeeCart_01", new Vector3(r.xMax - 0.7f, y, z + 0.9f), -90f, new Vector3(1.1f, 1.1f, 0.65f), true);
        // Coin repos : canapé, fauteuil, table basse, télé
        MakeSeat(Piece(L, MP("Sofas", "Sofa36", "Sofa34"), KFurniture + "loungeSofa", new Vector3(6f, y, r.yMax - 0.6f), 180f, new Vector3(2.1f, 0.9f, 0.95f), mp), 180f, "S'asseoir sur le canapé");
        MakeSeat(Piece(L, MP("Chairs", "Chair16"), KFurniture + "loungeChair", new Vector3(4.4f, y, r.yMax - 2.2f), 120f, new Vector3(0.9f, 0.9f, 0.9f), mp), 120f, "S'asseoir dans le fauteuil");
        Piece(L, MP("Tables", "Table33"), KFurniture + "tableCoffee", new Vector3(6f, y, r.yMax - 2.0f), 0f, new Vector3(1.2f, 0.45f, 0.7f), mp);
        var stand = Piece(L, MP("Tables", "Table35"), KFurniture + "tableCoffee", new Vector3(6f, y, r.yMin + 1.6f), 0f, new Vector3(1.4f, 0.5f, 0.5f), mp);
        float top = stand != null && TryBounds(stand, out var sb) ? sb.max.y : y + 0.5f;
        CaveProp(L, "Television_01", new Vector3(6f, top, r.yMin + 1.6f), 0f, new Vector3(0.9f, 0.7f, 0.5f));
        // Table du personnel et casiers
        TableWithChairs(L, new Vector3(10.5f, y, r.center.y + 1f), MP("Tables", "Table27"), new Vector3(1.1f, 0.78f, 1.1f), MP("Chairs", "Chair29"), 1, mp);
        CaveProp(L, "clipboard", new Vector3(10.4f, y + 0.79f, r.center.y + 1.1f), Rand(0f, 360f), new Vector3(0.25f, 0.03f, 0.33f));
        LockerBank(L.props, new Vector3(r.xMin + 8.5f, y, r.yMax - Wall * 0.5f), Vector3.back, Vector3.right, 5, Mat("Casier", new Color(0.45f, 0.5f, 0.52f)), Mat("Casier_Interieur", new Color(0.03f, 0.03f, 0.03f)));
        CaveProp(L, "boombox", new Vector3(r.xMax - 0.4f, y + 0.95f, r.yMin + 2.2f), -90f, new Vector3(0.5f, 0.3f, 0.2f));
        CaveProp(L, "wall_clock", new Vector3(10f, y + 2.3f, r.yMax - 0.08f), 180f, new Vector3(0.35f, 0.35f, 0.12f));
        CaveProp(L, "potted_plant_02", new Vector3(r.xMin + 0.5f, y, r.yMin + 0.6f), 0f, new Vector3(0.7f, 1.2f, 0.7f));
        Marker(L, "Salle_Repos_Personnel", new Vector3(r.center.x, y, r.center.y), Color.yellow);
    }

    static void FurnishStaffStudio(LevelDef L, Rect r, string owner)
    {
        var mp = Mat("Meuble_" + L.number, Color.Lerp(L.wall, L.floor, 0.5f) * 0.9f);
        float y = L.y;
        // Lit contre le mur est, chevet, armoire, bureau, fauteuil, petite table
        var bed = Piece(L, MegaBeds, KFurniture + "bedDouble", new Vector3(r.xMax - 1.2f, y, r.center.y), -90f, new Vector3(2.1f, 1.1f, 1.7f), mp);
        if (bed != null) AddSeat(bed, 0.5f, 90f, Vector3.zero, "S'asseoir sur le lit");
        Piece(L, MegaNightstands, KFurniture + "sideTable", new Vector3(r.xMax - 0.3f, y, r.yMax - 0.4f), -90f, new Vector3(0.5f, 0.6f, 0.45f), mp);
        Piece(L, MegaClosets, KFurniture + "bookcaseClosedDoors", new Vector3(r.center.x + 1.5f, y, r.yMax - 0.35f), 180f, new Vector3(1.4f, 2.1f, 0.6f), mp);
        Piece(L, MegaDesks, KFurniture + "desk", new Vector3(r.xMin + 1.6f, y, r.yMax - 0.45f), 180f, new Vector3(1.3f, 0.8f, 0.65f), mp);
        MakeSeat(Piece(L, MP("Chairs", "Chair31", "Chair33"), KFurniture + "chairDesk", new Vector3(r.xMin + 1.6f, y, r.yMax - 1.1f), 0f, new Vector3(0.6f, 1.1f, 0.6f), mp), 0f);
        CaveProp(L, "classic_laptop", new Vector3(r.xMin + 1.5f, y + 0.8f, r.yMax - 0.45f), 180f, new Vector3(0.36f, 0.28f, 0.28f));
        CaveProp(L, "vintage_radio_transceiver", new Vector3(r.xMin + 2.0f, y + 0.8f, r.yMax - 0.4f), 170f, new Vector3(0.35f, 0.2f, 0.25f));
        MakeSeat(Piece(L, MegaArmchairs, KFurniture + "loungeChair", new Vector3(r.center.x - 1f, y, r.yMin + 1f), 30f, new Vector3(0.85f, 0.9f, 0.85f), mp), 30f, "S'asseoir dans le fauteuil");
        CaveProp(L, "wall_clock", new Vector3(r.center.x, y + 2.3f, r.yMin + 0.08f), 0f, new Vector3(0.3f, 0.3f, 0.1f));
        CaveProp(L, "potted_plant_02", new Vector3(r.xMax - 0.5f, y, r.yMin + 0.5f), 0f, new Vector3(0.6f, 1.1f, 0.6f));
        Marker(L, "Studio_Personnel (" + owner + ")", new Vector3(r.center.x, y, r.center.y), Color.cyan);
    }

    /// Poste de surveillance (studio nord n°10 de l'étage 1, près de la porte de sortie) : murs d'écrans.
    static void FurnishSurveillance(LevelDef L, Rect r)
    {
        var mp = Mat("Meuble_" + L.number, Color.Lerp(L.wall, L.floor, 0.5f) * 0.9f);
        float y = L.y;
        // Étagère d'écrans contre le mur est (x = r.xMax), bureau devant
        var desk = Piece(L, MP("Tables", "Table48", "Table49"), KFurniture + "desk", new Vector3(r.xMax - 1.4f, y, r.center.y + 0.6f), -90f, new Vector3(0.7f, 0.78f, 1.6f), mp);
        MakeSeat(Piece(L, MP("Chairs", "Chair31", "Chair33"), KFurniture + "chairDesk", new Vector3(r.xMax - 2.2f, y, r.center.y + 0.3f), 90f, new Vector3(0.6f, 1.1f, 0.6f), mp), 90f);
        MakeSeat(Piece(L, MP("Chairs", "Chair31", "Chair33"), KFurniture + "chairDesk", new Vector3(r.xMax - 2.2f, y, r.center.y + 1.3f), 70f, new Vector3(0.6f, 1.1f, 0.6f), mp), 70f);
        float deskTop = desk != null && TryBounds(desk, out var db) ? db.max.y : y + 0.78f;
        CaveProp(L, "vintage_radio_transceiver", new Vector3(r.xMax - 1.4f, deskTop, r.center.y + 1.2f), -100f, new Vector3(0.35f, 0.2f, 0.25f));
        CaveProp(L, "clipboard", new Vector3(r.xMax - 1.5f, deskTop, r.center.y - 0.1f), Rand(-100f, -80f), new Vector3(0.25f, 0.03f, 0.33f));
        PlaceItem(L, StaffKey, new Vector3(r.xMax - 1.3f, deskTop, r.center.y + 0.5f), Rand(0f, 360f));
        CaveProp(L, "wall_clock", new Vector3(r.center.x, y + 2.4f, r.yMax - 0.08f), 180f, new Vector3(0.3f, 0.3f, 0.1f));

        // Six moniteurs cathodiques sur l'étagère (2 rangées × 3) ; leurs écrans affichent les caméras
        var screens = new List<Renderer>();
        var screenMat = CctvScreenMat();
        // Baie métallique contre le mur est : 2 étagères de 3 moniteurs
        var rack = Mat("Baie_Metal", new Color(0.16f, 0.17f, 0.18f), 0.45f);
        float rx = r.xMax - 0.38f, rz = r.center.y + 0.6f, rw = 2.3f, rd = 0.6f;
        float[] rows = { y + 0.72f, y + 1.32f };
        foreach (var sy in new[] { rows[0], rows[1], y + 1.92f })
            Cube(L.props, "Baie_Etagere", new Vector3(rx, sy - 0.02f, rz), new Vector3(rd, 0.04f, rw), Quaternion.identity, rack, true, _layerFurniture);
        foreach (var dz in new[] { -rw * 0.5f + 0.03f, rw * 0.5f - 0.03f })
            foreach (var dx in new[] { -rd * 0.5f + 0.03f, rd * 0.5f - 0.03f })
                Cube(L.props, "Baie_Montant", new Vector3(rx + dx, y + 0.96f, rz + dz), new Vector3(0.05f, 1.92f, 0.05f), Quaternion.identity, rack, false, -1);
        for (int row = 0; row < 2; row++)
            for (int k = 0; k < 3; k++)
            {
                float z = rz + (k - 1) * 0.74f;
                var tv = CaveProp(L, "television_02", new Vector3(rx, rows[row], z), -90f, new Vector3(0.55f, 0.52f, 0.68f));
                if (tv == null || !TryBounds(tv, out var b)) continue;
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "Ecran_CCTV";
                quad.transform.SetParent(L.props, false);
                // Face avant (vers -X, la pièce) : légèrement devant la caisse
                quad.transform.SetPositionAndRotation(new Vector3(b.min.x - 0.004f, b.center.y + b.size.y * 0.04f, b.center.z), Quaternion.Euler(0f, 90f, 0f));
                quad.transform.localScale = new Vector3(b.size.z * 0.62f, b.size.y * 0.55f, 1f);
                Object.DestroyImmediate(quad.GetComponent<Collider>());
                var qr = quad.GetComponent<Renderer>();
                qr.sharedMaterial = screenMat;
                qr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                screens.Add(qr);
            }
        var l = PointLight(L, new Vector3(r.xMax - 1f, y + 1.3f, r.center.y + 0.6f), 4f);
        l.color = new Color(0.55f, 0.75f, 1f); l.intensity = 0.5f;

        var sys = new GameObject("Systeme_CCTV");
        sys.transform.SetParent(L.props, false);
        sys.transform.position = new Vector3(r.center.x, y + 1f, r.center.y);
        var cctv = sys.AddComponent<CctvSystem>();
        cctv.screens = screens.ToArray();
        _cctvSystem = cctv;
        Plaque(L, "POSTE DE SURVEILLANCE", new Vector3(r.center.x, y + 2.45f, CorrN - Wall * 0.5f - 0.02f), Vector3.back);
        CaveView(L, "Vue_Surveillance", new Vector3(r.xMin + 0.5f, y, r.yMin + 0.6f), new Vector3(r.xMax, y + 0.9f, r.center.y + 0.6f));
        Marker(L, "Poste_de_Surveillance", new Vector3(r.center.x, y, r.center.y), Color.cyan);
    }

    static CctvSystem _cctvSystem;

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

    // ───────────────────────── Façades de l'aile (extérieur) ─────────────────────────

    static void BuildWingFacade(Transform ext, Material brick, float fy0)
    {
        var west = new List<Hole>(); var east = new List<Hole>(); var north = new List<Hole>();
        foreach (var lv in _levels.Values)
        {
            if (lv.kind != Kind.Dorm) continue;
            west.AddRange(WingWinHoles(lv, WingWestWinZ));
            east.AddRange(WingWinHoles(lv, WingEastWinZ));
            north.AddRange(WingWinHoles(lv, WingNorthWinX));
        }
        WallAlongZ(ext, "Facade_Aile_Ouest", WX0 - 0.2f, WZ0, WZ1 + 0.3f, fy0, FacadeTop, brick, west.ToArray());
        WallAlongZ(ext, "Facade_Aile_Est", WX1 + 0.2f, WZ0, WZ1 + 0.3f, fy0, FacadeTop, brick, east.ToArray());
        WallAlongX(ext, "Facade_Aile_Nord", WZ1 + 0.2f, WX0 - 0.3f, WX1 + 0.3f, fy0, FacadeTop, brick, north.ToArray());
    }

    static Rect WingFootprint => Rect.MinMaxRect(WX0 - 0.4f, 5.0f, WX1 + 0.4f, WZ1 + 0.4f);
}
