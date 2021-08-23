﻿using System;
using System.Reflection.Emit;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using HarmonyLib;

namespace RDTweaks
{
    [BepInPlugin("dev.huantian.plugins.rdtweaks", "RDTweaks", "0.2.0")]
    [BepInProcess("Rhythm Doctor.exe")]
    public class RDTweaks : BaseUnityPlugin
    {
        private ConfigEntry<SkipLocation> configSkipOnStartupTo;

        private ConfigEntry<bool> configSkipTitle;

        private ConfigEntry<bool> configCLSScrollWheel;
        private ConfigEntry<bool> configCLSRandom;
        private ConfigEntry<bool> configSkipToLibrary;

        private ConfigEntry<bool> configSwapP1P2;

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
            var customFile = new ConfigFile(Path.Combine(Paths.ConfigPath, "RDTweaks.cfg"), true);
            configSkipOnStartupTo = customFile.Bind("Startup", "SkipOnStartupTo", SkipLocation.Disabled,
                                              "Where to skip to on startup, i.e. skip the warning text, skipping to CLS.");
            configSkipTitle = customFile.Bind("MainMenu", "SkipLogo",  false,
                                              "Whether or not to skip the logo screen and go directly to the main menu.");
            configCLSScrollWheel = customFile.Bind("CLS", "ScrollWheel", false,
                                                   "Whether or not to enable using scroll wheel to scroll in CLS.");
            configCLSRandom = customFile.Bind("CLS", "EnableRandom", false,
                                              "Whether or not to enable random level selector in CLS.");
            configSkipToLibrary = customFile.Bind("CLS", "SkipToLibrary", false,
                                                  "Whether or not to automatically enter the level library when entering CLS.");
            configSwapP1P2 = customFile.Bind("Gameplay", "SwapP1P2", false,
                                             "Whether or not to automatically swap P1 and P2, so P1 is on the left.");

            if (configSkipOnStartupTo.Value != SkipLocation.Disabled)
            {
                Harmony.CreateAndPatchAll(typeof(SkipWarning), "dev.huantian.rdtweaks.skipWarning");
                switch (configSkipOnStartupTo.Value)
                {
                    case SkipLocation.CLS:
                        Harmony.CreateAndPatchAll(typeof(SkipToCLS), "dev.huantian.rdtweaks.skipToCLS");
                        break;
                    case SkipLocation.Editor:
                        Harmony.CreateAndPatchAll(typeof(SkipToEditor), "dev.huantian.rdtweaks.skipToEditor");
                        break;
                }
            }

            if (configSkipTitle.Value)
            {
                Harmony.CreateAndPatchAll(typeof(SkipLogo), "dev.huantian.rdtweaks.skipLogo");
            }

            if (configCLSScrollWheel.Value)
            {
                Harmony.CreateAndPatchAll(typeof(CLSScrollWheel), "dev.huantian.rdtweaks.CLSScrollWheel");
            }
            if (configCLSRandom.Value)
            {
                Harmony.CreateAndPatchAll(typeof(CLSRandom), "dev.huantian.rdtweaks.CLSRandom");
            }
            if (configSkipToLibrary.Value)
            {
                Harmony.CreateAndPatchAll(typeof(SkipToLibrary), "dev.huantian.rdtweaks.skipToLibrary");
            }

            if (configSwapP1P2.Value)
            {
                Harmony.CreateAndPatchAll(typeof(SwapP1P2), "dev.huantian.rdtweaks.swapP1P2");
            }

            //Harmony.CreateAndPatchAll(typeof(unwrapAll), "dev.huantian.rdtweaks.unwrapAll");


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
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                return new CodeMatcher(instructions)
                    .MatchForward(false,
                        new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(scnBase), "GoToMainMenu")))
                    .SetOperandAndAdvance(AccessTools.Method(typeof(scnBase), "GoToCustomLevelSelect"))
                    .InstructionEnumeration();
            }
        }

        public static class SkipToEditor
        {
            [HarmonyPatch(typeof(scnLogo), "Exit")]
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                return new CodeMatcher(instructions)
                    .MatchForward(false,
                        new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(scnBase), "GoToMainMenu")))
                    .SetOperandAndAdvance(AccessTools.Method(typeof(scnBase), "GoToLevelEditor"))
                    .InstructionEnumeration();
            }
        }

        public static class SkipLogo
        {
            [HarmonyPostfix]
            [HarmonyPatch(typeof(scnMenu), "Start")]
            public static void Postfix(scnMenu __instance)
            {
                var rdbase = (scnMenu)Traverse.Create(__instance).Field("_instance").GetValue();
                rdbase.StartCoroutine(GoToMain(__instance));
            }

            public static IEnumerator GoToMain(scnMenu __instance)
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

        public static class SwapP1P2
        {
            [HarmonyPostfix]
            [HarmonyPatch(typeof(RDInput), "Setup")]
            public static void Postfix()
            {
                // Copied from RDInput.SwapP1AndP2Controls()
                RDInput.p1.SwapSchemeIndex();
                RDInput.p1Default.SwapSchemeIndex();
                RDInput.p2.SwapSchemeIndex();
                RDInput.p2Default.SwapSchemeIndex();
                GC.PanP1 = ((RDInput.p1.schemeIndex == 0) ? 1f : -1f);
                GC.PanP2 = ((RDInput.p2.schemeIndex == 0) ? 1f : -1f);
            }
        }

        public static class SkipToLibrary
        {
            [HarmonyPostfix]
            [HarmonyPatch(typeof(scnCLS), "Start")]
            public static void Postfix(scnCLS __instance)
            {
                var rdbase = (scnCLS)Traverse.Create(__instance).Field("_instance").GetValue();

                // Copied from scnCLS.SelectWardOption()
                if (SteamIntegration.initialized) SteamWorkshop.ClearItemsInfoCache();
                rdbase.StartCoroutine(__instance.LoadLevelsData(-1f));
            }
        }

        public static class unwrapAll
        {
            [HarmonyPostfix]
            [HarmonyPatch(typeof(scnCLS), "Start")]
            public static void Postfix(scnCLS __instance)
            {
                var rdbase = (scnCLS)Traverse.Create(__instance).Field("_instance").GetValue();
                rdbase.StartCoroutine(Test(__instance));
            }

            public static IEnumerator Test(scnCLS __instance)
            {
                foreach (CustomLevelData levelData in __instance.levelDetail.CurrentLevelsData)
                {
                    //Debug.Log(levelData.tags);
                    if (Persistence.GetCustomLevelRank(levelData.Hash, 1f) == -3)
                    {
                        Debug.Log("hi");
                        Persistence.SetCustomLevelRank(levelData.Hash, -1, 1f);
                    }
                }
                yield break;
            }
        }

        public static class CLSRandom
        {
            [HarmonyPrefix]
            [HarmonyPatch(typeof(scnCLS), "Update")]
            public static bool Prefix(scnCLS __instance)
            {
                if (!CanSelectLevel(__instance)) return true;  // Not in right place

                if (!Input.GetKeyDown(KeyCode.R)) return true;  // Not pressing R
                
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
                if (!CanSelectLevel(__instance)) return true; // Not in right place

                var scrolling = Input.GetAxis("Mouse ScrollWheel");
                if (scrolling == 0f) return true;  // They aren't scrolling
                
                var direction = scrolling < 0f ? 1 : -1;
                var total = __instance.levelDetail.CurrentLevelsData.Count;
                var nextLocation = __instance.CurrentLevelIndex + direction;

                if (nextLocation > total - 1) nextLocation = 0;
                else if (nextLocation < 0) nextLocation = total - 1;

                GoToLevel(__instance, nextLocation);

                return false;
            }
        }
        private static void GoToLevel(scnCLS __instance, int index)
        {
            var rdbase = (scnCLS)Traverse.Create(__instance).Field("_instance").GetValue();

            //Copied from scnCLS.ChangeManyLevels()
            rdbase.StopCoroutine(__instance.sendLevelDataToLevelDetailCoroutine);
            rdbase.StopCoroutine(__instance.playLevelPreviewAudioClipCoroutine);
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
            rdbase.StartCoroutine(__instance.sendLevelDataToLevelDetailCoroutine);
            Traverse.Create(__instance).Method("ToggleScrollbar", true, false).GetValue();
            Traverse.Create(__instance).Method("ToggleScrollbar", false, false).GetValue();
        }

        public static bool CanSelectLevel(scnCLS __instance)
        {
            // Copied from scnCLS.Update()
            return __instance.CanReceiveInput && !__instance.levelDetail.showingErrorsContainer 
                && !__instance.levelImporter.Showing && !__instance.dialog.gameObject.activeInHierarchy
                // Custom ones to only be true when they're in the scrolling syrnge section.
                && (bool)Traverse.Create(__instance).Field("canSelectLevel").GetValue()
                && !__instance.SelectedLevel && !__instance.ShowingWard;
        }
    }
}
