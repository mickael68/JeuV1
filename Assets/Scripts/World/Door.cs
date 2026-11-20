using System.Collections;
using UnityEngine;

/// <summary>
/// Porte battante : le GameObject est la charnière (pivot), le battant est un enfant avec un collider.
/// E pour ouvrir / fermer. La porte s'ouvre toujours du côté opposé au joueur.
/// </summary>
public class Door : MonoBehaviour, IInteractable
{
    [Tooltip("Angle d'ouverture en degrés.")]
    public float openAngle = 100f;
    [Tooltip("Durée de l'animation (secondes).")]
    public float duration = 0.6f;
    public bool locked;
    [TextArea] public string lockedMessage = "C'est fermé à clé.";
    public string displayName = "la porte";
    [Tooltip("Ouverte au lancement (angle positif ou négatif).")]
    public float startAngle;

    [Header("Sons (optionnels)")]
    public AudioClip openClip;
    public AudioClip closeClip;

    private Quaternion _closed;
    private float _current;
    private Coroutine _anim;
    private AudioSource _audio;

    public bool IsOpen => Mathf.Abs(_current) > 1f;

    private void Awake()
    {
        _closed = transform.localRotation;
        if (Mathf.Abs(startAngle) > 0.01f)
        {
            _current = startAngle;
            transform.localRotation = _closed * Quaternion.Euler(0f, _current, 0f);
        }
    }

    public string GetInteractText()
    {
        if (locked) return displayName + " (verrouillée)";
        return (IsOpen ? "Fermer " : "Ouvrir ") + displayName;
    }

    public void Interact()
    {
        if (locked)
        {
            Debug.Log("[Porte] " + lockedMessage);
            return;
        }

        float target = 0f;
        if (!IsOpen)
        {
            // S'ouvre à l'opposé du joueur. Le battant part de la charnière vers +X local :
            // un angle positif le fait pivoter vers -Z local, un angle négatif vers +Z.
            var player = Camera.main != null ? Camera.main.transform : null;
            Vector3 normal = (transform.parent != null ? transform.parent.rotation : Quaternion.identity) * _closed * Vector3.forward;
            float side = player != null ? Vector3.Dot(player.position - transform.position, normal) : 1f;
            target = side > 0f ? openAngle : -openAngle;
        }

        Play(target != 0f ? openClip : closeClip);
        if (_anim != null) StopCoroutine(_anim);
        _anim = StartCoroutine(Rotate(target));
    }

    public void Unlock() => locked = false;

    private IEnumerator Rotate(float target)
    {
        float from = _current, t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, duration);
            _current = Mathf.Lerp(from, target, Mathf.SmoothStep(0f, 1f, t));
            transform.localRotation = _closed * Quaternion.Euler(0f, _current, 0f);
            yield return null;
        }
        _anim = null;
    }

    private void Play(AudioClip clip)
    {
        if (clip == null) return;
        if (_audio == null)
        {
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.spatialBlend = 1f;
            _audio.maxDistance = 15f;
        }
        _audio.PlayOneShot(clip);
    }
}
