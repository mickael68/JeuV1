using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Objets ramassables : création des fiches (Assets/Items/*.asset) et de leurs modèles
/// (Poly Haven pour la montre et le carnet, formes simples pour la pile, la clé et les feuilles),
/// puis placement dans la map.
/// </summary>
public static partial class EchoesMapGenerator
{
    const string ItemsFolder = "Assets/Items";
    static Dictionary<string, ItemData> _items;
    static int _notesPlaced, _batteriesPlaced0, _batteriesPlacedM1, _batteriesPlacedM2;

    static readonly string[] NoteTexts =
    {
        "Si tu lis ça, c'est que l'ascenseur t'a lâché toi aussi.\n\nNe crie pas. Le bruit porte loin ici, et ils écoutent.\n\nLes veilleuses vertes mènent quelque part. Pas forcément à la sortie.",
        "Jour 3.\n\nLes surveillants ne descendent jamais plus bas que le 0. Ils disent que c'est condamné.\n\nAlors pourquoi j'entends des pas sous mes pieds la nuit ?",
        "Liste des disparus (affichage retiré du hall) :\n\n– T. Morel, chambre 112\n– S. Benali, chambre 204\n– R. Lefèvre, chambre 318\n\nPersonne n'en parle. Leurs chambres ont été vidées en une nuit.",
        "Derrière les cartons, près de l'escalier muré, il y a une grille.\n\nOn peut passer accroupi. Après, ça descend.\n\nJ'y retourne demain avec une lampe qui marche.",
        "Ma mère m'a appelé depuis le couloir. Sa voix, exactement.\n\nMa mère est morte il y a deux ans.\n\nJe n'ai pas ouvert.",
        "Note du service technique :\n\nLe conduit d'aération du -2 n'est plus entretenu. Panneaux rouillés, risque d'effondrement. Les purges de vapeur se déclenchent toujours toutes les quelques secondes.\n\nNE PAS ENTRER.",
    };

    const string NotebookText =
        "Carnet de R. Lefèvre\n\n" +
        "12/10 — J'ai trouvé l'accès aux sous-sols. Tout le monde croit que ça s'arrête au -2.\n\n" +
        "15/10 — Le conduit sous les locaux techniques mène plus bas. Il faut ramper longtemps. Ne pas marcher sur les panneaux rouillés.\n\n" +
        "19/10 — Au fond il y a quelque chose qui imite les voix. Il connaît nos prénoms.\n\n" +
        "21/10 — J'ai caché ma clé de cave dans le vieux studio du labyrinthe, près du lit. Si je ne remonte pas, que quelqu'un vienne lire la suite ici.";

    // ───────────────────────── Fiches et modèles ─────────────────────────

    static void EnsureItems()
    {
        EnsureFolder(ItemsFolder);
        EnsureFolder(ItemsFolder + "/Prefabs");
        _items = new Dictionary<string, ItemData>();
        _notesPlaced = _batteriesPlaced0 = _batteriesPlacedM1 = _batteriesPlacedM2 = 0;

        var brass = Mat("Objet_Laiton", new Color(0.78f, 0.62f, 0.3f), 0.75f);
        brass.SetFloat("_Metallic", 0.9f);
        var paper = Mat("Objet_Papier", new Color(0.88f, 0.85f, 0.76f), 0.1f);
        var battBody = Mat("Objet_Pile", new Color(0.12f, 0.12f, 0.13f), 0.5f);
        var battLabel = Mat("Objet_Pile_Etiquette", new Color(0.95f, 0.55f, 0.1f), 0.4f);
        var battCap = Mat("Objet_Pile_Embout", new Color(0.75f, 0.75f, 0.78f), 0.8f);
        battCap.SetFloat("_Metallic", 0.9f);

        // Pile (taille D, couchée)
        var pile = SavePrefab("Pile", root =>
        {
            Part(root, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.034f, 0.03f, 0.034f), Quaternion.Euler(0, 0, 90), battBody);
            Part(root, PrimitiveType.Cylinder, new Vector3(0.012f, 0, 0), new Vector3(0.0345f, 0.012f, 0.0345f), Quaternion.Euler(0, 0, 90), battLabel);
            Part(root, PrimitiveType.Cylinder, new Vector3(0.033f, 0, 0), new Vector3(0.012f, 0.004f, 0.012f), Quaternion.Euler(0, 0, 90), battCap);
        }, 0.017f);

        // Clé (anneau, tige, dents)
        var cle = SavePrefab("Cle", root =>
        {
            Part(root, PrimitiveType.Cylinder, new Vector3(-0.035f, 0, 0), new Vector3(0.032f, 0.0015f, 0.032f), Quaternion.identity, brass);
            Part(root, PrimitiveType.Cube, new Vector3(0.008f, 0, 0), new Vector3(0.06f, 0.003f, 0.006f), Quaternion.identity, brass);
            Part(root, PrimitiveType.Cube, new Vector3(0.026f, 0, -0.006f), new Vector3(0.005f, 0.003f, 0.01f), Quaternion.identity, brass);
            Part(root, PrimitiveType.Cube, new Vector3(0.034f, 0, -0.005f), new Vector3(0.004f, 0.003f, 0.008f), Quaternion.identity, brass);
        }, 0.002f);

        // Feuille de papier pliée
        var feuille = SavePrefab("Feuille", root =>
        {
            Part(root, PrimitiveType.Cube, new Vector3(-0.052f, 0.004f, 0), new Vector3(0.105f, 0.0015f, 0.148f), Quaternion.Euler(0, 0, 4f), paper);
            Part(root, PrimitiveType.Cube, new Vector3(0.052f, 0.004f, 0), new Vector3(0.105f, 0.0015f, 0.148f), Quaternion.Euler(0, 0, -4f), paper);
        }, 0f);

        var montre = SavePolyHavenPrefab("Montre", "vintage_pocket_watch");
        var carnet = SavePolyHavenPrefab("Carnet", "binder_notebook");

        _items["pile"] = SaveItem("pile", "Pile", "Une grosse pile pour lampe torche. Encore chargée, apparemment.", ItemKind.Battery, pile, 0.12f, i => { i.stackable = true; i.maxStack = 4; i.batteryCharge = 1f; });
        _items["cle_cave"] = SaveItem("cle_cave", "Clé de cave", "Une petite clé en laiton. L'étiquette indique : « Cave R. Lefèvre — sous-sol ».", ItemKind.Key, cle, 0.13f, i => i.keyId = "cave_lefevre");
        _items["montre"] = SaveItem("montre", "Montre à gousset", "La vieille montre de ton grand-père. Elle avance un peu, mais elle marche.", ItemKind.Watch, montre, 0.11f, null);
        _items["carnet"] = SaveItem("carnet", "Carnet de R. Lefèvre", "Un carnet couvert de notes serrées. L'écriture devient de plus en plus nerveuse.", ItemKind.Note, carnet, 0.22f, i => i.noteText = NotebookText);
        for (int k = 0; k < NoteTexts.Length; k++)
        {
            string text = NoteTexts[k];
            _items["note_" + k] = SaveItem("note_" + k, "Feuille griffonnée", "Une feuille pliée, l'encre a bavé par endroits.", ItemKind.Note, feuille, 0.2f, i => i.noteText = text);
        }
        AssetDatabase.SaveAssets();
    }

    static void Part(Transform root, PrimitiveType type, Vector3 pos, Vector3 scale, Quaternion rot, Material m)
    {
        var p = GameObject.CreatePrimitive(type);
        p.transform.SetParent(root, false);
        p.transform.localPosition = pos;
        p.transform.localRotation = rot;
        p.transform.localScale = scale;
        Object.DestroyImmediate(p.GetComponent<Collider>());
        p.GetComponent<Renderer>().sharedMaterial = m;
    }

    /// Prefab fait de formes simples ; un collider (trigger) un peu plus grand pour viser facilement.
    static GameObject SavePrefab(string name, System.Action<Transform> build, float lift)
    {
        var root = new GameObject(name);
        var body = new GameObject("Modele").transform;
        body.SetParent(root.transform, false);
        body.localPosition = new Vector3(0, lift, 0);
        build(body);
        AddPickCollider(root);
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{ItemsFolder}/Prefabs/{name}.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    static GameObject SavePolyHavenPrefab(string name, string phId)
    {
        if (!_realisticPrepared) { PolyHavenModels.PrepareAll(false); _realisticPrepared = true; }
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(PolyHavenModels.FbxPath(phId));
        var root = new GameObject(name);
        if (fbx != null)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root.transform);
            inst.transform.localRotation = fbx.transform.localRotation;
            if (TryBounds(inst, out var b)) inst.transform.position += new Vector3(-b.center.x, -b.min.y, -b.center.z);
        }
        else Part(root.transform, PrimitiveType.Cube, new Vector3(0, 0.02f, 0), new Vector3(0.15f, 0.04f, 0.2f), Quaternion.identity, Mat("Objet_Papier", Color.white));
        AddPickCollider(root);
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{ItemsFolder}/Prefabs/{name}.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    static void AddPickCollider(GameObject root)
    {
        foreach (var c in root.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
        if (!TryBounds(root, out var b)) b = new Bounds(root.transform.position, Vector3.one * 0.1f);
        var bc = root.AddComponent<BoxCollider>();
        bc.isTrigger = true; // ne bloque pas le joueur, mais se vise facilement
        bc.center = b.center - root.transform.position;
        bc.size = Vector3.Max(b.size, new Vector3(0.22f, 0.1f, 0.22f));
    }

    static ItemData SaveItem(string id, string name, string desc, ItemKind kind, GameObject model, float hand, System.Action<ItemData> extra)
    {
        string path = $"{ItemsFolder}/{id}.asset";
        var item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
        if (item == null) { item = ScriptableObject.CreateInstance<ItemData>(); AssetDatabase.CreateAsset(item, path); }
        item.id = id; item.displayName = name; item.description = desc; item.kind = kind; item.model = model; item.handSize = hand;
        item.stackable = false; item.maxStack = 1; item.consumedOnUse = false; item.keyId = ""; item.noteText = "";
        extra?.Invoke(item);
        EditorUtility.SetDirty(item);
        return item;
    }

    // ───────────────────────── Placement ─────────────────────────

    /// Pose un objet ramassable (floorPos = point d'appui : sol ou dessus d'un meuble).
    static GameObject PlaceItem(LevelDef L, string id, Vector3 floorPos, float yaw = 0f, int count = 1)
    {
        if (_items == null || !_items.TryGetValue(id, out var item) || item.model == null) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(item.model, L.props);
        go.name = "Objet_" + item.displayName;
        go.transform.SetPositionAndRotation(floorPos + Vector3.up * 0.005f, Quaternion.Euler(0f, yaw, 0f));
        var p = go.AddComponent<ItemPickup>();
        p.item = item; p.count = count;
        NonStatic(go);
        return go;
    }

    /// Dessus d'un meuble (pour y poser un objet).
    static Vector3 TopOf(GameObject furniture, Vector3 fallback)
    {
        if (furniture != null && TryBounds(furniture, out var b)) return new Vector3(b.center.x, b.max.y, b.center.z);
        return fallback;
    }

    static string NextNoteId() => "note_" + (_notesPlaced++ % NoteTexts.Length);

    /// Pile au hasard, avec un plafond par niveau.
    static void MaybeBattery(LevelDef L, Vector3 floorPos, double chance)
    {
        ref int n = ref (L.number == 0 ? ref _batteriesPlaced0 : ref (L.number == -1 ? ref _batteriesPlacedM1 : ref _batteriesPlacedM2));
        if (n >= 3 || _rng.NextDouble() >= chance) return;
        n++;
        PlaceItem(L, "pile", floorPos, Rand(0f, 360f));
    }

    /// Ajoute l'inventaire et la pile de lampe au joueur.
    static void EnsurePlayerInventory()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;
        if (player.GetComponent<Inventory>() == null) player.AddComponent<Inventory>();
        if (player.GetComponent<FlashlightBattery>() == null) player.AddComponent<FlashlightBattery>();
    }
}
