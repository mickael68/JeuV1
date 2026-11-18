using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

/// <summary>
/// Menu principal du jeu — "The Echoes Below".
/// Porté depuis Godot avec les mêmes animations et transitions.
/// Les Tweens Godot sont remplacés par des Coroutines Unity.
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button sandboxButton;
    [SerializeField] private Button storyButton;
    [SerializeField] private Button rulesButton;
    [SerializeField] private Button guideButton;
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button creditsButton;
    [SerializeField] private Button quitButton;

    [Header("Audio")]
    [SerializeField] private AudioSource musicPlayer;
    [SerializeField] private AudioClip[] tracks;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI titleLabel;
    [SerializeField] private CanvasGroup mainMenuGroup;
    [SerializeField] private Image fadeOverlay;

    // Sub-panels
    [SerializeField] private GameObject optionsPanel;
    [SerializeField] private GameObject playPanel;
    [SerializeField] private GameObject quitModal;
    [SerializeField] private GameObject rulesPanel;
    [SerializeField] private GameObject guidePanel;
    [SerializeField] private GameObject creditsPanel;

    private bool _isTransitioning = false;
    private int _lastTrackIndex = -1;
    private GameMode _currentPlayMode = GameMode.None;

    private void Start()
    {
        // Assurer que la souris est visible
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1f;

        // Connecter les boutons
        sandboxButton.onClick.AddListener(() => ShowPlayMenu(GameMode.Sandbox));
        storyButton.onClick.AddListener(() => ShowPlayMenu(GameMode.Story));
        rulesButton.onClick.AddListener(() => ShowSubPanel(rulesPanel));
        guideButton.onClick.AddListener(() => ShowSubPanel(guidePanel));
        optionsButton.onClick.AddListener(() => ShowSubPanel(optionsPanel));
        creditsButton.onClick.AddListener(() => ShowSubPanel(creditsPanel));
        quitButton.onClick.AddListener(ShowQuitConfirmation);

        // Fade overlay transparent au départ
        if (fadeOverlay != null)
        {
            fadeOverlay.color = new Color(0, 0, 0, 0);
            fadeOverlay.raycastTarget = false;
        }

        // Cacher les sous-panels
        HideAllSubPanels();

        // Setup animations
        StartCoroutine(TitlePulseAnimation());
        SetupButtonHoverEffects();

        // Musique
        if (musicPlayer != null && tracks != null && tracks.Length > 0)
        {
            Debug.Log($"[AUDIO] Playlist initialisée avec {tracks.Length} titres.");
            PlayRandomTrack();
        }
    }

    private void Update()
    {
        // Vérifier la fin de la piste (remplacement du signal Finished de Godot)
        if (!_isTransitioning && musicPlayer != null && musicPlayer.isPlaying)
        {
            float remaining = musicPlayer.clip.length - musicPlayer.time;
            if (remaining <= 4.0f && musicPlayer.clip.length > 4.0f)
            {
                Debug.Log("[AUDIO] Fin de piste détectée, début du fade-out.");
                StartCoroutine(HandleMusicTransition());
            }
        }
    }

    // ── Musique ──

    private void PlayRandomTrack()
    {
        _isTransitioning = false;
        int index;
        if (tracks.Length > 1)
        {
            index = Random.Range(0, tracks.Length);
            if (index == _lastTrackIndex)
                index = (index + 1) % tracks.Length;
        }
        else
        {
            index = 0;
        }

        _lastTrackIndex = index;
        musicPlayer.clip = tracks[index];
        musicPlayer.volume = 0f;
        musicPlayer.loop = false;
        musicPlayer.Play();

        Debug.Log($"[AUDIO] Lecture de : {tracks[index].name} (Index: {index})");

        // Fade-In sur 4 secondes
        StartCoroutine(FadeAudio(musicPlayer, 0f, 1f, 4f));
    }

    private IEnumerator HandleMusicTransition()
    {
        if (_isTransitioning) yield break;
        _isTransitioning = true;

        Debug.Log("[AUDIO] Début du Fade-Out (4s)...");
        yield return StartCoroutine(FadeAudio(musicPlayer, musicPlayer.volume, 0f, 4f));
        musicPlayer.Stop();

        Debug.Log("[AUDIO] Pause de 4 secondes...");
        yield return new WaitForSeconds(4f);

        PlayRandomTrack();
    }

    private IEnumerator FadeAudio(AudioSource source, float from, float to, float duration)
    {
        float elapsed = 0f;
        source.volume = from;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            source.volume = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        source.volume = to;
    }

    // ── Navigation ──

    private void ShowPlayMenu(GameMode mode)
    {
        _currentPlayMode = mode;
        // TODO: Afficher le panel de sélection de sauvegarde
        // Pour le moment, lancer directement le jeu
        StartCoroutine(FadeOutAndStartGame());
    }

    private IEnumerator FadeOutAndStartGame()
    {
        if (_isTransitioning) yield break;
        _isTransitioning = true;

        SetButtonsInteractable(false);

        if (fadeOverlay != null)
            fadeOverlay.raycastTarget = true;

        // Fade parallèle : musique + écran noir
        float duration = 1.5f;
        float elapsed = 0f;
        float startVolume = musicPlayer != null ? musicPlayer.volume : 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            if (musicPlayer != null)
                musicPlayer.volume = Mathf.Lerp(startVolume, 0f, t);

            if (fadeOverlay != null)
                fadeOverlay.color = new Color(0, 0, 0, t);

            yield return null;
        }

        // Lancer le jeu
        if (GameManager.Instance != null)
            GameManager.Instance.StartGame(_currentPlayMode);
        else
            SceneManager.LoadScene("Main");
    }

    private void ShowSubPanel(GameObject panel)
    {
        if (panel == null) return;
        HideAllSubPanels();
        panel.SetActive(true);

        // Fade transition
        StartCoroutine(FadeTransition(mainMenuGroup, false, 0.3f));
        var panelGroup = EnsureCanvasGroup(panel);
        panelGroup.alpha = 0f;
        StartCoroutine(FadeCanvasGroupTo(panelGroup, 1f, 0.4f, 0.1f));
    }

    public void CloseSubPanel(GameObject panel)
    {
        if (panel == null) return;
        StartCoroutine(CloseSubPanelCoroutine(panel));
    }

    private IEnumerator CloseSubPanelCoroutine(GameObject panel)
    {
        var panelGroup = EnsureCanvasGroup(panel);
        yield return StartCoroutine(FadeCanvasGroupTo(panelGroup, 0f, 0.3f));
        panel.SetActive(false);

        // Réapparaître le menu principal
        mainMenuGroup.alpha = 0f;
        yield return StartCoroutine(FadeCanvasGroupTo(mainMenuGroup, 1f, 0.4f));
    }

    private void ShowQuitConfirmation()
    {
        if (quitModal == null) return;
        quitModal.SetActive(true);
        var group = EnsureCanvasGroup(quitModal);
        group.alpha = 0f;
        StartCoroutine(FadeCanvasGroupTo(group, 1f, 0.3f));
    }

    public void ConfirmQuit()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.QuitGame();
        else
            Application.Quit();
    }

    public void CancelQuit()
    {
        if (quitModal == null) return;
        StartCoroutine(HidePanel(quitModal));
    }

    // ── Animations ──

    private IEnumerator TitlePulseAnimation()
    {
        if (titleLabel == null) yield break;

        while (true)
        {
            // Fade à 0.6
            yield return StartCoroutine(FadeTextAlpha(titleLabel, 1f, 0.6f, 2f));
            // Retour à 1.0
            yield return StartCoroutine(FadeTextAlpha(titleLabel, 0.6f, 1f, 2f));
        }
    }

    private IEnumerator FadeTextAlpha(TextMeshProUGUI text, float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(from, to, elapsed / duration);
            text.alpha = alpha;
            yield return null;
        }
        text.alpha = to;
    }

    private void SetupButtonHoverEffects()
    {
        SetupHoverEffect(sandboxButton);
        SetupHoverEffect(storyButton);
        SetupHoverEffect(rulesButton);
        SetupHoverEffect(guideButton);
        SetupHoverEffect(optionsButton);
        SetupHoverEffect(creditsButton);
        SetupHoverEffect(quitButton);
    }

    private void SetupHoverEffect(Button button)
    {
        if (button == null) return;
        var trigger = button.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();

        // Hover Enter
        var enterEntry = new UnityEngine.EventSystems.EventTrigger.Entry();
        enterEntry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter;
        enterEntry.callback.AddListener((_) =>
        {
            var text = button.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null) text.color = new Color(1f, 0.7f, 0.2f);
            // Léger décalage à droite
            button.transform.localPosition += new Vector3(10f, 0f, 0f);
        });
        trigger.triggers.Add(enterEntry);

        // Hover Exit
        var exitEntry = new UnityEngine.EventSystems.EventTrigger.Entry();
        exitEntry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit;
        exitEntry.callback.AddListener((_) =>
        {
            var text = button.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null) text.color = Color.white;
            button.transform.localPosition -= new Vector3(10f, 0f, 0f);
        });
        trigger.triggers.Add(exitEntry);
    }

    // ── Utilitaires ──

    private void HideAllSubPanels()
    {
        if (optionsPanel != null) optionsPanel.SetActive(false);
        if (playPanel != null) playPanel.SetActive(false);
        if (quitModal != null) quitModal.SetActive(false);
        if (rulesPanel != null) rulesPanel.SetActive(false);
        if (guidePanel != null) guidePanel.SetActive(false);
        if (creditsPanel != null) creditsPanel.SetActive(false);
    }

    private void SetButtonsInteractable(bool interactable)
    {
        sandboxButton.interactable = interactable;
        storyButton.interactable = interactable;
        rulesButton.interactable = interactable;
        guideButton.interactable = interactable;
        optionsButton.interactable = interactable;
        creditsButton.interactable = interactable;
        quitButton.interactable = interactable;
    }

    private IEnumerator FadeTransition(CanvasGroup group, bool fadeIn, float duration)
    {
        float from = fadeIn ? 0f : 1f;
        float to = fadeIn ? 1f : 0f;
        yield return StartCoroutine(FadeCanvasGroupTo(group, to, duration));
        group.interactable = fadeIn;
        group.blocksRaycasts = fadeIn;
    }

    private IEnumerator FadeCanvasGroupTo(CanvasGroup group, float target, float duration, float delay = 0f)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        float start = group.alpha;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            group.alpha = Mathf.Lerp(start, target, elapsed / duration);
            yield return null;
        }
        group.alpha = target;
    }

    private IEnumerator HidePanel(GameObject panel)
    {
        var group = EnsureCanvasGroup(panel);
        yield return StartCoroutine(FadeCanvasGroupTo(group, 0f, 0.3f));
        panel.SetActive(false);
    }

    private CanvasGroup EnsureCanvasGroup(GameObject obj)
    {
        var cg = obj.GetComponent<CanvasGroup>();
        if (cg == null) cg = obj.AddComponent<CanvasGroup>();
        return cg;
    }
}
