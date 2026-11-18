using UnityEngine;

/// <summary>
/// Fait grésiller une lumière (néons fatigués, coupures brèves).
/// Plus on descend, plus le générateur de map règle un clignotement fort.
/// </summary>
[RequireComponent(typeof(Light))]
public class LightFlicker : MonoBehaviour
{
    [Range(0f, 1f)] public float strength = 0.5f;
    public float speed = 8f;
    [Tooltip("Probabilité (par seconde) d'une coupure complète.")]
    [Range(0f, 2f)] public float blackoutsPerSecond = 0.2f;

    private Light _light;
    private float _baseIntensity;
    private float _seed;
    private float _blackoutTimer;

    private void Awake()
    {
        _light = GetComponent<Light>();
        _baseIntensity = _light.intensity;
        _seed = Random.value * 100f;
    }

    private void Update()
    {
        if (_blackoutTimer > 0f)
        {
            _blackoutTimer -= Time.deltaTime;
            _light.intensity = 0f;
            return;
        }

        if (Random.value < blackoutsPerSecond * strength * Time.deltaTime)
            _blackoutTimer = Random.Range(0.05f, 0.5f);

        float noise = Mathf.PerlinNoise(_seed, Time.time * speed);
        _light.intensity = _baseIntensity * Mathf.Lerp(1f, noise, strength);
    }
}
