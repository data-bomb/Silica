﻿/*
 Silica Surrender Command Mod
 Copyright (C) 2023-2024 by databomb
 
 * Description *
 For Silica servers, provides a command (!surrender) which each
 each team's commander and/or players can use to have their team 
 give up early.

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
using Si_SurrenderCommand;
using SilicaAdminMod;
using System;
using UnityEngine;
using System.Collections.Generic;

[assembly: MelonInfo(typeof(SurrenderCommand), "Surrender Command", "1.6.2", "databomb", "https://github.com/data-bomb/Silica")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]
[assembly: MelonOptionalDependencies("Admin Mod")]

namespace Si_SurrenderCommand
{
    public class SurrenderCommand : MelonMod
    {
        static List<Player>[] votesToSurrender = null!;

        static MelonPreferences_Category _modCategory = null!;
        static MelonPreferences_Entry<bool> Pref_Surrender_CommanderImmediate = null!;
        static MelonPreferences_Entry<bool> Pref_Surrender_ShowNames = null!;
        static MelonPreferences_Entry<float> Pref_Surrender_Vote_Percent = null!;

        public override void OnInitializeMelon()
        {
            _modCategory ??= MelonPreferences.CreateCategory("Silica");
            Pref_Surrender_CommanderImmediate ??= _modCategory.CreateEntry<bool>("Surrender_CommanderImmediate", false);
            Pref_Surrender_ShowNames ??= _modCategory.CreateEntry<bool>("Surrender_ShowPlayerNames", true);
            Pref_Surrender_Vote_Percent ??= _modCategory.CreateEntry<float>("Surrender_Vote_PercentNeeded", 0.35f);

            votesToSurrender = new List<Player>[SiConstants.MaxPlayableTeams + 1];
            for (int i = 0; i < SiConstants.MaxPlayableTeams; i++)
            {
                votesToSurrender[i] = new List<Player>();
            }
        }

        public static bool IsCommander(Player player)
        {
            if (player == null)
            {
                return false;
            }

            Team team = player.Team;
            if (team == null)
            {
                return false;
            }

            return GameMode.CurrentGameMode.GetPlayerIsCommander(player);
        }

        public override void OnLateInitializeMelon()
        {
            HelperMethods.CommandCallback surrenderCallback = Command_Surrender;
            HelperMethods.RegisterPlayerCommand("surrender", surrenderCallback, true);
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            ClearSurrenderVotes();
        }

        public static void Command_Surrender(Player? callerPlayer, String args)
        {
			if (callerPlayer == null)
			{
				HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, " Console not supported.");
				return;
			}

            // check if they're on a valid team
            Team? team = callerPlayer.Team;
            if (team == null || team.Index == (int)SiConstants.ETeam.Wildlife)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, " This team does not support surrender.");
                return;
            }

            // check if game on-going
            if (!GameMode.CurrentGameMode.GameOngoing)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, " Can't surrender. Game not started.");
                return;
            }

            // check if we are actually a commander
            bool isCommander = IsCommander(callerPlayer);

            // if they're a commander then immediately take action if the preference is set
            if (isCommander && Pref_Surrender_CommanderImmediate.Value)
            {
                Surrender(team, callerPlayer);
                return;
            }

            // did the player already vote for surrender?
            if (votesToSurrender[team.Index].Contains(callerPlayer))
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, " Already voted for surrender. ", MoreSurrenderVotesNeeded(team).ToString(), " more players " + (Pref_Surrender_CommanderImmediate.Value ? "or 1 commander" : "") + " needed.");
                return;
            }

            votesToSurrender[team.Index].Add(callerPlayer);

            // if we haven't met the threshold then send a message to the teammates
            if (votesToSurrender[team.Index].Count < TeammatesNeededForSurrender(team))
            {
                HelperMethods.SendChatMessageToTeam(team, HelperMethods.chatPrefix, HelperMethods.GetTeamColor(team), " ", (Pref_Surrender_ShowNames.Value ? callerPlayer.PlayerName : "A teammate"), "</color> votes to surrender. ", MoreSurrenderVotesNeeded(team).ToString(), " more players " + (Pref_Surrender_CommanderImmediate.Value ? "or 1 commander" : "") + " needed.");
                if (Pref_Surrender_ShowNames.Value)
                {
                    HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, " Your surrender vote was recorded.");
                }

                return;
            }

            Surrender(team, callerPlayer);
        }

        public static int TeammatesNeededForSurrender(Team team)
        {
            int playerCount = team.GetNumPlayers();

            // don't count the commander as a player if set to immediate commander surrender
            if (GameMode.CurrentGameMode && Pref_Surrender_CommanderImmediate.Value)
            {
                if (GameMode.CurrentGameMode is MP_Strategy strategyInstance)
                {
                    playerCount -= (strategyInstance.GetCommanderForTeam(team) != null ? 1 : 0);
                }
                else if (GameMode.CurrentGameMode is MP_TowerDefense defenseInstance)
                {
                    playerCount -= (defenseInstance.GetCommanderForTeam(team) != null ? 1 : 0);
                }
            }

            int teammatesNeeded = (int)Math.Ceiling(playerCount * Pref_Surrender_Vote_Percent.Value);
            if (teammatesNeeded < 1)
            {
                return 1;
            }

            return teammatesNeeded;
        }

        public static int MoreSurrenderVotesNeeded(Team team)
        {
            int teammatesNeeded = TeammatesNeededForSurrender(team);
            int moreNeeded = teammatesNeeded - votesToSurrender[team.Index].Count;
            if (moreNeeded < 1)
            {
                return 1;
            }

            return moreNeeded;
        }

        public static void ClearSurrenderVotes()
        {
            for (int i = 0; i < SiConstants.MaxPlayableTeams; i++)
            {
                votesToSurrender[i].Clear();
            }
        }

        public static void Surrender(Team team, Player player)
        {
            // notify all players
            if (Pref_Surrender_ShowNames.Value)
            {
                HelperMethods.ReplyToCommand_Player(player, "used !surrender to end");
            }
            else
            {
                HelperMethods.SendChatMessageToAll(HelperMethods.chatPrefix, HelperMethods.GetTeamColor(team), team.TeamShortName, "</color> surrendered.");
            }

            if (GameMode.CurrentGameMode is MP_Strategy)
            {
                // find all construction sites we should destroy from the team that's surrendering
                RemoveConstructionSites(team, true);

                // destroy only critical structures on team that's surrendering
                RemoveStructures(team, true);

                // and destroy only critical units (e.g., the queen)
                RemoveUnits(team, true);
            }
            else if (GameMode.CurrentGameMode is MP_TowerDefense defenseInstance)
            {
                // change game state to EMissionState.ENDED
                defenseInstance.EndRound();
            }

            // clear all people who voted for a surrender
            ClearSurrenderVotes();
        }

        [HarmonyPatch(typeof(MusicJukeboxHandler), nameof(MusicJukeboxHandler.OnGameStarted))]
        private static class ApplyPatch_MusicJukeboxHandler_OnGameStarted
        {
            public static void Postfix(MusicJukeboxHandler __instance, GameMode __0)
            {
                try
                {
                    ClearSurrenderVotes();
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run MusicJukeboxHandler::OnGameStarted");
                }
            }
        }

        public static void RemoveConstructionSites(Team team, bool criticalOnly)
        {
            List<ConstructionSite> sitesToDestroy = new List<ConstructionSite>();

            foreach (ConstructionSite constructionSite in ConstructionSite.ConstructionSites)
            {
                if (constructionSite == null || constructionSite.Team == null)
                {
                    continue;
                }

                if (constructionSite.Team != team)
                {
                    continue;
                }

                if (criticalOnly && constructionSite.ObjectInfo && !constructionSite.ObjectInfo.Critical)
                {
                    continue;
                }

                MelonLogger.Msg("Found critical construction site: " + constructionSite.ToString());

                sitesToDestroy.Add(constructionSite);
            }

            foreach (ConstructionSite constructionSite in sitesToDestroy)
            {
                constructionSite.Deinit(false);
            }
        }

        public static void RemoveStructures(Team team, bool criticalOnly)
        {
            for (int i = 0; i < team.Structures.Count; i++)
            {
                if (team.Structures[i] == null)
                {
                    MelonLogger.Warning("Found null structure during surrender command.");
                    continue;
                }

                if (team.Structures[i].IsDestroyed)
                {
                    continue;
                }

                if (criticalOnly && team.Structures[i].ObjectInfo && !team.Structures[i].ObjectInfo.Critical)
                {
                    continue;
                }

                MelonLogger.Msg("Found critical structure: " + team.Structures[i].ToString());
                team.Structures[i].DamageManager.SetHealth01(0.0f);
            }
        }

        public static void RemoveUnits(Team team, bool criticalOnly)
        {
            for (int i = 0; i < team.Units.Count; i++)
            {
                if (team.Units[i] == null)
                {
                    MelonLogger.Warning("Found null unit during surrender command.");
                    continue;
                }

                if (team.Units[i].IsDestroyed)
                {
                    continue;
                }

                if (criticalOnly && team.Units[i].ObjectInfo && !team.Units[i].ObjectInfo.Critical)
                {
                    continue;
                }

                MelonLogger.Msg("Found critical unit: " + team.Units[i].ToString());

                team.Units[i].DamageManager.SetHealth01(0.0f);
            }
        }
    }
}