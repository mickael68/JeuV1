using UnityEngine;

/// <summary>
/// Meuble sur lequel on peut s'asseoir (chaise, fauteuil, lit) : E pour s'asseoir, E (ou Espace) pour se lever.
/// Le point d'assise est au centre du meuble, à "seatHeight" au-dessus du sol ; on regarde vers "facingYaw".
/// </summary>
public class Seat : MonoBehaviour, IInteractable
{
    [Tooltip("Hauteur de l'assise au-dessus du sol (m).")]
    public float seatHeight = 0.48f;
    [Tooltip("Direction du regard une fois assis (degrés autour de Y, monde).")]
    public float facingYaw;
    [Tooltip("Décalage horizontal du point d'assise depuis le centre du meuble (monde).")]
    public Vector3 offset;
    public string label = "S'asseoir";

    public string GetInteractText()
    {
        var p = PlayerController.Instance;
        return p != null && p.IsSeated ? "Se lever" : label;
    }

    public void Interact()
    {
        var p = PlayerController.Instance;
        if (p == null) return;
        if (p.IsSeated) p.StandUp();
        else p.SitOn(this);
    }

    /// Point d'assise (sous les fesses), en monde.
    public Vector3 SeatPoint
    {
        get
        {
            var c = transform.position;
            var col = GetComponentInChildren<Collider>();
            if (col != null) c = col.bounds.center;
            float floor = col != null ? col.bounds.min.y : transform.position.y;
            return new Vector3(c.x, floor + seatHeight, c.z) + offset;
        }
    }
}
