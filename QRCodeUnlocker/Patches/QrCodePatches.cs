using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using JetBrains.Annotations;
using QRCoder;
using SpinCore.UI;
using UnityEngine;

namespace QRCodeUnlocker.Patches;

[HarmonyPatch(typeof(TrackInfoMetadata), MethodType.Constructor)]
[HarmonyPatch([typeof(TrackInfo)])]
internal static class QrCodeTranspiler
{
    // i'm sure there's a better way to like, find all this. but this is hard to wrap my head around.
    // all that needs to be patched out is the TrackInfoMetadata constructor setting `spotifyLink` to string.Empty
    
    [SuppressMessage("ReSharper", "InvertIf")]
    [UsedImplicitly]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        int foundIndex = -1;

        List<CodeInstruction> codes = new(instructions);
        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].opcode == OpCodes.Ldsfld && codes[i].operand.ToString() == "System.String Empty")
            {
                if (codes[i + 1].opcode == OpCodes.Stfld && codes[i + 1].operand.ToString() == "System.String spotifyLink")
                {
                    foundIndex = i;
                    Plugin.Log.LogInfo("Found what we need to patch out");
                    break;
                }
            }
        }
        if (foundIndex > -1)
        {
            // ldarg_0, ldsfld, stfld
            codes.RemoveRange(foundIndex - 1, 3);
            foreach (CodeInstruction t in codes)
            {
                Plugin.Log.LogInfo($"{t.opcode}: {t.operand}");
            }
        }

        return codes.AsEnumerable();
    }
}

[HarmonyPatch]
internal static class QrCodePatches
{
    private static Transform? _trackURLInputFieldTransform;
    
    [HarmonyPatch(typeof(QRCodeDisplay), nameof(QRCodeDisplay.OpenLink))]
    [HarmonyPrefix]
    // ReSharper disable once InconsistentNaming
    private static bool QRCodeDisplay_OpenLink_Patch(QRCodeDisplay __instance)
    {
        if (string.IsNullOrEmpty(__instance.payload))
        {
            return false;
        }
        
        ModalMessageDialog.ModalMessage? modal = ModalMessageDialogExtensions.CreateYesNo();
        modal.message = $"This will open the following URL in your web browser:<br><b>{__instance.payload}</b>";
        modal.affirmativeCallback += () =>
        {
            Application.OpenURL(__instance.payload);
        };
        modal.cancelCallback += () => { };
        
        modal.Open();

        return false;
    }

    [HarmonyPatch(typeof(ClipInfoEditorPanel), nameof(ClipInfoEditorPanel.HandleSaveButton))]
    [HarmonyPrefix]
    // ReSharper disable once InconsistentNaming
    private static bool ClipInfoEditorPanel_HandleSaveButton_Patch(ClipInfoEditorPanel __instance)
    {
        string text = _trackURLInputFieldTransform?.GetComponent<CustomTextMeshProUGUI>().text ?? string.Empty;
        if (!string.IsNullOrEmpty(text))
        {
            // there's a weird 0x200b character added in if i'm not removing it here
            text = text[..^1];
        }

        __instance.CurrentTrackInfo.spotifyLink = text;
        return true;
    }

    // TODO: currently doesn't update when bringing up the details panel, shit borken
    [HarmonyPatch(typeof(ClipInfoEditorPanel), nameof(ClipInfoEditorPanel.OnEnable))]
    [HarmonyPostfix]
    // ReSharper disable once InconsistentNaming
    private static void ClipInfoEditorPanel_OnEnable_Patch(ClipInfoEditorPanel __instance)
    {
        Transform? trackURLTransform = __instance.trackDetailsPanel.transform.Find("TrackURL");
        if (trackURLTransform != null)
        {
            if (_trackURLInputFieldTransform != null)
                _trackURLInputFieldTransform.GetComponent<CustomTextMeshProUGUI>().text =
                    __instance.CurrentTrackInfo?.spotifyLink ?? string.Empty;
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

        _trackURLInputFieldTransform = trackURLInputArea.Find("InputField (TMP)/Text Area/Text");
        _trackURLInputFieldTransform.GetComponent<CustomTextMeshProUGUI>().text = __instance.CurrentTrackInfo?.spotifyLink ?? string.Empty;
    }
}