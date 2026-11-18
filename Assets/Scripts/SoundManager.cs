using UnityEngine;
using System.Collections;

/// <summary>
/// Gère l'audio ambiant du jeu avec transitions douces.
/// Remplace le système Godot AudioStreamPlayer par Unity AudioSource + AudioMixer.
/// </summary>
public class SoundManager : MonoBehaviour
{
    [Header("Ambiance Clips")]
    [SerializeField] private AudioClip dayAmbiance;
    [SerializeField] private AudioClip nightAmbiance;
    [SerializeField] private AudioClip basementAmbiance;

    private AudioSource _ambientSource;
    private int _currentDepth = -1; // -1 = Hub Résidence

    private void Start()
    {
        _ambientSource = gameObject.AddComponent<AudioSource>();
        _ambientSource.loop = true;
        _ambientSource.volume = 0f; // Start silent
        _ambientSource.spatialBlend = 0f; // 2D sound

        // Connexion au TimeManager
        var timeManager = FindFirstObjectByType<TimeManager>();
        if (timeManager != null)
        {
            timeManager.OnDayStarted += () => PlayAmbiance(dayAmbiance);
            timeManager.OnNightStarted += () => PlayAmbiance(nightAmbiance);
        }

        // Ambiance initiale avec fondu entrant
        if (dayAmbiance != null)
        {
            _ambientSource.clip = dayAmbiance;
            _ambientSource.Play();
            StartCoroutine(FadeVolume(_ambientSource, 0f, 1f, 2f));
        }
    }

    public void PlayAmbiance(AudioClip clip)
    {
        if (clip == null || _ambientSource.clip == clip) return;
        StartCoroutine(CrossfadeAmbiance(clip));
    }

    private IEnumerator CrossfadeAmbiance(AudioClip newClip)
    {
        // Fade out
        yield return StartCoroutine(FadeVolume(_ambientSource, _ambientSource.volume, 0.01f, 0.5f));

        // Switch
        _ambientSource.clip = newClip;
        _ambientSource.Play();

        // Fade in
        yield return StartCoroutine(FadeVolume(_ambientSource, 0.01f, 1f, 1f));
    }

    public void UpdateBasementAmbiance(int depth)
    {
        _currentDepth = depth;
        PlayAmbiance(basementAmbiance);

        // Ajuster le pitch pour rendre le son plus "profond" et angoissant
        _ambientSource.pitch = Mathf.Max(0.5f, 1.0f - (depth * 0.1f));
    }

    /// <summary>
    /// Coroutine de fondu audio (remplace les Tweens Godot).
    /// </summary>
    private IEnumerator FadeVolume(AudioSource source, float from, float to, float duration)
    {
        float elapsed = 0f;
        source.volume = from;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime; // unscaled pour marcher même en pause
            source.volume = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        source.volume = to;
    }
}
