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
    [Tooltip("Eau profonde : hauteur (monde) de la surface. Sous cette hauteur, la caméra passe en vue sous-marine.")]
    public bool hasSurface;
    public float surfaceY;

    static readonly List<WaterVolume> Inside = new List<WaterVolume>();
    static readonly List<WaterVolume> Deep = new List<WaterVolume>();
    Collider _col;

    /// La position est-elle sous la surface d'une eau profonde ?
    public static bool IsUnderwater(Vector3 pos)
    {
        for (int i = Deep.Count - 1; i >= 0; i--)
        {
            var w = Deep[i];
            if (w == null) { Deep.RemoveAt(i); continue; }
            if (pos.y >= w.surfaceY) continue;
            var b = w._col.bounds;
            if (pos.y < b.min.y - 4f) continue;
            var q = new Vector3(pos.x, Mathf.Clamp(pos.y, b.min.y + 0.01f, b.max.y - 0.01f), pos.z);
            if ((w._col.ClosestPoint(q) - q).sqrMagnitude < 1e-4f) return true;
        }
        return false;
    }

    private void OnEnable()
    {
        _col = GetComponent<Collider>();
        if (hasSurface && !Deep.Contains(this)) Deep.Add(this);
    }

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

    private void OnDisable() { Inside.Remove(this); Deep.Remove(this); }
}
