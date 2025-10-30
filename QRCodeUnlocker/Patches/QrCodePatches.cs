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
#if DEBUG
            foreach (CodeInstruction t in codes)
            {
                Plugin.Log.LogInfo($"{t.opcode}: {t.operand}");
            }
#endif
        }

        return codes.AsEnumerable();
    }
}

[HarmonyPatch]
internal static class QrCodePatches
{
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
}