using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Prépare les modèles Poly Haven (FBX + dossier textures/) pour URP :
/// un matériau URP Lit par matériau du FBX, avec couleur, normal map, et une carte métal/brillance
/// fabriquée à partir des cartes "rough" et "metal" de Poly Haven (URP lit le métal en R, la brillance en A).
/// Les modèles sont dans Assets/Models/PolyHaven/&lt;id&gt;/&lt;id&gt;_1k.fbx.
/// </summary>
public static class PolyHavenModels
{
    public const string Root = "Assets/Models/PolyHaven";

    [MenuItem("The Echoes Below/Préparer les modèles Poly Haven")]
    public static void PrepareAllMenu() => PrepareAll(true);

    /// force = refaire les matériaux même s'ils existent déjà.
    public static void PrepareAll(bool force)
    {
        if (!AssetDatabase.IsValidFolder(Root)) return;
        int done = 0;
        foreach (var dir in Directory.GetDirectories(Root))
        {
            string id = Path.GetFileName(dir);
            string fbx = $"{Root}/{id}/{id}_1k.fbx";
            if (!File.Exists(fbx)) continue;
            if (!force && AssetDatabase.IsValidFolder($"{Root}/{id}/Materials")) continue;
            try { Prepare(id, fbx); done++; }
            catch (System.Exception e) { Debug.LogWarning($"[PolyHaven] {id} : {e.Message}"); }
        }
        AssetDatabase.SaveAssets();
        if (done > 0) Debug.Log($"[PolyHaven] {done} modèle(s) préparé(s).");
    }

    public static string FbxPath(string id) => $"{Root}/{id}/{id}_1k.fbx";

    static readonly string[] Channels = { "nor_gl", "diff", "roughness", "rough", "metallic", "metal", "alpha", "disp", "arm", "ao" };

    static void Prepare(string id, string fbx)
    {
        // 1) Jeux de textures : préfixe → canal → chemin
        var sets = new Dictionary<string, Dictionary<string, string>>();
        string texDir = $"{Root}/{id}/textures";
        if (Directory.Exists(texDir))
            foreach (var f in Directory.GetFiles(texDir))
            {
                if (f.EndsWith(".meta")) continue;
                string file = Path.GetFileNameWithoutExtension(f);           // ex. metal_trash_can_rust_diff_1k
                if (file.EndsWith("_1k")) file = file.Substring(0, file.Length - 3);
                foreach (var ch in Channels)
                    if (file.EndsWith("_" + ch))
                    {
                        string prefix = file.Substring(0, file.Length - ch.Length - 1);
                        if (!sets.TryGetValue(prefix, out var s)) sets[prefix] = s = new Dictionary<string, string>();
                        string chan = ch == "roughness" ? "rough" : ch == "metallic" ? "metal" : ch;
                        s[chan] = f.Replace('\\', '/');
                        break;
                    }
            }

        // 2) Import du FBX (matériaux remappés vers nos matériaux URP)
        var imp = (ModelImporter)AssetImporter.GetAtPath(fbx);
        imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        imp.importAnimation = false;
        imp.importCameras = false;
        imp.importLights = false;
        imp.SaveAndReimport();

        var names = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Material>().Select(m => m.name).Distinct().ToList();
        if (names.Count == 0)
        {
            // Matériaux introuvables : on prend les noms référencés par les renderers
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            if (go != null)
                names = go.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).Select(m => m.name).Distinct().ToList();
        }

        string matDir = $"{Root}/{id}/Materials";
        if (!AssetDatabase.IsValidFolder(matDir)) AssetDatabase.CreateFolder($"{Root}/{id}", "Materials");

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        foreach (var name in names)
        {
            var set = PickSet(sets, name, id);
            string path = $"{matDir}/{Sanitize(name)}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
            else mat.shader = shader;
            Fill(mat, set, $"{matDir}/{Sanitize(name)}_mask.png");
            EditorUtility.SetDirty(mat);
            imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), mat);
        }
        imp.SaveAndReimport();
    }

    static string Sanitize(string s) => string.Concat(s.Split(Path.GetInvalidFileNameChars()));

    static Dictionary<string, string> PickSet(Dictionary<string, Dictionary<string, string>> sets, string matName, string id)
    {
        if (sets.Count == 0) return new Dictionary<string, string>();
        if (sets.Count == 1) return sets.Values.First();
        string m = matName.ToLowerInvariant();
        // Correspondance exacte, puis le préfixe le plus long contenu dans le nom (ou l'inverse)
        foreach (var kv in sets) if (kv.Key.ToLowerInvariant() == m) return kv.Value;
        var best = sets.OrderByDescending(kv => Score(kv.Key.ToLowerInvariant(), m, id.ToLowerInvariant())).First();
        return best.Value;
    }

    static int Score(string prefix, string mat, string id)
    {
        string part = prefix.StartsWith(id) ? prefix.Substring(id.Length).Trim('_') : prefix;
        if (part.Length == 0) return mat.Contains(id) ? 1 : 0;   // jeu "principal"
        if (mat.EndsWith(part) || mat.Contains("_" + part) || mat.Contains(part)) return 10 + part.Length;
        return 0;
    }

    static void Fill(Material mat, Dictionary<string, string> set, string maskPath)
    {
        mat.SetColor("_BaseColor", Color.white);
        if (set.TryGetValue("diff", out var diff))
        {
            SetImport(diff, TextureImporterType.Default, true, false);
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(diff));
        }
        if (set.TryGetValue("alpha", out _))
        {
            // Feuillage : découpe alpha (la texture d'alpha est fusionnée dans la couleur)
            mat.SetFloat("_AlphaClip", 1f);
            mat.SetFloat("_Cutoff", 0.5f);
            mat.EnableKeyword("_ALPHATEST_ON");
        }
        if (set.TryGetValue("nor_gl", out var nor))
        {
            SetImport(nor, TextureImporterType.NormalMap, false, false);
            mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(nor));
            mat.SetFloat("_BumpScale", 1f);
            mat.EnableKeyword("_NORMALMAP");
        }

        set.TryGetValue("rough", out var rough);
        set.TryGetValue("metal", out var metal);
        if (rough != null || metal != null)
        {
            var mask = BuildMask(rough, metal, maskPath);
            if (mask != null)
            {
                mat.SetTexture("_MetallicGlossMap", mask);
                mat.SetFloat("_Smoothness", 1f);
                mat.SetFloat("_SmoothnessTextureChannel", 0f);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
        }
        else mat.SetFloat("_Smoothness", 0.2f);
    }

    static Texture2D BuildMask(string rough, string metal, string maskPath)
    {
        Texture2D r = rough != null ? ReadLinear(rough) : null;
        Texture2D m = metal != null ? ReadLinear(metal) : null;
        var src = r ?? m;
        if (src == null) return null;
        int w = src.width, h = src.height;
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                float rv = r != null ? r.GetPixelBilinear(u, v).r : 0.7f;
                float mv = m != null ? m.GetPixelBilinear(u, v).r : 0f;
                px[y * w + x] = new Color32((byte)(Mathf.Clamp01(mv) * 255f), 0, 0, (byte)((1f - Mathf.Clamp01(rv)) * 255f));
            }
        var dst = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
        dst.SetPixels32(px);
        File.WriteAllBytes(maskPath, dst.EncodeToPNG());
        Object.DestroyImmediate(dst);
        if (r != null) Object.DestroyImmediate(r);
        if (m != null && m != r) Object.DestroyImmediate(m);
        AssetDatabase.ImportAsset(maskPath);
        SetImport(maskPath, TextureImporterType.Default, false, false);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath);
    }

    /// Copie lisible (valeurs linéaires) d'une texture du projet, JPG/PNG/EXR.
    static Texture2D ReadLinear(string path)
    {
        SetImport(path, TextureImporterType.Default, false, true);
        var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (t == null) return null;
        var copy = new Texture2D(t.width, t.height, TextureFormat.RGBAFloat, false, true);
        copy.SetPixels(t.GetPixels());
        copy.Apply();
        // On peut relâcher la lecture CPU ensuite
        SetImport(path, TextureImporterType.Default, false, false);
        return copy;
    }

    static void SetImport(string path, TextureImporterType type, bool srgb, bool readable)
    {
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) { AssetDatabase.ImportAsset(path); ti = AssetImporter.GetAtPath(path) as TextureImporter; }
        if (ti == null) return;
        bool changed = ti.textureType != type || ti.sRGBTexture != srgb || ti.isReadable != readable;
        if (readable && ti.textureCompression != TextureImporterCompression.Uncompressed) { ti.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
        if (!readable && ti.textureCompression == TextureImporterCompression.Uncompressed) { ti.textureCompression = TextureImporterCompression.Compressed; changed = true; }
        if (!changed) return;
        ti.textureType = type;
        ti.sRGBTexture = srgb;
        ti.isReadable = readable;
        ti.SaveAndReimport();
    }
}
