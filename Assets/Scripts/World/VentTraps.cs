using System.Collections;
using UnityEngine;

/// <summary>
/// Panneau rouillé du conduit d'aération : il cède sous le joueur, qui glisse dans un conduit
/// et se retrouve ramené plus loin en arrière (point de reprise). Le panneau se remet en place ensuite.
/// Le collider de ce GameObject est un déclencheur (trigger) au-dessus du panneau.
/// </summary>
public class VentTrapFloor : MonoBehaviour
{
    [Tooltip("Panneau qui tombe (désactivé pendant la chute).")]
    public GameObject panel;
    [Tooltip("Où le joueur réapparaît.")]
    public Transform respawn;
    public string message = "Le panneau rouillé a cédé… tu as glissé loin en arrière.";

    private bool _busy;

    private void OnTriggerEnter(Collider other)
    {
        if (_busy || !other.CompareTag("Player")) return;
        StartCoroutine(Drop(other.GetComponent<PlayerController>()));
    }

    private IEnumerator Drop(PlayerController player)
    {
        _busy = true;
        if (panel != null) panel.SetActive(false);
        yield return new WaitForSeconds(0.7f);
        if (player != null && respawn != null) player.Teleport(respawn.position, respawn.rotation);
        HudMessage.Show(message, 4f);
        yield return new WaitForSeconds(2f);
        if (panel != null) panel.SetActive(true);
        _busy = false;
    }
}

/// <summary>
/// Jet de vapeur brûlante dans le conduit : s'active par intervalles. Si le joueur est dans le jet,
/// il recule précipitamment (ramené au point "repli"). Il faut observer le rythme et passer entre deux jets.
/// </summary>
public class SteamVent : MonoBehaviour
{
    public float offSeconds = 3.2f;
    public float onSeconds = 1.6f;
    [Tooltip("Décalage du cycle (pour que les jets ne soient pas synchronisés).")]
    public float phase;
    public Transform fallback;
    public ParticleSystem steam;
    public Light glow;

    private bool _active;
    private bool _playerInside;
    private PlayerController _player;

    private void Update()
    {
        float cycle = offSeconds + onSeconds;
        float t = Mathf.Repeat(Time.time + phase, cycle);
        bool active = t > offSeconds;
        if (active != _active)
        {
            _active = active;
            if (steam != null) { if (active) steam.Play(); else steam.Stop(); }
            if (glow != null) glow.enabled = active;
        }
        if (_active && _playerInside && _player != null && fallback != null)
        {
            _playerInside = false;
            _player.Teleport(fallback.position, fallback.rotation);
            HudMessage.Show("La vapeur brûlante te fait reculer. Attends le bon moment pour passer.", 4f);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        _playerInside = true;
        _player = other.GetComponent<PlayerController>();
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player")) _playerInside = false;
    }
}
