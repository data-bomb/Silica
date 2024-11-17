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
using UnityEngine;
using MelonLoader;

namespace Si_CommanderManagement
{
    public class CommanderTransits
    {
        [HarmonyPatch(typeof(GameMode), nameof(GameMode.SpawnUnitForPlayer), new Type[] { typeof(Player), typeof(GameObject), typeof(Vector3), typeof(Quaternion) })]
        private static class CommanderManager_Patch_GameMode_SpawnUnitForPlayer
        {
            public static void Postfix(GameMode __instance, Unit __result, Player __0, UnityEngine.GameObject __1, UnityEngine.Vector3 __2, UnityEngine.Quaternion __3)
            {
                try
                {
                    if (CommanderApplications.teamswapCommanderChecks == null || __0 == null || __0.Team == null)
                    {
                        return;
                    }

                    if (GameMode.CurrentGameMode.Started && GameMode.CurrentGameMode.GameBegun)
                    {
                        // determine if player was a commander from any team
                        int commanderSwappedTeamIndex = -1;
                        for (int i = 0; i < SiConstants.MaxPlayableTeams; i++)
                        {
                            if (CommanderApplications.teamswapCommanderChecks[i] == __0)
                            {
                                commanderSwappedTeamIndex = i;
                                break;
                            }
                        }

                        // announce if player swapped from commander to infantry
                        if (commanderSwappedTeamIndex != -1)
                        {
                            Team departingTeam = Team.Teams[commanderSwappedTeamIndex];
                            CommanderApplications.teamswapCommanderChecks[commanderSwappedTeamIndex] = null;

                            // don't display if the team has been eliminated
                            if (departingTeam.NumMajorStructures <= 0)
                            {
                                return;
                            }

                            if (CommanderManager._TeamOnlyResponses.Value)
                            {
                                HelperMethods.SendChatMessageToTeam(__0.Team, HelperMethods.chatPrefix, HelperMethods.GetTeamColor(__0.Team), __0.PlayerName, "</color> left commander position vacant for " + HelperMethods.GetTeamColor(departingTeam) + departingTeam.TeamShortName + "</color> by switching to infantry");
                            }
                            else
                            {
                                HelperMethods.ReplyToCommand_Player(__0, "left commander position vacant for " + HelperMethods.GetTeamColor(departingTeam) + departingTeam.TeamShortName + "</color> by switching to infantry");
                            }
                            
                        }
                    }
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run GameMode::SpawnUnitForPlayer");
                }
            }
        }

        [HarmonyPatch(typeof(GameMode), nameof(GameMode.OnPlayerLeftBase))]
        private static class CommanderManager_Patch_GameMode_OnPlayerLeftBase
        {
            public static void Prefix(GameMode __instance, Player __0)
            {
                try
                {
                    if (CommanderApplications.commanderApplicants == null || __0 == null || __0.Team == null)
                    {
                        return;
                    }

                    if (GameMode.CurrentGameMode.Started && !GameMode.CurrentGameMode.GameBegun)
                    {
                        bool hasApplied = CommanderApplications.IsApplicant(__0);
                        
                        if (hasApplied)
                        {
                            CommanderApplications.commanderApplicants[__0.Team.Index].Remove(__0);
                            if (CommanderManager._TeamOnlyResponses.Value)
                            {
                                HelperMethods.SendChatMessageToTeam(__0.Team, HelperMethods.chatPrefix, HelperMethods.GetTeamColor(__0.Team), __0.PlayerName, "</color> was removed from consideration due to disconnect");
                            }
                            else
                            {
                                HelperMethods.ReplyToCommand_Player(__0, "was removed from consideration due to disconnect");
                            }
                        }
                    }

                    if (GameMode.CurrentGameMode.Started && GameMode.CurrentGameMode.GameBegun)
                    {
                        // don't display message if the team has since been eliminated
                        if (__0.IsCommander && __0.Team.NumMajorStructures > 0)
                        {
                            if (CommanderManager._TeamOnlyResponses.Value)
                            {
                                HelperMethods.SendChatMessageToTeam(__0.Team, HelperMethods.chatPrefix, HelperMethods.GetTeamColor(__0.Team), __0.PlayerName, "</color> left commander position vacant by disconnecting");
                            }
                            else
                            {
                                HelperMethods.ReplyToCommand_Player(__0, "left commander position vacant by disconnecting");
                            }
                        }
                    }
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run GameMode::OnPlayerLeftBase");
                }
            }
        }

        #if NET6_0
        [HarmonyPatch(typeof(MP_TowerDefense), nameof(MP_TowerDefense.SetCommander))]
        #else
        [HarmonyPatch(typeof(MP_TowerDefense), "SetCommander")]
        #endif
        private static class ApplyPatch_MPTowerDefense_SetCommander
        {
            public static bool Prefix(MP_TowerDefense __instance, Team __0, Player? __1)
            {
                try
                {
                    return SetCommanderPatch(__instance, __0, __1);
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run MP_TowerDefense::SetCommander");
                }

                return true;
            }
        }

        #if NET6_0
        [HarmonyPatch(typeof(MP_Strategy), nameof(MP_Strategy.SetCommander))]
        #else
        [HarmonyPatch(typeof(MP_Strategy), "SetCommander")]
        #endif
        private static class ApplyPatch_MPStrategy_SetCommander
        {
            public static bool Prefix(MP_Strategy __instance, Team __0, Player? __1)
            {
                try
                {
                    return SetCommanderPatch(__instance, __0, __1);
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run MP_Strategy::SetCommander");
                }

                return true;
            }
        }

        private static bool SetCommanderPatch<T>(T gameModeInstance, Team team, Player? player) where T : GameModeExt
        {
            MelonLogger.Msg("Reached SetCommander Patch for Team " + team.TeamShortName);

            if (gameModeInstance == null || team == null || CommanderBans.BanList == null || CommanderApplications.commanderApplicants == null || CommanderApplications.teamswapCommanderChecks == null || CommanderApplications.promotedCommanders == null)
            {
                return true;
            }

            if (player != null)
            {
                // when the game is in full swing
                if (GameMode.CurrentGameMode.Started && GameMode.CurrentGameMode.GameBegun)
                {
                    // determine if promoted commander was previously commanding another team
                    int commanderSwappedTeamIndex = -1;
                    for (int i = 0; i < SiConstants.MaxPlayableTeams; i++)
                    {
                        if (i == team.Index)
                        {
                            continue;
                        }

                        if (CommanderApplications.teamswapCommanderChecks[i] == player)
                        {
                            commanderSwappedTeamIndex = i;
                            break;
                        }
                    }

                    // announce a commander swapped to command another team
                    if (commanderSwappedTeamIndex != -1)
                    {
                        Team departingTeam = Team.Teams[commanderSwappedTeamIndex];
                        HelperMethods.ReplyToCommand_Player(player, "has left command of " + HelperMethods.GetTeamColor(departingTeam) + departingTeam.TeamShortName + "</color> and taken command of " + HelperMethods.GetTeamColor(team) + team.TeamShortName + "</color>");
                        CommanderApplications.teamswapCommanderChecks[commanderSwappedTeamIndex] = null;
                    }
                    else
                    {
                        // announce a new commander, if needed
                        if (CommanderApplications.teamswapCommanderChecks[team.Index] != player && CommanderApplications.promotedCommanders[team.Index] != player)
                        {
                            CommanderApplications.promotedCommanders[team.Index] = player;
                            if (CommanderManager._TeamOnlyResponses.Value)
                            {
                                HelperMethods.SendChatMessageToTeam(player.Team, HelperMethods.chatPrefix, HelperMethods.GetTeamColor(player.Team), player.PlayerName, "</color> has taken command of " + HelperMethods.GetTeamColor(team) + team.TeamShortName + "</color>");
                            }
                            else
                            {
                                HelperMethods.ReplyToCommand_Player(player, "has taken command of " + HelperMethods.GetTeamColor(team) + team.TeamShortName + "</color>");
                            }
                        }
                    }
                }
            }
            // player is null
            else
            {
                // check if there is a current commander
                Player? teamCommander = gameModeInstance.GetCommanderForTeam(team);
                if (teamCommander != null)
                {
                    CommanderApplications.teamswapCommanderChecks[team.Index] = teamCommander;
                }
            }

            return true;
        }
    }
}