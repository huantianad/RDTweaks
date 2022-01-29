using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using HarmonyLib;
using Steamworks;

namespace RDTweaks
{
    [BepInPlugin(Guid, "RDTweaks", "0.4.0")]
    [BepInProcess("Rhythm Doctor.exe")]
    public class RDTweaks : BaseUnityPlugin
    {
        private const string Guid = "dev.huantian.plugins.rdtweaks";

        private ConfigEntry<bool> configAlwaysUseSteam;
        private static ConfigEntry<SkipLocation> configSkipOnStartupTo;

        private ConfigEntry<bool> configSkipTitle;

        private ConfigEntry<bool> configCLSScrollWheel;
        private static ConfigEntry<bool> configCLSScrollSound;
        private ConfigEntry<bool> configSkipToLibrary;

        private enum SkipLocation
        {
            MainMenu,
            CLS,
            Editor,
        }

        // Awake is called once when both the game and the plug-in are loaded
        public void Awake()
        {
            configAlwaysUseSteam = Config.Bind("Startup", "AlwaysUseSteam", false,
                "Whether or not to force steam to be used to start the game.");
            configSkipOnStartupTo = Config.Bind("Startup", "SkipOnStartupTo", SkipLocation.MainMenu,
                "Where the game should go on startup.");
            configSkipTitle = Config.Bind("MainMenu", "SkipTitle",  false,
                "Whether or not to skip the logo screen and go directly to the main menu.");
            configCLSScrollWheel = Config.Bind("CLS", "ScrollWheel", false,
                "Whether or not to enable using scroll wheel to scroll in CLS.");
            configCLSScrollSound = Config.Bind("CLS", "ScrollSound", true,
                "Whether or not to play a sound when scrolling with scroll wheel.");
            configSkipToLibrary = Config.Bind("CLS", "SkipToLibrary", false, 
                "Whether or not to automatically enter the level library when entering CLS.");

            switch (configSkipOnStartupTo.Value)
            {
                case SkipLocation.CLS:
                case SkipLocation.Editor:
                    Harmony.CreateAndPatchAll(typeof(SkipOnStartup), Guid + ".SkipOnStartup");
                    break;
                case SkipLocation.MainMenu:
                    // Main Menu is default game behavior, don't change anything.
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (configAlwaysUseSteam.Value)
                Harmony.CreateAndPatchAll(typeof(AlwaysUseSteam), Guid + ".alwaysUseSteam");
            
            if (configSkipTitle.Value)
                Harmony.CreateAndPatchAll(typeof(SkipTitle), Guid + ".skipTitle");
            
            if (configCLSScrollWheel.Value)
                Harmony.CreateAndPatchAll(typeof(CLSScrollWheel), Guid + ".CLSScrollWheel");
            
            if (configSkipToLibrary.Value)
                Harmony.CreateAndPatchAll(typeof(SkipToLibrary), Guid + ".skipToLibrary");

            Logger.LogMessage("Loaded!");
        }
        
        public static class AlwaysUseSteam
        {
            [HarmonyPrefix]
            [HarmonyPatch(typeof(RDStartup), nameof(RDStartup.Setup))]
            public static bool Prefix()
            {
                if (SteamAPI.RestartAppIfNecessary((AppId_t) 774181))
                {
                    Application.Quit();
                    return false;
                }
                    
                return true;
            }
        }

        public static class SkipOnStartup
        {
            [HarmonyPatch(typeof(scnLogo), "Exit")]
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var methodName = configSkipOnStartupTo.Value == SkipLocation.Editor ? "GoToLevelEditor" : "GoToCustomLevelSelect";
                return new CodeMatcher(instructions)
                    .MatchForward(false,
                        new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(scnBase), "GoToMainMenu")))
                    .SetOperandAndAdvance(AccessTools.Method(typeof(scnBase), methodName))
                    .InstructionEnumeration();
            }
        }

        public static class SkipTitle
        {
            [HarmonyPostfix]
            [HarmonyPatch(typeof(scnMenu), "Start")]
            public static void Postfix(scnMenu __instance)
            {
                var rdBase = (scnMenu)Traverse.Create(__instance).Field("_instance").GetValue();
                rdBase.StartCoroutine(GoToMain(__instance));
            }

            private static IEnumerator GoToMain(scnMenu __instance)
            {
                yield return new WaitForSeconds(0.1f);
                AccessTools.Method(__instance.GetType(), "GoToSection").Invoke(__instance, new object[] { 1 });

                // Make sure the arrow doesn't get stuck on the side.
                // while ((bool)Traverse.Create(__instance).Field("changingSection").GetValue())
                // {
                //     Traverse.Create(__instance).Method("HighlightOption", 0, false, false).GetValue();
                //     yield return null;
                // }
            }
        }

        public static class SkipToLibrary
        {
            [HarmonyPostfix]
            [HarmonyPatch(typeof(scnCLS), "Start")]
            public static void Postfix(scnCLS __instance)
            {
                var rdBase = (scnCLS)Traverse.Create(__instance).Field("_instance").GetValue();

                // Copied from scnCLS.SelectWardOption()
                if (SteamIntegration.initialized) SteamWorkshop.ClearItemsInfoCache();
                rdBase.StartCoroutine(__instance.LoadLevelsData(-1f));
            }
        }

        public class CLSScrollWheel
        {
            [HarmonyPrefix]
            [HarmonyPatch(typeof(scnCLS), "Update")]
            public static bool Prefix(scnCLS __instance)
            {
                var rdBase = (scnCLS)Traverse.Create(__instance).Field("_instance").GetValue();
                
                if (!CanSelectLevel(__instance)) return true;  // Not in right place

                var scrolling = Input.GetAxis("Mouse ScrollWheel");
                if (scrolling == 0f) return true;  // They aren't scrolling
                
                // Now, based on the direction they scroll, move them up or down.
                var direction = scrolling < 0f ? 1 : -1;
                var total = __instance.levelDetail.CurrentLevelsData.Count;
                var nextLocation = __instance.CurrentLevelIndex + direction;

                if (nextLocation > total - 1) nextLocation = 0;
                else if (nextLocation < 0) nextLocation = total - 1;

                Traverse.Create(__instance)
                    .Method("ShowSyringesWithIndex", __instance.levelDetail.CurrentLevelsData, nextLocation)
                    .GetValue();
                __instance.sendLevelDataToLevelDetailCoroutine = __instance.SendLevelDataToLevelDetail(timeToUpdate: 0.0f);
                rdBase.StartCoroutine(__instance.sendLevelDataToLevelDetailCoroutine);
                
                if (configCLSScrollSound.Value)
                {
                    var sound = __instance.CurrentLevel.CurrentRank == -3 ? "sndLibrarySelectWrapper" : "sndLibrarySelectSyringe";
                    var percent = RDUtils.PitchSemitonesToPercent(direction);
                    __instance.CLSPlaySound(sound, pitch: percent);
                }

                return false;
            }
        }

        private static bool CanSelectLevel(scnCLS __instance)
        {
            // Copied from scnCLS.Update()
            return __instance.CanReceiveInput && !__instance.levelDetail.showingErrorsContainer
                && !__instance.levelImporter.Showing && !__instance.dialog.gameObject.activeInHierarchy
                
                // Custom checks, make sure they're in the syringe section, and not already selecting a level.
                && (bool)Traverse.Create(__instance).Field("canSelectLevel").GetValue()
                && !__instance.SelectedLevel && !__instance.ShowingWard;
        }
    }
}