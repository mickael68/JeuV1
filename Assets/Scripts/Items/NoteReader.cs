using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Lecture d'une note à l'écran (feuille de papier). E, clic ou Échap pour fermer.</summary>
public class NoteReader : MonoBehaviour
{
    public static bool IsOpen => _instance != null && _instance._root.activeSelf;
    private static NoteReader _instance;

    private GameObject _root;
    private TextMeshProUGUI _title, _body;
    private int _openFrame;

    public static void Open(string title, string text)
    {
        if (_instance == null) Create();
        if (_instance == null) return;
        _instance._title.text = title;
        _instance._body.text = string.IsNullOrEmpty(text) ? "(la page est illisible)" : text;
        _instance._root.SetActive(true);
        _instance._openFrame = Time.frameCount;
    }

    private static void Create()
    {
        var canvasGo = GameObject.Find("UI_Jeu");
        var canvas = canvasGo != null ? canvasGo.GetComponent<Canvas>() : null;
        if (canvas == null) return;
        var font = canvas.GetComponentInChildren<TextMeshProUGUI>(true)?.font;

        var root = new GameObject("Lecture_Note", typeof(RectTransform));
        root.layer = 5;
        root.transform.SetParent(canvas.transform, false);
        var rr = (RectTransform)root.transform;
        rr.anchorMin = Vector2.zero; rr.anchorMax = Vector2.one; rr.offsetMin = rr.offsetMax = Vector2.zero;
        root.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

        var paper = new GameObject("Feuille", typeof(RectTransform));
        paper.layer = 5;
        paper.transform.SetParent(root.transform, false);
        var pr = (RectTransform)paper.transform;
        pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);
        pr.sizeDelta = new Vector2(760f, 900f);
        paper.AddComponent<Image>().color = new Color(0.86f, 0.82f, 0.72f);

        TextMeshProUGUI Txt(string name, float size, Vector2 min, Vector2 max)
        {
            var g = new GameObject(name, typeof(RectTransform));
            g.layer = 5;
            g.transform.SetParent(paper.transform, false);
            var r = (RectTransform)g.transform;
            r.anchorMin = min; r.anchorMax = max; r.offsetMin = new Vector2(60f, 0f); r.offsetMax = new Vector2(-60f, 0f);
            var t = g.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.color = new Color(0.15f, 0.12f, 0.1f);
            t.raycastTarget = false;
            return t;
        }
        var title = Txt("Titre", 36f, new Vector2(0f, 0.88f), new Vector2(1f, 0.97f));
        title.alignment = TextAlignmentOptions.Center;
        title.fontStyle = FontStyles.Bold;
        var body = Txt("Texte", 27f, new Vector2(0f, 0.1f), new Vector2(1f, 0.86f));
        body.alignment = TextAlignmentOptions.TopLeft;
        body.lineSpacing = 6f;
        var hint = Txt("Aide", 20f, new Vector2(0f, 0.02f), new Vector2(1f, 0.08f));
        hint.alignment = TextAlignmentOptions.Center;
        hint.text = "E pour refermer";
        hint.color = new Color(0.35f, 0.3f, 0.25f);

        _instance = root.AddComponent<NoteReader>();
        _instance._root = root;
        _instance._title = title;
        _instance._body = body;
        root.SetActive(false);
    }

    private void Update()
    {
        if (!_root.activeSelf || Time.frameCount == _openFrame) return;
        if (KeyBindings.Down(GameAction.Interact) || KeyBindings.Down(GameAction.UseItem) || Input.GetKeyDown(KeyCode.Escape))
            _root.SetActive(false);
    }
}
