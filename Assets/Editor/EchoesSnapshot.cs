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
    struct View { public string name; public Vector3 pos; public Vector3 euler; public float ambient; }

    static readonly View[] Views =
    {
        new View { name = "etage1_couloir",   pos = new Vector3(-12f, 5.1f, -2.5f),  euler = new Vector3(5, 90, 0),  ambient = 0.35f },
        new View { name = "niveau0_hall",     pos = new Vector3(-2f, 1.6f, -1f),     euler = new Vector3(5, 0, 0),   ambient = 0.25f },
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
            cam.Render();
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
