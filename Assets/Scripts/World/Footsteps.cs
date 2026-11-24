using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bruits de pas du joueur selon le sol (béton, parquet, herbe).
/// Les fichiers de pas contiennent plusieurs pas à la suite : au démarrage, on repère le début de chaque pas
/// (montée d'énergie du son) et on joue un pas au hasard à chaque foulée.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class Footsteps : MonoBehaviour
{
    public AudioClip concrete;
    public AudioClip wood;
    public AudioClip grass;
    [Range(0f, 1f)] public float volume = 0.55f;
    public float walkStride = 1.9f;
    public float sprintStride = 2.6f;
    [Tooltip("Durée jouée pour un pas (secondes).")]
    public float stepLength = 0.42f;

    private CharacterController _cc;
    private PlayerController _player;
    private AudioSource _source;
    private Vector3 _lastPos;
    private float _distance;
    private float _stopAt = -1f;

    private static readonly Dictionary<AudioClip, float[]> Onsets = new Dictionary<AudioClip, float[]>();

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _player = GetComponent<PlayerController>();
        _source = gameObject.AddComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.spatialBlend = 0f;
        _lastPos = transform.position;
        foreach (var c in new[] { concrete, wood, grass }) if (c != null) GetOnsets(c);
    }

    private void Update()
    {
        if (_stopAt > 0f && Time.time >= _stopAt) { _source.Stop(); _stopAt = -1f; }

        var pos = transform.position;
        var delta = pos - _lastPos;
        _lastPos = pos;
        if (_player != null && _player.IsSeated) { _distance = 0f; return; }
        if (!_cc.enabled || !_cc.isGrounded) return;

        delta.y = 0f;
        float d = delta.magnitude;
        if (d < 0.001f || d > 2f) return; // immobile, ou téléporté (s'asseoir / se lever)
        _distance += d;

        bool sprint = KeyBindings.Held(GameAction.Sprint);
        bool crouch = _player != null && _player.IsCrouching;
        float stride = sprint ? sprintStride : walkStride;
        if (_distance < stride) return;
        _distance = 0f;
        Step(crouch ? 0.45f : sprint ? 1.15f : 1f);
    }

    private void Step(float loudness)
    {
        var clip = SurfaceClip();
        if (clip == null) return;
        var onsets = GetOnsets(clip);
        float start = onsets.Length > 0 ? onsets[Random.Range(0, onsets.Length)] : 0f;
        _source.clip = clip;
        _source.time = Mathf.Min(start, Mathf.Max(0f, clip.length - 0.05f));
        _source.volume = volume * loudness;
        _source.pitch = Random.Range(0.92f, 1.08f);
        _source.Play();
        _stopAt = Time.time + stepLength;
    }

    /// Sol sous les pieds, d'après le nom du matériau.
    private AudioClip SurfaceClip()
    {
        var wet = WaterVolume.PlayerSplash; // dans l'eau : éclaboussures
        if (wet != null) return wet;
        if (Physics.Raycast(transform.position + Vector3.up * 0.3f, Vector3.down, out var hit, 1.2f, ~0, QueryTriggerInteraction.Ignore))
        {
            var r = hit.collider.GetComponent<Renderer>();
            string m = r != null && r.sharedMaterial != null ? r.sharedMaterial.name : "";
            if (m.Contains("Herbe") && grass != null) return grass;
            if (IsWoodFloor(m) && wood != null) return wood;
        }
        return concrete != null ? concrete : wood;
    }

    private static bool IsWoodFloor(string m) => m == "Sol_1" || m == "Sol_2" || m == "Sol_3" || m == "Sol_-5" || m == "Sol_-4";

    /// Débuts de pas dans un fichier (montées d'énergie après un silence relatif).
    private static float[] GetOnsets(AudioClip clip)
    {
        if (Onsets.TryGetValue(clip, out var cached)) return cached;
        var list = new List<float>();
        try
        {
            if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
            int ch = Mathf.Max(1, clip.channels);
            var data = new float[clip.samples * ch];
            if (clip.GetData(data, 0))
            {
                int win = Mathf.Max(1, clip.frequency / 50) * ch; // fenêtres de 20 ms
                int n = data.Length / win;
                var energy = new float[n];
                float max = 0f;
                for (int w = 0; w < n; w++)
                {
                    float e = 0f;
                    for (int k = w * win; k < (w + 1) * win; k++) e += data[k] * data[k];
                    energy[w] = e; if (e > max) max = e;
                }
                float threshold = max * 0.08f;
                float lastOnset = -1f;
                for (int w = 2; w < n; w++)
                {
                    float t = w * 0.02f;
                    bool rise = energy[w] > threshold && energy[w] > energy[w - 2] * 3f;
                    if (rise && t - lastOnset > 0.25f && t < clip.length - 0.3f)
                    {
                        list.Add(Mathf.Max(0f, t - 0.02f));
                        lastOnset = t;
                    }
                }
            }
        }
        catch (System.Exception) { /* clip non lisible (streaming) : on jouera depuis le début */ }
        var arr = list.ToArray();
        Onsets[clip] = arr;
        return arr;
    }
}
