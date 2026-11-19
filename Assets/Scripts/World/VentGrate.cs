using UnityEngine;

/// <summary>
/// Grille de bouche d'aération : E pour la retirer, puis passer accroupi (Ctrl / C).
/// </summary>
[RequireComponent(typeof(Collider))]
public class VentGrate : MonoBehaviour, IInteractable
{
    public bool removed;

    public string GetInteractText() => "Retirer la grille d'aération";

    public void Interact()
    {
        if (removed) return;
        removed = true;
        GetComponent<Collider>().enabled = false;
        // La grille tombe au sol, à plat devant l'ouverture (forward = côté de la pièce).
        transform.position += transform.forward * 0.6f + Vector3.down * 0.5f;
        transform.Rotate(90f, 0f, 0f, Space.Self);
    }
}
