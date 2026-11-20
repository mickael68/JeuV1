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

        var title = MakeText("Titre", rt, "Touches", fontSize + 4, TextAlignmentOptions.Left, labelColor);
        title.fontStyle = FontStyles.Bold;
        Place(title.rectTransform, 0, rowHeight, 1f);

        int i = 1;
        foreach (var a in KeyBindings.All)
        {
            var label = MakeText("Action_" + a, rt, KeyBindings.ActionName(a), fontSize, TextAlignmentOptions.Left, labelColor);
            Place(label.rectTransform, i, rowHeight, 0.55f);

            var btnRt = NewRect("Touche_" + a, rt);
            btnRt.anchorMin = new Vector2(0.58f, 1f);
            btnRt.anchorMax = new Vector2(1f, 1f);
            btnRt.pivot = new Vector2(0.5f, 1f);
            btnRt.anchoredPosition = new Vector2(0f, -i * rowHeight - 3f);
            btnRt.sizeDelta = new Vector2(0f, rowHeight - 6f);
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
            i++;
        }

        // Réinitialiser
        var resetRt = NewRect("Reinitialiser", rt);
        resetRt.anchorMin = new Vector2(0.58f, 1f);
        resetRt.anchorMax = new Vector2(1f, 1f);
        resetRt.pivot = new Vector2(0.5f, 1f);
        resetRt.anchoredPosition = new Vector2(0f, -i * rowHeight - 10f);
        resetRt.sizeDelta = new Vector2(0f, rowHeight - 4f);
        var rImg = resetRt.gameObject.AddComponent<Image>();
        rImg.color = buttonColor;
        var rBtn = resetRt.gameObject.AddComponent<Button>();
        rBtn.targetGraphic = rImg;
        rBtn.onClick.AddListener(() => { KeyBindings.ResetDefaults(); _waiting = null; Listening = false; Refresh(); });
        var rText = MakeText("Texte", resetRt, "Réinitialiser", fontSize - 2, TextAlignmentOptions.Center, labelColor);
        var rr = rText.rectTransform;
        rr.anchorMin = Vector2.zero; rr.anchorMax = Vector2.one; rr.offsetMin = rr.offsetMax = Vector2.zero;

        var hint = MakeText("Aide", rt, "Clique sur une touche puis appuie sur la nouvelle (Échap : annuler). Les flèches marchent toujours.",
            fontSize - 6, TextAlignmentOptions.Left, new Color(0.62f, 0.60f, 0.57f));
        hint.rectTransform.anchorMin = new Vector2(0f, 1f);
        hint.rectTransform.anchorMax = new Vector2(0.56f, 1f);
        hint.rectTransform.pivot = new Vector2(0f, 1f);
        hint.rectTransform.anchoredPosition = new Vector2(0f, -i * rowHeight - 6f);
        hint.rectTransform.sizeDelta = new Vector2(0f, rowHeight * 1.6f);
        hint.textWrappingMode = TextWrappingModes.Normal;
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
