using UnityEngine;

/// <summary>
/// Vue sous-marine : quand la caméra passe sous la surface d'une eau profonde (lac),
/// brouillard dense vert-bleu, sons étouffés. Tout est rétabli en sortant de l'eau.
/// Se crée tout seul au lancement, rien à placer dans la scène.
/// </summary>
public class UnderwaterEffect : MonoBehaviour
{
    public Color waterColor = new Color(0.05f, 0.13f, 0.12f);
    public float density = 0.35f;
    [Tooltip("Fréquence de coupure du son sous l'eau (Hz).")]
    public float muffledCutoff = 600f;

    public static bool Active { get; private set; }

    bool _fog;
    FogMode _mode;
    float _density;
    Color _color;
    AudioLowPassFilter _lowPass;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindAnyObjectByType<UnderwaterEffect>() != null) return;
        var go = new GameObject("UnderwaterEffect");
        DontDestroyOnLoad(go);
        go.AddComponent<UnderwaterEffect>();
    }

    // LateUpdate : passe après le cycle jour/nuit et les zones d'étage qui règlent aussi le brouillard
    private void LateUpdate()
    {
        var cam = Camera.main;
        bool under = cam != null && WaterVolume.IsUnderwater(cam.transform.position);
        if (under && !Active) Enter();
        else if (!under && Active) Exit();
        if (!Active) return;

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared; // même mode que le reste du jeu (variantes de shader conservées en build)
        RenderSettings.fogDensity = density;
        RenderSettings.fogColor = waterColor;
    }

    void Enter()
    {
        Active = true;
        _fog = RenderSettings.fog;
        _mode = RenderSettings.fogMode;
        _density = RenderSettings.fogDensity;
        _color = RenderSettings.fogColor;

        var listener = FindAnyObjectByType<AudioListener>();
        if (listener != null)
        {
            _lowPass = listener.GetComponent<AudioLowPassFilter>();
            if (_lowPass == null) _lowPass = listener.gameObject.AddComponent<AudioLowPassFilter>();
            _lowPass.enabled = true;
            _lowPass.cutoffFrequency = muffledCutoff;
        }
    }

    void Exit()
    {
        Active = false;
        RenderSettings.fog = _fog;
        RenderSettings.fogMode = _mode;
        RenderSettings.fogDensity = _density;
        RenderSettings.fogColor = _color;
        if (_lowPass != null) _lowPass.enabled = false;
    }

    private void OnDisable()
    {
        if (Active) Exit();
    }
}
