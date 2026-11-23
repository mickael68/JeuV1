using UnityEngine;

/// <summary>Objet posé dans le monde : E pour le ramasser (s'il reste de la place dans l'inventaire).</summary>
public class ItemPickup : MonoBehaviour, IInteractable
{
    public ItemData item;
    public int count = 1;

    public string GetInteractText() => item != null ? "Ramasser : " + item.displayName : "Ramasser";

    public void Interact()
    {
        var inv = Inventory.Instance;
        if (inv == null || item == null) return;
        int left = inv.Add(item, count);
        if (left == count) { HudMessage.Show("Inventaire plein.", 2f); return; }
        if (left > 0) { count = left; return; }
        Destroy(gameObject);
    }
}
