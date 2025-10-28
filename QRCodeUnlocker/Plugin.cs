using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using SpinCore.Translation;

namespace QRCodeUnlocker;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("srxd.raoul1808.spincore", "1.1.2")]
public class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log = null!;
    private static readonly Harmony HarmonyInstance = new(MyPluginInfo.PLUGIN_GUID);

    private void Awake()
    {
        Log = Logger;
        
        TranslationHelper.AddTranslation($"{nameof(QRCodeUnlocker)}_TrackURLLabel", "Track URL");
        
        Log.LogInfo("Plugin loaded");
    }

    private void OnEnable()
    {
        HarmonyInstance.PatchAll();
    }

    private void OnDisable()
    {
        HarmonyInstance.UnpatchSelf();
    }
}