using UnityEngine;

/// <summary>
/// Pile de la lampe torche : elle se vide quand la lampe est allumée, la lumière faiblit et vacille
/// quand il reste peu de charge, puis s'éteint. Les piles ramassées la rechargent.
/// </summary>
public class FlashlightBattery : MonoBehaviour
{
    public static FlashlightBattery Instance { get; private set; }

    // Noms changés : les anciennes valeurs (7 min, 75 %) enregistrées dans la scène ne s'appliquent plus
    [Tooltip("Durée d'une charge complète, lampe allumée (minutes).")]
    public float fullChargeMinutes = 20f;
    [Range(0f, 1f)] public float initialCharge = 1f;

    public float Charge { get; private set; }

    private Light _light;
    private float _baseIntensity;
    private bool _warned;

    private void Awake()
    {
        Instance = this;
        Charge = initialCharge;
    }

    private void LateUpdate()
    {
        if (_light == null)
        {
            // La même lampe que celle que le joueur allume et éteint
            var player = PlayerController.Instance;
            _light = player != null ? player.Flashlight : null;
            if (_light == null) return;
            _baseIntensity = _light.intensity;
        }

        if (!_light.enabled) return;
        if (Charge <= 0f)
        {
            _light.enabled = false;
            _light.intensity = _baseIntensity;
            HudMessage.Show("La lampe torche est à plat. Il faut une pile.", 3f);
            return;
        }

        Charge = Mathf.Max(0f, Charge - Time.deltaTime / (fullChargeMinutes * 60f));
        float k = Charge > 0.2f ? 1f : Mathf.Lerp(0.25f, 1f, Charge / 0.2f);
        if (Charge < 0.12f && Random.value < 0.04f) k *= 0.2f; // ça vacille
        _light.intensity = _baseIntensity * k;

        if (Charge < 0.15f && !_warned) { _warned = true; HudMessage.Show("La lampe faiblit…", 2.5f); }
    }

    public void Recharge(float amount)
    {
        Charge = Mathf.Clamp01(Charge + amount);
        _warned = false;
        if (_light != null) _light.intensity = _baseIntensity;
    }
}
