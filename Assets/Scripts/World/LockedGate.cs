using UnityEngine;

/// <summary>
/// Grille verrouillée (ex. : escalier caché du -1 vers le -2).
/// Reste fermée tant que les missions du -1 ne sont pas terminées : appeler Unlock() depuis
/// le script de mission, puis le joueur l'ouvre avec E.
/// </summary>
[RequireComponent(typeof(Collider))]
public class LockedGate : MonoBehaviour, IInteractable
{
    public bool locked = true;
    [TextArea] public string lockedMessage = "La grille est cadenassée. Il reste quelque chose à faire ici...";

    public void Unlock() => locked = false;

    public string GetInteractText() => locked ? "Grille verrouillée" : "Ouvrir la grille";

    public void Interact()
    {
        if (locked)
        {
            Debug.Log("[Grille] " + lockedMessage);
            return;
        }
        gameObject.SetActive(false);
    }
}
