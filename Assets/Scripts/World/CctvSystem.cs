using UnityEngine;

/// <summary>
/// Poste de surveillance : les caméras murales filment pour de vrai et leurs images passent sur les écrans.
/// Pour rester léger, rien n'est rendu quand le joueur est loin ; près du poste, une seule caméra est rendue
/// à la fois, à tour de rôle (environ 4 images par seconde et par écran, comme une vieille vidéosurveillance).
/// S'il y a plus de caméras que d'écrans, les écrans font défiler les caméras.
/// </summary>
public class CctvSystem : MonoBehaviour
{
    public Camera[] cameras;
    public string[] labels;
    public Renderer[] screens;
    [Tooltip("Distance du joueur en dessous de laquelle les écrans s'allument.")]
    public float activeDistance = 14f;
    public float renderInterval = 0.04f;
    public float cycleSeconds = 6f;
    public Vector2Int resolution = new Vector2Int(256, 192);

    private RenderTexture[] _rts;
    private MaterialPropertyBlock _mpb;
    private int _next;
    private float _timer;
    private int _offset;
    private float _cycleTimer;

    private void Start()
    {
        _mpb = new MaterialPropertyBlock();
        if (cameras == null || screens == null) { enabled = false; return; }
        _rts = new RenderTexture[screens.Length];
        for (int i = 0; i < screens.Length; i++)
        {
            _rts[i] = new RenderTexture(resolution.x, resolution.y, 16) { name = "CCTV_" + i, filterMode = FilterMode.Point };
            _rts[i].Create();
            if (screens[i] == null) continue;
            screens[i].GetPropertyBlock(_mpb);
            _mpb.SetTexture("_BaseMap", _rts[i]);
            screens[i].SetPropertyBlock(_mpb);
        }
        foreach (var c in cameras) if (c != null) c.enabled = false;
    }

    private void Update()
    {
        var player = PlayerController.Instance;
        if (player == null || cameras.Length == 0 || screens.Length == 0) return;
        if ((player.transform.position - transform.position).sqrMagnitude > activeDistance * activeDistance) return;

        // Plus de caméras que d'écrans : on fait défiler
        if (cameras.Length > screens.Length)
        {
            _cycleTimer += Time.deltaTime;
            if (_cycleTimer > cycleSeconds) { _cycleTimer = 0f; _offset = (_offset + 1) % cameras.Length; }
        }

        _timer += Time.deltaTime;
        if (_timer < renderInterval) return;
        _timer = 0f;
        int s = _next % screens.Length;
        _next++;
        var cam = cameras[(s + _offset) % cameras.Length];
        if (cam == null || _rts[s] == null) return;
        cam.targetTexture = _rts[s];
        cam.Render();
        cam.targetTexture = null;
    }

    private void OnDestroy()
    {
        if (_rts == null) return;
        foreach (var rt in _rts) if (rt != null) rt.Release();
    }
}
