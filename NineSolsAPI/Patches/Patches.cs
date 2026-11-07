using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using NineSolsAPI.Preload;
using UnityEngine.Device;
using UnityEngine.SceneManagement;
using NineSolsAPI.Utils;

namespace NineSolsAPI.Patches;

[HarmonyPatch]
[SuppressMessage("ReSharper", "InconsistentNaming")]
public class Patches {
    [HarmonyPatch(typeof(AchievementData), "OnAcquired")]
    [HarmonyPrefix]
    private static bool AchievementAcquired(ref AchievementData __instance) => NineSolsAPICore.AchivementsEnabled;

    [HarmonyPatch(typeof(LogoLogic), nameof(LogoLogic.Start))]
    [HarmonyPostfix]
    private static void Start(ref LogoLogic __instance) {
        if (Application.buildGUID == GameVersions.SpeedrunPatch) {
            return;
        }
        
        RuntimeInitHandler.LoadCore();
        SceneManager.LoadScene(__instance.NextScene);
    }


    [HarmonyPatch(typeof(GameLevel), nameof(GameLevel.Awake))]
    [HarmonyPrefix]
    private static bool GameLevelAwake() => !Preloader.IsPreloading;

    [HarmonyPatch(typeof(SaveSlotUIButton), nameof(SaveSlotUIButton.Submit))]
    [HarmonyPrefix]
    private static bool Submit(SaveSlotUIButton __instance)
    {
        bool result = NineSolsAPICore.Instance.titlescreenModifications.modList?.CheckModDesyncsBeforeLoadSave(__instance) ?? true;
        return result;
    }
    
    [HarmonyPatch(typeof(StartMenuLogic), nameof(StartMenuLogic.CreateOrLoadSaveSlotAndPlay))]
    [HarmonyPrefix]
    private static void CreateOrLoadSaveSlotAndPlay()
    {
        SaveUtils.Start();
    }
    
}
