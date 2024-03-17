/*
Silica Admin Mod
Copyright (C) 2024 by databomb

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

using System;
using System.Collections.Generic;

namespace SilicaAdminMod
{
    public static class PlayerMethods
    {
        public static List<PlayerCommand> PlayerCommands = null!;
        public static List<PlayerCommand> PlayerPhrases = null!;

        public static PlayerCommand? FindPlayerCommandFromString(String commandText)
        {
            foreach (PlayerCommand command in PlayerCommands)
            {
                if (String.Equals(command.CommandName, commandText, StringComparison.OrdinalIgnoreCase))
                {
                    return command;
                }
            }

            return null;
        }

        public static PlayerCommand? FindPlayerPhraseFromString(String phraseText)
        {
            foreach (PlayerCommand phrase in PlayerPhrases)
            {
                if (String.Equals(phrase.CommandName, phraseText, StringComparison.OrdinalIgnoreCase))
                {
                    return phrase;
                }
            }

            return null;
        }

        public static void RegisterPlayerCommand(String playerCommand, HelperMethods.CommandCallback playerCallback, bool hideFromChat)
        {
            PlayerCommand thisCommand = new PlayerCommand
            {
                CommandName = playerCommand,
                PlayerCommandCallback = playerCallback,
                HideChatMessage = hideFromChat
            };

            PlayerCommands.Add(thisCommand);
        }

        public static void RegisterPlayerPhrase(String playerPhrase, HelperMethods.CommandCallback playerCallback, bool hideFromChat)
        {
            PlayerCommand thisCommand = new PlayerCommand
            {
                CommandName = playerPhrase,
                PlayerCommandCallback = playerCallback,
                HideChatMessage = hideFromChat
            };

            PlayerPhrases.Add(thisCommand);
        }

        public static bool UnregisterPlayerCommand(String playerCommand)
        {
            PlayerCommand? matchingCommand = PlayerMethods.FindPlayerCommandFromString(playerCommand);
            if (matchingCommand == null)
            {
                return false;
            }

            return PlayerCommands.Remove(matchingCommand);
        }

        public static bool UnregisterPlayerPhrase(String playerPhrase)
        {
            PlayerCommand? matchingCommand = PlayerMethods.FindPlayerPhraseFromString(playerPhrase);
            if (matchingCommand == null)
            {
                return false;
            }

            return PlayerPhrases.Remove(matchingCommand);
        }
    }
}