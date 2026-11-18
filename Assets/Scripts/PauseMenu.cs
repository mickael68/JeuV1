using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

/// <summary>
/// Menu de pause in-game avec options et confirmation de quitter.
/// Remplace le CanvasLayer Godot par un Canvas Unity.
/// Les animations Tween sont remplacées par des Coroutines.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject optionsPanel;
    [SerializeField] private GameObject quitPanel;

    [Header("Main Panel Buttons")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button quitButton;

    [Header("Options")]
    [SerializeField] private Slider sensitivitySlider;
    [SerializeField] private Slider fovSlider;
    [SerializeField] private Slider sprintFovSlider;
    [SerializeField] private Slider walkSpeedSlider;
    [SerializeField] private Slider sprintSpeedSlider;
    [SerializeField] private Button optionsBackButton;

    [Header("Quit Confirmation")]
    [SerializeField] private Button quitYesButton;
    [SerializeField] private Button quitNoButton;

    private CanvasGroup _mainCanvasGroup;
    private CanvasGroup _optionsCanvasGroup;
    private CanvasGroup _quitCanvasGroup;

    public bool IsOpen { get; private set; } = false;

    private void Awake()
    {
        // Ajouter des CanvasGroups pour les animations de fade
        _mainCanvasGroup = EnsureCanvasGroup(mainPanel);
        _optionsCanvasGroup = EnsureCanvasGroup(optionsPanel);
        _quitCanvasGroup = EnsureCanvasGroup(quitPanel);

        // Connecter les boutons
        if (resumeButton != null) resumeButton.onClick.AddListener(CloseMenu);
        if (optionsButton != null) optionsButton.onClick.AddListener(() => TransitionToPanel(mainPanel, optionsPanel));
        if (quitButton != null) quitButton.onClick.AddListener(() => TransitionToPanel(mainPanel, quitPanel));
        if (optionsBackButton != null) optionsBackButton.onClick.AddListener(() => TransitionToPanel(optionsPanel, mainPanel));
        if (quitYesButton != null) quitYesButton.onClick.AddListener(ConfirmQuit);
        if (quitNoButton != null) quitNoButton.onClick.AddListener(() => TransitionToPanel(quitPanel, mainPanel));

        // Setup sliders
        SetupSliders();

        // Désactivé au départ
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            if (optionsPanel.activeSelf)
                TransitionToPanel(optionsPanel, mainPanel);
            else if (quitPanel.activeSelf)
                TransitionToPanel(quitPanel, mainPanel);
            else
                CloseMenu();
        }
    }

    private void SetupSliders()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        SetupSlider(sensitivitySlider, 0.5f, 10f, gm.MouseSensitivity, v => { gm.MouseSensitivity = v; ApplySettingsToPlayer(); });
        SetupSlider(fovSlider, 50f, 90f, gm.NormalFov, v => { gm.NormalFov = v; ApplySettingsToPlayer(); });
        SetupSlider(sprintFovSlider, 60f, 100f, gm.SprintFov, v => { gm.SprintFov = v; ApplySettingsToPlayer(); });
        SetupSlider(walkSpeedSlider, 2f, 10f, gm.WalkSpeed, v => { gm.WalkSpeed = v; ApplySettingsToPlayer(); });
        SetupSlider(sprintSpeedSlider, 4f, 15f, gm.SprintSpeed, v => { gm.SprintSpeed = v; ApplySettingsToPlayer(); });
    }

    private void SetupSlider(Slider slider, float min, float max, float defaultValue, System.Action<float> onChanged)
    {
        if (slider == null) return;
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = defaultValue;
        slider.onValueChanged.AddListener(v => onChanged(v));
    }

    private void ApplySettingsToPlayer()
    {
        var player = FindFirstObjectByType<PlayerController>();
        if (player != null) player.UpdateSettings();
    }

    public void OpenMenu()
    {
        IsOpen = true;
        gameObject.SetActive(true);
        mainPanel.SetActive(true);
        optionsPanel.SetActive(false);
        quitPanel.SetActive(false);

        // Fade in
        _mainCanvasGroup.alpha = 0f;
        StartCoroutine(FadeCanvasGroup(_mainCanvasGroup, 0f, 1f, 0.3f));

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void CloseMenu()
    {
        StartCoroutine(CloseMenuCoroutine());
    }

    private IEnumerator CloseMenuCoroutine()
    {
        // Trouver le panel actif
        CanvasGroup activeGroup = mainPanel.activeSelf ? _mainCanvasGroup :
                                  optionsPanel.activeSelf ? _optionsCanvasGroup :
                                  _quitCanvasGroup;

        yield return StartCoroutine(FadeCanvasGroup(activeGroup, 1f, 0f, 0.2f));

        IsOpen = false;
        gameObject.SetActive(false);
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void TransitionToPanel(GameObject fromPanel, GameObject toPanel)
    {
        StartCoroutine(TransitionCoroutine(fromPanel, toPanel));
    }

    private IEnumerator TransitionCoroutine(GameObject fromPanel, GameObject toPanel)
    {
        CanvasGroup fromGroup = fromPanel.GetComponent<CanvasGroup>();
        CanvasGroup toGroup = toPanel.GetComponent<CanvasGroup>();

        // Fade out le panel actuel
        yield return StartCoroutine(FadeCanvasGroup(fromGroup, 1f, 0f, 0.2f));
        fromPanel.SetActive(false);

        // Fade in le nouveau panel
        toPanel.SetActive(true);
        toGroup.alpha = 0f;
        yield return StartCoroutine(FadeCanvasGroup(toGroup, 0f, 1f, 0.3f));
    }

    private void ConfirmQuit()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (GameManager.Instance != null)
            GameManager.Instance.ReturnToMenu();
        else
            SceneManager.LoadScene("MainMenu");
    }

    private CanvasGroup EnsureCanvasGroup(GameObject obj)
    {
        if (obj == null) return null;
        var cg = obj.GetComponent<CanvasGroup>();
        if (cg == null) cg = obj.AddComponent<CanvasGroup>();
        return cg;
    }

    /// <summary>
    /// Coroutine de fondu (remplace les Tweens Godot).
    /// Utilise unscaledDeltaTime car le jeu est en pause (Time.timeScale = 0).
    /// </summary>
    private IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
    {
        float elapsed = 0f;
        group.alpha = from;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        group.alpha = to;
    }
}
