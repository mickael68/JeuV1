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

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
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
