using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Interface de l'inventaire, créée à l'exécution dans le Canvas du jeu :
/// barre de 6 emplacements en bas (icône, quantité, numéro), nom de l'objet choisi,
/// panneau de détail (Tab) et jauge de la lampe torche.
/// </summary>
public class InventoryUI : MonoBehaviour
{
    private static readonly Color SlotBg = new Color(0.05f, 0.05f, 0.06f, 0.72f);
    private static readonly Color SlotSel = new Color(1f, 0.7f, 0.2f, 0.95f);
    private static readonly Color SlotBorder = new Color(1f, 1f, 1f, 0.12f);
    private static readonly Color Bone = new Color(0.87f, 0.85f, 0.80f);

    private Inventory _inv;
    private readonly List<Image> _borders = new List<Image>();
    private readonly List<RawImage> _icons = new List<RawImage>();
    private readonly List<TextMeshProUGUI> _counts = new List<TextMeshProUGUI>();
    private TextMeshProUGUI _name, _detailTitle, _detailBody;
    private GameObject _details;
    private Image _batteryFill;
    private TMP_FontAsset _font;
    private float _nameHideAt;

    public static void Ensure(Inventory inv)
    {
        var canvasGo = GameObject.Find("UI_Jeu");
        if (canvasGo == null) return;
        if (canvasGo.GetComponentInChildren<InventoryUI>(true) != null) return;
        var go = new GameObject("Inventaire_UI", typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(canvasGo.transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        var ui = go.AddComponent<InventoryUI>();
        ui._inv = inv;
        ui._font = canvasGo.GetComponentInChildren<TextMeshProUGUI>(true)?.font;
        ui.Build();
        inv.OnChanged += ui.Refresh;
        ui.Refresh();
    }

    private RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var g = new GameObject(name, typeof(RectTransform));
        g.layer = 5;
        g.transform.SetParent(parent, false);
        var r = (RectTransform)g.transform;
        r.anchorMin = r.anchorMax = anchor;
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos;
        r.sizeDelta = size;
        return r;
    }

    private TextMeshProUGUI Text(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, float fs, TextAlignmentOptions al, Color c)
    {
        var t = Rect(name, parent, anchor, pos, size).gameObject.AddComponent<TextMeshProUGUI>();
        if (_font != null) t.font = _font;
        t.fontSize = fs; t.alignment = al; t.color = c; t.raycastTarget = false;
        return t;
    }

    private void Build()
    {
        const float cell = 86f, gap = 10f;
        float total = Inventory.Size * cell + (Inventory.Size - 1) * gap;
        var bar = Rect("Barre", transform, new Vector2(0.5f, 0f), new Vector2(0f, 62f), new Vector2(total, cell));
        for (int i = 0; i < Inventory.Size; i++)
        {
            float x = -total * 0.5f + cell * 0.5f + i * (cell + gap);
            var border = Rect("Case_" + (i + 1), bar, new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(cell, cell)).gameObject.AddComponent<Image>();
            border.color = SlotBorder; border.raycastTarget = false;
            var bg = Rect("Fond", border.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(cell - 6f, cell - 6f)).gameObject.AddComponent<Image>();
            bg.color = SlotBg; bg.raycastTarget = false;
            var icon = Rect("Icone", border.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(cell - 14f, cell - 14f)).gameObject.AddComponent<RawImage>();
            icon.raycastTarget = false;
            var num = Text("Numero", border.transform, new Vector2(0f, 1f), new Vector2(12f, -12f), new Vector2(20f, 20f), 16f, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.5f));
            num.text = (i + 1).ToString();
            var count = Text("Quantite", border.transform, new Vector2(1f, 0f), new Vector2(-16f, 14f), new Vector2(30f, 22f), 18f, TextAlignmentOptions.Right, Bone);
            _borders.Add(border); _icons.Add(icon); _counts.Add(count);
        }
        _name = Text("Nom_Objet", transform, new Vector2(0.5f, 0f), new Vector2(0f, 128f), new Vector2(800f, 36f), 26f, TextAlignmentOptions.Center, Bone);

        // Jauge de la lampe torche
        var batt = Rect("Lampe", transform, new Vector2(0.5f, 0f), new Vector2(-total * 0.5f - 90f, 62f), new Vector2(120f, 18f));
        batt.gameObject.AddComponent<Image>().color = SlotBg;
        var fill = Rect("Charge", batt, new Vector2(0f, 0.5f), new Vector2(3f, 0f), new Vector2(114f, 12f));
        fill.pivot = new Vector2(0f, 0.5f);
        _batteryFill = fill.gameObject.AddComponent<Image>();
        _batteryFill.color = new Color(1f, 0.85f, 0.45f, 0.9f);
        var lbl = Text("Libelle", batt, new Vector2(0.5f, 1f), new Vector2(0f, 14f), new Vector2(140f, 22f), 16f, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.55f));
        lbl.text = "Lampe";

        // Panneau de détail (Tab)
        var d = Rect("Detail", transform, new Vector2(0.5f, 0f), new Vector2(0f, 260f), new Vector2(620f, 170f));
        d.gameObject.AddComponent<Image>().color = new Color(0.04f, 0.04f, 0.05f, 0.88f);
        _detailTitle = Text("Titre", d, new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(580f, 36f), 28f, TextAlignmentOptions.Left, SlotSel);
        _detailBody = Text("Texte", d, new Vector2(0.5f, 0.5f), new Vector2(0f, -18f), new Vector2(580f, 110f), 21f, TextAlignmentOptions.TopLeft, Bone);
        _details = d.gameObject;
        _details.SetActive(false);
    }

    private void Refresh()
    {
        if (_inv == null) return;
        for (int i = 0; i < Inventory.Size; i++)
        {
            var s = _inv.slots[i];
            bool sel = i == _inv.Selected;
            _borders[i].color = sel ? SlotSel : SlotBorder;
            _borders[i].rectTransform.localScale = Vector3.one * (sel ? 1.06f : 1f);
            _icons[i].texture = s.IsEmpty ? null : ItemIcons.Get(s.item);
            _icons[i].enabled = !s.IsEmpty;
            _counts[i].text = !s.IsEmpty && s.count > 1 ? s.count.ToString() : "";
        }
        var cur = _inv.Current;
        _name.text = cur != null ? cur.displayName : "";
        _nameHideAt = Time.unscaledTime + 2.5f;

        _details.SetActive(_inv.DetailsOpen);
        if (_inv.DetailsOpen)
        {
            _detailTitle.text = cur != null ? cur.displayName : "Emplacement vide";
            _detailBody.text = cur != null ? cur.description + "\n<color=#9E9A92><size=85%>" + UsageHint(cur) + "</size></color>" : "Rien en main.";
        }
    }

    private static string UsageHint(ItemData item)
    {
        string use = KeyBindings.KeyName(KeyBindings.Get(GameAction.UseItem));
        string drop = KeyBindings.KeyName(KeyBindings.Get(GameAction.DropItem));
        switch (item.kind)
        {
            case ItemKind.Note: return $"[{use}] lire   [{drop}] lâcher";
            case ItemKind.Battery: return $"[{use}] changer la pile de la lampe   [{drop}] lâcher";
            case ItemKind.Watch: return $"[{use}] regarder l'heure   [{drop}] lâcher";
            case ItemKind.Key: return $"Ouvre une porte verrouillée (E devant la porte)   [{drop}] lâcher";
            default: return $"[{use}] examiner   [{drop}] lâcher";
        }
    }

    private void Update()
    {
        if (_name != null && Time.unscaledTime > _nameHideAt && !_inv.DetailsOpen) _name.text = "";
        var fb = FlashlightBattery.Instance;
        if (_batteryFill != null && fb != null)
        {
            _batteryFill.rectTransform.sizeDelta = new Vector2(114f * fb.Charge, 12f);
            _batteryFill.color = fb.Charge < 0.2f ? new Color(1f, 0.3f, 0.25f, 0.9f) : new Color(1f, 0.85f, 0.45f, 0.9f);
        }
    }
}
