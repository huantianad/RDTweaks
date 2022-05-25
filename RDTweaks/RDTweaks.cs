using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using RDLevelEditor;
using Steamworks;
using UnityEngine;

namespace RDTweaks
{
    [BepInPlugin(Guid, "RDTweaks", "0.5.1")]
    [BepInProcess("Rhythm Doctor.exe")]
    public class RDTweaks : BaseUnityPlugin
    {
        private const string Guid = "dev.huantian.plugins.rdtweaks";

        private static class PConfig
        {
            internal static ConfigEntry<bool> AlwaysUseSteam;
            internal static ConfigEntry<SkipLocation> SkipOnStartupTo;

            internal static ConfigEntry<bool> SkipTitle;

            internal static ConfigEntry<bool> CLSScrollWheel;
            internal static ConfigEntry<bool> CLSScrollSound;
            internal static ConfigEntry<bool> SkipToLibrary;

            internal static ConfigEntry<bool> HideMouseCursor;
            internal static ConfigEntry<bool> BlockMouseInGame;
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
            PConfig.AlwaysUseSteam = Config.Bind("Startup", "AlwaysUseSteam", false,
                "Whether or not to force steam to be used to start the game.");
            PConfig.SkipOnStartupTo = Config.Bind("Startup", "SkipOnStartupTo", SkipLocation.MainMenu,
                "Where the game should go on startup.");
            PConfig.SkipTitle = Config.Bind("MainMenu", "SkipTitle", false,
                "Whether or not to skip the logo screen and go directly to the main menu.");
            PConfig.CLSScrollWheel = Config.Bind("CLS", "ScrollWheel", false,
                "Whether or not to enable using scroll wheel to scroll in CLS.");
            PConfig.CLSScrollSound = Config.Bind("CLS", "ScrollSound", true,
                "Whether or not to play a sound when scrolling with scroll wheel.");
            PConfig.SkipToLibrary = Config.Bind("CLS", "SkipToLibrary", false,
                "Whether or not to automatically enter the level library when entering CLS.");
            PConfig.HideMouseCursor = Config.Bind("Gameplay", "HideMouseCursor", false,
                "Whether or not to hide mouse cursor when in a level");
            PConfig.BlockMouseInGame = Config.Bind("Gameplay", "BlockMouseInGame", false,
                "Whether or not mouse input is disabled in a level");

            if (PConfig.SkipOnStartupTo.Value != SkipLocation.MainMenu)
                Harmony.CreateAndPatchAll(typeof(SkipOnStartup), Guid + ".SkipOnStartup");

            if (PConfig.AlwaysUseSteam.Value)
                Harmony.CreateAndPatchAll(typeof(AlwaysUseSteam), Guid + ".AlwaysUseSteam");

            Harmony.CreateAndPatchAll(typeof(SkipTitle), Guid + ".SkipTitle");
            Harmony.CreateAndPatchAll(typeof(CLSScrollWheel), Guid + ".CLSScrollWheel");
            Harmony.CreateAndPatchAll(typeof(SkipToLibrary), Guid + ".SkipToLibrary");
            Harmony.CreateAndPatchAll(typeof(HideMouseCursor), Guid + ".HideMouseCursor");
            Harmony.CreateAndPatchAll(typeof(BlockMouseInGame), Guid + ".BlockMouseInGame");

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
            [HarmonyPatch(typeof(scnLogo), nameof(scnLogo.Exit))]
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var methodName = PConfig.SkipOnStartupTo.Value == SkipLocation.Editor
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
            [HarmonyPatch(typeof(scnMenu), nameof(scnMenu.Start))]
            public static void Postfix(scnMenu __instance)
            {
                if (!PConfig.SkipTitle.Value) return;

                __instance.StartCoroutine(GoToMain(__instance));
            }

            private static IEnumerator GoToMain(scnMenu __instance)
            {
                yield return new WaitForSeconds(0.1f);
                __instance.GoToSection(scnMenu.MenuSection.MainMenu);
            }
        }

        public static class SkipToLibrary
        {
            [HarmonyPostfix]
            [HarmonyPatch(typeof(scnCLS), nameof(scnCLS.Start))]
            public static void Postfix(scnCLS __instance)
            {
                if (!PConfig.SkipToLibrary.Value) return;

                // Copied from scnCLS.SelectWardOption()
                if (SteamIntegration.initialized) SteamWorkshop.ClearItemsInfoCache();
                __instance.StartCoroutine(__instance.LoadLevelsData(-1f));
            }
        }

        public class CLSScrollWheel
        {
            [HarmonyPrefix]
            [HarmonyPatch(typeof(scnCLS), nameof(scnCLS.Update))]
            public static bool Prefix(scnCLS __instance)
            {
                if (!PConfig.CLSScrollWheel.Value) return true;

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

                __instance.ShowSyringesWithIndex(__instance.levelDetail.CurrentLevelsData, nextLocation);
                __instance.sendLevelDataToLevelDetailCoroutine =
                    __instance.SendLevelDataToLevelDetail(timeToUpdate: 0.5f);
                __instance.StartCoroutine(__instance.sendLevelDataToLevelDetailCoroutine);

                if (PConfig.CLSScrollSound.Value)
                {
                    var sound = __instance.CurrentLevel.CurrentRank == -3
                        ? "sndLibrarySelectWrapper"
                        : "sndLibrarySelectSyringe";
                    var percent = RDUtils.PitchSemitonesToPercent(direction);
                    __instance.CLSPlaySound(sound, pitch: percent);
                }

                return false;
            }

            private static bool CanSelectLevel(scnCLS __instance)
            {
                return
                    // Copied from scnCLS.Update()
                    __instance.CanReceiveInput
                    && !__instance.levelDetail.showingErrorsContainer
                    && !__instance.levelImporter.Showing
                    && !__instance.dialog.gameObject.activeInHierarchy

                    // Custom checks, make sure they're in the syringe section
                    // and not already selecting a level.
                    && __instance.canSelectLevel
                    && !__instance.SelectedLevel
                    && !__instance.ShowingWard;
            }
        }


        public static class HideMouseCursor
        {
            /// Toggle cursor when entering and leaving level
            [HarmonyPrefix]
            [HarmonyPatch(typeof(scnBase), nameof(scnBase.Start))]
            public static bool Prefix(scnBase __instance)
            {
                Cursor.visible = !(
                    PConfig.HideMouseCursor.Value
                    && __instance is scnGame
                    && !__instance.editorMode
                );

                return true;
            }

            /// Enable cursor in the pause menu, disable when exiting pause
            [HarmonyPostfix]
            [HarmonyPatch(typeof(scnGame), nameof(scnGame.TogglePauseGame))]
            public static void Postfix(scnGame __instance)
            {
                if (!PConfig.HideMouseCursor.Value) return;

                if (__instance.editorMode) return;

                Cursor.visible = __instance.paused;
            }
        }

        public static class BlockMouseInGame
        {
            [HarmonyPrefix]
            [HarmonyPatch(
                typeof(RDInputType_Keyboard),
                nameof(RDInputType_Keyboard.mouseButtonsAvailable),
                MethodType.Getter
            )]
            public static bool Prefix(RDInputType_Keyboard __instance, ref bool __result)
            {
                var game = scnGame.instance;
                var editor = scnEditor.instance;
                if (game != null && editor == null && !game.pauseMenu.showing && PConfig.BlockMouseInGame.Value)
                {
                    __result = false;
                    return false;
                }

                return true;
            }
        }
    }
}
