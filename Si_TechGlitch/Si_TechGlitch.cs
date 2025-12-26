/*
Silica Tech Glitch Command Mod
Copyright (C) 2023-2025 by databomb

* Description *
For Silica servers, provides a command (!techglitch) which
allows each team's commander to use if there is a synchronization
issue between the server and commander's tech status.

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
#else
using System.Reflection;
#endif

using HarmonyLib;
using MelonLoader;
using Si_TechGlitch;
using SilicaAdminMod;
using System;
using UnityEngine;

[assembly: MelonInfo(typeof(TechGlitch), "Tech Glitch Command", "1.1.0", "databomb", "https://github.com/data-bomb/Silica")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]
[assembly: MelonOptionalDependencies("Admin Mod")]

namespace Si_TechGlitch
{
    public class TechGlitch : MelonMod
    {
        public override void OnLateInitializeMelon()
        {
            HelperMethods.CommandCallback techGlitchCallback = Command_TechGlitch;
            HelperMethods.RegisterPlayerCommand("techglitch", techGlitchCallback, false);
        }

        public static void Command_TechGlitch(Player? callerPlayer, String args)
        {
            if (callerPlayer == null || callerPlayer.Team == null)
            {
                return;
            }

            GameModeExt? gameModeExt = GameMode.CurrentGameMode as GameModeExt;
            if (gameModeExt == null)
            {
                MelonLogger.Warning("Could not find GameModeExt instance from CurrentGameMode.");
                return;
            }

            if (gameModeExt.GameOngoing)
            {
                HelperMethods.ReplyToCommand_Player(callerPlayer, ": game must be active to use !techglitch");
                return;
            }

            if (gameModeExt.GetCommanderForTeam(callerPlayer.Team) != callerPlayer)
            {
                HelperMethods.ReplyToCommand_Player(callerPlayer, ": only the commander can use !techglitch");
                return;
            }

            HelperMethods.SendChatMessageToTeam(callerPlayer.Team, "Server has you at Tech Tier " + callerPlayer.Team.TechnologyTier + "/" + callerPlayer.Team.TechnologyTierLimitMax + " with a watermark tech tier of " + callerPlayer.Team.TechnologyTierHighestReached);

            callerPlayer.Team.UpdateTechnologyTier(false, true);

            HelperMethods.ReplyToCommand_Player(callerPlayer, "forced tech sync.");
        }
    }
}