using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering.Universal;

public static class FeatureController
{
    /// <summary>
    /// Enable/disable a renderer feature on the currently active URP renderer (by feature.name in the ForwardRendererData asset).
    /// </summary>
    public static bool SetFeatureEnabled(string featureName, bool enabled)
    {
        var feature = FindFeature(featureName);
        if (feature == null)
            return false;

        if (feature.isActive == enabled)
            return true;

        feature.SetActive(enabled);

        // Some features cache data in Create(); forcing rebuild makes changes take effect immediately.
        try { feature.Create(); } catch { /* ignore */ }
        return true;
    }

    /// <summary>
    /// Enable/disable the RenderObjects feature (PlayerStencil), and optionally change its stencil reference value.
    /// </summary>
    public static bool ConfigurePlayerStencil(string featureName, bool enabled, int? stencilReference = null)
    {
        var feature = FindFeature(featureName);
        if (feature == null)
            return false;

        feature.SetActive(enabled);

        if (feature is RenderObjects ro)
        {
            if (stencilReference.HasValue)
            {
                ro.settings.stencilSettings.overrideStencilState = true;
                ro.settings.stencilSettings.stencilReference = Mathf.Clamp(stencilReference.Value, 0, 255);
                // Keep other operations as authored in the asset (passOperation etc).
            }

            ro.Create(); // rebuild its internal pass with new settings
            return true;
        }

        // If for some reason the feature type differs, fall back to reflection on "settings.stencilSettings.stencilReference".
        if (stencilReference.HasValue)
            TrySetStencilReferenceByReflection(feature, Mathf.Clamp(stencilReference.Value, 0, 255));

        try { feature.Create(); } catch { /* ignore */ }
        return true;
    }

    private static ScriptableRendererFeature FindFeature(string featureName)
    {
        var rendererData = GetActiveRendererData();
        if (rendererData == null)
        {
            Debug.LogWarning("URP renderer data not found. Is URP active?");
            return null;
        }

        var features = rendererData.rendererFeatures;
        if (features == null)
            return null;

        for (int i = 0; i < features.Count; i++)
        {
            var f = features[i];
            if (f != null && f.name == featureName)
                return f;
        }

        return null;
    }

    private static ScriptableRendererData GetActiveRendererData()
    {
        // Prefer GraphicsSettings.currentRenderPipeline (works in build).
        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp == null)
            urp = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;

        if (urp == null)
            return null;

        // scriptableRendererData is internal in URP, but accessible via reflection at runtime.
        var prop = typeof(UniversalRenderPipelineAsset).GetProperty(
            "scriptableRendererData",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        if (prop != null)
            return prop.GetValue(urp) as ScriptableRendererData;

        // Fallback to the serialized field names (URP 14 uses these).
        var fieldList = typeof(UniversalRenderPipelineAsset).GetField(
            "m_RendererDataList",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var fieldIndex = typeof(UniversalRenderPipelineAsset).GetField(
            "m_DefaultRendererIndex",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        var list = fieldList?.GetValue(urp) as ScriptableRendererData[];
        var idx = fieldIndex != null ? (int)fieldIndex.GetValue(urp) : 0;
        if (list == null || list.Length == 0)
            return null;
        if (idx < 0 || idx >= list.Length)
            idx = 0;
        return list[idx];
    }

    private static void TrySetStencilReferenceByReflection(ScriptableRendererFeature feature, int stencilRef)
    {
        try
        {
            var t = feature.GetType();
            var settingsField = t.GetField("settings", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (settingsField == null)
                return;

            var settingsObj = settingsField.GetValue(feature);
            if (settingsObj == null)
                return;

            var stencilSettingsField = settingsObj.GetType().GetField("stencilSettings", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (stencilSettingsField == null)
                return;

            var stencilSettingsObj = stencilSettingsField.GetValue(settingsObj);
            if (stencilSettingsObj == null)
                return;

            var overrideField = stencilSettingsObj.GetType().GetField("overrideStencilState", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            overrideField?.SetValue(stencilSettingsObj, true);

            var refField = stencilSettingsObj.GetType().GetField("stencilReference", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            refField?.SetValue(stencilSettingsObj, stencilRef);
        }
        catch
        {
            // ignore
        }
    }
}

