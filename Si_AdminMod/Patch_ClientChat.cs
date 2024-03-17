/*
Silica Admin Mod
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

using HarmonyLib;
using MelonLoader;
using System;

namespace SilicaAdminMod
{
    public static class ClientChatHandler
    {
        public static void OnRequestPlayerChat(object? sender, OnRequestPlayerChatArgs args)
        {
            try
            { 
                if (args.Player == null)
                {
                    return;
                }

                // check if this even starts with a character that indicates it's a command
                if (!IsValidCommandPrefix(args.Text[0]))
                {
                    PlayerCommand? playerPhrase = GetPlayerPhrase(args.Text);
                    if (playerPhrase == null)
                    {
                        return;
                    }

                    // run the callback
                    playerPhrase.PlayerCommandCallback(args.Player, args.Text);

                    // let the command registrant decide whether the chat goes through
                    args.Block = playerPhrase.HideChatMessage;

                    return;
                }

                MelonLogger.Msg("Processing admin or player command.");

                // ignore team chat if preference is set
                if (args.TeamOnly && SiAdminMod.Pref_Admin_AcceptTeamChatCommands != null && !SiAdminMod.Pref_Admin_AcceptTeamChatCommands.Value)
                {
                    return;
                }

                // check if the first portion matches an admin command
                AdminCommand? adminCommand = GetAdminCommand(args.Text);
                if (adminCommand != null)
                {
                    MelonLogger.Msg("Processing admin command: " + adminCommand.AdminCommandText);

                    // are they an admin?
                    if (!args.Player.IsAdmin())
                    {
                        HelperMethods.ReplyToCommand_Player(args.Player, "is not an admin");
                        return;
                    }

                    // do they have the matching power?
                    Power callerPowers = args.Player.GetAdminPowers();

                    if (!AdminMethods.PowerInPowers(adminCommand.AdminPower, callerPowers))
                    {
                        HelperMethods.ReplyToCommand_Player(args.Player, "unauthorized command");
                        return;
                    }

                    // run the callback
                    adminCommand.AdminCallback(args.Player, args.Text);

                    // block if it's a '/' but not a '!'
                    args.Block = (args.Text[0] == '/');
                    return;
                }

                // check if the first portion matches a player command
                PlayerCommand? playerCommand = GetPlayerCommand(args.Text);
                if (playerCommand == null)
                {
                    return;
                }

                MelonLogger.Msg("Processing player command: " + playerCommand.CommandName);

                // run the callback
                playerCommand.PlayerCommandCallback(args.Player, args.Text);

                // let the command registrant decide whether the chat goes through
                args.Block = playerCommand.HideChatMessage;
            }
            catch (Exception error)
            {
                HelperMethods.PrintError(error, "Failed to run AdminMod::OnRequestPlayerChat");
            }
        }

        public static bool IsValidCommandPrefix(char commandFirstCharacter)
        {
            if (commandFirstCharacter == '!' || commandFirstCharacter == '/' || commandFirstCharacter == '.')
            {
                return true;
            }

            return false;
        }

        public static AdminCommand? GetAdminCommand(string commandString)
        {
            String thisCommandText = commandString.Split(' ')[0];
            // trim first character
            thisCommandText = thisCommandText[1..];
            return AdminMethods.FindAdminCommandFromString(thisCommandText);
        }

        public static PlayerCommand? GetPlayerCommand(string commandString)
        {
            String thisCommandText = commandString.Split(' ')[0];
            // trim first character
            thisCommandText = thisCommandText[1..];
            return PlayerMethods.FindPlayerCommandFromString(thisCommandText);
        }
        public static PlayerCommand? GetPlayerPhrase(string phraseString)
        {
            String thisPhrase = phraseString.Split(' ')[0];
            return PlayerMethods.FindPlayerPhraseFromString(thisPhrase);
        }

    }
}