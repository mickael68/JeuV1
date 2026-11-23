using TMPro;
using UnityEngine;

/// <summary>
/// Message court affiché en bas de l'écran (pièges, indices…). HudMessage.Show("…", 3f).
/// Le texte est créé à la volée dans le Canvas du jeu.
/// </summary>
public class HudMessage : MonoBehaviour
{
    private static HudMessage _instance;
    private TextMeshProUGUI _text;
    private float _hideAt;

    public static void Show(string message, float seconds = 3f)
    {
        if (_instance == null) Create();
        if (_instance == null) { Debug.Log("[Message] " + message); return; }
        _instance._text.text = message;
        _instance._text.gameObject.SetActive(true);
        _instance._hideAt = Time.unscaledTime + seconds;
    }

    private static void Create()
    {
        var canvasGo = GameObject.Find("UI_Jeu");
        var canvas = canvasGo != null ? canvasGo.GetComponent<Canvas>() : Object.FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        var go = new GameObject("Message_HUD", typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(canvas.transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 140f);
        rt.sizeDelta = new Vector2(1400f, 80f);

        var t = go.AddComponent<TextMeshProUGUI>();
        var reference = canvas.GetComponentInChildren<TextMeshProUGUI>(true);
        if (reference != null) t.font = reference.font;
        t.fontSize = 34f;
        t.alignment = TextAlignmentOptions.Center;
        t.color = new Color(0.92f, 0.88f, 0.8f);
        t.raycastTarget = false;

        _instance = go.AddComponent<HudMessage>();
        _instance._text = t;
        go.SetActive(true);
    }

    private void Update()
    {
        if (_text.gameObject.activeSelf && Time.unscaledTime >= _hideAt) _text.text = "";
    }
}
