using System;
using UnityEngine;

/// <summary>
/// Volume (trigger) qui couvre un étage de la résidence.
/// - Sait sur quel étage se trouve le joueur (FloorZone.Current / OnFloorChanged).
/// - Impose l'ambiance de l'étage (lumière ambiante + brouillard) quand overrideAmbience est actif.
///   Les étages habités (1 à 3) laissent DayNightCycle gérer l'ambiance.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class FloorZone : MonoBehaviour
{
    public static FloorZone Current { get; private set; }
    public static event Action<FloorZone> OnFloorChanged;

    [Header("Identité")]
    public int floorNumber;
    public string displayName;
    [Range(0, 5)] public int dangerLevel;

    [Header("Ambiance")]
    [Tooltip("Si vrai, l'étage impose son ambiance (prend le dessus sur DayNightCycle).")]
    public bool overrideAmbience = true;
    public Color ambientColor = Color.black;
    public Color fogColor = Color.black;
    [Range(0f, 0.3f)] public float fogDensity = 0.05f;
    [Tooltip("Reflets de l'environnement (ciel) : 0 sous terre.")]
    [Range(0f, 1f)] public float reflectionIntensity = 1f;

    // Ambiance d'origine de la scène, restaurée quand on revient sur un étage "normal".
    private static bool _captured;
    private static bool _fog0;
    private static float _fogDensity0;
    private static Color _fogColor0, _ambient0;
    private static float _reflection0 = 1f;
    private static UnityEngine.Rendering.AmbientMode _ambientMode0;

    private void Reset()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    private void Awake()
    {
        if (_captured) return;
        _captured = true;
        _fog0 = RenderSettings.fog;
        _fogDensity0 = RenderSettings.fogDensity;
        _fogColor0 = RenderSettings.fogColor;
        _ambient0 = RenderSettings.ambientLight;
        _ambientMode0 = RenderSettings.ambientMode;
        _reflection0 = RenderSettings.reflectionIntensity;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player") || Current == this) return;
        bool wasOverriding = Current != null && Current.overrideAmbience;
        Current = this;
        if (!overrideAmbience && wasOverriding)
        {
            // DayNightCycle reprend la main ensuite sur la lumière ambiante.
            RenderSettings.fog = _fog0;
            RenderSettings.fogDensity = _fogDensity0;
            RenderSettings.fogColor = _fogColor0;
            RenderSettings.ambientMode = _ambientMode0;
            RenderSettings.ambientLight = _ambient0;
            RenderSettings.reflectionIntensity = _reflection0;
        }
        OnFloorChanged?.Invoke(this);
        Debug.Log($"[FloorZone] Étage {floorNumber} : {displayName} (danger {dangerLevel})");
    }

    private void OnDisable()
    {
        if (Current == this) Current = null;
    }

    // LateUpdate : passe après DayNightCycle.Update, donc l'ambiance de l'étage gagne.
    private void LateUpdate()
    {
        if (Current != this || !overrideAmbience) return;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = ambientColor;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = fogDensity;
        RenderSettings.reflectionIntensity = reflectionIntensity;
    }
}
