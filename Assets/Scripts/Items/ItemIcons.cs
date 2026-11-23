using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Icônes des objets, rendues à la volée à partir de leur modèle 3D (une fois par objet), fond transparent.
/// </summary>
public static class ItemIcons
{
    private const int Layer = 31;
    private const int Size = 128;
    private static readonly Dictionary<ItemData, Texture2D> Cache = new Dictionary<ItemData, Texture2D>();

    public static Texture2D Get(ItemData item)
    {
        if (item == null || item.model == null) return null;
        if (Cache.TryGetValue(item, out var t) && t != null) return t;
        t = Render(item.model);
        Cache[item] = t;
        return t;
    }

    private static Texture2D Render(GameObject model)
    {
        var origin = new Vector3(0f, -2000f, 0f);
        var go = Object.Instantiate(model, origin, Quaternion.Euler(-20f, 35f, 10f));
        foreach (var tr in go.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = Layer;
        foreach (var c in go.GetComponentsInChildren<Collider>()) c.enabled = false;
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) { Object.Destroy(go); return null; }
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);

        var camGo = new GameObject("Icone_Camera");
        var cam = camGo.AddComponent<Camera>();
        cam.cullingMask = 1 << Layer;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.orthographic = true;
        cam.orthographicSize = Mathf.Max(b.extents.x, b.extents.y, b.extents.z) * 1.15f;
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 100f;
        camGo.transform.position = b.center + new Vector3(0f, 0f, -10f);
        camGo.transform.LookAt(b.center);

        var lightGo = new GameObject("Icone_Lumiere");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.4f;
        light.cullingMask = 1 << Layer;
        lightGo.transform.rotation = Quaternion.Euler(35f, -30f, 0f);

        var rt = RenderTexture.GetTemporary(Size, Size, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        cam.Render();
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        cam.targetTexture = null;
        RenderTexture.ReleaseTemporary(rt);
        Object.Destroy(camGo);
        Object.Destroy(lightGo);
        Object.Destroy(go);
        return tex;
    }
}
