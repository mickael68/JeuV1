using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Liste des touches dans les options : une ligne par action, cliquer sur la touche puis appuyer
/// sur la nouvelle touche (Échap annule). Les lignes sont construites au démarrage dans ce RectTransform.
/// </summary>
public class KeyBindingsUI : MonoBehaviour
{
    /// Vrai pendant qu'on attend une touche (le menu pause ne doit pas se fermer sur Échap).
    public static bool Listening { get; private set; }

    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private float fontSize = 24f;
    [SerializeField] private float rowHeight = 44f;
    [SerializeField] private Color labelColor = new Color(0.87f, 0.85f, 0.80f);
    [SerializeField] private Color keyColor = new Color(1f, 0.7f, 0.2f);
    [SerializeField] private Color buttonColor = new Color(1f, 1f, 1f, 0.07f);

    private readonly Dictionary<GameAction, TextMeshProUGUI> _keyLabels = new Dictionary<GameAction, TextMeshProUGUI>();
    private GameAction? _waiting;
    private int _waitFrame;
    private bool _built;

    private static readonly KeyCode[] AllKeys = (KeyCode[])System.Enum.GetValues(typeof(KeyCode));

    private void OnEnable()
    {
        if (!_built) Build();
        Refresh();
    }

    private void OnDisable()
    {
        _waiting = null;
        Listening = false;
    }

    private void Build()
    {
        _built = true;
        var rt = (RectTransform)transform;
        float width = rt.rect.width > 1f ? rt.rect.width : rt.sizeDelta.x;
        float avail = rt.rect.height > 1f ? rt.rect.height : rt.sizeDelta.y;

        // Deux colonnes dès que la liste est longue : lisible, gros caractères
        int n = KeyBindings.All.Length;
        int columns = n > 7 && width > 700f ? 2 : 1;
        int rowsPerCol = Mathf.CeilToInt(n / (float)columns);
        if (avail > 1f) rowHeight = Mathf.Clamp(avail / (rowsPerCol + 2.6f), 34f, 58f);
        fontSize = Mathf.Max(18f, rowHeight * 0.48f);
        const float colGap = 60f;
        float colW = (width - colGap * (columns - 1)) / columns;

        for (int idx = 0; idx < n; idx++)
        {
            var a = KeyBindings.All[idx];
            int col = idx / rowsPerCol, row = idx % rowsPerCol;
            float x0 = col * (colW + colGap);
            float y = -row * rowHeight;

            var label = MakeText("Action_" + a, rt, KeyBindings.ActionName(a), fontSize, TextAlignmentOptions.Left, labelColor);
            PlaceAbs(label.rectTransform, x0, y, colW * 0.56f, rowHeight);

            var btnRt = NewRect("Touche_" + a, rt);
            PlaceAbs(btnRt, x0 + colW * 0.58f, y - 4f, colW * 0.42f, rowHeight - 8f);
            var img = btnRt.gameObject.AddComponent<Image>();
            img.color = buttonColor;
            var btn = btnRt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var action = a;
            btn.onClick.AddListener(() => StartListening(action));

            var keyText = MakeText("Texte", btnRt, "", fontSize, TextAlignmentOptions.Center, keyColor);
            var k = keyText.rectTransform;
            k.anchorMin = Vector2.zero; k.anchorMax = Vector2.one; k.offsetMin = k.offsetMax = Vector2.zero;
            _keyLabels[a] = keyText;
        }

        // Réinitialiser + aide, sous la liste
        float yb = -rowsPerCol * rowHeight - 14f;
        var resetRt = NewRect("Reinitialiser", rt);
        PlaceAbs(resetRt, width - 280f, yb, 280f, rowHeight - 6f);
        var rImg = resetRt.gameObject.AddComponent<Image>();
        rImg.color = buttonColor;
        var rBtn = resetRt.gameObject.AddComponent<Button>();
        rBtn.targetGraphic = rImg;
        rBtn.onClick.AddListener(() => { KeyBindings.ResetDefaults(); _waiting = null; Listening = false; Refresh(); });
        var rText = MakeText("Texte", resetRt, "Réinitialiser", fontSize - 2, TextAlignmentOptions.Center, labelColor);
        var rr = rText.rectTransform;
        rr.anchorMin = Vector2.zero; rr.anchorMax = Vector2.one; rr.offsetMin = rr.offsetMax = Vector2.zero;

        var hint = MakeText("Aide", rt, "Clique sur une touche, puis appuie sur la nouvelle (Échap pour annuler).\nLes flèches servent toujours à se déplacer, Échap à faire pause.",
            Mathf.Max(16f, fontSize - 6f), TextAlignmentOptions.Left, new Color(0.62f, 0.60f, 0.57f));
        PlaceAbs(hint.rectTransform, 0f, yb, width - 310f, rowHeight * 1.5f);
        hint.textWrappingMode = TextWrappingModes.Normal;
    }

    /// Place en haut à gauche du cadre : x vers la droite, y vers le bas (négatif).
    private static void PlaceAbs(RectTransform r, float x, float y, float w, float h)
    {
        r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
        r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = new Vector2(x, y);
        r.sizeDelta = new Vector2(w, h);
    }

    private void StartListening(GameAction a)
    {
        _waiting = a;
        _waitFrame = Time.frameCount;
        Listening = true;
        Refresh();
    }

    private void Update()
    {
        if (_waiting == null || Time.frameCount <= _waitFrame) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            _waiting = null;
            Listening = false;
            Refresh();
            return;
        }

        foreach (var k in AllKeys)
        {
            if (k == KeyCode.None || k == KeyCode.Escape || k == KeyCode.Mouse0) continue;
            if ((int)k >= (int)KeyCode.JoystickButton0) continue; // pas de manette ici
            if (!Input.GetKeyDown(k)) continue;
            KeyBindings.Set(_waiting.Value, k);
            _waiting = null;
            Listening = false;
            Refresh();
            return;
        }
    }

    private void Refresh()
    {
        foreach (var kv in _keyLabels)
        {
            bool waiting = _waiting.HasValue && _waiting.Value == kv.Key;
            kv.Value.text = waiting ? "Appuie sur une touche…" : KeyBindings.KeyName(KeyBindings.Get(kv.Key));
            kv.Value.color = waiting ? labelColor : keyColor;
        }
    }

    // ── Aides UI ──

    private void Place(RectTransform r, int row, float h, float widthFraction)
    {
        r.anchorMin = new Vector2(0f, 1f);
        r.anchorMax = new Vector2(widthFraction, 1f);
        r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = new Vector2(0f, -row * h);
        r.sizeDelta = new Vector2(0f, h);
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private TextMeshProUGUI MakeText(string name, Transform parent, string text, float size, TextAlignmentOptions align, Color color)
    {
        var t = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.alignment = align;
        t.color = color;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        return t;
    }
}
