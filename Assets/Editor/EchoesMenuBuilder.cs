using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Génère l'interface du jeu :
///  - la scène MainMenu (fond, titre, boutons, panneaux Règles / Guide / Options / Crédits, musique, confirmation pour quitter) ;
///  - dans Main : GameManager, HUD (viseur + texte d'interaction) et menu pause (Échap) relié aux réglages du joueur.
/// Relancer régénère ces éléments sans toucher à la map ("ECHOES_MAP").
/// </summary>
public static class EchoesMenuBuilder
{
    const string MenuScenePath = "Assets/Scenes/MainMenu.unity";
    const string GameScenePath = "Assets/Scenes/Main.unity";
    const string BackgroundPath = "Assets/Textures/main_menu_bg.png";
    const string MusicFolder = "Assets/Sounds/Menu";
    const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    static readonly Color Bone = new Color(0.87f, 0.85f, 0.80f);
    static readonly Color Dim = new Color(0.62f, 0.60f, 0.57f);
    static readonly Color Accent = new Color(1f, 0.7f, 0.2f);
    static readonly Color PanelCol = new Color(0.04f, 0.04f, 0.05f, 0.94f);
    static readonly Color ButtonBg = new Color(1f, 1f, 1f, 0.07f);

    static TMP_FontAsset _font;

    // ───────────────────────── Points d'entrée ─────────────────────────

    [MenuItem("The Echoes Below/Importer TextMesh Pro (essentiels)")]
    public static void ImportTmpEssentials()
    {
        if (Directory.Exists("Assets/TextMesh Pro"))
        {
            Debug.Log("[EchoesMenu] TextMesh Pro est déjà importé.");
            return;
        }
        var info = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.ugui");
        string path = info != null
            ? Path.Combine(info.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage")
            : null;
        if (path == null || !File.Exists(path))
        {
            Debug.LogError("[EchoesMenu] Paquet TMP Essential Resources introuvable : " + path);
            return;
        }
        AssetDatabase.ImportPackage(path, false);
        AssetDatabase.Refresh();
        Debug.Log("[EchoesMenu] TMP Essential Resources importé.");
    }

    [MenuItem("The Echoes Below/Générer les menus (principal + pause)")]
    public static void BuildMenus()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        _font = LoadFont();
        if (_font == null)
        {
            Debug.LogError("[EchoesMenu] Aucune police TextMesh Pro : lance d'abord « Importer TextMesh Pro (essentiels) ».");
            return;
        }

        PrepareBackground();
        BuildMainMenuScene();
        BuildGameplayUI();
        UpdateBuildSettings();
        AssetDatabase.SaveAssets();

        Debug.Log("[EchoesMenu] Menus générés : " + MenuScenePath + " (menu principal) et menu pause + HUD dans " + GameScenePath + ".");
    }

    // ───────────────────────── Menu principal ─────────────────────────

    static void BuildMainMenuScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Camera") { tag = "MainCamera" };
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        camGo.AddComponent<AudioListener>();

        new GameObject("GameManager").AddComponent<GameManager>();
        CreateEventSystem();

        var music = new GameObject("Musique").AddComponent<AudioSource>();
        music.playOnAwake = false;
        music.loop = false;
        music.spatialBlend = 0f;

        var canvas = CreateCanvas("UI_Menu", 0);
        var root = canvas.transform;

        // Fond + assombrissement
        var bg = UI("Fond", root, typeof(RawImage));
        Stretch(bg);
        var raw = bg.GetComponent<RawImage>();
        raw.raycastTarget = false;
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(BackgroundPath);
        if (tex != null)
        {
            raw.texture = tex;
            var imp = (TextureImporter)AssetImporter.GetAtPath(BackgroundPath);
            imp.GetSourceTextureWidthAndHeight(out int w, out int h);
            var fit = bg.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = h > 0 ? (float)w / h : 16f / 9f;
        }
        else raw.color = new Color(0.03f, 0.03f, 0.04f);

        var shade = Img("Assombrissement", root, new Color(0f, 0f, 0f, 0.45f));
        shade.raycastTarget = false;
        Stretch(shade.rectTransform);

        // Groupe principal : titre + boutons
        var group = UI("Menu", root, typeof(CanvasGroup));
        Stretch(group);

        var title = Text("Titre", group, "THE ECHOES BELOW", 92, TextAlignmentOptions.TopLeft, Bone);
        title.characterSpacing = 8;
        Place(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(140, -150), new Vector2(1500, 130));

        var subtitle = Text("Sous_Titre", group, "Quelque chose vit sous la résidence.", 30, TextAlignmentOptions.TopLeft, Dim);
        subtitle.fontStyle = FontStyles.Italic;
        Place(subtitle.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(146, -285), new Vector2(1200, 50));

        string[] labels = { "Histoire", "Bac à sable", "Règles", "Guide", "Options", "Crédits", "Quitter" };
        var buttons = new Button[labels.Length];
        for (int i = 0; i < labels.Length; i++)
        {
            buttons[i] = MakeButton("Bouton_" + labels[i], group, labels[i], 38, new Color(0, 0, 0, 0), TextAlignmentOptions.Left);
            Place((RectTransform)buttons[i].transform, new Vector2(0, 1), new Vector2(0, 0.5f),
                  new Vector2(140, -420 - i * 72), new Vector2(460, 62));
        }

        var version = Text("Version", group, "Prototype — v0.1", 22, TextAlignmentOptions.BottomRight, Dim);
        Place(version.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-40, 30), new Vector2(500, 40));

        // Sous-panneaux
        var rules = InfoPanel(root, "Panel_Regles", "Règles", RulesText, TextAlignmentOptions.TopLeft, out var rulesBack);
        var guide = InfoPanel(root, "Panel_Guide", "Guide", GuideText, TextAlignmentOptions.TopLeft, out var guideBack);
        var options = OptionsPanelUI(root, out var optionsBack);
        var credits = InfoPanel(root, "Panel_Credits", "Crédits", CreditsText, TextAlignmentOptions.Top, out var creditsBack);

        // Confirmation pour quitter
        var quit = Img("Confirmation_Quitter", root, new Color(0f, 0f, 0f, 0.65f));
        Stretch(quit.rectTransform);
        var qBox = Img("Cadre", quit.transform, PanelCol);
        Place(qBox.rectTransform, Center, Center, Vector2.zero, new Vector2(720, 320));
        var qTitle = Text("Question", qBox.transform, "Quitter le jeu ?", 46, TextAlignmentOptions.Center, Bone);
        Place(qTitle.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -45), new Vector2(640, 70));
        var qSub = Text("Sous_Texte", qBox.transform, "Les échos t'attendront.", 26, TextAlignmentOptions.Center, Dim);
        qSub.fontStyle = FontStyles.Italic;
        Place(qSub.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -120), new Vector2(640, 40));
        var qYes = MakeButton("Oui", qBox.transform, "Oui", 32, ButtonBg, TextAlignmentOptions.Center);
        Place((RectTransform)qYes.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-130, 40), new Vector2(220, 64));
        var qNo = MakeButton("Non", qBox.transform, "Non", 32, ButtonBg, TextAlignmentOptions.Center);
        Place((RectTransform)qNo.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(130, 40), new Vector2(220, 64));

        // Fondu au noir (au-dessus de tout)
        var fade = Img("Fondu", root, new Color(0f, 0f, 0f, 0f));
        fade.raycastTarget = false;
        Stretch(fade.rectTransform);

        // Script MainMenu
        var menu = canvas.gameObject.AddComponent<MainMenu>();
        var so = new SerializedObject(menu);
        Set(so, "storyButton", buttons[0]);
        Set(so, "sandboxButton", buttons[1]);
        Set(so, "rulesButton", buttons[2]);
        Set(so, "guideButton", buttons[3]);
        Set(so, "optionsButton", buttons[4]);
        Set(so, "creditsButton", buttons[5]);
        Set(so, "quitButton", buttons[6]);
        Set(so, "musicPlayer", music);
        Set(so, "titleLabel", title);
        Set(so, "mainMenuGroup", group.GetComponent<CanvasGroup>());
        Set(so, "fadeOverlay", fade);
        Set(so, "optionsPanel", options);
        Set(so, "quitModal", quit.gameObject);
        Set(so, "rulesPanel", rules);
        Set(so, "guidePanel", guide);
        Set(so, "creditsPanel", credits);

        var clips = new List<AudioClip>();
        foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { MusicFolder }))
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
            if (clip != null) clips.Add(clip);
        }
        var tracks = so.FindProperty("tracks");
        tracks.arraySize = clips.Count;
        for (int i = 0; i < clips.Count; i++) tracks.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        // Boutons « Retour » et confirmation (listeners persistants)
        UnityEventTools.AddObjectPersistentListener<GameObject>(rulesBack.onClick, menu.CloseSubPanel, rules);
        UnityEventTools.AddObjectPersistentListener<GameObject>(guideBack.onClick, menu.CloseSubPanel, guide);
        UnityEventTools.AddObjectPersistentListener<GameObject>(optionsBack.onClick, menu.CloseSubPanel, options);
        UnityEventTools.AddObjectPersistentListener<GameObject>(creditsBack.onClick, menu.CloseSubPanel, credits);
        UnityEventTools.AddPersistentListener(qYes.onClick, menu.ConfirmQuit);
        UnityEventTools.AddPersistentListener(qNo.onClick, menu.CancelQuit);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, MenuScenePath);
    }

    // ───────────────────────── HUD + menu pause (scène Main) ─────────────────────────

    static void BuildGameplayUI()
    {
        if (!File.Exists(GameScenePath))
        {
            Debug.LogWarning("[EchoesMenu] " + GameScenePath + " n'existe pas : génère d'abord la map.");
            return;
        }
        var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        foreach (var n in new[] { "UI_Jeu", "GameManager", "EventSystem" })
        {
            var old = GameObject.Find(n);
            if (old != null) Object.DestroyImmediate(old);
        }

        // Permet de lancer Main directement ; détruit automatiquement s'il en existe déjà un (venu du menu)
        new GameObject("GameManager").AddComponent<GameManager>();
        CreateEventSystem();

        var canvas = CreateCanvas("UI_Jeu", 10);
        var root = canvas.transform;

        // HUD
        var hud = UI("HUD", root);
        Stretch(hud);
        var dot = Img("Viseur", hud, new Color(1f, 1f, 1f, 0.55f));
        dot.raycastTarget = false;
        Place(dot.rectTransform, Center, Center, Vector2.zero, new Vector2(6, 6));
        var label = Text("Texte_Interaction", hud, "[E] Interagir", 28, TextAlignmentOptions.Center, Bone);
        Place(label.rectTransform, Center, Center, new Vector2(0, -70), new Vector2(900, 50));
        label.gameObject.SetActive(false);

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            var im = player.GetComponent<InteractionManager>();
            if (im == null) im = player.AddComponent<InteractionManager>();
            var imSo = new SerializedObject(im);
            Set(imSo, "interactionLabel", label);
            imSo.ApplyModifiedPropertiesWithoutUndo();
        }
        else Debug.LogWarning("[EchoesMenu] Pas de joueur dans Main : le texte d'interaction n'est pas relié.");

        // Menu pause (racine active : PauseMenu.Awake la masque au lancement)
        var pause = Img("Menu_Pause", root, new Color(0f, 0f, 0f, 0.72f));
        Stretch(pause.rectTransform);

        // Panneau principal
        var main = UI("Pause_Principal", pause.transform);
        Stretch(main);
        var pTitle = Text("Titre", main, "PAUSE", 80, TextAlignmentOptions.Center, Bone);
        pTitle.characterSpacing = 12;
        Place(pTitle.rectTransform, Center, Center, new Vector2(0, 220), new Vector2(800, 110));
        var resume = MakeButton("Reprendre", main, "Reprendre", 34, ButtonBg, TextAlignmentOptions.Center);
        Place((RectTransform)resume.transform, Center, Center, new Vector2(0, 60), new Vector2(440, 68));
        var opt = MakeButton("Options", main, "Options", 34, ButtonBg, TextAlignmentOptions.Center);
        Place((RectTransform)opt.transform, Center, Center, new Vector2(0, -25), new Vector2(440, 68));
        var toMenu = MakeButton("Menu_Principal", main, "Menu principal", 34, ButtonBg, TextAlignmentOptions.Center);
        Place((RectTransform)toMenu.transform, Center, Center, new Vector2(0, -110), new Vector2(440, 68));

        // Panneau options (réglages du joueur)
        var opts = UI("Pause_Options", pause.transform);
        Stretch(opts);
        var oBox = Img("Cadre", opts, PanelCol);
        Place(oBox.rectTransform, Center, Center, Vector2.zero, new Vector2(1250, 760));
        var oTitle = Text("Titre", oBox.transform, "Options", 54, TextAlignmentOptions.Top, Bone);
        Place(oTitle.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -35), new Vector2(800, 75));
        var oSep = Img("Separateur", oBox.transform, new Color(1f, 1f, 1f, 0.08f));
        oSep.raycastTarget = false;
        Place(oSep.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(40, -130), new Vector2(2, 500));
        KeyBindingsBlock(oBox.transform, new Vector2(-70, -125), new Vector2(520, 520));

        string[] sliderNames = { "Sensibilité de la souris", "Champ de vision", "Champ de vision en course", "Vitesse de marche", "Vitesse de course" };
        var sliders = new Slider[sliderNames.Length];
        for (int i = 0; i < sliderNames.Length; i++)
        {
            float y = -140 - i * 98;
            var l = Text("Label_" + i, oBox.transform, sliderNames[i], 28, TextAlignmentOptions.Left, Bone);
            Place(l.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(80, y), new Vector2(520, 40));
            sliders[i] = MakeSlider(oBox.transform);
            Place((RectTransform)sliders[i].transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(80, y - 48), new Vector2(520, 22));
        }
        var oBack = MakeButton("Retour", oBox.transform, "Retour", 32, ButtonBg, TextAlignmentOptions.Center);
        Place((RectTransform)oBack.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 35), new Vector2(280, 64));
        opts.gameObject.SetActive(false);

        // Panneau de confirmation
        var qp = UI("Pause_Quitter", pause.transform);
        Stretch(qp);
        var qBox = Img("Cadre", qp, PanelCol);
        Place(qBox.rectTransform, Center, Center, Vector2.zero, new Vector2(780, 320));
        var q = Text("Question", qBox.transform, "Retourner au menu principal ?", 42, TextAlignmentOptions.Center, Bone);
        Place(q.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -45), new Vector2(720, 70));
        var qs = Text("Sous_Texte", qBox.transform, "La progression non sauvegardée sera perdue.", 26, TextAlignmentOptions.Center, Dim);
        Place(qs.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -120), new Vector2(720, 40));
        var yes = MakeButton("Oui", qBox.transform, "Oui", 32, ButtonBg, TextAlignmentOptions.Center);
        Place((RectTransform)yes.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-130, 40), new Vector2(220, 64));
        var no = MakeButton("Non", qBox.transform, "Non", 32, ButtonBg, TextAlignmentOptions.Center);
        Place((RectTransform)no.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(130, 40), new Vector2(220, 64));
        qp.gameObject.SetActive(false);

        var pm = pause.gameObject.AddComponent<PauseMenu>();
        var so = new SerializedObject(pm);
        Set(so, "mainPanel", main.gameObject);
        Set(so, "optionsPanel", opts.gameObject);
        Set(so, "quitPanel", qp.gameObject);
        Set(so, "resumeButton", resume);
        Set(so, "optionsButton", opt);
        Set(so, "quitButton", toMenu);
        Set(so, "sensitivitySlider", sliders[0]);
        Set(so, "fovSlider", sliders[1]);
        Set(so, "sprintFovSlider", sliders[2]);
        Set(so, "walkSpeedSlider", sliders[3]);
        Set(so, "sprintSpeedSlider", sliders[4]);
        Set(so, "optionsBackButton", oBack);
        Set(so, "quitYesButton", yes);
        Set(so, "quitNoButton", no);
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, GameScenePath);
    }

    // ───────────────────────── Textes ─────────────────────────

    const string RulesText =
        "•  Le jour, la résidence est calme : vis ta vie d'étudiant et écoute les rumeurs.\n\n" +
        "•  De 22 h à 6 h, les surveillants patrouillent dans les couloirs. Ne te fais pas voir.\n\n" +
        "•  Chaque niveau sous la résidence est plus sombre, plus abandonné et plus dangereux que le précédent.\n\n" +
        "•  Certains passages ne s'ouvrent qu'une fois le travail terminé à l'étage.\n\n" +
        "•  Ce qui vit en bas sait imiter. Méfie-toi de ce qui te ressemble.";

    const string GuideText =
        "<b>Le jour</b> : va en cours, fais tes tâches, parle aux autres étudiants.\n" +
        "<b>La nuit</b> : faufile-toi vers les étages inférieurs sans croiser les surveillants.\n\n" +
        "•  En bas, la lumière se fait rare : garde ta lampe torche à portée de main.\n\n" +
        "•  Les passages bas, les trous et les conduits se franchissent accroupi.\n\n" +
        "•  Fouille partout : tous les chemins ne sont pas à la vue de tous.\n\n" +
        "•  Si une voix familière t'appelle depuis l'obscurité… ce n'est peut-être pas elle.";

    const string CreditsText =
        "<b>The Echoes Below</b>\nUn jeu de Mickaël Marco\n\n" +
        "<b>Musiques du menu</b>\nEchoes from the Chthonic Depths\nEchoes in the Abyssal Deep\nEchoes of the Subterranean\n\n" +
        "<b>Modèles 3D</b>\nKenney — kenney.nl (CC0)\n\n" +
        "<b>Moteur</b>\nUnity 6";

    // ───────────────────────── Aides UI ─────────────────────────

    static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

    static TMP_FontAsset LoadFont()
    {
        var f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (f != null) return f;
        foreach (var guid in AssetDatabase.FindAssets("t:TMP_FontAsset"))
        {
            f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
            if (f != null) return f;
        }
        return null;
    }

    static void PrepareBackground()
    {
        // Image de fond nette : pas de redimensionnement en puissance de 2 ni de mipmaps
        var imp = AssetImporter.GetAtPath(BackgroundPath) as TextureImporter;
        if (imp == null) return;
        if (imp.npotScale != TextureImporterNPOTScale.None || imp.mipmapEnabled)
        {
            imp.npotScale = TextureImporterNPOTScale.None;
            imp.mipmapEnabled = false;
            imp.SaveAndReimport();
        }
    }

    static Canvas CreateCanvas(string name, int order)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.layer = 5;
        var c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = order;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        return c;
    }

    static void CreateEventSystem()
    {
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    static RectTransform UI(string name, Transform parent, params System.Type[] comps)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        foreach (var c in comps) go.AddComponent(c);
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static Image Img(string name, Transform parent, Color color)
    {
        var img = UI(name, parent, typeof(Image)).GetComponent<Image>();
        img.color = color;
        return img;
    }

    static TextMeshProUGUI Text(string name, Transform parent, string text, float size, TextAlignmentOptions align, Color color)
    {
        var t = UI(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        t.font = _font;
        t.text = text;
        t.fontSize = size;
        t.alignment = align;
        t.color = color;
        t.raycastTarget = false;
        return t;
    }

    static Button MakeButton(string name, Transform parent, string label, float fontSize, Color bg, TextAlignmentOptions align)
    {
        var img = Img(name, parent, bg);
        var b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        var t = Text("Texte", img.transform, label, fontSize, align, Color.white);
        Stretch(t.rectTransform);
        if (align == TextAlignmentOptions.Left) t.rectTransform.offsetMin = new Vector2(18, 0);
        return b;
    }

    static Slider MakeSlider(Transform parent)
    {
        var go = DefaultControls.CreateSlider(new DefaultControls.Resources());
        go.layer = 5;
        go.transform.SetParent(parent, false);
        go.transform.Find("Background").GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);
        go.transform.Find("Fill Area/Fill").GetComponent<Image>().color = Accent;
        go.transform.Find("Handle Slide Area/Handle").GetComponent<Image>().color = Bone;
        return go.GetComponent<Slider>();
    }

    static GameObject InfoPanel(Transform root, string name, string title, string body, TextAlignmentOptions bodyAlign, out Button back)
    {
        var p = UI(name, root);
        Stretch(p);
        var box = Img("Cadre", p, PanelCol);
        Place(box.rectTransform, Center, Center, Vector2.zero, new Vector2(1150, 800));

        var t = Text("Titre", box.transform, title, 58, TextAlignmentOptions.Top, Bone);
        Place(t.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(1000, 80));
        var line = Img("Trait", box.transform, new Color(Accent.r, Accent.g, Accent.b, 0.6f));
        line.raycastTarget = false;
        Place(line.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -128), new Vector2(160, 3));

        var b = Text("Texte", box.transform, body, 28, bodyAlign, new Color(0.82f, 0.80f, 0.76f));
        b.lineSpacing = 8;
        var rt = b.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(80, 130);
        rt.offsetMax = new Vector2(-80, -160);

        back = MakeButton("Retour", box.transform, "Retour", 32, ButtonBg, TextAlignmentOptions.Center);
        Place((RectTransform)back.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(280, 64));
        return p.gameObject;
    }

    /// Panneau Options du menu principal : réglages à gauche, contrôles à droite.
    static GameObject OptionsPanelUI(Transform root, out Button back)
    {
        var p = UI("Panel_Options", root);
        Stretch(p);
        var box = Img("Cadre", p, PanelCol);
        Place(box.rectTransform, Center, Center, Vector2.zero, new Vector2(1250, 800));

        var t = Text("Titre", box.transform, "Options", 58, TextAlignmentOptions.Top, Bone);
        Place(t.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(1000, 80));
        var line = Img("Trait", box.transform, new Color(Accent.r, Accent.g, Accent.b, 0.6f));
        line.raycastTarget = false;
        Place(line.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -128), new Vector2(160, 3));

        // Colonne de gauche : réglages
        string[] names = { "Volume général", "Musique", "Sensibilité de la souris", "Champ de vision" };
        var sliders = new Slider[names.Length];
        var values = new TextMeshProUGUI[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            float y = -175 - i * 100;
            var l = Text("Label_" + i, box.transform, names[i], 28, TextAlignmentOptions.Left, Bone);
            Place(l.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(80, y), new Vector2(460, 40));
            values[i] = Text("Valeur_" + i, box.transform, "", 26, TextAlignmentOptions.Right, Accent);
            Place(values[i].rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(440, y), new Vector2(160, 40));
            sliders[i] = MakeSlider(box.transform);
            Place((RectTransform)sliders[i].transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(80, y - 50), new Vector2(520, 22));
        }

        // Plein écran
        var fsLabel = Text("Label_PleinEcran", box.transform, "Plein écran", 28, TextAlignmentOptions.Left, Bone);
        Place(fsLabel.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(80, -575), new Vector2(400, 40));
        var toggle = MakeToggle(box.transform);
        Place((RectTransform)toggle.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(560, -575), new Vector2(40, 40));

        // Séparateur vertical
        var sep = Img("Separateur", box.transform, new Color(1f, 1f, 1f, 0.08f));
        sep.raycastTarget = false;
        Place(sep.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(40, -170), new Vector2(2, 460));

        // Colonne de droite : touches personnalisables
        KeyBindingsBlock(box.transform, new Vector2(-70, -160), new Vector2(520, 520));

        back = MakeButton("Retour", box.transform, "Retour", 32, ButtonBg, TextAlignmentOptions.Center);
        Place((RectTransform)back.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(280, 64));

        var op = p.gameObject.AddComponent<OptionsPanel>();
        var so = new SerializedObject(op);
        Set(so, "masterVolumeSlider", sliders[0]);
        Set(so, "musicVolumeSlider", sliders[1]);
        Set(so, "sensitivitySlider", sliders[2]);
        Set(so, "fovSlider", sliders[3]);
        Set(so, "fullscreenToggle", toggle);
        Set(so, "masterVolumeValue", values[0]);
        Set(so, "musicVolumeValue", values[1]);
        Set(so, "sensitivityValue", values[2]);
        Set(so, "fovValue", values[3]);
        so.ApplyModifiedPropertiesWithoutUndo();

        p.gameObject.SetActive(false);
        return p.gameObject;
    }

    /// Bloc "Touches" (construit à l'exécution par KeyBindingsUI), ancré en haut à droite du cadre.
    static void KeyBindingsBlock(Transform box, Vector2 pos, Vector2 size)
    {
        var rt = UI("Touches", box);
        Place(rt, new Vector2(1, 1), new Vector2(1, 1), pos, size);
        var ui = rt.gameObject.AddComponent<KeyBindingsUI>();
        var so = new SerializedObject(ui);
        Set(so, "font", _font);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static Toggle MakeToggle(Transform parent)
    {
        var bg = Img("PleinEcran", parent, new Color(1f, 1f, 1f, 0.12f));
        var toggle = bg.gameObject.AddComponent<Toggle>();
        toggle.targetGraphic = bg;
        var check = Img("Coche", bg.transform, Accent);
        check.raycastTarget = false;
        Stretch(check.rectTransform);
        check.rectTransform.offsetMin = new Vector2(8, 8);
        check.rectTransform.offsetMax = new Vector2(-8, -8);
        toggle.graphic = check;
        toggle.isOn = true;
        return toggle;
    }

    static void Set(SerializedObject so, string prop, Object value)
    {
        var p = so.FindProperty(prop);
        if (p == null) { Debug.LogError("[EchoesMenu] Champ introuvable : " + so.targetObject.GetType().Name + "." + prop); return; }
        p.objectReferenceValue = value;
    }

    static void UpdateBuildSettings()
    {
        var list = new List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene(MenuScenePath, true),
            new EditorBuildSettingsScene(GameScenePath, true),
        };
        foreach (var s in EditorBuildSettings.scenes)
            if (s.path != MenuScenePath && s.path != GameScenePath) list.Add(s);
        EditorBuildSettings.scenes = list.ToArray();
    }
}
