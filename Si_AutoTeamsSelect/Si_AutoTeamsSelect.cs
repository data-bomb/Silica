/*
Silica Versus Auto-Select
Copyright (C) 2024 by databomb

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

[assembly: MelonInfo(typeof(VersusTeamsAutoSelectMod), "Versus Auto-Select Team", "1.1.5", "databomb", "https://github.com/data-bomb/Silica")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]
[assembly: MelonOptionalDependencies("Admin Mod")]

namespace VersusTeamsAutoSelect
{

    public class VersusTeamsAutoSelectMod : MelonMod
    {
        static MP_Strategy? strategyInstance;
        static bool bTimerExpired;
        static bool bRestartHasppened;
        static KeyCode overrideKey;
        static MP_Strategy.ETeamsVersus requestedMode;

        private static System.Timers.Timer? DelayTimer;

        static MelonPreferences_Category _modCategory = null!;
        static MelonPreferences_Entry<MP_Strategy.ETeamsVersus> _versusAutoSelectMode = null!;

        private const string ModCategory = "Silica";
        private const string AutoSelectMode = "VersusAutoSelectMode";

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
            HelperMethods.CommandCallback changeNextModeCallback = Command_ChangeNextMode;
            HelperMethods.RegisterAdminCommand("nextmode", changeNextModeCallback, Power.Map);

            #if NET6_0
            bool QListLoaded = RegisteredMelons.Any(m => m.Info.Name == "QList");
            if (!QListLoaded)
            {
                return;
            }

            QList.Options.RegisterMod(this);

            QList.OptionTypes.IntOption negativeThreshold = new(_versusAutoSelectMode, false, (int)_versusAutoSelectMode.Value, 0, 5);

            QList.Options.AddOption(negativeThreshold);
            #endif
        }

        public static void Command_ChangeNextMode(Player callerPlayer, String args)
        {
            string commandName = args.Split(' ')[0];
            
            // validate argument count
            int argumentCount = args.Split(' ').Count() - 1;
            if (argumentCount > 1)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Too many arguments");
                return;
            }
            else if (argumentCount < 1)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Too few arguments");
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
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Invalid next mode input");
                return;
            }

            // indicate we want to manually select the next mode
            requestedMode = desiredVersusMode;
            HelperMethods.ReplyToCommand(args.Split(' ')[0] + ": Selected next mode as " + desiredVersusMode.ToString());
        }

        private static void HandleTimerAutoRestart(object? source, ElapsedEventArgs e)
        {
            bTimerExpired = true;
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
                    if (bRestartHasppened == true && bTimerExpired == true && _versusAutoSelectMode != null)
                    {
                        bRestartHasppened = false;
                        MP_Strategy.ETeamsVersus versusMode = _versusAutoSelectMode.Value;

                        if (strategyInstance != null)
                        {
                            // check for override key to allow host to manually select the versus mode
                            if (Input.GetKey(overrideKey))
                            {
                                MelonLogger.Msg("Skipped Versus Mode selection for this round. Select desired Versus Mode manually.");
                            }
                            // check if an admin wanted to manually select the versus mode
                            else if (requestedMode != MP_Strategy.ETeamsVersus.NONE)
                            {
                                strategyInstance.SetTeamVersusMode(requestedMode);
                                MelonLogger.Msg("Selected Versus Mode for new round: " + requestedMode.ToString());
                                requestedMode = MP_Strategy.ETeamsVersus.NONE;
                            }
                            // no requests to deviate from configured versus mode
                            else
                            {
                                strategyInstance.SetTeamVersusMode(versusMode);
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
                    strategyInstance = __instance;
                    bTimerExpired = false;
                    bRestartHasppened = true;

                    // introduce a delay to account for issue on latest game version causing clients and server to become desynchronized
                    double interval = 2000.0;
                    DelayTimer = new System.Timers.Timer(interval);
                    DelayTimer.Elapsed += new ElapsedEventHandler(VersusTeamsAutoSelectMod.HandleTimerAutoRestart);
                    DelayTimer.AutoReset = false;
                    DelayTimer.Enabled = true;
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run MP_Strategy::Restart");
                }
            }
        }
    }
}