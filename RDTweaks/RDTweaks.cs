using System;
using System.Reflection.Emit;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using HarmonyLib;
using RDLevelEditor;

namespace RDTweaks
{
    [BepInPlugin(Guid, "RDTweaks", "0.2.0")]
    [BepInProcess("Rhythm Doctor.exe")]
    public class RDTweaks : BaseUnityPlugin
    {
        private const string Guid = "dev.huantian.plugins.rdtweaks";
        
        private ConfigEntry<SkipLocation> configSkipOnStartupTo;

        private static ConfigEntry<bool> configSkipTitle;

        private static ConfigEntry<bool> configCLSScrollWheel;
        private static ConfigEntry<bool> configCLSScrollSound;
        private static ConfigEntry<bool> configCLSRandom;
        private static ConfigEntry<KeyboardShortcut> configCLSRandomKeybinding;
        private static ConfigEntry<bool> configSkipToLibrary;

        private static ConfigEntry<KeyboardShortcut> configEditorScaleUp;
        private static ConfigEntry<KeyboardShortcut> configEditorScaleDown;

        private enum SkipLocation
        {
            Disabled,
            MainMenu,
            CLS,
            Editor,
        }

        // Awake is called once when both the game and the plug-in are loaded
        public void Awake()
        {
            configSkipOnStartupTo = Config.Bind("Startup", "SkipOnStartupTo", SkipLocation.Disabled,
                "Where to skip to on startup, i.e. skip the warning text, skipping to CLS.");
            configSkipTitle = Config.Bind("MainMenu", "SkipTitle",  false,
                "Whether or not to skip the logo screen and go directly to the main menu.");
            configCLSScrollWheel = Config.Bind("CLS", "ScrollWheel", false,
                "Whether or not to enable using scroll wheel to scroll in CLS.");
            configCLSScrollSound = Config.Bind("CLS", "ScrollSound", true,
                "Whether or not to play a sound when scrolling with scroll wheel.");
            configCLSRandom = Config.Bind("CLS", "EnableRandom", false,
                "Whether or not to enable random level selector in CLS.");
            configCLSRandomKeybinding = Config.Bind("CLS", "RandomKeybinding", new KeyboardShortcut(KeyCode.R),
                "Key to press for selecting a random level.");
            configSkipToLibrary = Config.Bind("CLS", "SkipToLibrary", false, 
                "Whether or not to automatically enter the level library when entering CLS.");
            configEditorScaleUp = Config.Bind("Editor", "EditorScaleUp", new KeyboardShortcut(),
                "Keybinding to increase the UI scale of the editor.");
            configEditorScaleDown = Config.Bind("Editor", "EditorScaleDown", new KeyboardShortcut(),
                "Keybinding to decrease the UI scale of the editor.");

            if (configSkipOnStartupTo.Value != SkipLocation.Disabled)
            {
                Harmony.CreateAndPatchAll(typeof(SkipWarning), Guid + ".skipWarning");
                switch (configSkipOnStartupTo.Value)
                {
                    case SkipLocation.CLS:
                        Harmony.CreateAndPatchAll(typeof(SkipToCLS), Guid + ".skipToCLS");
                        break;
                    case SkipLocation.Editor:
                        Harmony.CreateAndPatchAll(typeof(SkipToEditor), Guid + ".skipToEditor");
                        break;
                }
            }


            Harmony.CreateAndPatchAll(typeof(SkipTitle), Guid + ".skipTitle");

            Harmony.CreateAndPatchAll(typeof(CLSScrollWheel), Guid + ".CLSScrollWheel");
            Harmony.CreateAndPatchAll(typeof(CLSRandom), Guid + ".CLSRandom");
            Harmony.CreateAndPatchAll(typeof(SkipToLibrary), Guid + ".skipToLibrary");

            Harmony.CreateAndPatchAll(typeof(EditorScaleKeybinding), Guid + ".editorScaleKeybinding");

            // Harmony.CreateAndPatchAll(typeof(UnwrapAll), Guid + ".unwrapAll");

            Logger.LogMessage("Loaded!");
        }
        public static class SkipWarning
        {
            [HarmonyPostfix]
            [HarmonyPatch(typeof(scnLogo), "Start")]
            public static void Postfix(scnLogo __instance)
            {
                Traverse.Create(__instance).Method("Exit").GetValue();
            }
        }

        public static class SkipToCLS
        {
            [HarmonyPatch(typeof(scnLogo), "Exit")]
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
                new CodeMatcher(instructions)
                    .MatchForward(false,
                        new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(scnBase), "GoToMainMenu")))
                    .SetOperandAndAdvance(AccessTools.Method(typeof(scnBase), "GoToCustomLevelSelect"))
                    .InstructionEnumeration();
        }

        public static class SkipToEditor
        {
            [HarmonyPatch(typeof(scnLogo), "Exit")]
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
                new CodeMatcher(instructions)
                    .MatchForward(false,
                        new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(scnBase), "GoToMainMenu")))
                    .SetOperandAndAdvance(AccessTools.Method(typeof(scnBase), "GoToLevelEditor"))
                    .InstructionEnumeration();
        }

        public static class SkipTitle
        {
            [HarmonyPostfix]
            [HarmonyPatch(typeof(scnMenu), "Start")]
            public static void Postfix(scnMenu __instance)
            {
                if (!configSkipTitle.Value) return;
                
                var rdBase = (scnMenu)Traverse.Create(__instance).Field("_instance").GetValue();
                rdBase.StartCoroutine(GoToMain(__instance));
            }

            private static IEnumerator GoToMain(scnMenu __instance)
            {
                AccessTools.Method(__instance.GetType(), "GoToSection").Invoke(__instance, new object[] { 1 });

                // Make sure the arrow doesn't get stuck on the side.
                while ((bool)Traverse.Create(__instance).Field("changingSection").GetValue())
                {
                    Traverse.Create(__instance).Method("HighlightOption", 0, false, false).GetValue();
                    yield return null;
                }
            }
        }

        public static class SkipToLibrary
        {
            [HarmonyPostfix]
            [HarmonyPatch(typeof(scnCLS), "Start")]
            public static void Postfix(scnCLS __instance)
            {
                if (!configSkipToLibrary.Value) return;
                
                var rdBase = (scnCLS)Traverse.Create(__instance).Field("_instance").GetValue();

                // Copied from scnCLS.SelectWardOption()
                if (SteamIntegration.initialized) SteamWorkshop.ClearItemsInfoCache();
                rdBase.StartCoroutine(__instance.LoadLevelsData(-1f));
            }
        }

        public static class CLSRandom
        {
            [HarmonyPrefix]
            [HarmonyPatch(typeof(scnCLS), "Update")]
            public static bool Prefix(scnCLS __instance)
            {
                if (!configCLSRandom.Value) return true;  // Tweak not enabled
                if (!CanSelectLevel(__instance)) return true;  // Not in right place

                if (!configCLSRandomKeybinding.Value.IsDown()) return true;  // Not pressing R

                var rand = new System.Random();
                var total = __instance.levelDetail.CurrentLevelsData.Count;
                GoToLevel(__instance, rand.Next(total));

                return false;
            }
        }

        public class CLSScrollWheel
        {
            [HarmonyPrefix]
            [HarmonyPatch(typeof(scnCLS), "Update")]
            public static bool Prefix(scnCLS __instance)
            {
                if (!configCLSScrollWheel.Value) return true;  // Tweak not enabled
                if (!CanSelectLevel(__instance)) return true;  // Not in right place

                var scrolling = Input.GetAxis("Mouse ScrollWheel");
                if (scrolling == 0f) return true;  // They aren't scrolling
                
                // Now, based on the direction they scroll, move them up or down.
                var direction = scrolling < 0f ? 1 : -1;
                var total = __instance.levelDetail.CurrentLevelsData.Count;
                var nextLocation = __instance.CurrentLevelIndex + direction;

                if (nextLocation > total - 1) nextLocation = 0;
                else if (nextLocation < 0) nextLocation = total - 1;

                GoToLevel(__instance, nextLocation);
                
                if (configCLSScrollSound.Value)
                {
                    var sound = __instance.CurrentLevel.CurrentRank == -3 ? "sndLibrarySelectWrapper" : "sndLibrarySelectSyringe";
                    var percent = RDUtils.PitchSemitonesToPercent(direction);
                    __instance.CLSPlaySound(sound, pitch: percent);
                }

                return false;
            }
        }

        public class EditorScaleKeybinding
        {
            [HarmonyPrefix]
            [HarmonyPatch(typeof(scnEditor), "Update")]
            public static bool Prefix(scnEditor __instance)
            {
                var scaleUp = configEditorScaleUp.Value.IsDown();
                var scaleDown = configEditorScaleDown.Value.IsDown();

                if (!(scaleUp | scaleDown)) return true;
                
                var rdBase = (scnEditor) Traverse.Create(__instance).Field("_instance").GetValue();
                var updateSize = Traverse.Create(__instance).Method("UpdateEditorSizeCo", scaleUp).GetValue();
                rdBase.StartCoroutine((IEnumerator) updateSize);

                return false;
            }
        }
        private static void GoToLevel(scnCLS __instance, int index)
        {
            var rdBase = (scnCLS)Traverse.Create(__instance).Field("_instance").GetValue();

            //Copied from scnCLS.ChangeManyLevels()
            rdBase.StopCoroutine(__instance.sendLevelDataToLevelDetailCoroutine);
            rdBase.StopCoroutine(__instance.playLevelPreviewAudioClipCoroutine);
            __instance.previewSongPlayer.Stop(0f);

            __instance.ShowSyringes(__instance.levelDetail.CurrentLevelsData, index, false);
            foreach (var customLevel in __instance.visibleLevels)
            {
                var flag = customLevel == __instance.CurrentLevel;
                customLevel.PlayPlungerIdle(flag);
                customLevel.ToggleSyringUsb(flag, false);
                customLevel.FadeOverlay(flag, true);
            }

            __instance.sendLevelDataToLevelDetailCoroutine = __instance.SendLevelDataToLevelDetail(false, 0f);
            rdBase.StartCoroutine(__instance.sendLevelDataToLevelDetailCoroutine);
            Traverse.Create(__instance).Method("ToggleScrollbar", true, false).GetValue();
            Traverse.Create(__instance).Method("ToggleScrollbar", false, false).GetValue();
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
