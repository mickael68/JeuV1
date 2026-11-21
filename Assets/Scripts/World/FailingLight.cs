using UnityEngine;

/// <summary>
/// Néon défaillant : il tient quelques secondes, bégaie (allumages en rafale), s'éteint parfois longtemps,
/// puis tente de se rallumer. Le tube (émissif) suit la lumière. Un son de grésillement est optionnel.
/// </summary>
[RequireComponent(typeof(Light))]
public class FailingLight : MonoBehaviour
{
    public enum Mode { Failing, Dying, Dead }

    [Tooltip("Failing : coupures régulières.  Dying : presque toujours éteint, rares éclairs.  Dead : éteint.")]
    public Mode mode = Mode.Failing;
    [Tooltip("Tube lumineux dont l'émission suit la lumière.")]
    public Renderer tube;
    public Color emission = new Color(0.85f, 0.95f, 1f) * 2.5f;
    [Tooltip("Grésillement (optionnel), joué quand le néon est allumé.")]
    public AudioClip buzz;

    private Light _light;
    private float _base;
    private float _timer;
    private int _burst;          // allumages restants dans une rafale
    private bool _on;
    private MaterialPropertyBlock _mpb;
    private AudioSource _audio;
    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    private void Awake()
    {
        _light = GetComponent<Light>();
        _base = _light.intensity;
        _mpb = new MaterialPropertyBlock();
        if (buzz != null)
        {
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.clip = buzz; _audio.loop = true; _audio.spatialBlend = 1f; _audio.maxDistance = 8f; _audio.volume = 0.35f;
        }
        _timer = Random.Range(0f, 2f);
        Set(mode != Mode.Dead && Random.value < 0.5f, 1f);
    }

    private void Update()
    {
        if (mode == Mode.Dead) { Set(false, 0f); return; }

        _timer -= Time.deltaTime;
        if (_timer > 0f)
        {
            // Allumé : léger tremblement du tube
            if (_on) _light.intensity = _base * (0.9f + 0.1f * Mathf.PerlinNoise(Time.time * 20f, transform.position.x));
            return;
        }

        if (_burst > 0)
        {
            // Rafale : allume / éteint très vite (tube qui n'arrive pas à démarrer)
            _burst--;
            Set(!_on, Random.Range(0.4f, 1f));
            _timer = Random.Range(0.03f, 0.12f);
            return;
        }

        if (_on)
        {
            // Coupure : courte, ou longue pour un néon mourant
            Set(false, 0f);
            _timer = mode == Mode.Dying ? Random.Range(4f, 12f) : (Random.value < 0.3f ? Random.Range(2f, 6f) : Random.Range(0.08f, 0.6f));
        }
        else
        {
            // Tentative de rallumage, en rafale
            _burst = Random.Range(2, 9) * 2;
            _timer = 0.02f;
            if (Random.value < 0.6f || mode == Mode.Dying)
            {
                // ... puis reste allumé un moment
                Set(true, 1f);
                _timer = mode == Mode.Dying ? Random.Range(0.1f, 0.8f) : Random.Range(1f, 7f);
                _burst = 0;
            }
        }
    }

    private void Set(bool on, float level)
    {
        _on = on;
        _light.enabled = on;
        _light.intensity = _base * level;
        if (tube != null)
        {
            tube.GetPropertyBlock(_mpb);
            _mpb.SetColor(EmissionId, on ? emission * level : Color.black);
            tube.SetPropertyBlock(_mpb);
        }
        if (_audio != null)
        {
            if (on && !_audio.isPlaying) _audio.Play();
            else if (!on && _audio.isPlaying) _audio.Stop();
        }
    }
}
