using UnityEngine;

/// <summary>
/// Écran d'étage de l'ascenseur (cabine et paliers) : numéro de l'étage où se trouve la cabine,
/// flèche ▲ / ▼ pendant le trajet ; « - - » qui clignote quand l'ascenseur est en panne.
/// </summary>
public class ElevatorDisplay : MonoBehaviour
{
    public Elevator elevator;
    public TextMesh text;

    private string _last;

    private void Update()
    {
        if (elevator == null || text == null) return;
        string s;
        if (elevator.IsBroken) s = Mathf.Repeat(Time.time, 1f) < 0.5f ? "- -" : "";
        else
        {
            string arrow = elevator.Direction > 0 ? "▲ " : elevator.Direction < 0 ? "▼ " : "";
            s = arrow + elevator.DisplayFloor;
        }
        if (s == _last) return;
        _last = s;
        text.text = s;
    }
}
