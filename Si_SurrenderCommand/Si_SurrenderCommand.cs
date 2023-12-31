/*
 Silica Surrender Command Mod
 Copyright (C) 2023 by databomb
 
 * Description *
 For Silica listen servers, provides a command (!surrender) which
 each team's commander can use to have their team give up early.
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

[assembly: MelonInfo(typeof(SurrenderCommand), "Surrender Command", "1.2.1", "databomb", "https://github.com/data-bomb/Silica")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]
[assembly: MelonOptionalDependencies("Admin Mod")]

namespace Si_SurrenderCommand
{
    public class SurrenderCommand : MelonMod
    {
        public static bool IsCommander(Player thePlayer)
        {
            if (thePlayer == null)
            {
                return false;
            }

            Team theTeam = thePlayer.Team;
            if (theTeam == null)
            {
                return false;
            }

            MP_Strategy strategyInstance = GameObject.FindObjectOfType<MP_Strategy>();
            Player teamCommander = strategyInstance.GetCommanderForTeam(theTeam);

            if (teamCommander == thePlayer)
            {
                return true;
            }

            return false;
        }

        #if NET6_0
        [HarmonyPatch(typeof(Il2CppSilica.UI.Chat), nameof(Il2CppSilica.UI.Chat.MessageReceived))]
        #else
        [HarmonyPatch(typeof(Silica.UI.Chat), "MessageReceived")]
        #endif
        private static class ApplyChatReceiveSurrenderPatch
        {
            #if NET6_0
            public static void Postfix(Il2CppSilica.UI.Chat __instance, Player __0, string __1, bool __2)
            #else
            public static void Postfix(Silica.UI.Chat __instance, Player __0, string __1, bool __2)
            #endif
            {
                try
                {
                    // each faction has its own chat manager but by looking at alien and only global messages this catches commands only once
                    if (!__instance.ToString().Contains("alien") || __2 == true)
                    {
                        return;
                    }

                    bool isSurrenderCommand = String.Equals(__1, "!surrender", StringComparison.OrdinalIgnoreCase);
                    if (!isSurrenderCommand)
                    {
                        return;
                    }

                    // check if we are actually a commander
                    bool isCommander = IsCommander(__0);

                    if (!isCommander)
                    {
                        // notify player on invalid usage
                        HelperMethods.ReplyToCommand_Player(__0, ": only commanders can use !surrender");
                        return;
                    }

                    // is there a game currently started?
                    if (GameMode.CurrentGameMode.GameOngoing)
                    {
                        // destroy all structures on team that's surrendering
                        Team SurrenderTeam = __0.Team;
                        for (int i = 0; i < SurrenderTeam.Structures.Count; i++)
                        {
                            SurrenderTeam.Structures[i].DamageManager.SetHealth01(0.0f);
                        }

                        // notify all players
                        HelperMethods.ReplyToCommand_Player(__0, "used !surrender to end");
                    }
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run Chat::MessageReceived");
                }
            }
        }
    }
}