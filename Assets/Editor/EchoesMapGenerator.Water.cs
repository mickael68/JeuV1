using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Eau : matériaux (shader "TheEchoesBelow/Water"), volumes où l'on patauge, sons d'eau.
/// </summary>
public static partial class EchoesMapGenerator
{
    // Sons d'eau (Assets/Sounds/Ambiance, à télécharger) : absents = silence, sans erreur
    static AudioClip SndWaterSteps => Snd("footsteps", "water") ?? Snd("water-footsteps") ?? Snd("walking", "water") ?? Snd("splash");
    static AudioClip SndRiver => Snd("river") ?? Snd("stream") ?? Snd("water-flow") ?? Snd("flowing");
    static AudioClip SndDrip => Snd("drip") ?? Snd("water-drop") ?? Snd("dripping");

    /// Profondeur et image de la scène, nécessaires à la transparence et à la réfraction de l'eau.
    static void EnableWaterRendering()
    {
        if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
        {
            if (!urp.supportsCameraDepthTexture || !urp.supportsCameraOpaqueTexture)
            {
                urp.supportsCameraDepthTexture = true;
                urp.supportsCameraOpaqueTexture = true;
                EditorUtility.SetDirty(urp);
                AssetDatabase.SaveAssetIfDirty(urp);
            }
        }
    }

    /// Matériau d'eau. depthMax : profondeur (m) où l'eau devient opaque ; flow : courant (m/s) le long de +Z de l'objet.
    static Material WaterMat(string name, Color shallow, Color deep, float depthMax, float flow, float waves, float waveScale = 0.6f, float foam = 0.12f)
    {
        var shader = Shader.Find("TheEchoesBelow/Water");
        if (shader == null) return Mat(name, deep, 0.95f); // repli : surface sombre et brillante
        string path = $"{MatFolder}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        m.shader = shader;
        m.SetColor("_ShallowColor", shallow);
        m.SetColor("_DeepColor", deep);
        m.SetFloat("_DepthMax", depthMax);
        m.SetFloat("_FlowSpeed", flow);
        m.SetFloat("_WaveStrength", waves);
        m.SetFloat("_WaveScale", waveScale);
        m.SetFloat("_FoamWidth", foam);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material WaterLake => WaterMat("Eau_Lac", new Color(0.5f, 0.6f, 0.62f), new Color(0.006f, 0.02f, 0.028f), 1.4f, 0f, 0.25f, 0.9f);
    static Material WaterRiver => WaterMat("Eau_Riviere", new Color(0.62f, 0.7f, 0.7f), new Color(0.01f, 0.03f, 0.04f), 0.5f, 0.9f, 0.55f, 0.45f, 0.08f);
    static Material WaterPuddle => WaterMat("Eau_Flaque", new Color(0.35f, 0.36f, 0.37f), new Color(0.02f, 0.025f, 0.03f), 0.03f, 0f, 0.12f, 0.25f, 0f);
    static Material WaterFlood => WaterMat("Eau_Inondation", new Color(0.45f, 0.5f, 0.52f), new Color(0.01f, 0.02f, 0.03f), 0.25f, 0.05f, 0.2f, 0.7f, 0.05f);

    /// Volume (déclencheur) où l'on patauge : ralentit et fait des pas mouillés.
    static void WaterVolumeBox(Transform p, string name, Vector3 center, Vector3 size, Quaternion rot, float slow)
    {
        var go = new GameObject(name);
        go.transform.SetParent(p, false);
        go.transform.SetPositionAndRotation(center, rot);
        var bc = go.AddComponent<BoxCollider>();
        bc.isTrigger = true;
        bc.size = size;
        var wv = go.AddComponent<WaterVolume>();
        wv.speedMultiplier = slow;
        wv.splash = SndWaterSteps;
        NonStatic(go);
    }

    static void WaterVolumeDisc(Transform p, string name, Vector3 surface, float radius, float slow)
    {
        var go = new GameObject(name);
        go.transform.SetParent(p, false);
        go.transform.position = surface;
        var cc = go.AddComponent<CapsuleCollider>();
        cc.isTrigger = true;
        cc.radius = radius;
        cc.height = Mathf.Max(radius * 2f, 4f);
        var wv = go.AddComponent<WaterVolume>();
        wv.speedMultiplier = slow;
        wv.splash = SndWaterSteps;
        NonStatic(go);
    }

    /// Son d'eau en boucle, spatialisé (rivière, clapotis, gouttes).
    static void WaterSound(Transform p, Vector3 pos, AudioClip clip, float volume, float maxDistance)
    {
        if (clip == null) return;
        var go = new GameObject("Son_" + clip.name);
        go.transform.SetParent(p, false);
        go.transform.position = pos;
        var a = go.AddComponent<AudioSource>();
        a.clip = clip;
        a.loop = true;
        a.playOnAwake = true;
        a.volume = volume;
        a.spatialBlend = 1f;
        a.rolloffMode = AudioRolloffMode.Linear;
        a.minDistance = 1f;
        a.maxDistance = maxDistance;
        a.dopplerLevel = 0f;
        a.time = 0f;
        NonStatic(go);
    }
}
