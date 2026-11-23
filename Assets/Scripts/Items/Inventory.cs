using System;
using UnityEngine;

/// <summary>
/// Inventaire du joueur : une barre de 6 emplacements. Touches 1 à 6 ou molette pour choisir,
/// l'objet choisi est tenu en main ; clic gauche pour l'utiliser, G pour le lâcher, Tab pour le détail.
/// </summary>
public class Inventory : MonoBehaviour
{
    public const int Size = 6;

    [Serializable]
    public struct Slot
    {
        public ItemData item;
        public int count;
        public bool IsEmpty => item == null || count <= 0;
    }

    public static Inventory Instance { get; private set; }

    public readonly Slot[] slots = new Slot[Size];
    public int Selected { get; private set; }
    public bool DetailsOpen { get; private set; }

    public event Action OnChanged;

    private Transform _hand;
    private GameObject _held;
    private ItemData _heldItem;

    private void Awake() => Instance = this;

    private void Start()
    {
        var cam = Camera.main != null ? Camera.main.transform : transform;
        _hand = new GameObject("Main_Objet").transform;
        _hand.SetParent(cam, false);
        _hand.localPosition = new Vector3(0.32f, -0.28f, 0.55f);
        _hand.localRotation = Quaternion.Euler(8f, -18f, 0f);
        InventoryUI.Ensure(this);
        RefreshHeld();
    }

    // ── Contenu ──

    public ItemData Current => slots[Selected].IsEmpty ? null : slots[Selected].item;

    /// Ajoute des objets ; renvoie le nombre qui n'a pas trouvé de place.
    public int Add(ItemData item, int count = 1)
    {
        if (item == null || count <= 0) return count;
        if (item.stackable)
            for (int i = 0; i < Size && count > 0; i++)
                if (slots[i].item == item && slots[i].count < item.maxStack)
                {
                    int n = Mathf.Min(count, item.maxStack - slots[i].count);
                    slots[i].count += n; count -= n;
                }
        for (int i = 0; i < Size && count > 0; i++)
            if (slots[i].IsEmpty)
            {
                int n = item.stackable ? Mathf.Min(count, item.maxStack) : 1;
                slots[i] = new Slot { item = item, count = n };
                count -= n;
                if (Current == null) Selected = i;
            }
        if (count == 0) HudMessage.Show("Ramassé : " + item.displayName, 2f);
        Changed();
        return count;
    }

    public bool HasKey(string keyId)
    {
        if (string.IsNullOrEmpty(keyId)) return false;
        foreach (var s in slots) if (!s.IsEmpty && s.item.kind == ItemKind.Key && s.item.keyId == keyId) return true;
        return false;
    }

    public void RemoveAt(int index, int n = 1)
    {
        if (slots[index].IsEmpty) return;
        slots[index].count -= n;
        if (slots[index].count <= 0) slots[index] = new Slot();
        Changed();
    }

    private void Changed()
    {
        RefreshHeld();
        OnChanged?.Invoke();
    }

    // ── Entrées ──

    private void Update()
    {
        if (Cursor.lockState != CursorLockMode.Locked) return; // pause / menus
        if (NoteReader.IsOpen) return;

        for (int i = 0; i < Size; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i)) Select(i);
        float wheel = Input.mouseScrollDelta.y;
        if (wheel > 0.1f) Select((Selected + Size - 1) % Size);
        else if (wheel < -0.1f) Select((Selected + 1) % Size);

        if (KeyBindings.Down(GameAction.Inventory)) { DetailsOpen = !DetailsOpen; OnChanged?.Invoke(); }
        if (KeyBindings.Down(GameAction.UseItem)) Use();
        if (KeyBindings.Down(GameAction.DropItem)) Drop();
    }

    public void Select(int i)
    {
        if (i == Selected) return;
        Selected = Mathf.Clamp(i, 0, Size - 1);
        Changed();
    }

    // ── Utiliser / lâcher ──

    private void Use()
    {
        var item = Current;
        if (item == null) return;
        switch (item.kind)
        {
            case ItemKind.Note:
                NoteReader.Open(item.displayName, item.noteText);
                break;
            case ItemKind.Battery:
                var fb = FlashlightBattery.Instance;
                if (fb == null) break;
                if (fb.Charge > 0.97f) { HudMessage.Show("La lampe est déjà chargée.", 2f); break; }
                fb.Recharge(item.batteryCharge);
                HudMessage.Show("Pile changée : la lampe torche est rechargée.", 2.5f);
                RemoveAt(Selected);
                break;
            case ItemKind.Watch:
                var tm = FindFirstObjectByType<TimeManager>();
                if (tm != null)
                {
                    float hours = tm.NormalizedTime * 24f;
                    int h = Mathf.FloorToInt(hours) % 24, m = Mathf.FloorToInt((hours - Mathf.Floor(hours)) * 60f);
                    HudMessage.Show($"La montre indique {h:00}h{m:00}.", 3f);
                }
                break;
            case ItemKind.Key:
                HudMessage.Show(item.displayName + " : approche-toi d'une porte verrouillée et appuie sur E.", 3f);
                break;
            default:
                if (!string.IsNullOrEmpty(item.description)) HudMessage.Show(item.description, 3.5f);
                break;
        }
        if (item.consumedOnUse && item.kind != ItemKind.Battery) RemoveAt(Selected);
    }

    private void Drop()
    {
        var item = Current;
        if (item == null || item.model == null) return;
        var cam = Camera.main.transform;
        var from = cam.position + cam.forward * 0.9f;
        var pos = Physics.Raycast(from, Vector3.down, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore) ? hit.point : transform.position + transform.forward;
        if (Physics.Raycast(cam.position, cam.forward, out var wall, 1f, ~0, QueryTriggerInteraction.Ignore)) // pas à travers un mur
            pos = new Vector3(transform.position.x, pos.y, transform.position.z) + transform.forward * 0.4f;
        SpawnPickup(item, 1, pos, Quaternion.Euler(0f, cam.eulerAngles.y, 0f));
        RemoveAt(Selected);
    }

    /// Crée un objet ramassable dans le monde (aussi utilisé par le générateur de map).
    public static GameObject SpawnPickup(ItemData item, int count, Vector3 floorPos, Quaternion rot)
    {
        var go = Instantiate(item.model, floorPos, rot);
        go.name = "Objet_" + item.id;
        if (go.GetComponentInChildren<Collider>() == null)
        {
            var b = Bounds(go);
            var bc = go.AddComponent<BoxCollider>();
            bc.center = go.transform.InverseTransformPoint(b.center);
            bc.size = Vector3.Scale(b.size, new Vector3(1f / go.transform.lossyScale.x, 1f / go.transform.lossyScale.y, 1f / go.transform.lossyScale.z)) + Vector3.one * 0.05f;
        }
        var p = go.GetComponent<ItemPickup>();
        if (p == null) p = go.AddComponent<ItemPickup>();
        p.item = item; p.count = count;
        return go;
    }

    // ── Objet tenu en main ──

    private void RefreshHeld()
    {
        if (_hand == null) return;
        var item = Current;
        if (item == _heldItem && _held != null) return;
        if (_held != null) Destroy(_held);
        _held = null; _heldItem = item;
        if (item == null || item.model == null) return;

        _held = Instantiate(item.model, _hand);
        _held.name = "En_Main_" + item.id;
        foreach (var c in _held.GetComponentsInChildren<Collider>()) Destroy(c);
        foreach (var p in _held.GetComponentsInChildren<ItemPickup>()) Destroy(p);
        foreach (var r in _held.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _held.transform.localPosition = Vector3.zero;
        _held.transform.localRotation = Quaternion.identity;
        _held.transform.localScale = Vector3.one;
        var b = Bounds(_held);
        float size = Mathf.Max(b.size.x, b.size.y, b.size.z);
        if (size > 0.001f) _held.transform.localScale = Vector3.one * (item.handSize / size);
        b = Bounds(_held);
        _held.transform.position += _hand.position - b.center;
    }

    private static Bounds Bounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one * 0.1f);
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }
}
