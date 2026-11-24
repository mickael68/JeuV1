using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Studios étudiants : grand lit avec couette, armoire, bureau + chaise de bureau, table de nuit,
/// kitchenette (évier, frigo, micro-ondes), et une fenêtre sur le mur extérieur.
/// Les meubles viennent du "Furniture Mega Pack - Free" (Asset Store, non versionné) ;
/// s'il est absent, on retombe sur les modèles Poly Haven / Kenney.
/// </summary>
public static partial class EchoesMapGenerator
{
    const string Mega = "Assets/Furniture Mega Pack/Prefabs/";
    static readonly string[] MegaBeds = { "Beds/Bed01", "Beds/Bed02", "Beds/Bed03", "Beds/Bed04", "Beds/Bed05", "Beds/Bed11", "Beds/Bed12", "Beds/Bed13", "Beds/Bed14" };
    static readonly string[] MegaClosets = { "Closets/Closet01", "Closets/Closet02", "Closets/Closet04", "Closets/Closet06", "Closets/Closet09", "Closets/Closet11" };
    static readonly string[] MegaDesks = { "Tables/Table43", "Tables/Table44", "Tables/Table48", "Tables/Table49", "Tables/Table31" };
    static readonly string[] MegaChairs = { "Chairs/Chair31", "Chairs/Chair32", "Chairs/Chair33", "Chairs/Chair07", "Chairs/Chair09" };
    static readonly string[] MegaArmchairs = { "Chairs/Chair11", "Chairs/Chair12" };
    static readonly string[] MegaNightstands = { "Drawers/Drawer11", "Drawers/Drawer12", "Drawers/Drawer13", "Drawers/Drawer14" };
    static readonly string[] MegaSinks = { "Kitchen/CabinetB_Sink", "Kitchen/CabinetA_Sink", "Kitchen/CabinetE_Sink" };
    static readonly string[] MegaCabinets = { "Kitchen/CabinetB01", "Kitchen/CabinetA01" };
    static readonly string[] MegaFridges = { "Kitchen/Refrigerator01", "Kitchen/Refrigerator04" };
    static readonly string[] MegaMicrowaves = { "Kitchen/MicrowaveOven01", "Kitchen/MicrowaveOven03" };

    /// Correction d'orientation des prefabs du Mega Pack (face avant attendue vers +Z).
    static readonly Dictionary<string, float> MegaYaw = new Dictionary<string, float>
    {
    };

    // Fenêtres des studios
    const float WinW = 1.4f, WinSill = 0.9f, WinTop = 2.2f;

    /// Centres X des fenêtres d'une rangée de studios (pas de fenêtre au palier de l'ascenseur).
    static List<float> StudioWindowXs(bool north)
    {
        var xs = new List<float>();
        for (int k = 0; k < 10; k++)
        {
            if (!north && k == ElevRoomIndex) continue;
            if (north && k == WingRoomIndex) continue; // passage vers l'aile nord
            xs.Add(-HalfX + 4f * k + 2f);
        }
        return xs;
    }

    static Hole[] StudioWindowHoles(LevelDef L, bool north)
    {
        if (L.kind != Kind.Dorm) return new Hole[0];
        var list = new List<Hole>();
        foreach (var x in StudioWindowXs(north)) list.Add(new Hole(x, WinW, L.y + WinSill, L.y + WinTop));
        if (north) list.Add(new Hole(-HalfX + 4f * WingRoomIndex + 2f, 3f, L.y, L.y + DoorH + 0.2f)); // passage vers l'aile
        return list.ToArray();
    }

    /// Cadre, croisillon, vitre et appui de fenêtre dans le mur extérieur (zWall = plan intérieur du mur).
    static void BuildStudioWindows(LevelDef L, bool north, float zWall)
    {
        if (L.kind != Kind.Dorm) return;
        var frame = Mat("Fenetre_Cadre", new Color(0.92f, 0.92f, 0.9f), 0.4f);
        var glass = GlassMat();
        var parent = Child(L.root, north ? "Fenetres_Nord" : "Fenetres_Sud");
        float o = north ? 1f : -1f;            // vers l'extérieur
        float zc = zWall + o * 0.2f;           // milieu (mur intérieur + façade)
        float y0 = L.y + WinSill, y1 = L.y + WinTop, h = y1 - y0;
        foreach (var x in StudioWindowXs(north))
        {
            float t = 0.07f, d = 0.12f;
            Cube(parent, "Cadre", new Vector3(x, y0 + t * 0.5f, zc), new Vector3(WinW, t, d), Quaternion.identity, frame, true, _layerWalls);
            Cube(parent, "Cadre", new Vector3(x, y1 - t * 0.5f, zc), new Vector3(WinW, t, d), Quaternion.identity, frame, true, _layerWalls);
            Cube(parent, "Cadre", new Vector3(x - WinW * 0.5f + t * 0.5f, (y0 + y1) * 0.5f, zc), new Vector3(t, h, d), Quaternion.identity, frame, true, _layerWalls);
            Cube(parent, "Cadre", new Vector3(x + WinW * 0.5f - t * 0.5f, (y0 + y1) * 0.5f, zc), new Vector3(t, h, d), Quaternion.identity, frame, true, _layerWalls);
            Cube(parent, "Croisillon", new Vector3(x, (y0 + y1) * 0.5f, zc), new Vector3(0.05f, h, d * 0.8f), Quaternion.identity, frame, false, -1);
            Cube(parent, "Vitre", new Vector3(x, (y0 + y1) * 0.5f, zc), new Vector3(WinW - 0.1f, h - 0.1f, 0.012f), Quaternion.identity, glass, true, _layerWalls)
                .GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // Appui intérieur
            Cube(parent, "Appui", new Vector3(x, y0 - 0.02f, zWall - o * 0.1f), new Vector3(WinW + 0.2f, 0.04f, 0.22f), Quaternion.identity, frame, true, _layerFurniture);
        }
    }

    static Material GlassMat()
    {
        var m = Mat("Vitre", new Color(0.55f, 0.65f, 0.7f, 0.22f), 0.95f);
        m.SetColor("_BaseColor", new Color(0.55f, 0.65f, 0.7f, 0.22f));
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        EditorUtility.SetDirty(m);
        return m;
    }

    /// Rend un meuble utilisable comme siège (E pour s'asseoir / se lever).
    static void AddSeat(GameObject go, float height, float facingYaw, Vector3 offset, string label)
    {
        if (go == null) return;
        if (go.GetComponentInChildren<Collider>() == null) return;
        var seat = go.AddComponent<Seat>();
        seat.seatHeight = height;
        seat.facingYaw = facingYaw;
        seat.offset = offset;
        seat.label = label;
    }

    // ───────────────────────── Aménagement d'un studio ─────────────────────────

    /// Pose un meuble du Mega Pack (au hasard parmi "options") ; sinon le modèle de repli (Kenney → Poly Haven).
    static GameObject Piece(LevelDef L, string[] options, string fallback, Vector3 floorPos, float rotY, Vector3 fit, Material fb, bool collider = true)
    {
        string pick = options[_rng.Next(options.Length)];
        string path = Mega + pick + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
        {
            rotY += MegaYaw.TryGetValue(pick, out var fix) ? fix : 180f; // les meubles du pack regardent vers -Z
            var go = InstantiateAt(L, path, floorPos, rotY, fit);
            if (go != null && collider && go.GetComponentInChildren<Collider>() == null && TryBounds(go, out var b))
            {
                var bc = go.AddComponent<BoxCollider>();
                bc.center = go.transform.InverseTransformPoint(b.center);
                var ls = go.transform.lossyScale;
                bc.size = Quaternion.Inverse(go.transform.rotation) * b.size;
                bc.size = new Vector3(Mathf.Abs(bc.size.x / ls.x), Mathf.Abs(bc.size.y / ls.y), Mathf.Abs(bc.size.z / ls.z));
            }
            return go;
        }
        var alt = fallback != null ? Model(L, fallback, floorPos, rotY, fit, fb, collider) : null;
        if (alt != null && alt.name.StartsWith("old_bed_frame")) AddMattress(L, alt); // sommier Poly Haven nu
        return alt;
    }

    /// Studio de 4 m × 6 m. backNorth : mur du fond (fenêtre) au nord ; mirror : résidence inversée (-5).
    static void FurnishStudio(LevelDef L, Rect r, bool backNorth, bool mirror, bool deskOnCeiling, bool playerRoom)
    {
        var mp = Mat("Meuble_" + L.number, Color.Lerp(L.wall, L.floor, 0.5f) * 0.9f);
        float back = backNorth ? r.yMax : r.yMin;
        float s = backNorth ? -1f : 1f;              // du fond vers le couloir
        float sideL = mirror ? r.xMax : r.xMin, sx = mirror ? -1f : 1f;
        float face = backNorth ? 180f : 0f;           // face avant tournée vers le couloir
        float y = L.y;
        Vector3 P(float u, float d, float up = 0f) => new Vector3(sideL + sx * u, y + up, back + s * (d + 0.1f));
        float faceRight = face + 90f * sx * s;        // dos au mur de gauche, face vers la droite
        float faceLeft = face - 90f * sx * s;         // dos au mur de droite

        // Lit double, tête contre le mur du fond, le long du mur de gauche (on s'assoit au bord, face à la pièce)
        var bed = Piece(L, MegaBeds, KFurniture + "bedSingle", P(0.85f, 1.05f), face, new Vector3(1.6f, 1.1f, 2.15f), mp);
        AddSeat(bed, 0.5f, faceRight, new Vector3(sx * 0.45f, 0f, 0f), "S'asseoir sur le lit");
        Piece(L, MegaNightstands, KFurniture + "sideTable", P(1.95f, 0.25f), face, new Vector3(0.5f, 0.6f, 0.45f), mp);

        // Bureau contre le mur de droite + chaise de bureau
        var desk = Piece(L, MegaDesks, KFurniture + "desk", P(3.55f, 1.2f), faceLeft, new Vector3(0.7f, 0.78f, 1.4f), mp);
        if (playerRoom) PlaceItem(L, "montre", TopOf(desk, P(3.55f, 1.2f) + Vector3.up * 0.78f), Rand(0f, 360f));
        if (desk != null && deskOnCeiling)
            desk.transform.RotateAround(new Vector3(P(3.55f, 1.2f).x, y + L.h * 0.5f, P(3.55f, 1.2f).z), Vector3.right, 180f);
        var chair = Piece(L, MegaChairs, KFurniture + "chairDesk", P(2.85f, 1.2f), faceRight, new Vector3(0.65f, 1.1f, 0.65f), mp);
        AddSeat(chair, 0.48f, faceRight, Vector3.zero, "S'asseoir");

        // Grande armoire contre le mur de droite, côté couloir
        Piece(L, MegaClosets, KFurniture + "bookcaseClosedDoors", P(3.65f, 3.35f), faceLeft, new Vector3(0.62f, 2.1f, 1.5f), mp);

        // Kitchenette contre le mur de gauche, côté couloir : meuble, évier, frigo
        Piece(L, MegaCabinets, null, P(0.32f, 3.25f), faceRight, new Vector3(0.62f, 0.9f, 0.62f), mp);
        Piece(L, MegaSinks, null, P(0.32f, 3.9f), faceRight, new Vector3(0.62f, 0.9f, 0.62f), mp);
        Piece(L, MegaFridges, null, P(0.35f, 4.6f), faceRight, new Vector3(0.66f, 1.75f, 0.66f), mp);
        Piece(L, MegaMicrowaves, null, P(0.3f, 3.25f, 0.9f), faceRight, new Vector3(0.36f, 0.3f, 0.5f), mp, false);

        // Salle de bains côté couloir, à droite de l'entrée (u 2,7 → 4,0 ; d 4,3 → couloir), porte vers l'entrée
        {
            float xa = sideL + sx * 2.7f, xb = sideL + sx * 4.0f; // dégage la porte du studio (u 1,45 → 2,55)
            float za = back + s * 4.4f, zc = back + s * 6.0f;
            var bath = Rect.MinMaxRect(Mathf.Min(xa, xb), Mathf.Min(za, zc), Mathf.Max(xa, xb), Mathf.Max(za, zc));
            SmallBathroom(L, bath, new Vector3(sx, 0f, 0f), 0f, xa < xb, xa > xb, za < zc, za > zc);
        }

        // Poubelle sous le bureau (pièce dégagée au centre)
        if (_rng.NextDouble() < 0.6)
            Model(L, KFurniture + "trashcan", P(3.6f, 2.15f), face, new Vector3(0.35f, 0.5f, 0.35f), mp, false);
    }
}
