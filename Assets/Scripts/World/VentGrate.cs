using UnityEngine;

/// <summary>
/// Grille de bouche d'aération : E pour la retirer, puis passer accroupi.
/// floorGrate : grille posée au sol (trappe) — elle est poussée sur le côté au lieu de tomber.
/// </summary>
[RequireComponent(typeof(Collider))]
public class VentGrate : MonoBehaviour, IInteractable
{
    public bool removed;
    public bool floorGrate;

    public string GetInteractText() => floorGrate ? "Retirer la grille (le conduit descend)" : "Retirer la grille d'aération";

    public void Interact()
    {
        if (removed) return;
        removed = true;
        GetComponent<Collider>().enabled = false;
        if (floorGrate)
        {
            // Trappe au sol : glissée à côté de l'ouverture
            transform.position += transform.right * 1.3f + Vector3.up * 0.02f;
            transform.Rotate(0f, 12f, 0f, Space.World);
            HudMessage.Show("Un conduit étroit descend dans le noir… Il faudra ramper (accroupi).", 4f);
            return;
        }
        // La grille tombe au sol, à plat devant l'ouverture (forward = côté de la pièce).
        transform.position += transform.forward * 0.6f + Vector3.down * 0.5f;
        transform.Rotate(90f, 0f, 0f, Space.Self);
    }
}
