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

        RenderTexture.active = null;
        cam.targetTexture = null;
        RenderSettings.ambientLight = oldAmbient;
        RenderSettings.ambientMode = oldMode;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(camGo);
        Debug.Log("[EchoesSnapshot] " + Views.Length + " captures dans Captures/");
    }
}
