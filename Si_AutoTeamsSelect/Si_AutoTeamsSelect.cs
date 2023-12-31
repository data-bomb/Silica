/*
Silica Versus Auto-Select
Copyright (C) 2023 by databomb

* Description *
For Silica listen servers, automatically sets the versus mode after
round restarts to allow for unattended listen servers operating as
a dedicated server.

* License *
This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

#if NET6_0
using Il2Cpp;
#endif

using HarmonyLib;
using MelonLoader;
using System.Timers;
using UnityEngine;
using VersusTeamsAutoSelect;
using System.Linq;
using SilicaAdminMod;
using System;

[assembly: MelonInfo(typeof(VersusTeamsAutoSelectMod), "Versus Auto-Select Team", "1.1.1", "databomb", "https://github.com/data-bomb/Silica")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]
[assembly: MelonOptionalDependencies("Admin Mod")]

namespace VersusTeamsAutoSelect
{

    public class VersusTeamsAutoSelectMod : MelonMod
    {
        static MP_Strategy strategyInstance;
        static bool bTimerExpired;
        static bool bRestartHasppened;
        static KeyCode overrideKey;
        static MP_Strategy.ETeamsVersus requestedMode;

        private static System.Timers.Timer DelayTimer;

        static MelonPreferences_Category _modCategory;
        static MelonPreferences_Entry<MP_Strategy.ETeamsVersus> _versusAutoSelectMode;

        private const string ModCategory = "Silica";
        private const string AutoSelectMode = "VersusAutoSelectMode";

        static bool AdminModAvailable = false;

        public override void OnInitializeMelon()
        {
            overrideKey = KeyCode.Space;
            requestedMode = MP_Strategy.ETeamsVersus.NONE;

            if (_modCategory == null)
            {
                _modCategory = MelonPreferences.CreateCategory(ModCategory);
            }
            if (_versusAutoSelectMode == null)
            {
                _versusAutoSelectMode = _modCategory.CreateEntry<MP_Strategy.ETeamsVersus>(AutoSelectMode, MP_Strategy.ETeamsVersus.HUMANS_VS_ALIENS, "Valid choices are HUMANS_VS_HUMANS, HUMANS_VS_ALIENS, or HUMANS_VS_HUMANS_VS_ALIENS");
            }
        }
        public override void OnLateInitializeMelon()
        {
            AdminModAvailable = RegisteredMelons.Any(m => m.Info.Name == "Admin Mod");

            if (AdminModAvailable)
            {
                HelperMethods.CommandCallback changeNextModeCallback = Command_ChangeNextMode;
                HelperMethods.RegisterAdminCommand("!nextmode", changeNextModeCallback, Power.Map);
            }
            else
            {
                MelonLogger.Warning("Dependency missing: Admin Mod");
            }
        }

        public static void Command_ChangeNextMode(Player callerPlayer, String args)
        {
            // validate argument count
            int argumentCount = args.Split(' ').Count() - 1;
            if (argumentCount > 1)
            {
                HelperMethods.ReplyToCommand(args.Split(' ')[0] + ": Too many arguments");
                return;
            }
            else if (argumentCount < 1)
            {
                HelperMethods.ReplyToCommand(args.Split(' ')[0] + ": Too few arguments");
                return;
            }

            // validate argument contents
            String modeText = args.Split(' ')[1];
            MP_Strategy.ETeamsVersus desiredVersusMode = MP_Strategy.ETeamsVersus.NONE;

            if (String.Equals(modeText, "HvH", StringComparison.OrdinalIgnoreCase) || String.Equals(modeText, "HH", StringComparison.OrdinalIgnoreCase))
            {
                desiredVersusMode = MP_Strategy.ETeamsVersus.HUMANS_VS_HUMANS;
            }
            else if (String.Equals(modeText, "HvA", StringComparison.OrdinalIgnoreCase) || String.Equals(modeText, "HA", StringComparison.OrdinalIgnoreCase))
            {
                desiredVersusMode = MP_Strategy.ETeamsVersus.HUMANS_VS_ALIENS;
            }
            else if (String.Equals(modeText, "HvHvA", StringComparison.OrdinalIgnoreCase) || String.Equals(modeText, "HHA", StringComparison.OrdinalIgnoreCase))
            {
                desiredVersusMode = MP_Strategy.ETeamsVersus.HUMANS_VS_HUMANS_VS_ALIENS;
            }

            if (desiredVersusMode == MP_Strategy.ETeamsVersus.NONE)
            {
                HelperMethods.ReplyToCommand(args.Split(' ')[0] + ": Invalid next mode input");
                return;
            }

            // indicate we want to manually select the next mode
            requestedMode = desiredVersusMode;
            HelperMethods.ReplyToCommand(args.Split(' ')[0] + ": Selected next mode as " + desiredVersusMode.ToString());
        }

        private static void HandleTimerAutoRestart(object source, ElapsedEventArgs e)
        {
            VersusTeamsAutoSelectMod.bTimerExpired = true;
        }

        #if NET6_0
        [HarmonyPatch(typeof(GameMode), nameof(GameMode.Update))]
        #else
        [HarmonyPatch(typeof(GameMode), "Update")]
        #endif
        private static class ApplyPatch_GameModeUpdate
        {
            private static void Postfix(GameMode __instance)
            {
                try
                {
                    // check if timer expired
                    if (VersusTeamsAutoSelectMod.bRestartHasppened == true && VersusTeamsAutoSelectMod.bTimerExpired == true)
                    {
                        VersusTeamsAutoSelectMod.bRestartHasppened = false;
                        MP_Strategy.ETeamsVersus versusMode = VersusTeamsAutoSelectMod._versusAutoSelectMode.Value;

                        if (VersusTeamsAutoSelectMod.strategyInstance != null)
                        {
                            // check for override key to allow host to manually select the versus mode
                            if (Input.GetKey(overrideKey))
                            {
                                MelonLogger.Msg("Skipped Versus Mode selection for this round. Select desired Versus Mode manually.");
                            }
                            // check if an admin wanted to manually select the versus mode
                            else if (requestedMode != MP_Strategy.ETeamsVersus.NONE)
                            {
                                VersusTeamsAutoSelectMod.strategyInstance.SetTeamVersusMode(requestedMode);
                                MelonLogger.Msg("Selected Versus Mode for new round: " + requestedMode.ToString());
                                requestedMode = MP_Strategy.ETeamsVersus.NONE;
                            }
                            // no requests to deviate from configured versus mode
                            else
                            {
                                VersusTeamsAutoSelectMod.strategyInstance.SetTeamVersusMode(versusMode);
                                MelonLogger.Msg("Selected Versus Mode for new round: " + versusMode.ToString());
                            }
                        }
                    }
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run GameMode::Update");
                }
            }
        }

        #if NET6_0
        [HarmonyPatch(typeof(MP_Strategy), nameof(MP_Strategy.Restart))]
        #else
        [HarmonyPatch(typeof(MP_Strategy), "Restart")]
        #endif
        public static class ApplyPatchSelectHumansVersusAliens
        {
            public static void Postfix(MP_Strategy __instance)
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
                    HelperMethods.PrintError(error, "Failed to run MP_Strategy::Restart");
                }
            }
        }
    }
}