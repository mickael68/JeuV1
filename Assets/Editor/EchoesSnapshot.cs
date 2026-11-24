using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Prend des captures de la map depuis quelques points de vue (vérification du rendu, images de fond du menu).
/// Les images sont enregistrées dans le dossier Captures/ à la racine du projet (hors Assets).
/// </summary>
public static class EchoesSnapshot
{
    struct View { public string name; public Vector3 pos; public Vector3 euler; public float ambient; public string sky; }

    static readonly View[] Views =
    {
        new View { name = "etage1_couloir",   pos = new Vector3(-12f, 5.1f, -2.5f),  euler = new Vector3(5, 90, 0),  ambient = 0.35f },
        new View { name = "etage1_portes",    pos = new Vector3(6f, 5.1f, -2.0f),    euler = new Vector3(5, 200, 0), ambient = 0.35f },
        new View { name = "etage1_sortie",    pos = new Vector3(12f, 5.1f, -2.5f),   euler = new Vector3(5, 90, 0),  ambient = 0.35f },
        new View { name = "exterieur_entree", pos = new Vector3(34f, 6.2f, 6f),      euler = new Vector3(8, 235, 0), ambient = 0.5f },
        new View { name = "exterieur_allee",  pos = new Vector3(22f, 5.1f, -2.5f),   euler = new Vector3(3, 90, 0),  ambient = 0.5f },
        new View { name = "exterieur_ville_jour", pos = new Vector3(30f, 5.4f, -2.5f), euler = new Vector3(-2, 55, 0), ambient = 0.5f },
        new View { name = "exterieur_ville_nuit", pos = new Vector3(30f, 5.4f, -2.5f), euler = new Vector3(-2, 55, 0), ambient = 0.08f, sky = "Ciel_Nuit" },
        new View { name = "exterieur_collines",   pos = new Vector3(-30f, 5.4f, 20f),  euler = new Vector3(-3, 300, 0), ambient = 0.5f },
        new View { name = "exterieur_panorama",   pos = new Vector3(16f, 16.5f, 30f), euler = new Vector3(4, 20, 0), ambient = 0.5f },
        new View { name = "exterieur_montagnes",  pos = new Vector3(-20f, 16.5f, -12f), euler = new Vector3(2, 230, 0), ambient = 0.5f },
        new View { name = "etage1_studio",        pos = new Vector3(-10f, 5.0f, -0.3f), euler = new Vector3(15, 10, 0), ambient = 0.45f },
        new View { name = "niveau-1_caves",       pos = new Vector3(-6f, -2.9f, -0.5f), euler = new Vector3(15, 20, 0), ambient = 0.3f },
        new View { name = "niveau0_hall",    pos = new Vector3(-2f, 1.6f, -1f),     euler = new Vector3(5, 0, 0),   ambient = 0.25f },
        new View { name = "niveau-1",         pos = new Vector3(-12f, -2.9f, -2.5f), euler = new Vector3(5, 90, 0),  ambient = 0.25f },
        new View { name = "niveau-2",         pos = new Vector3(-12f, -8.4f, -3f),   euler = new Vector3(5, 90, 0),  ambient = 0.2f },
        new View { name = "niveau-3_tunnels", pos = new Vector3(-12f, -12.9f, -3f),  euler = new Vector3(5, 90, 0),  ambient = 0.2f },
        new View { name = "niveau-6_fond",    pos = new Vector3(-12f, -31.4f, -3f),  euler = new Vector3(0, 90, 0),  ambient = 0.2f },
    };

    [MenuItem("The Echoes Below/Capturer des vues de la map")]
    public static void Capture()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);
        Directory.CreateDirectory("Captures");

        var camGo = new GameObject("SnapshotCam");
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 70f;
        cam.nearClipPlane = 0.05f;
        // Torche du joueur simulée
        var torch = new GameObject("Torch").AddComponent<Light>();
        torch.type = LightType.Spot; torch.spotAngle = 60f; torch.range = 18f; torch.intensity = 6f;
        torch.color = new Color(1f, 0.95f, 0.85f);
        torch.transform.SetParent(camGo.transform, false);

        var rt = new RenderTexture(1920, 1080, 24);
        cam.targetTexture = rt;
        var oldAmbient = RenderSettings.ambientLight;
        var oldMode = RenderSettings.ambientMode;

        foreach (var v in Views)
        {
            camGo.transform.SetPositionAndRotation(v.pos, Quaternion.Euler(v.euler));
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(v.ambient, v.ambient, v.ambient);
            var oldSky = RenderSettings.skybox;
            var oldFog = RenderSettings.fogColor;
            var sun = RenderSettings.sun;
            bool sunWas = sun != null && sun.enabled;
            if (!string.IsNullOrEmpty(v.sky))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>($"Assets/Materials/Ciel/{v.sky}.mat");
                if (m != null) RenderSettings.skybox = m;
                if (sun != null) sun.enabled = false; // nuit : pas de soleil
                RenderSettings.fogColor = new Color(0.05f, 0.05f, 0.1f);
            }
            cam.Render();
            RenderSettings.skybox = oldSky;
            RenderSettings.fogColor = oldFog;
            if (sun != null) sun.enabled = sunWas;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            File.WriteAllBytes($"Captures/{v.name}.png", tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        // Vue dynamique : premier studio abandonné du niveau 0 (repère posé par le générateur)
        foreach (var (markerName, file, height, pitch) in new[] {
            ("◆ Vue_Studio_Abandonne", "niveau0_studio_abandonne", 1.1f, 18f),
            ("◆ Conduit_Arrivee (ramper : accroupi)", "conduit_aeration", 0.35f, 5f),
            ("◆ Vue_Cafeteria", "residence_cafeteria", 1.6f, 14f),
            ("◆ Vue_Cuisine", "residence_cuisine", 1.6f, 12f),
            ("◆ Vue_Cour", "residence_cour", 1.6f, 0f),
            ("◆ Vue_Accueil", "residence_accueil", 1.6f, 8f),
            ("◆ Vue_Salle_De_Bains", "residence_salle_de_bains", 1.55f, 20f),
            ("◆ Vue_Toilettes", "residence_toilettes", 1.6f, 12f),
            ("◆ Vue_Salle_Repos", "residence_salle_repos", 1.6f, 12f),
            ("◆ Vue_Aile_Exterieur", "residence_aile_exterieur", 1.7f, -2f),
            ("◆ Vue_Salle_Etude", "residence_etude", 1.6f, 12f),
            ("◆ Vue_Studio_Personnel", "residence_studio_personnel", 1.6f, 12f),
            ("◆ Vue_Espace_Detente", "residence_detente", 1.6f, 14f),
            ("◆ Vue_Surveillance", "residence_surveillance", 1.55f, 6f),
            ("◆ Vue_Sous_Sol_-1", "sous_sol_1", 1.5f, 4f),
            ("◆ Vue_Sous_Sol_-2", "sous_sol_2", 1.5f, 4f),
            ("◆ Vue_Grotte_Faille", "grotte_faille", 1f, 8f),
            ("◆ Vue_Grotte_Foret", "grotte_foret", 1f, -4f),
            ("◆ Vue_Grotte_Lac", "grotte_lac", 1f, 4f),
            ("◆ Vue_Grotte_Campement", "grotte_campement", 1.3f, 20f) })
        {
        var studioView = GameObject.Find(markerName);
        if (studioView != null)
        {
            camGo.transform.SetPositionAndRotation(studioView.transform.position + Vector3.up * height,
                studioView.transform.rotation * Quaternion.Euler(pitch, 0f, 0f));
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.3f, 0.3f, 0.3f);
            var markerR = studioView.GetComponent<Renderer>();
            if (markerR != null) markerR.enabled = false;
            cam.Render();
            if (markerR != null) markerR.enabled = true;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            File.WriteAllBytes($"Captures/{file}.png", tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
        }

        RenderTexture.active = null;
        cam.targetTexture = null;
        RenderSettings.ambientLight = oldAmbient;
        RenderSettings.ambientMode = oldMode;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(camGo);
        Debug.Log("[EchoesSnapshot] " + Views.Length + " captures dans Captures/");
    }
}
