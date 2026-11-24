using UnityEngine;

/// <summary>
/// Cycle jour/nuit continu : le soleil tourne en douceur, et ciel, lumière ambiante et brouillard
/// passent progressivement de la nuit à l'aube, au jour, au crépuscule puis à la nuit.
/// Les quatre ciels HDRI sont fondus deux à deux par le shader "TheEchoesBelow/Skybox Blend".
/// </summary>
public class DayNightCycle : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Light sunLight;
    [SerializeField] private TimeManager timeManager;
    [SerializeField] private Shader skyBlendShader;

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

    // Moments clés de la journée (heure, ambiance). Entre deux clés, tout est fondu en douceur.
    enum Phase { Night, Morning, Day, Evening }
    static readonly (float hour, Phase phase)[] Keys =
    {
        (0f, Phase.Night), (4.5f, Phase.Night), (6f, Phase.Morning), (8f, Phase.Day),
        (17f, Phase.Day), (19f, Phase.Evening), (21f, Phase.Night), (24f, Phase.Night),
    };

    private Material _sky;
    private float _lastGiUpdate = -10f;
    private float _lastBlendKey = -1f;

    private void Start()
    {
        if (sunLight == null) sunLight = RenderSettings.sun != null ? RenderSettings.sun : FindFirstObjectByType<Light>();
        if (timeManager == null) timeManager = FindFirstObjectByType<TimeManager>();
        if (timeManager == null) { Debug.LogError("[DayNightCycle] TimeManager introuvable !"); enabled = false; return; }

        if (skyBlendShader == null) skyBlendShader = Shader.Find("TheEchoesBelow/Skybox Blend");
        if (skyBlendShader != null) _sky = new Material(skyBlendShader) { name = "Ciel_Fondu (runtime)" };
        Apply(true);
    }

    private void Update() => Apply(false);

    private void Apply(bool force)
    {
        float hour = timeManager.NormalizedTime * 24f;

        // 1) Soleil : rotation continue, intensité et couleur selon sa hauteur
        if (sunLight != null)
        {
            float angle = -((hour - 6f) / 24f * 360f);
            sunLight.transform.rotation = Quaternion.Euler(angle, -30f, 0f);
            float elevation = Mathf.Sin((hour - 6f) / 24f * Mathf.PI * 2f); // 1 à midi, -1 à minuit
            float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.08f, 0.3f, elevation));
            sunLight.intensity = 1.2f * k;
            sunLight.color = Color.Lerp(new Color(1f, 0.55f, 0.3f), new Color(1f, 0.96f, 0.9f), Mathf.InverseLerp(0.05f, 0.45f, elevation));
            sunLight.enabled = k > 0.01f;
        }

        // 2) Entre quelles phases est-on, et à quel point ?
        int i = 0;
        while (i < Keys.Length - 2 && hour >= Keys[i + 1].hour) i++;
        var a = Keys[i]; var b = Keys[i + 1];
        float t = a.phase == b.phase ? 0f : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a.hour, b.hour, hour));

        // 3) Ciel fondu
        if (_sky != null)
        {
            SetSky("_TexA", "_ExposureA", SkyOf(a.phase));
            SetSky("_TexB", "_ExposureB", SkyOf(b.phase));
            _sky.SetFloat("_Blend", t);
            if (RenderSettings.skybox != _sky) RenderSettings.skybox = _sky;
        }
        else if (SkyOf(t < 0.5f ? a.phase : b.phase) is Material m && RenderSettings.skybox != m)
            RenderSettings.skybox = m; // repli sans le shader : changement net à mi-transition

        // 4) Lumière ambiante et brouillard (les sous-sols imposent les leurs via FloorZone)
        RenderSettings.ambientLight = Color.Lerp(AmbientOf(a.phase), AmbientOf(b.phase), t);
        RenderSettings.ambientIntensity = Mathf.Lerp(IntensityOf(a.phase), IntensityOf(b.phase), t);
        RenderSettings.fogColor = Color.Lerp(FogOf(a.phase), FogOf(b.phase), t);

        // L'éclairage ambiant venant du ciel : mis à jour régulièrement pendant les transitions (coûteux)
        float blendKey = i + Mathf.Round(t * 20f) / 20f;
        if (force || (blendKey != _lastBlendKey && Time.unscaledTime - _lastGiUpdate > 1.5f))
        {
            _lastBlendKey = blendKey;
            _lastGiUpdate = Time.unscaledTime;
            DynamicGI.UpdateEnvironment();
        }
    }

    private void SetSky(string texProp, string expProp, Material src)
    {
        if (src == null) return;
        _sky.SetTexture(texProp, src.GetTexture("_MainTex"));
        _sky.SetFloat(expProp, src.HasProperty("_Exposure") ? src.GetFloat("_Exposure") : 1f);
    }

    private Material SkyOf(Phase p) => p switch
    {
        Phase.Morning => morningSkybox,
        Phase.Day => daySkybox,
        Phase.Evening => eveningSkybox,
        _ => nightSkybox,
    };

    private Color AmbientOf(Phase p) => p == Phase.Day ? dayAmbientColor : p == Phase.Night ? nightAmbientColor : dawnDuskAmbientColor;
    private Color FogOf(Phase p) => p == Phase.Day ? dayFogColor : p == Phase.Night ? nightFogColor : dawnDuskFogColor;
    private static float IntensityOf(Phase p) => p == Phase.Day ? 1f : p == Phase.Night ? 0.15f : 0.5f;

    private void OnDestroy()
    {
        if (_sky != null) Destroy(_sky);
    }
}
