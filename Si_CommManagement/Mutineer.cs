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
    public class Mutineer
    {
        public static void InitializeMutineerList()
        {
            CommanderManager.mutineerPlayers = new List<Player>[SiConstants.MaxPlayableTeams + 1];
            for (int i = 0; i < SiConstants.MaxPlayableTeams; i++)
            {
                CommanderManager.mutineerPlayers[i] = new List<Player>();
            }
        }

        public static void ClearMutineerList()
        {
            for (int i = 0; i < SiConstants.MaxPlayableTeams; i++)
            {
                CommanderManager.mutineerPlayers[i].Clear();
            }
        }

        public static void Command_Mutiny(Player? callerPlayer, String args)
        {
            if (callerPlayer == null)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, " Console not supported.");
                return;
            }

            if (callerPlayer.Team == null)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, " On invalid team.");
                return;
            }

            if (!GameMode.CurrentGameMode || !GameMode.CurrentGameMode.GameOngoing)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, " Cannot mutiny at this time.");
                return;
            }

            if (callerPlayer.IsCommander)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, " Cannot mutiny when commader.");
                return;
            }

            // is there no commander at all?
            if (CommanderPrimitives.GetCommander(callerPlayer.Team) == null)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, " Cannot mutiny when no one is commander.");
                return;
            }

            // did the player already vote for a mutiny?
            if (CommanderManager.mutineerPlayers[callerPlayer.Team.Index].Contains(callerPlayer))
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, " Already voted to mutiny. ", MoreMutinyVotesNeeded(callerPlayer.Team).ToString(), " more players needed.");
                return;
            }

            CommanderManager.mutineerPlayers[callerPlayer.Team.Index].Add(callerPlayer);

            // if we haven't met the threshold then send a message to the teammates
            if (CommanderManager.mutineerPlayers[callerPlayer.Team.Index].Count < TeammatesNeededForMutiny(callerPlayer.Team))
            {
                HelperMethods.SendChatMessageToTeamNoCommander(callerPlayer.Team, HelperMethods.chatPrefix, HelperMethods.GetTeamColor(callerPlayer), " ", callerPlayer.PlayerName, "</color> wants to mutiny. ", MoreMutinyVotesNeeded(callerPlayer.Team).ToString(), " more players needed.");
                return;
            }

            Mutiny(callerPlayer.Team);
        }
        public static int TeammatesNeededForMutiny(Team team)
        {
            int playerCount = team.GetNumPlayers();

            // don't count the commander as a player here
            playerCount -= (CommanderPrimitives.GetCommander(team) != null ? 1 : 0);

            int teammatesNeeded = (int)Math.Ceiling(playerCount * CommanderManager._MutinyVotePercent.Value);
            if (teammatesNeeded < 1)
            {
                return 1;
            }

            return teammatesNeeded;
        }

        public static int MoreMutinyVotesNeeded(Team team)
        {
            int teammatesNeeded = TeammatesNeededForMutiny(team);
            int moreNeeded = teammatesNeeded - CommanderManager.mutineerPlayers[team.Index].Count;
            if (moreNeeded < 1)
            {
                return 1;
            }

            return moreNeeded;
        }

        public static void Mutiny(Team team)
        {
            // notify all players
            HelperMethods.ReplyToCommand(HelperMethods.GetTeamColor(team) + team.TeamShortName + "</color> had a mutiny against the current commander.");
            
            CommanderPrimitives.DemoteTeamsCommander(team);

            // clear all people who voted for a mutiny
            ClearMutineerList();
        }
    }
}