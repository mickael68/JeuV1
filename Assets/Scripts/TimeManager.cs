using UnityEngine;
using System;

/// <summary>
/// Gère le cycle temporel du jeu (24h compressées).
/// Remplace les Signals Godot par des events C# standard.
/// </summary>
public class TimeManager : MonoBehaviour
{
    // ── Events (remplace les Signals Godot) ──
    public event Action<int> OnHourChanged;
    public event Action OnNightStarted;
    public event Action OnDayStarted;

    [Header("Time Settings")]
    [SerializeField] private float dayDurationSeconds = 300f; // 5 minutes par jour
    [SerializeField] private int startHour = 8;

    private float _time = 0f;
    private int _currentHour;
    private bool _isNight = false;

    public int CurrentHour => _currentHour;
    public float NormalizedTime => _time / dayDurationSeconds; // 0-1 pour le cycle

    private void Start()
    {
        _currentHour = startHour;
        _time = (_currentHour / 24f) * dayDurationSeconds;
    }

    private void Update()
    {
        _time += Time.deltaTime;
        if (_time >= dayDurationSeconds) _time = 0f;

        int newHour = Mathf.FloorToInt((_time / dayDurationSeconds) * 24f);

        if (newHour != _currentHour)
        {
            _currentHour = newHour;
            OnHourChanged?.Invoke(_currentHour);
            CheckTimeState();
        }
    }

    private void CheckTimeState()
    {
        // La nuit commence à 22h et finit à 6h
        if ((_currentHour >= 22 || _currentHour < 6) && !_isNight)
        {
            _isNight = true;
            OnNightStarted?.Invoke();
            Debug.Log("La nuit tombe... Soyez discret.");
        }
        else if (_currentHour >= 6 && _currentHour < 22 && _isNight)
        {
            _isNight = false;
            OnDayStarted?.Invoke();
            Debug.Log("Le jour se lève. C'est l'heure des cours.");
        }
    }
}
