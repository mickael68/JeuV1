using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Génère la map "greybox" verticale de The Echoes Below dans Assets/Scenes/Main.unity.
///
///   Étages 1-3 : résidence habitée (couloir + chambres)
///   Étage 0    : aile à moitié abandonnée, sans lumière
///   -1 : sous-sol ordinaire       -2 : locaux techniques
///   -3 : anciens tunnels          -4 : archives inondées
///   -5 : résidence inversée       -6 : le fond
///
/// Les cages d'escalier alternent Est / Ouest pour obliger à traverser chaque étage.
/// Menu : The Echoes Below > Générer la map (greybox). Relancer régénère uniquement "ECHOES_MAP".
/// </summary>
public static class EchoesMapGenerator
{
    // ───────────────────────── Réglages ─────────────────────────
    const string ScenePath = "Assets/Scenes/Main.unity";
    const string NavMeshPath = "Assets/Scenes/Main_NavMesh.asset";
    const string MatFolder = "Assets/Materials/Greybox";
    const string RootName = "ECHOES_MAP";
    const int Seed = 666;

    const float HalfX = 20f;      // empreinte de chaque étage : x ∈ [-20, 20]
    const float Wall = 0.2f;      // épaisseur des murs
    const float Slab = 0.3f;      // épaisseur des dalles
    const float DoorW = 1.4f, DoorH = 2.2f;
    const float DoorZ = -3f;      // toutes les portes de cage d'escalier sont à z = -3

    // Disposition "couloir" (dortoirs, sous-sol)
    const float CorrS = -4f, CorrN = -1f, RoomDepth = 6f;

    // Disposition "grille" (technique, tunnels, archives, fond) : cellules de 2 m
    const int GridNI = 20, GridNJ = 10;
    const float Cell = 2f, GridZMin = -10f;
    const int DoorJ = 3;          // la cellule j=3 couvre z ∈ [-4, -2] → porte à z = -3

    // Cage d'escalier
    const float ShaftW = 5f, Landing = 1.6f, MaxRise = 0.2f;

    enum Kind { Dorm, Abandoned, Maze, Basement, Technical, Tunnels, Archives, Inverted, Abyss }

    // Labyrinthe de l'étage 0 : nœuds de 4 m, x ∈ [-40, 40], z ∈ [-30, 26]
    const float MazeP = 4f, MazeX0 = -40f, MazeZ0 = -30f;
    const int MazeNX = 20, MazeNZ = 14;
    const int MazeWestDoorI = 5, MazeEastDoorI = 15, MazeDoorJ = 6; // portes à x = -20 et x = 20, z ∈ [-6, -2]

    // Modèles Kenney déjà présents dans le projet
    const string KFurniture = "Assets/Models/Environment/Outside/kenney_furniture-kit/Models/FBX format/";
    const string KSurvival = "Assets/Models/Environment/Outside/kenney_survival-kit/Models/FBX format/";
    const string KGraveyard = "Assets/Models/Environment/Outside/kenney_graveyard-kit/Models/FBX format/";
    enum Side { East, West }

    class LevelDef
    {
        public int number; public string name; public Kind kind;
        public float y, h;
        public Color wall, floor;
        public bool overrideAmbience = true;
        public Color ambient; public float fog;
        public Color lightColor; public float lightIntensity; public int lightCount; public float flicker;
        public int danger;
        public bool doorEast, doorWest;
        public Transform root, props, markers;
        public float halfX = HalfX, zMin, zMax;   // emprise réelle (renseignée à la construction)
        public List<Vector3> patrol;              // ronde personnalisée (sinon couloir central)
    }

    class ShaftDef
    {
        public Side side; public int[] levels; // du plus bas au plus haut
        public ShaftDef(Side s, params int[] l) { side = s; levels = l; }
    }

    struct Hole
    {
        public float c, w, y0, y1;
        public Hole(float c, float w, float y0, float y1) { this.c = c; this.w = w; this.y0 = y0; this.y1 = y1; }
    }

    static Dictionary<string, Material> _mats;
    static Dictionary<string, GameObject> _models;
    static Dictionary<int, LevelDef> _levels;
    static System.Random _rng;
    static int _layerGround, _layerWalls, _layerFurniture, _layerNpc;

    // ───────────────────────── Définition des étages ─────────────────────────
    static List<LevelDef> DefineLevels()
    {
        Color C(float r, float g, float b) => new Color(r, g, b);
        var dormWall = C(0.82f, 0.80f, 0.74f); var dormFloor = C(0.45f, 0.40f, 0.35f);
        var warm = C(1f, 0.92f, 0.78f);

        return new List<LevelDef>
        {
            new LevelDef { number = 3, name = "Etage 3 - Chambres", kind = Kind.Dorm, y = 10.5f, h = 3.2f, wall = dormWall, floor = dormFloor,
                overrideAmbience = false, lightColor = warm, lightIntensity = 1.6f, danger = 0 },
            new LevelDef { number = 2, name = "Etage 2 - Chambres", kind = Kind.Dorm, y = 7f, h = 3.2f, wall = dormWall, floor = dormFloor,
                overrideAmbience = false, lightColor = warm, lightIntensity = 1.6f, danger = 0 },
            new LevelDef { number = 1, name = "Etage 1 - Chambres (joueur)", kind = Kind.Dorm, y = 3.5f, h = 3.2f, wall = dormWall, floor = dormFloor,
                overrideAmbience = false, lightColor = warm, lightIntensity = 1.6f, danger = 0 },
            // h = 3.1 : le plafond (3.1 → 3.4) ne se superpose pas exactement au sol de l'étage 1 (3.2 → 3.5)
            new LevelDef { number = 0, name = "Etage 0 - Labyrinthe abandonne (tutoriel)", kind = Kind.Maze, y = 0f, h = 3.1f,
                wall = C(0.55f, 0.53f, 0.50f), floor = C(0.30f, 0.28f, 0.26f),
                ambient = C(0.035f, 0.035f, 0.045f), fog = 0.04f, danger = 1 },
            new LevelDef { number = -1, name = "-1 Sous-sol ordinaire", kind = Kind.Basement, y = -4.5f, h = 3.7f,
                wall = C(0.62f, 0.62f, 0.60f), floor = C(0.40f, 0.40f, 0.40f),
                ambient = C(0.06f, 0.06f, 0.065f), fog = 0.03f, lightColor = C(0.85f, 0.95f, 1f), lightIntensity = 1.3f, flicker = 0.15f, danger = 1 },
            new LevelDef { number = -2, name = "-2 Locaux techniques", kind = Kind.Technical, y = -10f, h = 4.7f,
                wall = C(0.45f, 0.44f, 0.42f), floor = C(0.28f, 0.27f, 0.26f),
                ambient = C(0.04f, 0.035f, 0.03f), fog = 0.04f, lightColor = C(1f, 0.6f, 0.3f), lightIntensity = 1.4f, lightCount = 7, flicker = 0.4f, danger = 2 },
            new LevelDef { number = -3, name = "-3 Anciens tunnels", kind = Kind.Tunnels, y = -14.5f, h = 3.7f,
                wall = C(0.36f, 0.33f, 0.30f), floor = C(0.22f, 0.20f, 0.18f),
                ambient = C(0.02f, 0.02f, 0.02f), fog = 0.06f, lightColor = C(0.9f, 0.85f, 0.6f), lightIntensity = 1.0f, lightCount = 5, flicker = 0.6f, danger = 3 },
            new LevelDef { number = -4, name = "-4 Archives inondees", kind = Kind.Archives, y = -20f, h = 4.7f,
                wall = C(0.30f, 0.32f, 0.36f), floor = C(0.18f, 0.19f, 0.21f),
                ambient = C(0.015f, 0.02f, 0.03f), fog = 0.07f, lightColor = C(0.5f, 0.7f, 1f), lightIntensity = 0.9f, lightCount = 5, flicker = 0.5f, danger = 3 },
            new LevelDef { number = -5, name = "-5 Residence inversee", kind = Kind.Inverted, y = -24f, h = 3.2f,
                wall = C(0.55f, 0.36f, 0.36f), floor = C(0.25f, 0.15f, 0.15f),
                ambient = C(0.025f, 0.01f, 0.01f), fog = 0.08f, lightColor = C(1f, 0.25f, 0.2f), lightIntensity = 1.1f, flicker = 0.7f, danger = 4 },
            new LevelDef { number = -6, name = "-6 Le fond", kind = Kind.Abyss, y = -33f, h = 8.2f,
                wall = C(0.18f, 0.14f, 0.14f), floor = C(0.10f, 0.08f, 0.08f),
                ambient = C(0.01f, 0.004f, 0.004f), fog = 0.1f, lightColor = C(0.8f, 0.1f, 0.1f), lightIntensity = 1.8f, lightCount = 4, flicker = 0.3f, danger = 5 },
        };
    }

    static List<ShaftDef> DefineShafts() => new List<ShaftDef>
    {
        new ShaftDef(Side.West, 0, 1, 2, 3), // escalier principal de la résidence
        new ShaftDef(Side.East, -1, 0),      // escalier condamné vers le sous-sol
        new ShaftDef(Side.West, -2, -1),
        new ShaftDef(Side.East, -3, -2),
        new ShaftDef(Side.West, -4, -3),
        new ShaftDef(Side.East, -5, -4),
        new ShaftDef(Side.West, -6, -5),
    };

    // ───────────────────────── Point d'entrée ─────────────────────────
    [MenuItem("The Echoes Below/Générer la map (greybox)")]
    public static void Generate()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EnsureFolder("Assets/Scenes");
        EnsureFolder("Assets/Materials");
        EnsureFolder(MatFolder);

        Scene scene = File.Exists(ScenePath)
            ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
            : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var old = GameObject.Find(RootName);
        if (old != null) Object.DestroyImmediate(old);

        _mats = new Dictionary<string, Material>();
        _models = new Dictionary<string, GameObject>();
        _rng = new System.Random(Seed);
        _layerGround = Layer("Ground"); _layerWalls = Layer("Walls");
        _layerFurniture = Layer("Furniture"); _layerNpc = Layer("NPC");

        try
        {
            var root = new GameObject(RootName).transform;
            var levels = DefineLevels();
            var shafts = DefineShafts();
            _levels = new Dictionary<int, LevelDef>();
            foreach (var l in levels) _levels[l.number] = l;

            foreach (var s in shafts)
                foreach (var n in s.levels)
                {
                    if (s.side == Side.East) _levels[n].doorEast = true; else _levels[n].doorWest = true;
                }

            for (int i = 0; i < levels.Count; i++)
            {
                var L = levels[i];
                EditorUtility.DisplayProgressBar("The Echoes Below", "Construction : " + L.name, (float)i / levels.Count);
                BuildLevel(root, L);
            }

            var shaftRoot = new GameObject("Cages_Escalier").transform;
            shaftRoot.SetParent(root, false);
            foreach (var s in shafts) BuildShaft(shaftRoot, s);

            BuildActors(root);
            EnsureSunPlayerAndSystems();

            EditorUtility.DisplayProgressBar("The Echoes Below", "Calcul du NavMesh…", 0.95f);
            BakeNavMesh(root);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuild();
        AssetDatabase.SaveAssets();

        Debug.Log("[EchoesMap] Map générée dans " + ScenePath + " — 10 niveaux, 7 cages d'escalier.");
    }

    // ───────────────────────── Étages ─────────────────────────
    static void BuildLevel(Transform root, LevelDef L)
    {
        var lvl = new GameObject($"Etage_{L.number}  [{L.name}]").transform;
        lvl.SetParent(root, false);
        L.root = lvl;
        L.props = Child(lvl, "Props");
        L.markers = Child(lvl, "Marqueurs");

        float zMin, zMax;
        switch (L.kind)
        {
            case Kind.Dorm:
            case Kind.Abandoned:
            case Kind.Inverted:
                zMin = CorrS - RoomDepth; zMax = CorrN + RoomDepth;
                BuildCorridorLevel(L, zMin, zMax, Repeat(4f, 10), Repeat(4f, 10));
                break;
            case Kind.Basement:
                zMin = CorrS - RoomDepth; zMax = CorrN + RoomDepth;
                BuildCorridorLevel(L, zMin, zMax, new[] { 12f, 8f, 6f, 14f }, Repeat(4f, 10));
                break;
            case Kind.Maze:
                BuildMazeLevel(L);
                zMin = L.zMin; zMax = L.zMax;
                break;
            default:
                zMin = GridZMin; zMax = GridZMin + GridNJ * Cell;
                BuildGridLevel(L);
                break;
        }

        // Volume de l'étage : ambiance + "sur quel étage est le joueur"
        var zoneGo = new GameObject("Zone_Etage");
        zoneGo.transform.SetParent(lvl, false);
        var box = zoneGo.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = new Vector3(0f, L.y + L.h * 0.5f, (zMin + zMax) * 0.5f);
        box.size = new Vector3(L.halfX * 2f, L.h, zMax - zMin);
        var zone = zoneGo.AddComponent<FloorZone>();
        zone.floorNumber = L.number;
        zone.displayName = L.name;
        zone.dangerLevel = L.danger;
        zone.overrideAmbience = L.overrideAmbience;
        zone.ambientColor = L.ambient;
        zone.fogColor = L.ambient * 0.6f;
        zone.fogDensity = L.fog;
    }

    static void Slabs(LevelDef L, float zMin, float zMax)
    {
        var mf = Mat("Sol_" + L.number, L.floor);
        var mc = Mat("Plafond_" + L.number, L.wall * 0.8f);
        Box(L.root, "Sol", new Vector3(-HalfX - 0.1f, L.y - Slab, zMin - 0.1f), new Vector3(HalfX + 0.1f, L.y, zMax + 0.1f), mf, _layerGround);
        Box(L.root, "Plafond", new Vector3(-HalfX - 0.1f, L.y + L.h, zMin - 0.1f), new Vector3(HalfX + 0.1f, L.y + L.h + Slab, zMax + 0.1f), mc, _layerWalls);
    }

    static Hole[] ShaftDoor(LevelDef L, bool hasDoor) =>
        hasDoor ? new[] { new Hole(DoorZ, DoorW, L.y, L.y + DoorH) } : new Hole[0];

    // ── Disposition couloir : chambres au sud et au nord d'un couloir est-ouest ──
    static void BuildCorridorLevel(LevelDef L, float zMin, float zMax, float[] south, float[] north)
    {
        var mw = Mat("Mur_" + L.number, L.wall);
        float y0 = L.y, y1 = L.y + L.h;
        Slabs(L, zMin, zMax);

        WallAlongX(L.root, "Mur_Sud", zMin, -HalfX, HalfX, y0, y1, mw);
        WallAlongX(L.root, "Mur_Nord", zMax, -HalfX, HalfX, y0, y1, mw);
        WallAlongZ(L.root, "Mur_Est", HalfX, zMin, zMax, y0, y1, mw, ShaftDoor(L, L.doorEast));
        WallAlongZ(L.root, "Mur_Ouest", -HalfX, zMin, zMax, y0, y1, mw, ShaftDoor(L, L.doorWest));

        BuildRoomRow(L, south, false, mw);
        BuildRoomRow(L, north, true, mw);

        // Éclairage du couloir
        float[] xs = L.kind == Kind.Inverted ? new[] { -12f, 4f, 16f } : new[] { -16f, -8f, 0f, 8f, 16f };
        if (L.kind != Kind.Abandoned)
            foreach (var x in xs) PointLight(L, new Vector3(x, y1 - 0.4f, (CorrS + CorrN) * 0.5f), 9f);

        // Marqueurs propres à certains étages
        float zc = (CorrS + CorrN) * 0.5f;
        if (L.kind == Kind.Abandoned)
        {
            Marker(L, "Acces_Sous-sol (escalier condamne)", new Vector3(HalfX - 1.5f, y0, zc), Color.yellow);
            Marker(L, "Entree_Nocturne", new Vector3(-HalfX + 1.5f, y0, zc), Color.yellow);
        }
        if (L.kind == Kind.Inverted)
        {
            Marker(L, "Doppelganger_Spawn", new Vector3(10f, y0, zc), Color.red);
            Marker(L, "Anomalie_Boucle_Couloir_Est", new Vector3(HalfX - 2f, y0, zc + 1f), Color.magenta);
            Marker(L, "Anomalie_Boucle_Couloir_Ouest", new Vector3(-HalfX + 2f, y0, zc + 1f), Color.magenta);
        }
        if (L.kind == Kind.Basement)
            Marker(L, "Premier_Indice", new Vector3(-14f, y0, zMax - 2f), Color.yellow);
    }

    static void BuildRoomRow(LevelDef L, float[] widths, bool north, Material mw)
    {
        float zc = north ? CorrN : CorrS;
        float zFar = north ? CorrN + RoomDepth : CorrS - RoomDepth;
        float zLo = Mathf.Min(zc, zFar), zHi = Mathf.Max(zc, zFar);
        var holes = new List<Hole>();
        float x = -HalfX;

        for (int k = 0; k < widths.Length; k++)
        {
            float w = widths[k];
            float doorW = L.kind == Kind.Basement && w > 6f ? 1.8f : 1.1f;
            if (L.kind == Kind.Abandoned && _rng.NextDouble() < 0.3) doorW = 2.4f; // porte arrachée
            holes.Add(new Hole(x + w * 0.5f, doorW, L.y, L.y + DoorH - 0.1f));

            if (k < widths.Length - 1)
            {
                float top = L.y + L.h;
                if (L.kind == Kind.Abandoned && _rng.NextDouble() < 0.25) top = L.y + 1.1f; // cloison effondrée
                WallAlongZ(L.root, "Cloison", x + w, zLo, zHi, L.y, top, mw);
            }

            var room = Rect.MinMaxRect(x, zLo, x + w, zHi);
            Furnish(L, room, north, k, widths.Length);
            x += w;
        }

        WallAlongX(L.root, north ? "Mur_Couloir_Nord" : "Mur_Couloir_Sud", zc, -HalfX, HalfX, L.y, L.y + L.h, mw, holes.ToArray());
    }

    static void Furnish(LevelDef L, Rect r, bool north, int idx, int count)
    {
        var mp = Mat("Meuble_" + L.number, Color.Lerp(L.wall, L.floor, 0.5f) * 0.9f);
        float back = north ? r.yMax : r.yMin;  // mur du fond (z)
        float s = north ? -1f : 1f;           // direction vers le couloir
        float y = L.y;

        switch (L.kind)
        {
            case Kind.Dorm:
            case Kind.Abandoned:
            case Kind.Inverted:
            {
                bool mirror = L.kind == Kind.Inverted;
                FurnishBedroom(L, r, north, mirror, L.kind == Kind.Abandoned, mirror);

                int playerRoom = 4, invertedRoom = count - 1 - 4;
                if (north && L.number == 1 && idx == playerRoom)
                    Marker(L, "Chambre_Joueur", new Vector3(r.center.x, y, r.center.y), Color.cyan);
                if (north && L.kind == Kind.Inverted && idx == invertedRoom)
                    Marker(L, "Chambre_Joueur_Inversee", new Vector3(r.center.x, y, r.center.y), Color.red);
                break;
            }
            case Kind.Basement when !north:
            {
                float face = north ? 180f : 0f;
                if (idx == 0) // buanderie
                {
                    int k = 0;
                    for (float x = r.xMin + 1f; x < r.xMax - 0.8f; x += 1.1f, k++)
                        Model(L, KFurniture + (k % 3 == 2 ? "dryer" : "washer"), new Vector3(x, y, back + s * 0.5f), face, new Vector3(0.75f, 0.9f, 0.75f), mp);
                    Model(L, KFurniture + "table", new Vector3(r.center.x, y, back + s * 3.2f), face, new Vector3(2.5f, 0.9f, 1f), mp);
                    Model(L, KFurniture + "trashcan", new Vector3(r.xMax - 0.6f, y, back + s * 4.5f), face, new Vector3(0.5f, 0.8f, 0.5f), mp);
                }
                else if (idx == 1) // local vélos (pas de modèle : silhouettes simples)
                {
                    for (float x = r.xMin + 0.8f; x < r.xMax - 0.5f; x += 0.7f)
                        Prop(L, "Velo", new Vector3(x, y + 0.5f, back + s * 1.2f), new Vector3(0.08f, 1f, 1.8f), mp);
                }
                else if (idx == 2) // local poubelles
                {
                    for (int i = 0; i < 3; i++)
                        Model(L, KFurniture + "trashcan", new Vector3(r.xMin + 1f + i * 1.6f, y, back + s * 0.9f), face, new Vector3(1.2f, 1.3f, 1.2f), mp);
                }
                else // stockage
                {
                    for (float x = r.xMin + 1.5f; x < r.xMax - 1f; x += 2.2f)
                        Model(L, KFurniture + "bookcaseOpen", new Vector3(x, y, back + s * 0.4f), face, new Vector3(2f, 2f, 0.5f), mp);
                    ScatterBoxes(L, r, 4);
                }
                break;
            }
            case Kind.Basement: // caves au nord
                ScatterBoxes(L, Rect.MinMaxRect(r.xMin + 0.4f, north ? back - 3.5f : back + 0.4f, r.xMax - 0.4f, north ? back - 0.4f : back + 3.5f), _rng.Next(2, 6));
                if (_rng.NextDouble() < 0.3)
                    Model(L, KSurvival + "barrel", new Vector3(r.xMax - 0.7f, y, back + s * 0.7f), 0f, new Vector3(0.7f, 1f, 0.7f), mp);
                break;
        }
    }

    // ── Chambre meublée (lit, bureau + chaise, armoire, tapis…) ──
    // backNorth : le mur du fond est au nord ; mirror : meubles inversés gauche/droite ;
    // wreck : chambre abandonnée ; deskOnCeiling : résidence inversée.
    static GameObject FurnishBedroom(LevelDef L, Rect r, bool backNorth, bool mirror, bool wreck, bool deskOnCeiling)
    {
        var mp = Mat("Meuble_" + L.number, Color.Lerp(L.wall, L.floor, 0.5f) * 0.9f);
        float back = backNorth ? r.yMax : r.yMin;
        float s = backNorth ? -1f : 1f;
        float face = backNorth ? 180f : 0f;
        float sideL = mirror ? r.xMax : r.xMin, sideR = mirror ? r.xMin : r.xMax;
        float sx = mirror ? -1f : 1f;
        float y = L.y;

        Furn(L, KFurniture + "bedSingle", new Vector3(sideL + sx * 0.6f, y, back + s * 1.15f), face, new Vector3(1f, 0.7f, 2.1f), mp, wreck);
        Furn(L, KFurniture + "sideTable", new Vector3(sideL + sx * 1.45f, y, back + s * 0.35f), face, new Vector3(0.5f, 0.6f, 0.45f), mp, wreck);

        var deskPos = new Vector3(sideR - sx * 0.4f, y, back + s * 1.0f);
        var desk = Furn(L, KFurniture + "desk", deskPos, face + 90f * sx, new Vector3(0.6f, 0.8f, 1.3f), mp, wreck);
        if (desk != null && deskOnCeiling)
            desk.transform.RotateAround(new Vector3(deskPos.x, y + L.h * 0.5f, deskPos.z), Vector3.right, 180f);
        Furn(L, KFurniture + "chairDesk", new Vector3(sideR - sx * 1.05f, y, back + s * 1.0f), face - 90f * sx, new Vector3(0.55f, 1f, 0.55f), mp, wreck);

        var wardrobe = Furn(L, KFurniture + "bookcaseClosedDoors", new Vector3(sideR - sx * 0.35f, y, back + s * 2.6f), face + 90f * sx, new Vector3(0.6f, 2f, 1.1f), mp, wreck);
        Furn(L, KFurniture + "rugRectangle", new Vector3(r.center.x, y, back + s * 3.2f), face, new Vector3(1.6f, 0.02f, 1.1f), mp, wreck, false);
        if (!wreck && _rng.NextDouble() < 0.5)
            Furn(L, KFurniture + "trashcan", new Vector3(sideR - sx * 0.35f, y, back + s * 1.95f), face, new Vector3(0.35f, 0.5f, 0.35f), mp, false);

        if (wreck)
        {
            ScatterBoxes(L, r, _rng.Next(0, 3));
            for (int i = 0, n = _rng.Next(1, 4); i < n; i++)
                Model(L, KGraveyard + (_rng.NextDouble() < 0.5 ? "debris" : "debris-wood"),
                    new Vector3(Rand(r.xMin + 0.6f, r.xMax - 0.6f), y, Rand(r.yMin + 0.6f, r.yMax - 0.6f)), Rand(0, 360), new Vector3(0.8f, 0.4f, 0.8f), mp);
        }
        return wardrobe;
    }

    // Meuble éventuellement "abîmé" : disparu, tourné, ou renversé.
    static GameObject Furn(LevelDef L, string path, Vector3 floorPos, float rotY, Vector3 fit, Material fallback, bool wreck, bool collider = true)
    {
        if (wreck && _rng.NextDouble() < 0.3) return null;
        var go = Model(L, path, floorPos, rotY + (wreck ? Rand(-25f, 25f) : 0f), fit, fallback, collider);
        if (wreck && go != null && _rng.NextDouble() < 0.25)
        {
            go.transform.Rotate(Rand(70f, 90f), 0f, 0f, Space.Self); // renversé
            Ground(go, floorPos);
        }
        return go;
    }

    static void ScatterBoxes(LevelDef L, Rect r, int count)
    {
        var mp = Mat("Meuble_" + L.number, Color.Lerp(L.wall, L.floor, 0.5f) * 0.9f);
        string[] boxes = { KFurniture + "cardboardBoxClosed", KFurniture + "cardboardBoxOpen", KSurvival + "box", KSurvival + "box-open" };
        for (int i = 0; i < count; i++)
        {
            float sz = Rand(0.45f, 0.8f);
            var p = new Vector3(Rand(r.xMin + 0.5f, r.xMax - 0.5f), L.y, Rand(r.yMin + 0.5f, r.yMax - 0.5f));
            var box = Model(L, boxes[_rng.Next(boxes.Length)], p, Rand(0f, 360f), new Vector3(sz, sz, sz), mp);
            if (box != null && _rng.NextDouble() < 0.3) // carton empilé
                Model(L, boxes[_rng.Next(boxes.Length)], p + Vector3.up * sz * 0.95f, Rand(0f, 360f), new Vector3(sz * 0.8f, sz * 0.8f, sz * 0.8f), mp);
        }
    }

    // ── Placement d'un modèle (FBX) mis à l'échelle pour tenir dans "fit" (emprise monde x/z, hauteur y) ──
    // Pose le modèle au sol en floorPos ; s'il manque, un cube de la même taille le remplace.
    static GameObject Model(LevelDef L, string path, Vector3 floorPos, float rotY, Vector3 fit, Material fallback, bool collider = true)
    {
        var asset = LoadModel(path);
        if (asset == null)
            return Cube(L.props, Path.GetFileName(path), floorPos + Vector3.up * fit.y * 0.5f, fit, Quaternion.Euler(0, rotY, 0), fallback, collider, _layerFurniture);

        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, L.props);
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        go.transform.localScale = Vector3.one;
        if (!TryBounds(go, out var b))
        {
            Object.DestroyImmediate(go);
            return Cube(L.props, Path.GetFileName(path), floorPos + Vector3.up * fit.y * 0.5f, fit, Quaternion.Euler(0, rotY, 0), fallback, collider, _layerFurniture);
        }

        // Oriente le grand côté du modèle comme le grand côté demandé
        float snapped = Mathf.Round(rotY / 90f) * 90f;
        bool swapped = Mathf.Abs(Mathf.Repeat(snapped, 180f) - 90f) < 1f;
        float wx = swapped ? b.size.z : b.size.x, wz = swapped ? b.size.x : b.size.z;
        if ((fit.x > fit.z * 1.15f && wz > wx * 1.15f) || (fit.z > fit.x * 1.15f && wx > wz * 1.15f))
        {
            rotY += 90f;
            (wx, wz) = (wz, wx);
        }

        float scale = Mathf.Min(fit.x / Mathf.Max(wx, 0.01f), fit.z / Mathf.Max(wz, 0.01f));
        if (fit.y > 0.05f) scale = Mathf.Min(scale, fit.y / Mathf.Max(b.size.y, 0.01f) * 1.25f);

        go.name = Path.GetFileName(path);
        go.transform.localScale = Vector3.one * scale;
        go.transform.rotation = Quaternion.Euler(0, rotY, 0);
        Ground(go, floorPos);

        if (collider)
        {
            var bc = go.AddComponent<BoxCollider>();
            bc.center = b.center;
            bc.size = b.size;
        }
        SetLayerRecursive(go, _layerFurniture);
        return go;
    }

    // Repose l'objet pour que le bas de ses bounds soit sur floorPos (centré en x/z).
    static void Ground(GameObject go, Vector3 floorPos)
    {
        if (!TryBounds(go, out var b)) return;
        go.transform.position += floorPos - new Vector3(b.center.x, b.min.y, b.center.z);
    }

    static bool TryBounds(GameObject go, out Bounds b)
    {
        b = default;
        bool any = false;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }
        return any && b.size.sqrMagnitude > 1e-6f;
    }

    static GameObject LoadModel(string path)
    {
        if (_models == null) _models = new Dictionary<string, GameObject>();
        if (_models.TryGetValue(path, out var m)) return m;
        m = AssetDatabase.LoadAssetAtPath<GameObject>(path + ".fbx");
        if (m == null) Debug.LogWarning("[EchoesMap] Modèle introuvable, remplacé par un cube : " + path);
        _models[path] = m;
        return m;
    }

    static void SetLayerRecursive(GameObject go, int layer)
    {
        foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
    }

    // ───────────────────────── Étage 0 : grand labyrinthe (tutoriel) ─────────────────────────
    // Labyrinthe "parfait" (un seul chemin) creusé par backtracking, puis quelques boucles,
    // un hall d'entrée, des chambres abandonnées, des impasses avec des notes et des
    // veilleuses de secours vertes comme seuls repères lumineux.
    static void BuildMazeLevel(LevelDef L)
    {
        L.halfX = -MazeX0;
        L.zMin = MazeZ0;
        L.zMax = MazeZ0 + MazeNZ * MazeP;
        var mw = Mat("Mur_" + L.number, L.wall);
        var mf = Mat("Sol_" + L.number, L.floor);
        var mc = Mat("Plafond_" + L.number, L.wall * 0.8f);
        var mp = Mat("Meuble_" + L.number, Color.Lerp(L.wall, L.floor, 0.5f) * 0.9f);

        var open = new bool[MazeNX, MazeNZ];
        for (int i = 0; i < MazeNX; i++)
            for (int j = 0; j < MazeNZ; j++)
                open[i, j] = true;
        // Emplacements des cages d'escalier (fermés)
        for (int i = 3; i <= 4; i++) for (int j = 6; j <= 8; j++) open[i, j] = false;
        for (int i = 15; i <= 16; i++) for (int j = 6; j <= 8; j++) open[i, j] = false;

        bool Open(int i, int j) => i >= 0 && i < MazeNX && j >= 0 && j < MazeNZ && open[i, j];
        var east = new bool[MazeNX, MazeNZ];   // passage entre (i,j) et (i+1,j)
        var north = new bool[MazeNX, MazeNZ];  // passage entre (i,j) et (i,j+1)
        void Link(int i, int j, int ni, int nj)
        {
            if (ni > i) east[i, j] = true; else if (ni < i) east[ni, j] = true;
            else if (nj > j) north[i, j] = true; else north[i, nj] = true;
        }

        // 1) Backtracking depuis la porte ouest (arrivée depuis les dortoirs)
        var visited = new bool[MazeNX, MazeNZ];
        var stack = new Stack<Vector2Int>();
        stack.Push(new Vector2Int(MazeWestDoorI, MazeDoorJ));
        visited[MazeWestDoorI, MazeDoorJ] = true;
        var dirs = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
        var choices = new List<Vector2Int>();
        while (stack.Count > 0)
        {
            var c = stack.Peek();
            choices.Clear();
            foreach (var d in dirs)
            {
                var n = c + d;
                if (Open(n.x, n.y) && !visited[n.x, n.y]) choices.Add(n);
            }
            if (choices.Count == 0) { stack.Pop(); continue; }
            var next = choices[_rng.Next(choices.Count)];
            Link(c.x, c.y, next.x, next.y);
            visited[next.x, next.y] = true;
            stack.Push(next);
        }

        // 2) Hall d'entrée 3×3 devant la porte ouest
        var special = new int[MazeNX, MazeNZ]; // 0 couloir, 1 hall, 2+ chambre
        for (int i = 5; i <= 7; i++)
            for (int j = 5; j <= 7; j++)
            {
                special[i, j] = 1;
                if (i < 7) east[i, j] = true;
                if (j < 7) north[i, j] = true;
            }

        // 3) Chambres abandonnées 2×2 (8 × 8 m)
        var rooms = new List<RectInt>();
        for (int tries = 0; tries < 400 && rooms.Count < 8; tries++)
        {
            int i = _rng.Next(MazeNX - 1), j = _rng.Next(MazeNZ - 1);
            bool ok = true;
            for (int a = i - 1; a <= i + 2 && ok; a++)
                for (int b = j - 1; b <= j + 2 && ok; b++)
                {
                    bool inside = a >= i && a <= i + 1 && b >= j && b <= j + 1;
                    if (inside && !Open(a, b)) ok = false;
                    if (a >= 0 && a < MazeNX && b >= 0 && b < MazeNZ && special[a, b] != 0) ok = false; // marge d'un nœud
                }
            if (ok && (Mathf.Abs(i - (MazeEastDoorI - 1)) + Mathf.Abs(j - MazeDoorJ) < 3)) ok = false;
            if (!ok) continue;

            int id = rooms.Count + 2;
            special[i, j] = special[i + 1, j] = special[i, j + 1] = special[i + 1, j + 1] = id;
            east[i, j] = east[i, j + 1] = north[i, j] = north[i + 1, j] = true;
            rooms.Add(new RectInt(i, j, 2, 2));
        }

        // 4) Quelques boucles pour éviter un labyrinthe trop "parfait"
        for (int i = 0; i < MazeNX; i++)
            for (int j = 0; j < MazeNZ; j++)
            {
                if (!Open(i, j)) continue;
                if (Open(i + 1, j) && !east[i, j] && _rng.NextDouble() < 0.08) east[i, j] = true;
                if (Open(i, j + 1) && !north[i, j] && _rng.NextDouble() < 0.08) north[i, j] = true;
            }

        // 5) Sol et plafond par bandes (pas au-dessus des cages d'escalier)
        float y0 = L.y, y1 = L.y + L.h;
        for (int j = 0; j < MazeNZ; j++)
        {
            int start = -1;
            for (int i = 0; i <= MazeNX; i++)
            {
                bool o = i < MazeNX && open[i, j];
                if (o && start < 0) start = i;
                if (!o && start >= 0)
                {
                    float xa = MazeX0 + start * MazeP, xb = MazeX0 + i * MazeP, za = MazeZ0 + j * MazeP, zb = za + MazeP;
                    Box(L.root, "Sol", new Vector3(xa, y0 - Slab, za), new Vector3(xb, y0, zb), mf, _layerGround);
                    Box(L.root, "Plafond", new Vector3(xa, y1, za), new Vector3(xb, y1 + Slab, zb), mc, _layerWalls);
                    start = -1;
                }
            }
        }

        // 6) Murs (fusionnés en segments), portes des cages d'escalier
        for (int j = 0; j <= MazeNZ; j++)
        {
            float z = MazeZ0 + j * MazeP;
            int start = -1;
            for (int i = 0; i <= MazeNX; i++)
            {
                bool need = false;
                if (i < MazeNX)
                {
                    bool a = Open(i, j - 1), b = Open(i, j);
                    need = a && b ? !north[i, j - 1] : a || b;
                }
                if (need && start < 0) start = i;
                if (!need && start >= 0)
                {
                    Box(L.root, "Mur", new Vector3(MazeX0 + start * MazeP - Wall / 2, y0, z - Wall / 2), new Vector3(MazeX0 + i * MazeP + Wall / 2, y1, z + Wall / 2), mw, _layerWalls);
                    start = -1;
                }
            }
        }
        for (int i = 0; i <= MazeNX; i++)
        {
            float x = MazeX0 + i * MazeP;
            int start = -1;
            for (int j = 0; j <= MazeNZ; j++)
            {
                bool need = false, door = false;
                if (j < MazeNZ)
                {
                    bool a = Open(i - 1, j), b = Open(i, j);
                    need = a && b ? !east[i - 1, j] : a || b;
                    door = j == MazeDoorJ && (i == MazeWestDoorI || i == MazeEastDoorI);
                    if (door)
                    {
                        WallAlongZ(L.root, "Mur_Porte_Escalier", x, MazeZ0 + j * MazeP, MazeZ0 + (j + 1) * MazeP, y0, y1, mw,
                            new Hole(DoorZ, DoorW, y0, y0 + DoorH));
                        need = false;
                    }
                }
                if (need && start < 0) start = j;
                if (!need && start >= 0)
                {
                    Box(L.root, "Mur", new Vector3(x - Wall / 2, y0, MazeZ0 + start * MazeP - Wall / 2), new Vector3(x + Wall / 2, y1, MazeZ0 + j * MazeP + Wall / 2), mw, _layerWalls);
                    start = -1;
                }
            }
        }

        Vector3 Node(int i, int j) => new Vector3(MazeX0 + (i + 0.5f) * MazeP, y0, MazeZ0 + (j + 0.5f) * MazeP);
        int Degree(int i, int j) =>
            (east[i, j] ? 1 : 0) + (i > 0 && east[i - 1, j] ? 1 : 0) + (north[i, j] ? 1 : 0) + (j > 0 && north[i, j - 1] ? 1 : 0);

        // 7) Chambres : deux lits face à face, une armoire sert de cachette (tutoriel)
        for (int k = 0; k < rooms.Count; k++)
        {
            var rm = rooms[k];
            float xa = MazeX0 + rm.x * MazeP, za = MazeZ0 + rm.y * MazeP;
            var left = Rect.MinMaxRect(xa + 0.3f, za + 1.8f, xa + 4f, za + 7.7f);
            var right = Rect.MinMaxRect(xa + 4f, za + 1.8f, xa + 7.7f, za + 7.7f);
            var wardrobe = FurnishBedroom(L, left, true, false, true, false);
            FurnishBedroom(L, right, true, true, true, false);
            ScatterBoxes(L, Rect.MinMaxRect(xa + 0.6f, za + 0.6f, xa + 7.4f, za + 1.8f), _rng.Next(1, 4));
            if (k == 0)
                Marker(L, "Tuto_Cachette (armoire)", wardrobe != null ? wardrobe.transform.position : Node(rm.x, rm.y), Color.yellow);
        }

        // 8) Hall : premières consignes du tutoriel
        var hall = Node(6, 6);
        Marker(L, "Entree_Nocturne (arrivee des dortoirs)", Node(5, 6) + new Vector3(-1.2f, 0, 0), Color.yellow);
        Marker(L, "Tuto_LampeTorche", hall, Color.yellow);
        Marker(L, "Tuto_Discretion (bruit de pas)", Node(7, 5), Color.yellow);
        Model(L, KFurniture + "bench", Node(6, 7) + new Vector3(0, 0, 1.4f), 180f, new Vector3(1.8f, 0.5f, 0.5f), mp);
        Model(L, KFurniture + "coatRackStanding", Node(7, 7) + new Vector3(1.4f, 0, 1.4f), 0f, new Vector3(0.5f, 1.8f, 0.5f), mp);
        Model(L, KFurniture + "pottedPlant", Node(5, 5) + new Vector3(-1.4f, 0, -1.4f), 0f, new Vector3(0.6f, 1.2f, 0.6f), mp);
        ScatterBoxes(L, new Rect(hall.x + 1f, hall.z - 5.5f, 3f, 2f), 3);
        Marker(L, "Acces_Sous-sol (escalier condamne)", Node(MazeEastDoorI - 1, MazeDoorJ) + new Vector3(1.2f, 0, 0), Color.yellow);

        // 9) Couloirs : encombrement, impasses avec notes
        string[] clutter = { KFurniture + "chair", KFurniture + "chairCushion", KFurniture + "trashcan", KFurniture + "cardboardBoxOpen",
                             KSurvival + "barrel", KGraveyard + "debris", KGraveyard + "debris-wood", KFurniture + "bookcaseOpenLow" };
        int notes = 0;
        for (int i = 0; i < MazeNX; i++)
            for (int j = 0; j < MazeNZ; j++)
            {
                if (!open[i, j] || special[i, j] != 0) continue;
                if (j == MazeDoorJ && (i == MazeWestDoorI || i == MazeEastDoorI - 1)) continue;
                var c = Node(i, j);
                var off = new Vector3(Rand(-1.2f, 1.2f), 0, Rand(-1.2f, 1.2f));

                if (Degree(i, j) == 1 && notes < 10 && _rng.NextDouble() < 0.55)
                {
                    notes++;
                    Model(L, KFurniture + "sideTableDrawers", c + off, Rand(0, 360), new Vector3(0.6f, 0.7f, 0.5f), mp);
                    Marker(L, $"Note_{notes:00} (indice / lore)", c + off + Vector3.up * 0.75f, Color.yellow);
                    continue;
                }

                double roll = _rng.NextDouble();
                if (roll < 0.2) ScatterBoxes(L, new Rect(c.x + off.x - 0.6f, c.z + off.z - 0.6f, 1.2f, 1.2f), _rng.Next(1, 3));
                else if (roll < 0.5)
                {
                    var go = Model(L, clutter[_rng.Next(clutter.Length)], c + off, Rand(0, 360), new Vector3(0.8f, 1f, 0.8f), mp);
                    if (go != null && _rng.NextDouble() < 0.35) { go.transform.Rotate(90f, 0, 0, Space.Self); Ground(go, c + off); }
                }
                else if (roll < 0.58) // dalle de faux plafond tombée
                    Cube(L.props, "Dalle_Plafond_Tombee", c + off + Vector3.up * 0.15f, new Vector3(1.2f, 0.04f, 0.6f),
                        Quaternion.Euler(Rand(-25f, 25f), Rand(0, 360), Rand(-10f, 10f)), mc, true, _layerFurniture);
            }

        // 10) Seules lumières : veilleuses de secours vertes (l'électricité est coupée)
        var green = Mat("Veilleuse_Secours", new Color(0.2f, 1f, 0.35f), 0.8f);
        var exitLight = new LevelDef { lightColor = new Color(0.25f, 1f, 0.4f), lightIntensity = 0.5f, flicker = 0.5f, root = L.root };
        var signNodes = new List<Vector2Int> { new Vector2Int(MazeWestDoorI, MazeDoorJ), new Vector2Int(MazeEastDoorI - 1, MazeDoorJ) };
        for (int tries = 0; tries < 300 && signNodes.Count < 12; tries++)
        {
            var n = new Vector2Int(_rng.Next(MazeNX), _rng.Next(MazeNZ));
            if (!open[n.x, n.y] || special[n.x, n.y] >= 2) continue;
            bool far = true;
            foreach (var o in signNodes) if (Mathf.Abs(o.x - n.x) + Mathf.Abs(o.y - n.y) < 4) { far = false; break; }
            if (far) signNodes.Add(n);
        }
        foreach (var n in signNodes)
        {
            var p = Node(n.x, n.y);
            Cube(L.props, "Veilleuse_Secours", new Vector3(p.x, y1 - 0.25f, p.z), new Vector3(0.45f, 0.18f, 0.06f), Quaternion.identity, green, false, -1);
            PointLight(exitLight, new Vector3(p.x, y1 - 0.45f, p.z), 6f);
        }

        // 11) Ronde du surveillant à travers le labyrinthe
        L.patrol = new List<Vector3> { Node(7, 6), Node(10, 2), Node(13, 6), Node(10, 11) };
    }

    // ── Disposition grille : murs générés automatiquement autour des cellules praticables ──
    static void BuildGridLevel(LevelDef L)
    {
        bool[,] w = GridMask(L.kind);
        var mw = Mat("Mur_" + L.number, L.wall);
        Slabs(L, GridZMin, GridZMin + GridNJ * Cell);

        bool W(int i, int j) => i >= 0 && i < GridNI && j >= 0 && j < GridNJ && w[i, j];
        float y0 = L.y, y1 = L.y + L.h;

        // Murs parallèles à X (frontières entre lignes j)
        for (int jb = 0; jb <= GridNJ; jb++)
        {
            int start = -1;
            for (int i = 0; i <= GridNI; i++)
            {
                bool need = i < GridNI && W(i, jb - 1) != W(i, jb);
                if (need && start < 0) start = i;
                if (!need && start >= 0)
                {
                    float z = GridZMin + jb * Cell;
                    Box(L.root, "Mur", new Vector3(-HalfX + start * Cell, y0, z - Wall / 2), new Vector3(-HalfX + i * Cell, y1, z + Wall / 2), mw, _layerWalls);
                    start = -1;
                }
            }
        }

        // Murs parallèles à Z (frontières entre colonnes i), avec les portes des cages d'escalier
        for (int ib = 0; ib <= GridNI; ib++)
        {
            float x = -HalfX + ib * Cell;
            int start = -1;
            for (int j = 0; j <= GridNJ; j++)
            {
                bool door = j == DoorJ && ((ib == GridNI && L.doorEast) || (ib == 0 && L.doorWest));
                bool need = j < GridNJ && !door && W(ib - 1, j) != W(ib, j);
                if (door)
                    WallAlongZ(L.root, "Mur_Porte_Escalier", x, GridZMin + j * Cell, GridZMin + (j + 1) * Cell, y0, y1, mw,
                        new Hole(DoorZ, DoorW, y0, y0 + DoorH));
                if (need && start < 0) start = j;
                if (!need && start >= 0)
                {
                    Box(L.root, "Mur", new Vector3(x - Wall / 2, y0, GridZMin + start * Cell), new Vector3(x + Wall / 2, y1, GridZMin + j * Cell), mw, _layerWalls);
                    start = -1;
                }
            }
        }

        DecorateGridLevel(L, w);
        GridLights(L, w);
    }

    static Vector3 CellCenter(LevelDef L, int i, int j) => new Vector3(-HalfX + (i + 0.5f) * Cell, L.y, GridZMin + (j + 0.5f) * Cell);

    static bool[,] GridMask(Kind kind)
    {
        var w = new bool[GridNI, GridNJ];
        void Fill(int i0, int i1, int j0, int j1, bool v)
        {
            for (int i = i0; i <= i1; i++)
                for (int j = j0; j <= j1; j++)
                    if (i >= 0 && i < GridNI && j >= 0 && j < GridNJ) w[i, j] = v;
        }
        void Blob(float ci, float cj, float r)
        {
            for (int i = 0; i < GridNI; i++)
                for (int j = 0; j < GridNJ; j++)
                    if ((i - ci) * (i - ci) + (j - cj) * (j - cj) <= r * r) w[i, j] = true;
        }

        switch (kind)
        {
            case Kind.Technical: // grande salle avec des blocs de machinerie
                Fill(0, 19, 0, 9, true);
                Fill(4, 6, 6, 8, false);
                Fill(10, 12, 0, 1, false);
                Fill(14, 16, 6, 9, false);
                Fill(8, 9, 6, 9, false);
                break;

            case Kind.Tunnels: // tunnels de 2 m, avec impasses et une petite salle
                Fill(14, 19, 3, 3, true);  // entrée est
                Fill(14, 14, 0, 8, true);  // descente + impasse sud
                Fill(6, 14, 8, 8, true);   // galerie nord
                Fill(10, 10, 4, 8, true);  // impasse
                Fill(6, 6, 1, 8, true);
                Fill(2, 8, 1, 1, true);
                Fill(8, 10, 0, 2, true);   // salle des affaires disparues
                Fill(2, 2, 1, 3, true);
                Fill(0, 2, 3, 3, true);    // sortie ouest
                Fill(17, 18, 5, 7, true);  // recoin aveugle
                Fill(17, 17, 4, 4, true);
                break;

            case Kind.Archives: // salle d'archives avec zones effondrées
                Fill(0, 19, 0, 9, true);
                Fill(16, 19, 7, 9, false);
                Fill(0, 2, 7, 9, false);
                Fill(9, 10, 9, 9, false);
                break;

            case Kind.Abyss: // caverne organique
                Fill(0, 4, 3, 3, true); // couloir d'arrivée depuis l'ouest
                Blob(10f, 5f, 4.5f);
                Blob(5f, 3.5f, 2.6f);
                Blob(15f, 6.5f, 3.2f);
                Blob(14f, 2f, 2.4f);
                break;
        }
        return w;
    }

    static void DecorateGridLevel(LevelDef L, bool[,] w)
    {
        var mp = Mat("Meuble_" + L.number, Color.Lerp(L.wall, L.floor, 0.5f));
        var metal = Mat("Metal_" + L.number, new Color(0.35f, 0.33f, 0.3f));
        bool WalkAt(Vector3 p)
        {
            int i = Mathf.FloorToInt((p.x + HalfX) / Cell), j = Mathf.FloorToInt((p.z - GridZMin) / Cell);
            return i >= 0 && i < GridNI && j >= 0 && j < GridNJ && w[i, j];
        }
        float y = L.y, top = L.y + L.h;

        switch (L.kind)
        {
            case Kind.Technical:
                foreach (var px in new[] { -12f, -4f, 4f, 12f })
                    foreach (var pz in new[] { -6f, 6f })
                        if (WalkAt(new Vector3(px, 0, pz)))
                            Prop(L, "Pilier", new Vector3(px, y + L.h / 2, pz), new Vector3(0.6f, L.h, 0.6f), mp);
                foreach (var b in new[] { new Vector3(-15f, 0, 6f), new Vector3(-9f, 0, -7f), new Vector3(8f, 0, 6f) })
                    Cylinder(L.props, "Chaudiere", new Vector3(b.x, y + 1.5f, b.z), 1.2f, 3f, Quaternion.identity, metal, true);
                foreach (var pz in new[] { -8.5f, -0.5f, 4.5f })
                    Cylinder(L.props, "Tuyau", new Vector3(0f, top - 0.4f, pz), 0.15f, HalfX * 2f - 0.4f, Quaternion.Euler(0, 0, 90), metal, false);
                foreach (var p in new[] { new Vector3(-18f, 0, -9f), new Vector3(-17f, 0, -8.8f), new Vector3(16f, 0, -9f), new Vector3(2f, 0, 9f), new Vector3(3f, 0, 9.1f) })
                    Model(L, KSurvival + (_rng.NextDouble() < 0.3 ? "barrel-open" : "barrel"), new Vector3(p.x, y, p.z), Rand(0, 360), new Vector3(0.8f, 1.1f, 0.8f), mp);
                Model(L, KSurvival + "workbench", new Vector3(-2f, y, -9.2f), 0f, new Vector3(2f, 1f, 0.8f), mp);
                Model(L, KSurvival + "metal-panel-screws", new Vector3(12f, y, 9.6f), 180f, new Vector3(2f, 2.5f, 0.2f), metal);
                Marker(L, "Generateur_Panne", new Vector3(-16f, y, -1f), Color.yellow);
                break;

            case Kind.Tunnels:
            {
                var room = CellCenter(L, 9, 1);
                // Campement des étudiants disparus
                Prop(L, "Sac_A_Dos", room + new Vector3(-0.8f, 0.25f, 0.4f), new Vector3(0.4f, 0.5f, 0.3f), mp, 20f);
                Prop(L, "Carnet", room + new Vector3(0.5f, 0.02f, -0.3f), new Vector3(0.2f, 0.04f, 0.3f), mp, -35f);
                Model(L, KSurvival + "bedroll", room + new Vector3(1.4f, 0, 1f), 80f, new Vector3(0.9f, 0.3f, 2f), mp);
                Model(L, KSurvival + "bedroll-packed", room + new Vector3(-1.6f, 0, -1.2f), 10f, new Vector3(0.6f, 0.4f, 0.6f), mp);
                Model(L, KSurvival + "bottle", room + new Vector3(0.2f, 0, 1.6f), 0f, new Vector3(0.12f, 0.3f, 0.12f), mp);
                Model(L, KSurvival + "bucket", room + new Vector3(-1.8f, 0, 1.5f), 0f, new Vector3(0.4f, 0.4f, 0.4f), mp);
                Model(L, KGraveyard + "candle-multiple", room + new Vector3(0.6f, 0, 0.6f), 0f, new Vector3(0.3f, 0.3f, 0.3f), mp);
                Marker(L, "Indice_Disparus", room, Color.yellow);
                Marker(L, "Ombre_Furtive", CellCenter(L, 17, 6), Color.magenta);
                for (int i = 0; i < 18; i++)
                {
                    int ci = _rng.Next(GridNI), cj = _rng.Next(GridNJ);
                    if (!w[ci, cj] || cj == DoorJ) continue;
                    Model(L, KGraveyard + (_rng.NextDouble() < 0.5 ? "debris" : "debris-wood"),
                        CellCenter(L, ci, cj) + new Vector3(Rand(-0.6f, 0.6f), 0, Rand(-0.6f, 0.6f)), Rand(0, 360), new Vector3(0.9f, 0.5f, 0.9f), mp);
                }
                for (int i = 0; i < 6; i++)
                {
                    int ci = _rng.Next(GridNI), cj = _rng.Next(GridNJ);
                    if (!w[ci, cj] || cj == DoorJ) continue;
                    Model(L, KSurvival + "tool-shovel", CellCenter(L, ci, cj) + new Vector3(0.7f, 0, 0.3f), Rand(0, 360), new Vector3(0.3f, 1.2f, 0.3f), mp, false);
                }
                break;
            }

            case Kind.Archives:
            {
                foreach (var z in new[] { -8f, -6f, 0.5f, 3f, 5.5f })
                    foreach (var x in new[] { -14f, -5f, 5f, 14f })
                        if (WalkAt(new Vector3(x, 0, z)) && WalkAt(new Vector3(x - 3f, 0, z)) && WalkAt(new Vector3(x + 3f, 0, z)))
                        {
                            float lean = _rng.NextDouble() < 0.15 ? Rand(8f, 20f) : 0f; // rayonnage qui penche
                            for (float dx = -2.4f; dx <= 2.41f; dx += 1.6f)
                            {
                                var shelf = Model(L, KFurniture + "bookcaseClosedWide", new Vector3(x + dx, y, z), 0f, new Vector3(1.6f, 2.4f, 0.6f), mp);
                                if (shelf != null && lean > 0) shelf.transform.RotateAround(new Vector3(x + dx, y, z), Vector3.right, lean);
                            }
                            if (_rng.NextDouble() < 0.5)
                                ScatterBoxes(L, new Rect(x - 3f, z + 0.5f, 6f, 0.8f), _rng.Next(1, 4));
                        }
                var water = Cube(L.props, "Eau_Noire", new Vector3(0, y + 0.06f, 0), new Vector3(HalfX * 2f, 0.12f, GridNJ * Cell),
                    Quaternion.identity, Mat("Eau", new Color(0.02f, 0.03f, 0.05f), 0.9f), false, -1);
                water.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
                Prop(L, "Bureau_Archiviste", new Vector3(0f, y + 0.4f, 8f), new Vector3(2f, 0.8f, 1f), mp);
                Marker(L, "Dossiers_Secrets", new Vector3(0f, y, 7f), Color.yellow);
                Marker(L, "Silhouette_Immobile", new Vector3(12f, y, -8f), Color.magenta);
                break;
            }

            case Kind.Abyss:
            {
                var dark = Mat("Monolithe", new Color(0.03f, 0.02f, 0.02f), 0.6f);
                for (int i = 0; i < GridNI; i++)
                    for (int j = 0; j < GridNJ; j++)
                    {
                        if (!w[i, j] || (j == DoorJ && i < 6)) continue;
                        bool edge = i == 0 || j == 0 || i == GridNI - 1 || j == GridNJ - 1 || !w[i - 1, j] || !w[i + 1, j] || !w[i, j - 1] || !w[i, j + 1];
                        if (!edge || _rng.NextDouble() > 0.4) continue;
                        float sz = Rand(1.2f, 2.6f);
                        Model(L, KGraveyard + (_rng.NextDouble() < 0.4 ? "rocks-tall" : "rocks"),
                            CellCenter(L, i, j) + new Vector3(Rand(-0.5f, 0.5f), 0, Rand(-0.5f, 0.5f)), Rand(0, 360), new Vector3(sz, sz * 1.5f, sz), mp);
                    }
                var center = CellCenter(L, 10, 5);
                Cube(L.props, "Monolithe", center + new Vector3(0, 3f, 0), new Vector3(2f, 6f, 2f), Quaternion.Euler(0, 20, 4), dark, true, _layerFurniture);
                Model(L, KGraveyard + "altar-stone", center + new Vector3(0, 0, -2f), 0f, new Vector3(1.6f, 1f, 0.9f), mp);
                for (int k = 0; k < 7; k++)
                {
                    float a = k / 7f * Mathf.PI * 2f;
                    Model(L, KGraveyard + "candle-multiple", center + new Vector3(Mathf.Cos(a) * 3f, 0, Mathf.Sin(a) * 3f), Rand(0, 360), new Vector3(0.35f, 0.35f, 0.35f), mp, false);
                }
                foreach (var p in new[] { CellCenter(L, 14, 2), CellCenter(L, 6, 4), CellCenter(L, 16, 7) })
                    Model(L, KGraveyard + "coffin-old", p, Rand(0, 360), new Vector3(0.8f, 0.6f, 2f), mp);
                Marker(L, "Entite_Principale", center + new Vector3(0, 0, -2.5f), Color.red);
                Marker(L, "Skinwalker_Spawn", CellCenter(L, 15, 7), Color.red);
                break;
            }
        }
    }

    static void GridLights(LevelDef L, bool[,] w)
    {
        var placed = new List<Vector3>();
        // Toujours une lumière près de chaque porte pour se repérer
        if (L.doorEast) placed.Add(CellCenter(L, GridNI - 1, DoorJ));
        if (L.doorWest) placed.Add(CellCenter(L, 0, DoorJ));

        for (int tries = 0; tries < 400 && placed.Count < L.lightCount; tries++)
        {
            int i = _rng.Next(GridNI), j = _rng.Next(GridNJ);
            if (!w[i, j]) continue;
            var p = CellCenter(L, i, j);
            bool tooClose = false;
            foreach (var q in placed) if (Vector3.Distance(p, q) < 8f) { tooClose = true; break; }
            if (!tooClose) placed.Add(p);
        }

        float range = Mathf.Max(9f, L.h + 4f);
        foreach (var p in placed) PointLight(L, new Vector3(p.x, L.y + L.h - 0.4f, p.z), range);
    }

    // ───────────────────────── Cages d'escalier ─────────────────────────
    // Chaque paire d'étages : demi-volée montante vers le nord (moitié A), palier nord,
    // demi-volée montante vers le sud (moitié B), arrivée au palier sud de l'étage supérieur.
    static void BuildShaft(Transform parent, ShaftDef s)
    {
        var lv = new List<LevelDef>();
        foreach (var n in s.levels) lv.Add(_levels[n]);
        lv.Sort((a, b) => a.y.CompareTo(b.y));

        float run = 0f;
        for (int k = 0; k < lv.Count - 1; k++)
        {
            float half = (lv[k + 1].y - lv[k].y) * 0.5f;
            run = Mathf.Max(run, Mathf.Ceil(half / MaxRise) * 0.28f);
        }
        float depth = Landing * 2f + run;
        float z0 = DoorZ - Landing * 0.5f;

        bool east = s.side == Side.East;
        float x0 = east ? HalfX + 0.1f + Wall : -HalfX - 0.1f - Wall - ShaftW; // intérieur : [x0, x0 + ShaftW]
        float xBuilding = east ? HalfX + 0.1f + Wall / 2 : -HalfX - 0.1f - Wall / 2;
        float xOuter = east ? x0 + ShaftW + Wall / 2 : x0 - Wall / 2;

        var bottom = lv[0]; var topL = lv[lv.Count - 1];
        float yBot = bottom.y - Slab, yTop = topL.y + topL.h;

        var go = new GameObject($"Escalier_{(east ? "Est" : "Ouest")}_{bottom.number}_a_{topL.number}").transform;
        go.SetParent(parent, false);
        var mw = Mat("Escalier_Mur", new Color(0.5f, 0.5f, 0.48f));
        var ms = Mat("Escalier_Marche", new Color(0.33f, 0.32f, 0.3f));

        var doors = new List<Hole>();
        foreach (var L in lv) doors.Add(new Hole(DoorZ, DoorW, L.y, L.y + DoorH));
        WallAlongZ(go, "Mur_Cote_Batiment", xBuilding, z0 - Wall, z0 + depth + Wall, yBot, yTop, mw, doors.ToArray());
        WallAlongZ(go, "Mur_Exterieur", xOuter, z0 - Wall, z0 + depth + Wall, yBot, yTop, mw);
        WallAlongX(go, "Mur_Sud", z0 - Wall / 2, x0 - Wall, x0 + ShaftW + Wall, yBot, yTop, mw);
        WallAlongX(go, "Mur_Nord", z0 + depth + Wall / 2, x0 - Wall, x0 + ShaftW + Wall, yBot, yTop, mw);
        Box(go, "Fond", new Vector3(x0, yBot, z0), new Vector3(x0 + ShaftW, bottom.y, z0 + depth), ms, _layerGround);
        Box(go, "Toit", new Vector3(x0, yTop, z0), new Vector3(x0 + ShaftW, yTop + Slab, z0 + depth), mw, _layerWalls);

        float hw = (ShaftW - Wall) * 0.5f;
        float ax0 = x0, ax1 = x0 + hw;                     // moitié A (volée vers le nord)
        float bx0 = x0 + hw + Wall, bx1 = x0 + ShaftW;     // moitié B (volée vers le sud)
        float zs = z0 + Landing, zn = z0 + Landing + run;
        Box(go, "Mur_Central", new Vector3(ax1, yBot, zs), new Vector3(bx0, yTop, zn), mw, _layerWalls);

        foreach (var L in lv)
        {
            if (L != bottom)
                Box(go, "Palier_" + L.number, new Vector3(x0, L.y - Slab, z0), new Vector3(x0 + ShaftW, L.y, zs), ms, _layerGround);
            var lamp = PointLight(L, new Vector3(x0 + ShaftW * 0.5f, L.y + 2.6f, z0 + 0.8f), 7f, go);
            lamp.intensity *= 0.6f;
        }

        for (int k = 0; k < lv.Count - 1; k++)
        {
            var lo = lv[k]; var hi = lv[k + 1];
            float half = (hi.y - lo.y) * 0.5f;
            int n = Mathf.CeilToInt(half / MaxRise);
            float rise = half / n, tread = run / n;

            for (int i = 0; i < n; i++)
            {
                float t = lo.y + (i + 1) * rise;
                Box(go, "Marche", new Vector3(ax0, t - 0.3f, zs + i * tread), new Vector3(ax1, t, zs + (i + 1) * tread), ms, _layerGround);
            }
            Box(go, "Palier_Intermediaire", new Vector3(x0, lo.y + half - Slab, zn), new Vector3(x0 + ShaftW, lo.y + half, z0 + depth), ms, _layerGround);
            for (int j = 0; j < n; j++)
            {
                float t = lo.y + half + (j + 1) * rise;
                Box(go, "Marche", new Vector3(bx0, t - 0.3f, zn - (j + 1) * tread), new Vector3(bx1, t, zn - j * tread), ms, _layerGround);
            }
        }
    }

    // ───────────────────────── Acteurs, joueur, systèmes ─────────────────────────
    static void BuildActors(Transform root)
    {
        var actors = Child(root, "Acteurs");
        actors.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
        var body = Mat("Surveillant", new Color(0.1f, 0.12f, 0.25f));

        foreach (var n in new[] { 0, 1, 2, 3 })
        {
            var L = _levels[n];
            float zc = (CorrS + CorrN) * 0.5f;

            var route = new GameObject($"Ronde_Etage_{n}").transform;
            route.SetParent(L.markers, false);
            var points = L.patrol;
            if (points == null)
            {
                points = new List<Vector3>();
                foreach (var x in new[] { -16f, -6f, 6f, 16f }) points.Add(new Vector3(x, L.y, zc));
            }
            foreach (var p in points)
            {
                var wp = new GameObject("Point").transform;
                wp.SetParent(route, false);
                wp.position = p;
            }

            var staff = new GameObject($"Surveillant_Etage_{n}");
            staff.transform.SetParent(actors, false);
            staff.transform.position = L.patrol != null ? points[0] : new Vector3(-12f, L.y, zc);
            staff.layer = _layerNpc;
            var agent = staff.AddComponent<UnityEngine.AI.NavMeshAgent>();
            agent.radius = 0.35f; agent.height = 1.8f;
            var ai = staff.AddComponent<StaffAI>();
            var so = new SerializedObject(ai);
            so.FindProperty("patrolPointsParent").objectReferenceValue = route;
            so.ApplyModifiedPropertiesWithoutUndo();

            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "Corps";
            capsule.layer = _layerNpc;
            capsule.transform.SetParent(staff.transform, false);
            capsule.transform.localPosition = new Vector3(0, 0.9f, 0);
            capsule.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
            capsule.GetComponent<Renderer>().sharedMaterial = body;
        }
    }

    static void EnsureSunPlayerAndSystems()
    {
        var sun = GameObject.Find("Soleil");
        if (sun == null)
        {
            sun = new GameObject("Soleil");
            var l = sun.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 1.2f;
            l.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        if (GameObject.Find("Systemes") == null)
        {
            var sys = new GameObject("Systemes");
            sys.AddComponent<TimeManager>();
            var cycle = sys.AddComponent<DayNightCycle>();
            var so = new SerializedObject(cycle);
            var p = so.FindProperty("sunLight");
            if (p != null) { p.objectReferenceValue = sun.GetComponent<Light>(); so.ApplyModifiedPropertiesWithoutUndo(); }
        }

        if (GameObject.FindGameObjectWithTag("Player") == null)
        {
            var spawn = GameObject.Find("Chambre_Joueur");
            Vector3 pos = spawn != null ? spawn.transform.position + new Vector3(0.3f, 0.05f, -1f) : new Vector3(-2f, 3.6f, 1.5f);

            var player = new GameObject("Joueur") { tag = "Player" };
            player.transform.position = pos;
            player.transform.rotation = Quaternion.Euler(0, 180f, 0); // regarde vers la porte
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f; cc.radius = 0.35f; cc.center = new Vector3(0, 0.9f, 0);
            cc.stepOffset = 0.3f; cc.slopeLimit = 50f;

            var cam = new GameObject("Camera");
            cam.transform.SetParent(player.transform, false);
            cam.transform.localPosition = new Vector3(0, 1.6f, 0);
            var c = cam.AddComponent<Camera>();
            c.nearClipPlane = 0.05f;
            cam.AddComponent<AudioListener>();
            cam.tag = "MainCamera";

            player.AddComponent<PlayerController>();
        }
    }

    static void BakeNavMesh(Transform root)
    {
        var surface = root.gameObject.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.RenderMeshes;
        surface.BuildNavMesh();

        if (surface.navMeshData != null)
        {
            AssetDatabase.DeleteAsset(NavMeshPath);
            AssetDatabase.CreateAsset(surface.navMeshData, NavMeshPath);
        }
    }

    static void AddSceneToBuild()
    {
        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!list.Exists(s => s.path == ScenePath))
        {
            list.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }
    }

    // ───────────────────────── Primitives ─────────────────────────
    static GameObject Cube(Transform p, string name, Vector3 center, Vector3 size, Quaternion rot, Material m, bool collider, int layer)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(p, false);
        go.transform.SetPositionAndRotation(center, rot);
        go.transform.localScale = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
        go.GetComponent<Renderer>().sharedMaterial = m;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        if (layer >= 0) go.layer = layer;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
        return go;
    }

    static GameObject Box(Transform p, string name, Vector3 min, Vector3 max, Material m, int layer) =>
        Cube(p, name, (min + max) * 0.5f, max - min, Quaternion.identity, m, true, layer);

    static GameObject Prop(LevelDef L, string name, Vector3 center, Vector3 size, Material m, float rotY = 0f) =>
        Cube(L.props, name, center, size, Quaternion.Euler(0, rotY, 0), m, true, _layerFurniture);

    static GameObject Cylinder(Transform p, string name, Vector3 center, float radius, float height, Quaternion rot, Material m, bool collider)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(p, false);
        go.transform.SetPositionAndRotation(center, rot);
        go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
        go.GetComponent<Renderer>().sharedMaterial = m;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        go.layer = _layerFurniture;
        return go;
    }

    // Mur le long de X (plan z = const), découpé autour des ouvertures.
    static void WallAlongX(Transform p, string n, float z, float a, float b, float y0, float y1, Material m, params Hole[] holes) =>
        WallWithHoles(p, n, true, z, a, b, y0, y1, m, holes);

    // Mur le long de Z (plan x = const).
    static void WallAlongZ(Transform p, string n, float x, float a, float b, float y0, float y1, Material m, params Hole[] holes) =>
        WallWithHoles(p, n, false, x, a, b, y0, y1, m, holes);

    static void WallWithHoles(Transform p, string n, bool alongX, float fixedC, float a, float b, float y0, float y1, Material m, Hole[] holes)
    {
        var cuts = new List<float> { a, b };
        foreach (var h in holes)
        {
            cuts.Add(Mathf.Clamp(h.c - h.w / 2, a, b));
            cuts.Add(Mathf.Clamp(h.c + h.w / 2, a, b));
        }
        cuts.Sort();

        for (int i = 0; i < cuts.Count - 1; i++)
        {
            float s = cuts[i], e = cuts[i + 1];
            if (e - s < 0.01f) continue;
            float mid = (s + e) * 0.5f;

            var spans = new List<Vector2> { new Vector2(y0, y1) };
            foreach (var h in holes)
                if (mid > h.c - h.w / 2 && mid < h.c + h.w / 2)
                    spans = Subtract(spans, h.y0, h.y1);

            foreach (var sp in spans)
            {
                if (sp.y - sp.x < 0.01f) continue;
                Vector3 mn = alongX ? new Vector3(s, sp.x, fixedC - Wall / 2) : new Vector3(fixedC - Wall / 2, sp.x, s);
                Vector3 mx = alongX ? new Vector3(e, sp.y, fixedC + Wall / 2) : new Vector3(fixedC + Wall / 2, sp.y, e);
                Box(p, n, mn, mx, m, _layerWalls);
            }
        }
    }

    static List<Vector2> Subtract(List<Vector2> spans, float h0, float h1)
    {
        var r = new List<Vector2>();
        foreach (var s in spans)
        {
            if (h1 <= s.x || h0 >= s.y) { r.Add(s); continue; }
            if (h0 > s.x) r.Add(new Vector2(s.x, h0));
            if (h1 < s.y) r.Add(new Vector2(h1, s.y));
        }
        return r;
    }

    static Light PointLight(LevelDef L, Vector3 pos, float range, Transform parent = null)
    {
        var go = new GameObject("Lumiere");
        go.transform.SetParent(parent != null ? parent : L.root, false);
        go.transform.position = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = L.lightColor == default ? new Color(0.8f, 0.8f, 0.8f) : L.lightColor;
        l.intensity = L.lightIntensity > 0 ? L.lightIntensity : 0.8f;
        l.range = range;
        l.shadows = LightShadows.None;
        if (L.flicker > 0f)
        {
            var f = go.AddComponent<LightFlicker>();
            f.strength = L.flicker;
        }
        return l;
    }

    static void Marker(LevelDef L, string name, Vector3 feetPos, Color c)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "◆ " + name;
        go.transform.SetParent(L.markers, false);
        go.transform.position = feetPos + Vector3.up * 0.5f;
        go.transform.localScale = Vector3.one * 0.5f;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.GetComponent<Renderer>().sharedMaterial = Mat("Marqueur_" + ColorUtility.ToHtmlStringRGB(c), c);
        if (L.markers.GetComponent<NavMeshModifier>() == null)
            L.markers.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
        if (name == "Chambre_Joueur") go.name = "Chambre_Joueur"; // retrouvé par nom pour placer le joueur
    }

    // ───────────────────────── Utilitaires ─────────────────────────
    static Material Mat(string name, Color c, float smoothness = 0.1f)
    {
        if (_mats.TryGetValue(name, out var m)) return m;

        bool urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null;
        var shader = Shader.Find(urp ? "Universal Render Pipeline/Lit" : "Standard");
        string path = $"{MatFolder}/{name}.mat";
        m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
        }
        else if (m.shader != shader) m.shader = shader;

        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
        EditorUtility.SetDirty(m);
        _mats[name] = m;
        return m;
    }

    static Transform Child(Transform parent, string name)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    static float[] Repeat(float v, int n)
    {
        var a = new float[n];
        for (int i = 0; i < n; i++) a[i] = v;
        return a;
    }

    static float Rand(float a, float b) => a + (float)_rng.NextDouble() * (b - a);

    static int Layer(string name)
    {
        int l = LayerMask.NameToLayer(name);
        return l < 0 ? 0 : l;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
