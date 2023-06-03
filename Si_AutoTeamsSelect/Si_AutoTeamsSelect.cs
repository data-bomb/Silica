using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using System.Timers;
using UnityEngine;
using VersusTeamsAutoSelect;

[assembly: MelonInfo(typeof(VersusTeamsAutoSelectMod), "[Si] Versus Auto-Select Team", "1.0.3", "databomb", "https://github.com/data-bomb/Silica_ListenServer")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]

namespace VersusTeamsAutoSelect
{

    public class VersusTeamsAutoSelectMod : MelonMod
    {
        static MP_Strategy strategyInstance;
        static bool bTimerExpired;
        static bool bRestartHasppened;
        static KeyCode overrideKey;

        private static System.Timers.Timer DelayTimer;

        public static void PrintError(Exception exception, string message = null)
        {
            if (message != null)
            {
                MelonLogger.Msg(message);
            }
            string error = exception.Message;
            error += "\n" + exception.TargetSite;
            error += "\n" + exception.StackTrace;
            MelonLogger.Error(error);
        }

        static MelonPreferences_Category _modCategory;
        static MelonPreferences_Entry<Il2Cpp.MP_Strategy.ETeamsVersus> _versusAutoSelectMode;

        private const string ModCategory = "Silica";
        private const string AutoSelectMode = "VersusAutoSelectMode";
        
        public override void OnInitializeMelon()
        {
            overrideKey = KeyCode.Space;

            if (_modCategory == null)
            {
                _modCategory = MelonPreferences.CreateCategory(ModCategory);
            }
            if (_versusAutoSelectMode == null)
            {
                _versusAutoSelectMode = _modCategory.CreateEntry<Il2Cpp.MP_Strategy.ETeamsVersus>(AutoSelectMode, Il2Cpp.MP_Strategy.ETeamsVersus.HUMANS_VS_ALIENS, "Valid choices are HUMANS_VS_HUMANS, HUMANS_VS_ALIENS, or HUMANS_VS_HUMANS_VS_ALIENS");
            }
        }

        private static void HandleTimerAutoRestart(object source, ElapsedEventArgs e)
        {
            VersusTeamsAutoSelectMod.bTimerExpired = true;
        }

        [HarmonyPatch(typeof(Il2Cpp.GameMode), nameof(Il2Cpp.GameMode.Update))]
        private static class ApplyPatch_GameModeUpdate
        {
            private static void Postfix(Il2Cpp.GameMode __instance)
            {
                try
                {
                    // check if timer expired
                    if (VersusTeamsAutoSelectMod.bRestartHasppened == true && VersusTeamsAutoSelectMod.bTimerExpired == true)
                    {
                        VersusTeamsAutoSelectMod.bRestartHasppened = false;
                        Il2Cpp.MP_Strategy.ETeamsVersus versusMode = VersusTeamsAutoSelectMod._versusAutoSelectMode.Value;

                        if (VersusTeamsAutoSelectMod.strategyInstance != null)
                        {
                            // check for override key to allow host to manually select the versus mode
                            if (Input.GetKeyDown(overrideKey))
                            {
                                MelonLogger.Msg("Skipped Versus Mode selection for this round. Select desired Versus Mode manually.");
                            }
                            else
                            {
                                VersusTeamsAutoSelectMod.strategyInstance.SetTeamVersusMode(versusMode);
                                MelonLogger.Msg("Selected Versus Mode for new round");
                            }
                        }
                    }
                }
                catch (Exception error)
                {
                    VersusTeamsAutoSelectMod.PrintError(error, "Failed to run Update");
                }
            }
        }

        [HarmonyPatch(typeof(Il2Cpp.MP_Strategy), nameof(Il2Cpp.MP_Strategy.Restart))]
        public static class ApplyPatchSelectHumansVersusAliens
        {
            public static void Postfix(Il2Cpp.MP_Strategy __instance)
            {
                try
                {
                    VersusTeamsAutoSelectMod.strategyInstance = __instance;
                    VersusTeamsAutoSelectMod.bTimerExpired = false;
                    VersusTeamsAutoSelectMod.bRestartHasppened = true;

                    // introduce a delay to account for issue on latest game version causing clients and server to become desynchronized
                    double interval = 2000.0;
                    VersusTeamsAutoSelectMod.DelayTimer = new System.Timers.Timer(interval);
                    VersusTeamsAutoSelectMod.DelayTimer.Elapsed += new ElapsedEventHandler(VersusTeamsAutoSelectMod.HandleTimerAutoRestart);
                    VersusTeamsAutoSelectMod.DelayTimer.AutoReset = false;
                    VersusTeamsAutoSelectMod.DelayTimer.Enabled = true;
                }
                catch (Exception error)
                {
                    VersusTeamsAutoSelectMod.PrintError(error, "Failed to run Restart");
                }
            }
        }
    }
}