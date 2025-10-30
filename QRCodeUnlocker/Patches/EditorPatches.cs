using HarmonyLib;
using UnityEngine;

namespace QRCodeUnlocker.Patches;

[HarmonyPatch]
internal static class EditorPatches
{
    private static Transform? _trackURLInputFieldTransform;
    
    [HarmonyPatch(typeof(ClipInfoEditorPanel), nameof(ClipInfoEditorPanel.HandleSaveButton))]
    [HarmonyPrefix]
    // ReSharper disable once InconsistentNaming
    private static bool ClipInfoEditorPanel_HandleSaveButton_Patch(ClipInfoEditorPanel __instance)
    {
        string text = _trackURLInputFieldTransform?.GetComponent<XDInputField>().tmpField.text ?? string.Empty;
        __instance.CurrentTrackInfo.spotifyLink = text;
        
        return true;
    }
    
    [HarmonyPatch(typeof(ClipInfoEditorPanel), nameof(ClipInfoEditorPanel.OnEnable))]
    [HarmonyPostfix]
    // ReSharper disable once InconsistentNaming
    private static void ClipInfoEditorPanel_OnEnable_Patch(ClipInfoEditorPanel __instance)
    {
        Transform? trackURLTransform = __instance.trackDetailsPanel.transform.Find("TrackURL");
        if (trackURLTransform != null)
        {
            return;
        }
        
        Transform? charterTransform = __instance.trackDetailsPanel.transform.Find("Charter");
        if (charterTransform == null)
        {
            Plugin.Log.LogWarning("charterTransform is null");
            return;
        }

        if (__instance.trackDetailsPanel.transform == null)
        {
            Plugin.Log.LogWarning("__instance.trackDetailsPanel.transform is null");
            return;
        }
        
        GameObject trackURLContainer = Object.Instantiate(charterTransform.gameObject, __instance.trackDetailsPanel.transform);
        trackURLContainer.name = "TrackURL";
        trackURLTransform = trackURLContainer.transform;
        trackURLTransform.SetSiblingIndex(charterTransform.GetSiblingIndex() + 1);

        Transform trackURLTitleArea = trackURLTransform.Find("CharterTitleArea");
        trackURLTitleArea.name = "TrackURLTitleArea";
        Transform trackURLInputArea = trackURLTransform.Find("CharterInputArea");
        trackURLInputArea.name = "TrackURLInputArea";

        Transform trackURLTitle = trackURLTitleArea.Find("CharterTitle");
        trackURLTitle.name = "TrackURLTitle";
        trackURLTitle.GetComponent<TranslatedTextMeshPro>().SetTranslationKey($"{nameof(QRCodeUnlocker)}_TrackURLLabel");

        _trackURLInputFieldTransform = trackURLInputArea.Find("InputField (TMP)");
        XDInputField inputField = _trackURLInputFieldTransform.GetComponent<XDInputField>();
        inputField.tmpField.characterLimit = 2332; // QR code renders empty past this point
        CustomTextMeshProUGUI? tmp = _trackURLInputFieldTransform.Find("Text Area/Text")?.GetComponent<CustomTextMeshProUGUI>();
        if (tmp != null)
        {
            tmp.fontSizeMin = 8;
            tmp.fontSizeMax = 8;
            tmp.fontSize = 8;
        }
        
        inputField.tmpField.SetText(string.Empty);
        inputField.tmpField.OnStoppedEditing += () =>
        {
            if (inputField.tmpField.textWhenStartedEditing != inputField.tmpField.text)
            {
                __instance.CurrentTrackInfo.SetDirty();
            }
        };
    }

    [HarmonyPatch(typeof(TrackEditorGUI), nameof(TrackEditorGUI.OnEditClipInfoPressed))]
    [HarmonyPostfix]
    // ReSharper disable once InconsistentNaming
    private static void TrackEditorGUI_OnEditClipInfoPressed_Patch(TrackEditorGUI __instance)
    {
        if (__instance.trackDataToEdit == null)
        {
            Plugin.Log.LogWarning("__instance.trackDataToEdit is null");
            return;
        }
        if (_trackURLInputFieldTransform == null)
        {
            Plugin.Log.LogWarning("_trackURLInputFieldTransform is null");
            return;
        }

        string link = __instance.trackDataToEdit.GetInfoRefForFirstSegment().asset.spotifyLink;
        _trackURLInputFieldTransform.GetComponent<XDInputField>().tmpField.SetText(link);
    }
}

