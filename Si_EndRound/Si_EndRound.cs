/*
Silica End Round
Copyright (C) 2023-2024 by databomb

* Description *
Provides an admin command to end the round early (e.g., if the 
commander is not !surrender'ing as needed or server performance 
is too low)

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
using Si_EndRound;
using SilicaAdminMod;
using System;
using System.Linq;
using UnityEngine;

[assembly: MelonInfo(typeof(EndRound), "End Round", "1.0.2", "databomb", "https://github.com/data-bomb/Silica")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]
[assembly: MelonOptionalDependencies("Admin Mod")]

namespace Si_EndRound
{
    public class EndRound : MelonMod
    {
        public override void OnLateInitializeMelon()
        {
            HelperMethods.CommandCallback roundEndCallback = Command_EndRound;
            HelperMethods.RegisterAdminCommand("endround", roundEndCallback, Power.End);
            HelperMethods.RegisterAdminCommand("endgame", roundEndCallback, Power.End);
        }

        public static void Command_EndRound(Player callerPlayer, String args)
        {
            // validate argument count
            int argumentCount = args.Split(' ').Count() - 1;
            if (argumentCount > 0)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Too many arguments");
                return;
            }

            if (!GameMode.CurrentGameMode.GameOngoing)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Cannot end round until round starts");
                return;
            }

            Force_EndRound();
            HelperMethods.AlertAdminAction(callerPlayer, "ended the round");
        }

        public static void Force_EndRound()
        {
            // destroy all structures on all teams
            for (int i = 0; i < SiConstants.MaxPlayableTeams; i++)
            {
                for (int j = 0; j < Team.Teams[i].Structures.Count; j++)
                {
                    Team.Teams[i].Structures[j].DamageManager.SetHealth01(0.0f);
                }
            }
        }
    }
}