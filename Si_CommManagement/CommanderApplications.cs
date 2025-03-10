/*
Silica Commander Management Mod
Copyright (C) 2023-2024 by databomb

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
using SilicaAdminMod;
using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using UnityEngine;
using System.Reflection;
using System.Text;

namespace Si_CommanderManagement
{
    public class CommanderApplications
    {
        public static List<Player>[] commanderApplicants = null!;
        public static List<PreviousCommander>? previousCommanders;
        public static Player?[]? teamswapCommanderChecks;
        public static Player[]? promotedCommanders;

        public static void InitializeApplications()
        {
            commanderApplicants = new List<Player>[SiConstants.MaxPlayableTeams + 1];
            for (int i = 0; i < SiConstants.MaxPlayableTeams; i++)
            {
                commanderApplicants[i] = new List<Player>();
            }

            previousCommanders = new List<PreviousCommander>();
            teamswapCommanderChecks = new Player[SiConstants.MaxPlayableTeams + 1];
            promotedCommanders = new Player[SiConstants.MaxPlayableTeams + 1];
        }

        public static bool IsApplicant(Player player)
        {
            if (player.Team == null)
            {
                return false;
            }

            return commanderApplicants[player.Team.Index].Any(k => k == player);
        }

        public static void Command_Commander(Player? callerPlayer, String args)
        {
            if (callerPlayer == null)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, " Console not supported.");
                return;
            }

            if (commanderApplicants == null)
            {
                MelonLogger.Warning("Commander applicant pool missing.");
                return;
            }

            if (callerPlayer.Team == null)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, " On invalid team.");
                return;
            }

            if (!GameMode.CurrentGameMode.Started || GameMode.CurrentGameMode.GameBegun)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, " Cannot apply for commander during the game.");
                return;
            }

            // check if they are already on the commander applicant list
            bool hasApplied = commanderApplicants[callerPlayer.Team.Index].Any(k => k == callerPlayer);

            if (!hasApplied)
            {

                commanderApplicants[callerPlayer.Team.Index].Add(callerPlayer);
                HelperMethods.ReplyToCommand_Player(callerPlayer, "applied for commander");
            }
            else
            {
                commanderApplicants[callerPlayer.Team.Index].Remove(callerPlayer);

                // spawn a unit for them
                GameMode.CurrentGameMode.SpawnUnitForPlayer(callerPlayer, callerPlayer.Team);

                HelperMethods.ReplyToCommand_Player(callerPlayer, "removed themselves from commander lottery");
            }
        }

        public static bool AllTeamsHaveCommanderApplicants()
        {
            if (commanderApplicants == null)
            {
                return true;
            }

            for (int i = 0; i < SiConstants.MaxPlayableTeams; i++)
            {
                if (Team.Teams[i] == null)
                {
                    continue;
                }

                // does the team have at least 1 player?
                if (Team.Teams[i].GetNumPlayers() >= 1)
                {
                    if (commanderApplicants[i].Count == 0)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        #if NET6_0
        [HarmonyPatch(typeof(MP_Strategy), nameof(MP_Strategy.OnMissionStateChanged))]
        #else
        [HarmonyPatch(typeof(MP_Strategy), "OnMissionStateChanged")]
        #endif
        private static class ApplyPatch_Strategy_OnMissionStateChanged
        {
            public static void Postfix(MP_Strategy __instance, MP_Strategy.EMissionState __0)
            {
                try
                {
                    // run just after round starts
                    if (__0 == MP_Strategy.EMissionState.STARTED)
                    {
                        HoldCommanderLottery();
                    }
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run MP_Strategy::OnMissionStateChanged");
                }
            }
        }

        #if NET6_0
        [HarmonyPatch(typeof(MP_TowerDefense), nameof(MP_TowerDefense.OnMissionStateChanged))]
        #else
        [HarmonyPatch(typeof(MP_TowerDefense), "OnMissionStateChanged")]
        #endif
        private static class ApplyPatch_TowerDefense_OnMissionStateChanged
        {
            public static void Postfix(MP_TowerDefense __instance, MP_TowerDefense.EMissionState __0)
            {
                try
                {
                    // run just after round starts
                    if (__0 == MP_TowerDefense.EMissionState.STARTED)
                    {
                        HoldCommanderLottery();
                    }
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run MP_TowerDefense::OnMissionStateChanged");
                }
            }
        }

        public static void HoldCommanderLottery()
        {
            Mutineer.ClearMutineerList();

            if (CommanderApplications.commanderApplicants == null || CommanderApplications.previousCommanders == null || promotedCommanders == null)
            {
                return;
            }

            System.Random randomIndex = new System.Random();
            Player? RemovePlayer = null;

            for (int i = 0; i < SiConstants.MaxPlayableTeams; i++)
            {
                // clear previous commander tracking status, if any
                if (teamswapCommanderChecks != null)
                {
                    teamswapCommanderChecks[i] = null;
                }

                if (Team.Teams[i] == null)
                {
                    // account for transitions from 2 to 3 team rounds
                    commanderApplicants[i].Clear();
                    continue;
                }

                if (commanderApplicants[i].Count == 0)
                {
                    continue;
                }

                // remove previous commanders from applicant list, if there is a choice
                if (commanderApplicants[i].Count > 1)
                {
                    for (int j = 0; j < previousCommanders.Count; j++)
                    {
                        RemovePlayer = commanderApplicants[i].Find(k => k == previousCommanders[j].Commander);
                        if (RemovePlayer != null)
                        {
                            MelonLogger.Msg("Removing applicant from 2 rounds ago from random selection: " + RemovePlayer.PlayerName);
                            GameMode.CurrentGameMode.SpawnUnitForPlayer(RemovePlayer, RemovePlayer.Team);
                            commanderApplicants[i].Remove(RemovePlayer);
                        }
                    }

                    if (commanderApplicants[i].Count == 0)
                    {
                        continue;
                    }
                }

                int iCommanderIndex = randomIndex.Next(0, commanderApplicants[i].Count - 1);
                Player CommanderPlayer = commanderApplicants[i][iCommanderIndex];

                if (CommanderPlayer != null && CommanderPlayer.Team.Index == i)
                {
                    HelperMethods.ReplyToCommand("Promoted " + HelperMethods.GetTeamColor(CommanderPlayer) + CommanderPlayer.PlayerName + "</color> to commander for " + HelperMethods.GetTeamColor(CommanderPlayer) + CommanderPlayer.Team.TeamShortName + "</color>");
                    promotedCommanders[CommanderPlayer.Team.Index] = CommanderPlayer;
                    CommanderPrimitives.PromoteToCommander(CommanderPlayer);
                    PreviousCommander prevCommander = new PreviousCommander()
                    {
                        Commander = CommanderPlayer,
                        RoundsLeft = 2
                    };
                    previousCommanders.Add(prevCommander);
                    commanderApplicants[i].RemoveAt(iCommanderIndex);
                }
                else
                {
                    MelonLogger.Warning("Can't find lottery winner. Not promoting for team " + Team.Teams[i].TeamShortName);
                }

                // switch remaining players to infantry
                foreach (Player infantryPlayer in commanderApplicants[i])
                {
                    if (infantryPlayer == null)
                    {
                        continue;
                    }

                    MelonLogger.Msg("Player " + infantryPlayer.PlayerName + " lost commander lottery. Spawning as infantry.");
                    GameMode.CurrentGameMode.SpawnUnitForPlayer(infantryPlayer, infantryPlayer.Team);
                }

                // everyone is promoted or moved to infantry, clear for the next round
                commanderApplicants[i].Clear();
                MelonLogger.Msg("Clearing commander lottery for team " + Team.Teams[i].TeamShortName);
            }

            // we want to remove the oldest commanders from the list, if we have any
            if (previousCommanders == null || previousCommanders.Count == 0)
            {
                MelonLogger.Msg("Skipping clearing previous commanders. Nothing to do.");
                return;
            }

            List<PreviousCommander>? stalePreviousCommanders = new List<PreviousCommander>();
            foreach (PreviousCommander previousCommander in previousCommanders)
            {
                previousCommander.RoundsLeft -= 1;
                if (previousCommander.Commander == null)
                {
                    stalePreviousCommanders.Add(previousCommander);
                    MelonLogger.Msg("Adding stale, invalid commander.");
                    continue;
                }

                if (previousCommander.RoundsLeft <= 0)
                {
                    stalePreviousCommanders.Add(previousCommander);
                    MelonLogger.Msg("Adding stale commander with 0 rounds left: " + previousCommander.Commander.PlayerName);
                }
            }

            foreach (PreviousCommander stalePreviousCommander in stalePreviousCommanders)
            {
                previousCommanders.Remove(stalePreviousCommander);
                MelonLogger.Msg("Removed stale commander.");
            }
        }

        #if NET6_0
        [HarmonyPatch(typeof(MP_TowerDefense), nameof(MP_TowerDefense.RPC_TimerUpdate))]
        #else
        [HarmonyPatch(typeof(MP_TowerDefense), "RPC_TimerUpdate")]
        #endif
        private static class CommanderManager_Patch_TowerDefense_TimerUpdate
        {
            private static void Postfix(MP_TowerDefense __instance)
            {
                Process_RPC_TimerUpdate(__instance);
            }
        }

        #if NET6_0
        [HarmonyPatch(typeof(MP_Strategy), nameof(MP_Strategy.RPC_TimerUpdate))]
        #else
        [HarmonyPatch(typeof(MP_Strategy), "RPC_TimerUpdate")]
        #endif
        private static class CommanderManager_Patch_Strategy_TimerUpdate
        {
            private static void Postfix(MP_Strategy __instance)
            {
                Process_RPC_TimerUpdate(__instance);
            }
        }

        private static float GameModeTimer
        {
            get
            {
                if (GameMode.CurrentGameMode is GameModeExt gameModeExt)
                {
                    return gameModeExt.Timer;
                }
                throw new ArgumentException("Cannot access gamemode timer");
            }
            set
            {
                if (GameMode.CurrentGameMode is GameModeExt gameModeExt)
                {
                    #if NET6_0
                    gameModeExt.Timer = value;
                    #else
                    Type gameModeExtType = gameModeExt.GetType();
                    PropertyInfo timerProperty = gameModeExtType.GetProperty("Timer");
                    timerProperty.SetValue(gameModeExt, value);
                    #endif
                }
                throw new ArgumentException("Cannot set gamemode timer");
            }
        }

        private static void RemovePlayerFromRespawnTracker<T>(T gameModeInstance, Player player) where T : GameModeExt
        {
            if (gameModeInstance is MP_Strategy strategyInstance)
            {
                #if NET6_0
                if (strategyInstance.PlayerRespawnTracker.ContainsKey(player))
                {
                    strategyInstance.PlayerRespawnTracker.Remove(player);
                }
                #else
                FieldInfo playerRespawnTrackerField = typeof(MP_Strategy).GetField("PlayerRespawnTracker", BindingFlags.NonPublic | BindingFlags.Instance);
                Dictionary<Player, float> localPlayerRespawnTracker = (Dictionary<Player, float>)playerRespawnTrackerField.GetValue(strategyInstance);
                if (localPlayerRespawnTracker.ContainsKey(player))
                {
                    localPlayerRespawnTracker.Remove(player);
                    playerRespawnTrackerField.SetValue(strategyInstance, localPlayerRespawnTracker);
                }
                #endif
            }
            else if (gameModeInstance is MP_TowerDefense defenseInstance)
            {
                #if NET6_0
                if (defenseInstance.PlayerRespawnTracker.ContainsKey(player))
                {
                    defenseInstance.PlayerRespawnTracker.Remove(player);
                }
                #else
                FieldInfo playerRespawnTrackerField = typeof(MP_TowerDefense).GetField("PlayerRespawnTracker", BindingFlags.NonPublic | BindingFlags.Instance);
                Dictionary<Player, float> localPlayerRespawnTracker = (Dictionary<Player, float>)playerRespawnTrackerField.GetValue(defenseInstance);
                if (localPlayerRespawnTracker.ContainsKey(player))
                {
                    localPlayerRespawnTracker.Remove(player);
                    playerRespawnTrackerField.SetValue(defenseInstance, localPlayerRespawnTracker);
                }
                #endif
            }
        }

        private static void Process_RPC_TimerUpdate<T>(T gameModeInstance) where T : GameModeExt
        {
            if (gameModeInstance.GameOver)
            {
                return;
            }

            float timerValue = GameModeTimer;

            // the last second before the round is about to start
            if (timerValue <= 1f && timerValue > 0f)
            {
                // switch all applicants to freecam
                for (int i = 0; i < SiConstants.MaxPlayableTeams; i++)
                {
                    if (commanderApplicants[i].Count == 0)
                    {
                        continue;
                    }

                    foreach (Player applicantPlayer in commanderApplicants[i])
                    {
                        if (applicantPlayer == null)
                        {
                            continue;
                        }

                        // send to role NONE
                        CommanderPrimitives.SendToRole(applicantPlayer, GameModeExt.ETeamRole.NONE);
                        MelonLogger.Msg("Player " + applicantPlayer.PlayerName + " is being sent to role NONE.");

                        // force switching to role NONE
                        gameModeInstance.DestroyAllUnitsForPlayer(applicantPlayer);
                        RemovePlayerFromRespawnTracker(gameModeInstance, applicantPlayer);
                    }
                }
            }

            if (!CommanderManager._BlockRoundStartUntilEnoughApplicants.Value)
            {
                return;
            }

            if (AllTeamsHaveCommanderApplicants())
            {
                return;
            }

            if (timerValue <= 5f && timerValue > 4f)
            {
                HelperMethods.ReplyToCommand("Round cannot start because all teams don't have a commander. Chat !commander to apply.");
                GameModeTimer = 26f;
            }
        }
    }
}