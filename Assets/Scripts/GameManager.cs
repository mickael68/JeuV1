using UnityEngine;

public enum GameMode
{
    None,
    Sandbox,
    Story
}

/// <summary>
/// Singleton central du jeu — persiste entre les scènes.
/// Stocke les réglages gameplay et gère les transitions de scène.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public GameMode CurrentMode { get; set; } = GameMode.None;

    // ── Réglages Gameplay (persistants entre les scènes) ──
    [Header("Gameplay Settings")]
    public float MouseSensitivity = 2.0f;       // Unity utilise des valeurs plus hautes que Godot
    public float NormalFov = 70f;
    public float SprintFov = 80f;
    public float HeadBobAmplitude = 0.04f;
    public float WalkSpeed = 5.0f;
    public float SprintSpeed = 8.0f;

    // ── Réglages audio / affichage ──
    [Header("Audio / Display")]
    [Range(0f, 1f)] public float MasterVolume = 1f;
    [Range(0f, 1f)] public float MusicVolume = 0.8f;
    public bool Fullscreen = true;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadSettings();
    }

    // ── Sauvegarde des réglages (PlayerPrefs) ──

    public void LoadSettings()
    {
        MouseSensitivity = PlayerPrefs.GetFloat("opt_sensitivity", MouseSensitivity);
        NormalFov = PlayerPrefs.GetFloat("opt_fov", NormalFov);
        SprintFov = PlayerPrefs.GetFloat("opt_sprint_fov", SprintFov);
        WalkSpeed = PlayerPrefs.GetFloat("opt_walk_speed", WalkSpeed);
        SprintSpeed = PlayerPrefs.GetFloat("opt_sprint_speed", SprintSpeed);
        MasterVolume = PlayerPrefs.GetFloat("opt_master_volume", MasterVolume);
        MusicVolume = PlayerPrefs.GetFloat("opt_music_volume", MusicVolume);
        Fullscreen = PlayerPrefs.GetInt("opt_fullscreen", Fullscreen ? 1 : 0) == 1;
        ApplyAudioAndDisplay();
    }

    public void SaveSettings()
    {
        PlayerPrefs.SetFloat("opt_sensitivity", MouseSensitivity);
        PlayerPrefs.SetFloat("opt_fov", NormalFov);
        PlayerPrefs.SetFloat("opt_sprint_fov", SprintFov);
        PlayerPrefs.SetFloat("opt_walk_speed", WalkSpeed);
        PlayerPrefs.SetFloat("opt_sprint_speed", SprintSpeed);
        PlayerPrefs.SetFloat("opt_master_volume", MasterVolume);
        PlayerPrefs.SetFloat("opt_music_volume", MusicVolume);
        PlayerPrefs.SetInt("opt_fullscreen", Fullscreen ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void ApplyAudioAndDisplay()
    {
        AudioListener.volume = MasterVolume;
        if (!Application.isEditor && Screen.fullScreen != Fullscreen) Screen.fullScreen = Fullscreen;
    }

    public void StartGame(GameMode mode)
    {
        CurrentMode = mode;
        UnityEngine.SceneManagement.SceneManager.LoadScene("Main");
    }

    public void ReturnToMenu()
    {
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
