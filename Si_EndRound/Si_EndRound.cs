/*
Silica End Round
Copyright (C) 2023 by databomb

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

using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using Si_EndRound;
using UnityEngine;
using AdminExtension;
using System.Reflection.Metadata.Ecma335;

[assembly: MelonInfo(typeof(EndRound), "End Round", "1.0.0", "databomb")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]

namespace Si_EndRound
{
    public class EndRound : MelonMod
    {
        static bool AdminModAvailable = false;

        public override void OnLateInitializeMelon()
        {
            AdminModAvailable = RegisteredMelons.Any(m => m.Info.Name == "Admin Mod");

            if (AdminModAvailable)
            {
                HelperMethods.CommandCallback roundEndCallback = Command_EndRound;
                HelperMethods.RegisterAdminCommand("!endround", roundEndCallback, Power.End);
                HelperMethods.RegisterAdminCommand("!endgame", roundEndCallback, Power.End);
            }
            else
            {
                MelonLogger.Warning("Dependency missing: Admin Mod");
            }
        }

        public void Command_EndRound(Il2Cpp.Player callerPlayer, String args)
        {
            // validate argument count
            int argumentCount = args.Split(' ').Count() - 1;
            if (argumentCount > 0)
            {
                HelperMethods.ReplyToCommand(args.Split(' ')[0] + ": Too many arguments");
                return;
            }

            if (!Il2Cpp.GameMode.CurrentGameMode.GameOngoing)
            {
                HelperMethods.ReplyToCommand(args.Split(' ')[0] + ": Cannot end round until round starts");
                return;
            }

            Force_EndRound();
            HelperMethods.AlertAdminAction(callerPlayer, "ended the round");
        }

        public void Force_EndRound()
        {
            // destroy all structures on all teams
            for (int i = 0; i < Il2Cpp.Team.Teams.Count; i++)
            {
                for (int j = 0; j < Il2Cpp.Team.Teams[i].Structures.Count; j++)
                {
                    Il2Cpp.Team.Teams[i].Structures[j].DamageManager.SetHealth01(0.0f);
                }
            }
        }
    }
}