using System;
using System.Collections;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.UI;
using HarmonyLib;

namespace RDTweaks
{
    [BepInPlugin("dev.huantian.plugins.rdtweaks", "RDTweaks", "0.1.0.0")]
    [BepInProcess("Rhythm Doctor.exe")]
    public class RDTweaks : BaseUnityPlugin
    {
        private ConfigEntry<SkipLocation> SkipOnStartupTo;
        private ConfigEntry<bool> configSkipTitle;
        private ConfigEntry<bool> configCLSRandom;
        private ConfigEntry<bool> configSkipToLibrary;

        enum SkipLocation
        {
            Disabled,
            MainMenu,
            CLS
        }

        // Awake is called once when both the game and the plug-in are loaded
        void Awake()
        {
            var customFile = new ConfigFile(Path.Combine(Paths.ConfigPath, "RDTweaks.cfg"), true);
            SkipOnStartupTo = customFile.Bind("Startup", "SkipOnStartupTo", SkipLocation.Disabled,
                                              "Where to skip to on startup, i.e. skip the warning text, skipping to CLS.");
            configSkipTitle = customFile.Bind("MainMenu", "SkipLogo",  false,
                                              "Whether or not to skip the logo screen and go directly to the main menu.");
            configCLSRandom = customFile.Bind("CLS", "EnableRandom", false,
                                              "Whether or not to enable random level selector in CLS.");
            configSkipToLibrary = customFile.Bind("CLS", "SkipToLibrary", false,
                                                  "Whether or not to automatically enter the level library when entering CLS.");

            if (SkipOnStartupTo.Value == SkipLocation.MainMenu)
            {
                Harmony.CreateAndPatchAll(typeof(skipWarning), "dev.huantiain.rdtweaks.skipWarning");
            } 
            else if (SkipOnStartupTo.Value == SkipLocation.CLS)
            {
                Harmony.CreateAndPatchAll(typeof(skipToCLS), "dev.huantiain.rdtweaks.skipToCLS");
            }

            if (configSkipTitle.Value)
            {
                Harmony.CreateAndPatchAll(typeof(skipLogo), "dev.huantiain.rdtweaks.skipLogo");
            }

            if (configCLSRandom.Value)
            {
                Harmony.CreateAndPatchAll(typeof(CLSRandom), "dev.huantiain.rdtweaks.CLSRandom");
            }
            if (configSkipToLibrary.Value)
            {
                Harmony.CreateAndPatchAll(typeof(skipToLibrary), "dev.huantiain.rdtweaks.skipToLibrary");
            }
        }


        public static class skipWarning
        {
            [HarmonyPostfix]
            [HarmonyPatch(typeof(scnLogo), "Start")]
            public static void Postfix(scnLogo __instance)
            {
                Traverse.Create(__instance).Method("Exit").GetValue();
            }
        }

        public static class skipToCLS
        {
            [HarmonyPostfix]
            [HarmonyPatch(typeof(scnLogo), "Start")]
            public static void Postfix(scnLogo __instance)
            {
                scnBase.GoToCustomLevelSelect();
            }
        }

        public static class skipLogo
        {
            [HarmonyPostfix]
            [HarmonyPatch(typeof(scnMenu), "Start")]
            public static void Postfix(scnMenu __instance)
            {
                scnMenu rdbase = (scnMenu) Traverse.Create(__instance).Field("_instance").GetValue();
                rdbase.StartCoroutine(GoToMain(__instance));
            }

            public static IEnumerator GoToMain(scnMenu __instance)
            {
                yield return new WaitForSeconds(.03f);
                AccessTools.Method(__instance.GetType(), "GoToSection").Invoke(__instance, new object[] { 1 });

                //while ((bool) Traverse.Create(__instance).Field("changingSection").GetValue())
                //{
                //    yield return null;
                //}

                //Traverse.Create(__instance).Method("HighlightOption", 0, false, false).GetValue();
            }
        }

        public class skipToLibrary
        {
            [HarmonyPostfix]
            [HarmonyPatch(typeof(scnCLS), "Start")]
            public static void Postfix(scnCLS __instance)
            {
                Traverse.Create(__instance).Method("SelectWardOption").GetValue();
            }
        }

        public class CLSRandom
        {
            [HarmonyPrefix]
            [HarmonyPatch(typeof(scnCLS), "Update")]
            public static bool Prefix(scnCLS __instance)
            {
                if (!__instance.CanReceiveInput || __instance.levelDetail.showingErrorsContainer ||
                    __instance.levelImporter.Showing || __instance.dialog.gameObject.activeInHierarchy
                    || !(bool)Traverse.Create(__instance).Field("canSelectLevel").GetValue() || __instance.SelectedLevel
                    // || Time.frameCount == StandaloneFileBrowser.lastFrameCount
                    )
                {
                    return true;
                }

                if (Input.GetKeyDown(KeyCode.R))
                {
                    var rand = new System.Random();
                    int total = __instance.levelDetail.CurrentLevelsData.Count;
                    GoToLevel(__instance, rand.Next(0, total));
                    return false;
                }

                return true;
            }

            public static void GoToLevel(scnCLS __instance, int index)
            {
                scnCLS rdbase = (scnCLS) Traverse.Create(__instance).Field("_instance").GetValue();
                rdbase.StopCoroutine(__instance.sendLevelDataToLevelDetailCoroutine);
                rdbase.StopCoroutine(__instance.playLevelPreviewAudioClipCoroutine);
                __instance.previewSongPlayer.Stop(0f);

                __instance.ShowSyringes(__instance.levelDetail.CurrentLevelsData, index, false);
                foreach (CustomLevel customLevel in __instance.visibleLevels)
                {
                    bool flag = customLevel == __instance.CurrentLevel;
                    customLevel.PlayPlungerIdle(flag);
                    customLevel.ToggleSyringUsb(flag, false);
                    customLevel.FadeOverlay(flag, true);
                }

                __instance.sendLevelDataToLevelDetailCoroutine = __instance.SendLevelDataToLevelDetail(false, 0f);
                rdbase.StartCoroutine(__instance.sendLevelDataToLevelDetailCoroutine);
                Traverse.Create(__instance).Method("ToggleScrollbar", true, false).GetValue();
                Traverse.Create(__instance).Method("ToggleScrollbar", false, false).GetValue();
            }
        }
    }
}
