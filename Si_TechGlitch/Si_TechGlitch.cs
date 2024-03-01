/*
Silica Tech Glitch Command Mod
Copyright (C) 2023-2024 by databomb

* Description *
For Silica listen servers, provides a command (!techglitch) which
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

[assembly: MelonInfo(typeof(TechGlitch), "Tech Glitch Command", "1.0.2", "databomb", "https://github.com/data-bomb/Silica")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]
[assembly: MelonOptionalDependencies("Admin Mod")]

namespace Si_TechGlitch
{
    public class TechGlitch : MelonMod
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
        private static class TechGlitch_Chat_MessageReceived
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
                    if (!__instance.ToString().Contains("alien") || __2)
                    {
                        return;
                    }
                    
                    bool isTechGlitchCommand = String.Equals(__1, "!techglitch", StringComparison.OrdinalIgnoreCase);
                    if (!isTechGlitchCommand)
                    {
                        return;
                    }

                    // check if we are actually a commander
                    bool isCommander = IsCommander(__0);

                    if (!isCommander)
                    {
                        // notify player on invalid usage
                        HelperMethods.ReplyToCommand_Player(__0, ": only commanders can use !techglitch");
                        return;
                    }

                    // is there a game currently started?
                    if (!GameMode.CurrentGameMode.GameOngoing)
                    {
                        return;
                    }

                    HelperMethods.ReplyToCommand(__0.Team.TeamShortName + " is at tech level " + __0.Team.CurrentTechnologyTier.ToString());

                    // look for ResearchFacility and QuantumCortex
                    String techStructureName = __0.Team.TeamName.Contains("Human") ? "ResearchF" : "QuantumC";
                    int techStructures = 0;

                    for (int i = 0; i < __0.Team.Structures.Count; i++)
                    {
                        if (__0.Team.Structures[i].ToString().StartsWith(techStructureName))
                        {
                            techStructures++;

                            #if NET6_0
                            __0.Team.Structures[i].RPC_SynchTechnologyTier();
                            #else
                            Type structureType = typeof(Structure);
                            MethodInfo synchTechMethod = structureType.GetMethod("RPC_SynchTechnologyTier");

                            synchTechMethod.Invoke(structureType, null);
                            #endif
                        }
                    }

                    // notify all players
                    HelperMethods.ReplyToCommand_Player(__0, "forced sync on " + techStructures.ToString() + " tech structure" + (techStructures > 1 ? "s" : ""));

                    __0.Team.UpdateTechnologyTier();
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run Chat::MessageReceived");
                }
            }
        }

    }
}