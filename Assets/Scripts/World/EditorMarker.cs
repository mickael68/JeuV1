using UnityEngine;

/// <summary>
/// Repère de l'éditeur (sphère colorée : chambre du joueur, indices, apparitions…) : visible dans la scène,
/// caché en jeu.
/// </summary>
public class EditorMarker : MonoBehaviour
{
    private void Awake()
    {
        foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = false;
    }
}
