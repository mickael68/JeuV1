using UnityEngine;

/// <summary>
/// Ambiance sonore de fond selon l'endroit où se trouve le joueur : résidence (étages 1 à 3), dehors,
/// sous-sols (0 à -3), profondeurs (-4, -5) et le fond (-6). Fondu enchaîné entre deux sources.
/// </summary>
public class AmbienceManager : MonoBehaviour
{
    public AudioClip residence;
    public AudioClip outside;
    public AudioClip basement;
    public AudioClip deep;
    public AudioClip abyss;
    [Range(0f, 1f)] public float volume = 0.45f;
    public float fadeSeconds = 2.5f;

    [Header("Emprise du bâtiment (pour savoir si le joueur est dehors)")]
    public Rect building = Rect.MinMaxRect(-20.3f, -10.3f, 20.3f, 5.3f);
    public float groundY = 3.5f;

    private AudioSource _a, _b;
    private bool _aActive = true;
    private AudioClip _current;
    private float _nextCheck;

    private void Awake()
    {
        _a = NewSource();
        _b = NewSource();
    }

    private AudioSource NewSource()
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.loop = true; s.playOnAwake = false; s.spatialBlend = 0f; s.volume = 0f;
        return s;
    }

    private void Update()
    {
        if (Time.unscaledTime >= _nextCheck)
        {
            _nextCheck = Time.unscaledTime + 0.5f;
            var wanted = Choose();
            if (wanted != _current) SwitchTo(wanted);
        }

        // Fondu
        var on = _aActive ? _a : _b;
        var off = _aActive ? _b : _a;
        float step = Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeSeconds) * volume;
        on.volume = Mathf.MoveTowards(on.volume, on.clip != null ? volume : 0f, step);
        off.volume = Mathf.MoveTowards(off.volume, 0f, step);
        if (off.isPlaying && off.volume <= 0f) off.Stop();
    }

    private AudioClip Choose()
    {
        var player = PlayerController.Instance;
        int floor = FloorZone.Current != null ? FloorZone.Current.floorNumber : 1;
        if (player != null)
        {
            var p = player.transform.position;
            bool insideFootprint = building.Contains(new Vector2(p.x, p.z));
            if (!insideFootprint && p.y > groundY - 1f) return outside != null ? outside : residence;
        }
        if (floor >= 1) return residence;
        if (floor >= -3) return basement;
        if (floor >= -5) return deep != null ? deep : basement;
        return abyss != null ? abyss : deep;
    }

    private void SwitchTo(AudioClip clip)
    {
        _current = clip;
        _aActive = !_aActive;
        var on = _aActive ? _a : _b;
        on.clip = clip;
        if (clip != null)
        {
            on.time = Random.Range(0f, Mathf.Max(0f, clip.length - 1f)); // pas toujours le même début
            on.Play();
        }
    }
}
