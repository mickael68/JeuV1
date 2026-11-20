using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panneau d'options du menu principal : volumes, sensibilité, champ de vision, plein écran.
/// Les valeurs sont stockées dans le GameManager et sauvegardées (PlayerPrefs) à chaque changement.
/// </summary>
public class OptionsPanel : MonoBehaviour
{
    [Header("Réglages")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sensitivitySlider;
    [SerializeField] private Slider fovSlider;
    [SerializeField] private Toggle fullscreenToggle;

    [Header("Affichage des valeurs")]
    [SerializeField] private TextMeshProUGUI masterVolumeValue;
    [SerializeField] private TextMeshProUGUI musicVolumeValue;
    [SerializeField] private TextMeshProUGUI sensitivityValue;
    [SerializeField] private TextMeshProUGUI fovValue;

    private bool _bound;

    private void OnEnable()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        if (!_bound) Bind(gm);

        // Valeurs actuelles, sans déclencher les callbacks
        Init(masterVolumeSlider, 0f, 1f, gm.MasterVolume);
        Init(musicVolumeSlider, 0f, 1f, gm.MusicVolume);
        Init(sensitivitySlider, 0.5f, 10f, gm.MouseSensitivity);
        Init(fovSlider, 50f, 100f, gm.NormalFov);
        if (fullscreenToggle != null) fullscreenToggle.SetIsOnWithoutNotify(gm.Fullscreen);
        RefreshLabels();
    }

    private void Bind(GameManager gm)
    {
        _bound = true;
        if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.AddListener(v =>
        {
            gm.MasterVolume = v; gm.ApplyAudioAndDisplay(); Changed();
        });
        if (musicVolumeSlider != null) musicVolumeSlider.onValueChanged.AddListener(v =>
        {
            gm.MusicVolume = v;
            var menu = FindFirstObjectByType<MainMenu>();
            if (menu != null) menu.RefreshMusicVolume();
            Changed();
        });
        if (sensitivitySlider != null) sensitivitySlider.onValueChanged.AddListener(v => { gm.MouseSensitivity = v; Changed(); });
        if (fovSlider != null) fovSlider.onValueChanged.AddListener(v =>
        {
            // Le champ de vision en course suit le champ de vision normal
            gm.SprintFov = v + (gm.SprintFov - gm.NormalFov);
            gm.NormalFov = v;
            Changed();
        });
        if (fullscreenToggle != null) fullscreenToggle.onValueChanged.AddListener(on =>
        {
            gm.Fullscreen = on; gm.ApplyAudioAndDisplay(); Changed();
        });
    }

    private void Changed()
    {
        GameManager.Instance.SaveSettings();
        RefreshLabels();
    }

    private void RefreshLabels()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        if (masterVolumeValue != null) masterVolumeValue.text = Mathf.RoundToInt(gm.MasterVolume * 100f) + " %";
        if (musicVolumeValue != null) musicVolumeValue.text = Mathf.RoundToInt(gm.MusicVolume * 100f) + " %";
        if (sensitivityValue != null) sensitivityValue.text = gm.MouseSensitivity.ToString("0.0");
        if (fovValue != null) fovValue.text = Mathf.RoundToInt(gm.NormalFov) + "°";
    }

    private static void Init(Slider s, float min, float max, float value)
    {
        if (s == null) return;
        s.minValue = min;
        s.maxValue = max;
        s.SetValueWithoutNotify(value);
    }
}
