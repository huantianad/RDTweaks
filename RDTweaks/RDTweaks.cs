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
    [BepInPlugin(Guid, "RDTweaks", "0.5.0")]
    [BepInProcess("Rhythm Doctor.exe")]
    public class RDTweaks : BaseUnityPlugin
    {
        private const string Guid = "dev.huantian.plugins.rdtweaks";

        private static class PConfig
        {
            internal static ConfigEntry<bool> alwaysUseSteam;
            internal static ConfigEntry<SkipLocation> skipOnStartupTo;

            internal static ConfigEntry<bool> skipTitle;

            internal static ConfigEntry<bool> CLSScrollWheel;
            internal static ConfigEntry<bool> CLSScrollSound;
            internal static ConfigEntry<bool> skipToLibrary;

            internal static ConfigEntry<bool> hideMouseCursor;
        }


        private enum SkipLocation
        {
            MainMenu,
            CLS,
            Editor,
        }

        // Awake is called once when both the game and the plug-in are loaded
        public void Awake()
        {
            PConfig.alwaysUseSteam = Config.Bind("Startup", "AlwaysUseSteam", false,
                "Whether or not to force steam to be used to start the game.");
            PConfig.skipOnStartupTo = Config.Bind("Startup", "SkipOnStartupTo", SkipLocation.MainMenu,
                "Where the game should go on startup.");
            PConfig.skipTitle = Config.Bind("MainMenu", "SkipTitle",  false,
                "Whether or not to skip the logo screen and go directly to the main menu.");
            PConfig.CLSScrollWheel = Config.Bind("CLS", "ScrollWheel", false,
                "Whether or not to enable using scroll wheel to scroll in CLS.");
            PConfig.CLSScrollSound = Config.Bind("CLS", "ScrollSound", true,
                "Whether or not to play a sound when scrolling with scroll wheel.");
            PConfig.skipToLibrary = Config.Bind("CLS", "SkipToLibrary", false,
                "Whether or not to automatically enter the level library when entering CLS.");
            PConfig.hideMouseCursor = Config.Bind("Gameplay", "hideMouseCursor", false,
                "Whether or not to hide mouse cursor when in a level");

            if (PConfig.skipOnStartupTo.Value != SkipLocation.MainMenu)
                Harmony.CreateAndPatchAll(typeof(SkipOnStartup), Guid + ".SkipOnStartup");

            if (PConfig.alwaysUseSteam.Value)
                Harmony.CreateAndPatchAll(typeof(AlwaysUseSteam), Guid + ".alwaysUseSteam");

            Harmony.CreateAndPatchAll(typeof(SkipTitle), Guid + ".skipTitle");
            Harmony.CreateAndPatchAll(typeof(CLSScrollWheel), Guid + ".CLSScrollWheel");
            Harmony.CreateAndPatchAll(typeof(SkipToLibrary), Guid + ".skipToLibrary");
            Harmony.CreateAndPatchAll(typeof(HideMouseCursor), Guid + ".hideMouseCursor");

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
                var methodName = PConfig.skipOnStartupTo.Value == SkipLocation.Editor
                    ? "GoToLevelEditor"
                    : "GoToCustomLevelSelect";

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
                if (!PConfig.skipTitle.Value) return;

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
                if (!PConfig.skipToLibrary.Value) return;

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
                if (!PConfig.CLSScrollWheel.Value) return true;

                var rdBase = (scnCLS)Traverse.Create(__instance).Field("_instance").GetValue();

                // Check if they're in the level select
                if (!CanSelectLevel(__instance)) return true;

                // Check if they can scroll
                var scrolling = Input.GetAxis("Mouse ScrollWheel");
                if (scrolling == 0f) return true;
                
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
                
                if (PConfig.CLSScrollSound.Value)
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

        public static class HideMouseCursor
        {
            [HarmonyPrefix]
            [HarmonyPatch(typeof(scnGame), "Start")]
            public static bool Prefix(scnGame __instance)
            {
                if (!PConfig.hideMouseCursor.Value) return true;

                if (__instance.editor != null) return true;

                Cursor.visible = false;
                return true;
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(scnBase), "Start")]
            public static bool Prefix(scnBase __instance)
            {
                if (!(__instance is scnGame))
                    Cursor.visible = true;

                return true;
            }
        }
    }
}