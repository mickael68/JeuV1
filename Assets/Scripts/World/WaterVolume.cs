using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Volume d'eau où l'on marche (lac, rivière, salle inondée, flaque) :
/// ralentit le joueur et remplace le bruit de pas par des éclaboussures.
/// </summary>
[RequireComponent(typeof(Collider))]
public class WaterVolume : MonoBehaviour
{
    [Tooltip("Vitesse du joueur dans l'eau (1 = normale).")]
    [Range(0.2f, 1f)] public float speedMultiplier = 0.65f;
    [Tooltip("Bruit de pas dans l'eau (vide = bruit de pas normal).")]
    public AudioClip splash;

    static readonly List<WaterVolume> Inside = new List<WaterVolume>();

    /// Ralentissement actuel du joueur (le plus fort des volumes où il se trouve).
    public static float PlayerSpeedMultiplier
    {
        get
        {
            float m = 1f;
            for (int i = Inside.Count - 1; i >= 0; i--)
                if (Inside[i] == null) Inside.RemoveAt(i); else m = Mathf.Min(m, Inside[i].speedMultiplier);
            return m;
        }
    }

    /// Bruit de pas mouillé du volume où se trouve le joueur, ou null.
    public static AudioClip PlayerSplash
    {
        get
        {
            for (int i = Inside.Count - 1; i >= 0; i--)
                if (Inside[i] != null && Inside[i].splash != null) return Inside[i].splash;
            return null;
        }
    }

    public static bool PlayerInWater => Inside.Count > 0;

    private void Reset() => GetComponent<Collider>().isTrigger = true;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !Inside.Contains(this)) Inside.Add(this);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player")) Inside.Remove(this);
    }

    private void OnDisable() => Inside.Remove(this);
}
