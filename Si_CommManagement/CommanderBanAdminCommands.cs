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

using SilicaAdminMod;
using System;
using UnityEngine;

namespace Si_CommanderManagement
{
    public class CommanderAdminCommands
    {
        public static void Command_CommanderBan(Player? callerPlayer, String args)
        {
            if (CommanderBans.BanList == null)
            {
                return;
            }

            string commandName = args.Split(' ')[0];

            // count number of arguments
            int argumentCount = args.Split(' ').Length - 1;
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

            String sTarget = args.Split(' ')[1];
            Player? playerToCmdrBan = HelperMethods.FindTargetPlayer(sTarget);

            if (playerToCmdrBan == null)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Ambiguous or invalid target");
                return;
            }

            if (callerPlayer != null && !callerPlayer.CanAdminTarget(playerToCmdrBan))
            {
                HelperMethods.ReplyToCommand_Player(playerToCmdrBan, "is immune due to level");
                return;
            }

            CommanderBans.AddBan(playerToCmdrBan);
            HelperMethods.AlertAdminAction(callerPlayer, "restricted " + HelperMethods.GetTeamColor(playerToCmdrBan) + playerToCmdrBan.PlayerName + "</color> to play as infantry only");
        }

        public static void Command_CommanderDemote(Player? callerPlayer, String args)
        {
            string commandName = args.Split(' ')[0];

            // count number of arguments
            int argumentCount = args.Split(' ').Length - 1;
            if (argumentCount > 1)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Too many arguments");
                return;
            }

            int targetTeamIndex = -1;
            MP_Strategy strategyInstance = GameObject.FindObjectOfType<MP_Strategy>();

            // if no team was specified then try and use current team of the admin
            if (argumentCount == 0)
            {
                if (callerPlayer == null || callerPlayer.Team == null)
                {
                    HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Too few arguments");
                    return;
                }

                targetTeamIndex = callerPlayer.Team.Index;
            }
            // argument is present and targets team where current commander needs to get demoted
            else
            {
                String targetTeamText = args.Split(' ')[1];

                if (String.Equals(targetTeamText, "Human", StringComparison.OrdinalIgnoreCase))
                {
                    // check gamemode - if Humans vs Aliens or the other ones
                    if (strategyInstance.TeamsVersus == MP_Strategy.ETeamsVersus.HUMANS_VS_ALIENS)
                    {
                        // if it's human vs aliens then human translates to the Human (Sol) team index
                        targetTeamIndex = 2;
                    }
                    // otherwise, it's ambigious and we can't make a decision
                }
                else if (String.Equals(targetTeamText, "Alien", StringComparison.OrdinalIgnoreCase))
                {
                    targetTeamIndex = 0;
                }
                else if (targetTeamText.Contains("Cent", StringComparison.OrdinalIgnoreCase))
                {
                    targetTeamIndex = 1;
                }
                else if (String.Equals(targetTeamText, "Sol", StringComparison.OrdinalIgnoreCase))
                {
                    targetTeamIndex = 2;
                }

                // check if we still don't have a valid target
                if (targetTeamIndex < 0)
                {
                    HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Valid targets are Alien, Centauri, or Sol");
                    return;
                }
            }

            Team targetTeam = Team.GetTeamByIndex(targetTeamIndex);
            if (targetTeam == null)
            {
                HelperMethods.ReplyToCommand(args.Split(' ')[0] + ": No valid team found");
                return;
            }

            // check if they have a commander to demote
            Player? targetPlayer = strategyInstance.GetCommanderForTeam(targetTeam);

            // team has a commander if targetPlayer isn't null
            if (targetPlayer == null)
            {
                HelperMethods.ReplyToCommand(args.Split(' ')[0] + ": No commander found on specified team");
                return;
            }

            if (callerPlayer != null && !callerPlayer.CanAdminTarget(targetPlayer))
            {
                HelperMethods.ReplyToCommand_Player(targetPlayer, "is immune due to level");
                return;
            }

            CommanderPrimitives.DemoteTeamsCommander(strategyInstance, targetTeam);
            HelperMethods.AlertAdminActivity(callerPlayer, targetPlayer, "demoted");
        }

        public static void Command_CommanderUnban(Player? callerPlayer, String args)
        {
            if (CommanderBans.BanList == null)
            {
                return;
            }

            string commandName = args.Split(' ')[0];

            // count number of arguments
            int argumentCount = args.Split(' ').Length - 1;
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

            String sTarget = args.Split(' ')[1];
            Player? playerToUnCmdrBan = HelperMethods.FindTargetPlayer(sTarget);

            if (playerToUnCmdrBan == null)
            {
                HelperMethods.ReplyToCommand(args.Split(' ')[0] + ": Ambiguous or invalid target");
                return;
            }


            if (callerPlayer != null && !callerPlayer.CanAdminTarget(playerToUnCmdrBan))
            {
                HelperMethods.ReplyToCommand_Player(playerToUnCmdrBan, "is immune due to level");
                return;
            }

            bool removed = CommanderBans.RemoveBan(playerToUnCmdrBan);
            if (removed)
            {
                HelperMethods.AlertAdminAction(callerPlayer, "permitted " + HelperMethods.GetTeamColor(playerToUnCmdrBan) + playerToUnCmdrBan.PlayerName + "</color> to play as commander");
            }
            else
            {
                HelperMethods.ReplyToCommand_Player(playerToUnCmdrBan, "not commander banned");
            }
        }
    }
}