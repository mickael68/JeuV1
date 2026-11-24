using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Mode développeur, pour tester le jeu sans tout refaire depuis le début.
/// Disponible seulement dans l'éditeur Unity et dans les builds "Development" (jamais dans le jeu final).
///   F1 : activer / désactiver le mode dev (mémorisé)
///   F2 : monter d'un niveau   ·   F3 : descendre d'un niveau (téléportation)
/// Mode dev actif : grilles cadenassées et portes fermées à clé s'ouvrent sans clé ni mission.
/// </summary>
public class DevMode : MonoBehaviour
{
    const string PrefKey = "echoes_dev_mode";

    /// Vrai si le mode dev est disponible ET activé.
    public static bool Enabled => Available && PlayerPrefs.GetInt(PrefKey, 1) == 1;
    public static bool Available => Application.isEditor || Debug.isDebugBuild;

    static GUIStyle _style;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (!Available || FindAnyObjectByType<DevMode>() != null) return;
        var go = new GameObject("DevMode");
        DontDestroyOnLoad(go);
        go.AddComponent<DevMode>();
    }

    void Update()
    {
        if (Time.timeScale == 0f) return; // menu pause ouvert
        if (Input.GetKeyDown(KeyCode.F1))
        {
            PlayerPrefs.SetInt(PrefKey, Enabled ? 0 : 1);
            HudMessage.Show(Enabled ? "Mode dev activé (F2 / F3 : changer de niveau)" : "Mode dev désactivé", 2.5f);
        }
        if (!Enabled) return;
        if (Input.GetKeyDown(KeyCode.F2)) GoToLevel(+1);
        if (Input.GetKeyDown(KeyCode.F3)) GoToLevel(-1);
    }

    void OnGUI()
    {
        if (!Enabled) return;
        _style ??= new GUIStyle(GUI.skin.label) { fontSize = 13, normal = { textColor = new Color(1f, 0.85f, 0.2f, 0.85f) } };
        GUI.Label(new Rect(10, Screen.height - 26, 600, 22), "MODE DEV — grilles et portes ouvertes · F2 monter · F3 descendre · F1 désactiver", _style);
    }

    /// Téléporte le joueur au niveau au-dessus (+1) ou en dessous (-1) du niveau actuel.
    static void GoToLevel(int dir)
    {
        var player = PlayerController.Instance;
        if (player == null) return;

        // Un volume par numéro de niveau (le plus grand, pour ignorer les petites zones comme le conduit)
        var zones = FindObjectsByType<FloorZone>(FindObjectsSortMode.None)
            .GroupBy(z => z.floorNumber)
            .Select(g => g.OrderByDescending(z => Volume(z)).First())
            .OrderBy(z => z.floorNumber)
            .ToList();
        if (zones.Count == 0) return;

        int current = FloorZone.Current != null ? FloorZone.Current.floorNumber : NearestFloor(zones, player.transform.position.y);
        var target = dir > 0 ? zones.FirstOrDefault(z => z.floorNumber > current) : zones.LastOrDefault(z => z.floorNumber < current);
        if (target == null) { HudMessage.Show(dir > 0 ? "Déjà au niveau le plus haut." : "Déjà au niveau le plus bas.", 2f); return; }

        if (!FindSpot(target, out var spot)) { HudMessage.Show("Pas d'endroit sûr trouvé au " + target.displayName, 2.5f); return; }
        player.Teleport(spot + Vector3.up * 0.1f, player.transform.rotation);
        HudMessage.Show("[Dev] " + target.displayName, 2.5f);
    }

    static float Volume(FloorZone z)
    {
        var b = z.GetComponent<BoxCollider>();
        return b == null ? 0f : b.size.x * b.size.y * b.size.z;
    }

    static int NearestFloor(List<FloorZone> zones, float y) =>
        zones.OrderBy(z => Mathf.Abs(z.GetComponent<Collider>().bounds.center.y - y)).First().floorNumber;

    /// Point marchable (NavMesh) à l'intérieur du volume du niveau, le plus proche possible de son centre.
    static bool FindSpot(FloorZone zone, out Vector3 spot)
    {
        spot = Vector3.zero;
        var b = zone.GetComponent<Collider>().bounds;
        float best = float.MaxValue;
        bool found = false;
        for (float y = b.min.y + 0.5f; y < b.max.y; y += 1.5f)
            for (float x = b.min.x + 2f; x < b.max.x; x += 4f)
                for (float z = b.min.z + 2f; z < b.max.z; z += 4f)
                {
                    if (!NavMesh.SamplePosition(new Vector3(x, y, z), out var hit, 1.2f, NavMesh.AllAreas)) continue;
                    var p = hit.position;
                    if (p.y < b.min.y - 0.2f || p.y > b.max.y - 1.8f) continue; // doit être dans le niveau, avec de la hauteur
                    float d = new Vector2(p.x - b.center.x, p.z - b.center.z).sqrMagnitude;
                    if (d < best) { best = d; spot = p; found = true; }
                }
        return found;
    }
}
