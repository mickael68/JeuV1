using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Crée et assigne l'asset URP du projet, réimporte les modèles pour que leurs matériaux
/// passent en URP, puis régénère la map greybox (matériaux en URP Lit).
/// Menu : The Echoes Below > Configurer URP.
/// </summary>
public static class EchoesURPSetup
{
    const string Folder = "Assets/Settings";
    const string RendererPath = Folder + "/EchoesBelow_Renderer.asset";
    const string PipelinePath = Folder + "/EchoesBelow_URP.asset";
    const string PostProcessPath = "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset";

    [MenuItem("The Echoes Below/Configurer URP")]
    public static void SetupAndRegenerate()
    {
        var pipeline = CreateOrLoadPipeline();

        // Assigné partout : réglages graphiques + chaque niveau de qualité.
        GraphicsSettings.defaultRenderPipeline = pipeline;
        int current = QualitySettings.GetQualityLevel();
        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            QualitySettings.SetQualityLevel(i, false);
            QualitySettings.renderPipeline = pipeline;
        }
        QualitySettings.SetQualityLevel(current, false);
        AssetDatabase.SaveAssets();

        ReimportModels();
        EchoesMapGenerator.Generate();

        Debug.Log("[EchoesURP] URP configuré : " + PipelinePath + " (Forward+). Map régénérée en URP Lit.");
    }

    static UniversalRenderPipelineAsset CreateOrLoadPipeline()
    {
        var existing = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
        if (existing != null) return existing;

        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "Settings");

        var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
        // Forward+ : pas de limite de 8 lumières par objet (nos couloirs en ont beaucoup).
        renderer.renderingMode = RenderingMode.ForwardPlus;
        if (renderer.postProcessData == null)
            renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(PostProcessPath);
        AssetDatabase.CreateAsset(renderer, RendererPath);

        var pipeline = UniversalRenderPipelineAsset.Create(renderer);
        pipeline.supportsHDR = true;
        pipeline.supportsCameraDepthTexture = true;
        pipeline.shadowDistance = 40f;
        pipeline.maxAdditionalLightsCount = 8;
        AssetDatabase.CreateAsset(pipeline, PipelinePath);
        AssetDatabase.SaveAssets();
        return pipeline;
    }

    // Les matériaux intégrés aux modèles (FBX, OBJ…) sont générés à l'import :
    // on réimporte pour qu'ils utilisent le shader URP au lieu du Standard (rose sinon).
    static void ReimportModels()
    {
        var guids = AssetDatabase.FindAssets("t:Model", new[] { "Assets" });
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var g in guids)
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(g), ImportAssetOptions.ForceUpdate);
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }
        Debug.Log($"[EchoesURP] {guids.Length} modèles réimportés.");
    }
}
