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
    [Header("Voyant (optionnel)")]
    public Renderer lamp;
    public Material lampOff, lampOn;

    private bool _lit;

    private void Update()
    {
        if (lamp == null || elevator == null) return;
        bool lit = !elevator.IsBroken && elevator.IsMoving && elevator.TargetFloor == floorNumber;
        if (lit == _lit) return;
        _lit = lit;
        lamp.sharedMaterial = lit ? lampOn : lampOff;
    }

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
