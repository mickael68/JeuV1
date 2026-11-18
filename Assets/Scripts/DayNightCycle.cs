using UnityEngine;

/// <summary>
/// Cycle jour/nuit — fait tourner le soleil et change le skybox.
/// Remplace le système Godot WorldEnvironment + PanoramaSkyMaterial.
/// </summary>
public class DayNightCycle : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Light sunLight;
    [SerializeField] private TimeManager timeManager;

    [Header("Sky Materials")]
    [SerializeField] private Material morningSkybox;
    [SerializeField] private Material daySkybox;
    [SerializeField] private Material eveningSkybox;
    [SerializeField] private Material nightSkybox;

    [Header("Ambient Colors")]
    [SerializeField] private Color dayAmbientColor = new Color(0.65f, 0.75f, 0.85f);
    [SerializeField] private Color dawnDuskAmbientColor = new Color(0.85f, 0.55f, 0.35f);
    [SerializeField] private Color nightAmbientColor = new Color(0.05f, 0.05f, 0.1f);

    [Header("Fog Colors")]
    [SerializeField] private Color dayFogColor = new Color(0.65f, 0.75f, 0.85f);
    [SerializeField] private Color dawnDuskFogColor = new Color(0.85f, 0.55f, 0.35f);
    [SerializeField] private Color nightFogColor = new Color(0.05f, 0.05f, 0.1f);

    private void Start()
    {
        if (sunLight == null) sunLight = FindFirstObjectByType<Light>();
        if (timeManager == null) timeManager = FindFirstObjectByType<TimeManager>();

        if (timeManager != null)
        {
            timeManager.OnHourChanged += UpdateEnvironment;
            UpdateEnvironment(timeManager.CurrentHour);
        }
        else
        {
            Debug.LogError("[DayNightCycle] TimeManager introuvable !");
        }
    }

    private void UpdateEnvironment(int hour)
    {
        // 1. Rotation du soleil (identique à Godot)
        float angle = -((hour - 6.0f) / 24.0f * 360.0f);
        if (sunLight != null)
        {
            sunLight.transform.rotation = Quaternion.Euler(angle, -30f, 0f);

            // Intensité
            if (hour >= 6 && hour < 20)
                sunLight.intensity = 1.2f;
            else if (hour >= 20 || hour < 5)
                sunLight.intensity = 0.1f;
            else
                sunLight.intensity = 0.5f;

            // Masquer le soleil sous l'horizon
            sunLight.enabled = (hour >= 5 && hour < 20);
        }

        // 2. Skybox + ambiance
        Material targetSkybox = null;
        Color ambientColor;
        Color fogColor;
        float ambientIntensity;

        if (hour >= 6 && hour < 18)
        {
            // Plein Jour
            targetSkybox = daySkybox;
            ambientColor = dayAmbientColor;
            fogColor = dayFogColor;
            ambientIntensity = 1.0f;
        }
        else if ((hour >= 18 && hour < 20) || (hour >= 5 && hour < 6))
        {
            // Aube / Crépuscule
            targetSkybox = hour >= 18 ? eveningSkybox : morningSkybox;
            ambientColor = dawnDuskAmbientColor;
            fogColor = dawnDuskFogColor;
            ambientIntensity = 0.5f;
        }
        else
        {
            // Nuit
            targetSkybox = nightSkybox;
            ambientColor = nightAmbientColor;
            fogColor = nightFogColor;
            ambientIntensity = 0.15f;
        }

        // Appliquer le skybox
        if (targetSkybox != null)
        {
            RenderSettings.skybox = targetSkybox;
            DynamicGI.UpdateEnvironment(); // Mettre à jour le Global Illumination
        }

        // Appliquer l'éclairage ambiant
        RenderSettings.ambientLight = ambientColor;
        RenderSettings.ambientIntensity = ambientIntensity;

        // Brouillard
        RenderSettings.fogColor = fogColor;
    }

    private void OnDestroy()
    {
        if (timeManager != null)
        {
            timeManager.OnHourChanged -= UpdateEnvironment;
        }
    }
}
