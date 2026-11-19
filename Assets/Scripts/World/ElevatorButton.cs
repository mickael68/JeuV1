using UnityEngine;

/// <summary>
/// Bouton d'ascenseur (appel sur le palier ou choix d'étage dans la cabine). Touche E.
/// </summary>
public class ElevatorButton : MonoBehaviour, IInteractable
{
    public Elevator elevator;
    public int floorNumber;
    [Tooltip("Vrai pour le bouton d'appel d'un palier.")]
    public bool isCallButton;

    public string GetInteractText()
    {
        if (elevator != null && elevator.IsBroken) return "L'ascenseur ne répond plus";
        return isCallButton ? "Appeler l'ascenseur" : $"Étage {floorNumber}";
    }

    public void Interact()
    {
        if (elevator != null) elevator.Request(floorNumber);
    }
}
