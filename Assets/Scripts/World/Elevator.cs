using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ascenseur de la résidence (étages 3 → 0).
/// En mode histoire, il tombe en panne en arrivant au niveau 0 : la lumière de la cabine
/// grésille puis s'éteint, la "musique de panne" se joue, et il ne répond plus ensuite.
/// Le joueur présent dans la cabine est transporté avec elle.
/// </summary>
public class Elevator : MonoBehaviour
{
    [Serializable]
    public class Stop
    {
        public int floorNumber;
        public float height;      // hauteur du sol de l'étage
        public Transform door;    // porte palière (coulisse quand la cabine est là)
    }

    [Header("Cabine")]
    public Transform cab;
    public Vector3 cabHalfExtents = new Vector3(1.8f, 1.3f, 1.8f);
    public Light cabLight;
    public float speed = 1.2f;

    [Header("Étages desservis")]
    public List<Stop> stops = new List<Stop>();
    public float doorSlide = 1.45f;
    public float doorSpeed = 1.5f;

    [Header("Panne (mode histoire)")]
    public int breakdownFloor = 0;
    [Tooltip("Panne aussi quand la scène est lancée directement (sans GameManager), pratique pour tester.")]
    public bool breakdownWithoutGameManager = true;

    [Header("Sons (à assigner)")]
    public AudioClip moveClip;
    public AudioClip arrivalClip;
    [Tooltip("La petite musique de panne.")]
    public AudioClip breakdownClip;

    public event Action OnBreakdown;

    public bool IsBroken => _broken;
    public bool IsMoving => _moving;
    public int CurrentFloor => stops.Count > 0 ? stops[_current].floorNumber : 0;

    private int _current;
    private bool _moving, _broken;
    private AudioSource _audio;
    private readonly Dictionary<Transform, Vector3> _doorClosed = new Dictionary<Transform, Vector3>();

    private void Awake()
    {
        _audio = cab != null ? cab.GetComponent<AudioSource>() : null;
        if (_audio == null && cab != null)
        {
            _audio = cab.gameObject.AddComponent<AudioSource>();
            _audio.spatialBlend = 1f;
            _audio.playOnAwake = false;
        }

        foreach (var s in stops)
            if (s.door != null) _doorClosed[s.door] = s.door.position;

        // Étage de départ = le plus proche de la cabine
        float best = float.MaxValue;
        for (int i = 0; i < stops.Count; i++)
        {
            float d = Mathf.Abs(stops[i].height - cab.position.y);
            if (d < best) { best = d; _current = i; }
        }
        for (int i = 0; i < stops.Count; i++) SetDoor(i, i == _current ? 1f : 0f);
    }

    /// <summary>Demande la cabine à un étage (bouton d'appel ou bouton de la cabine).</summary>
    public void Request(int floorNumber)
    {
        if (_broken)
        {
            Debug.Log("[Ascenseur] Plus aucune réaction... il est en panne.");
            return;
        }
        if (_moving) return;

        int target = stops.FindIndex(s => s.floorNumber == floorNumber);
        if (target < 0 || target == _current) return;
        StartCoroutine(Travel(target));
    }

    private IEnumerator Travel(int target)
    {
        _moving = true;
        yield return AnimateDoor(_current, 0f);

        var player = GameObject.FindGameObjectWithTag("Player");
        bool riding = player != null && IsInsideCab(player.transform.position);
        if (riding) player.transform.SetParent(cab, true);

        Play(moveClip, true);
        float targetY = stops[target].height;
        while (Mathf.Abs(cab.position.y - targetY) > 0.001f)
        {
            var p = cab.position;
            p.y = Mathf.MoveTowards(p.y, targetY, speed * Time.deltaTime);
            cab.position = p;
            yield return null;
        }
        _current = target;
        if (_audio != null) _audio.Stop();
        if (riding && player != null) player.transform.SetParent(null, true);

        if (stops[target].floorNumber == breakdownFloor && ShouldBreakDown())
        {
            _broken = true;
            Play(breakdownClip, false);
            if (cabLight != null) StartCoroutine(FlickerOut());
            Debug.Log("[Ascenseur] Coupure de courant ! L'ascenseur est en panne au niveau " + breakdownFloor + ".");
            OnBreakdown?.Invoke();
        }
        else Play(arrivalClip, false);

        yield return AnimateDoor(_current, 1f);
        _moving = false;
    }

    private bool ShouldBreakDown()
    {
        if (GameManager.Instance == null) return breakdownWithoutGameManager;
        return GameManager.Instance.CurrentMode == GameMode.Story;
    }

    private bool IsInsideCab(Vector3 p)
    {
        Vector3 local = p - cab.position;
        return Mathf.Abs(local.x) < cabHalfExtents.x && Mathf.Abs(local.z) < cabHalfExtents.z
               && local.y > -0.5f && local.y < cabHalfExtents.y * 2f;
    }

    private IEnumerator AnimateDoor(int index, float target)
    {
        var door = stops[index].door;
        if (door == null) yield break;
        float t = Mathf.InverseLerp(0f, doorSlide, Vector3.Distance(door.position, _doorClosed[door]));
        while (!Mathf.Approximately(t, target))
        {
            t = Mathf.MoveTowards(t, target, doorSpeed * Time.deltaTime);
            SetDoor(index, t);
            yield return null;
        }
    }

    private void SetDoor(int index, float openAmount)
    {
        var door = stops[index].door;
        if (door == null || !_doorClosed.ContainsKey(door)) return;
        door.position = _doorClosed[door] + Vector3.right * (doorSlide * openAmount);
    }

    private IEnumerator FlickerOut()
    {
        float baseIntensity = cabLight.intensity;
        for (int i = 0; i < 14; i++)
        {
            cabLight.intensity = UnityEngine.Random.value < 0.5f ? 0f : baseIntensity * UnityEngine.Random.Range(0.2f, 1f);
            yield return new WaitForSeconds(UnityEngine.Random.Range(0.04f, 0.25f));
        }
        cabLight.enabled = false;
    }

    private void Play(AudioClip clip, bool loop)
    {
        if (_audio == null || clip == null) return;
        _audio.clip = clip;
        _audio.loop = loop;
        _audio.Play();
    }
}
