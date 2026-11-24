using UnityEngine;

/// <summary>
/// Fond d'un gouffre : si le joueur y tombe, il remonte péniblement jusqu'au bord (point de reprise).
/// </summary>
[RequireComponent(typeof(Collider))]
public class FallTrap : MonoBehaviour
{
    public Transform respawn;
    public string message = "Tu as glissé dans une faille… Tu remontes péniblement jusqu'au bord.";

    private void Reset() => GetComponent<Collider>().isTrigger = true;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player") || respawn == null) return;
        var p = other.GetComponent<PlayerController>();
        if (p == null) return;
        p.Teleport(respawn.position, respawn.rotation);
        HudMessage.Show(message, 4f);
    }
}
