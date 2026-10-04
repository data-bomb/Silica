/*
Silica Admin Mod
Copyright (C) 2025 by databomb

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
using System;
using System.Linq;
using MelonLoader;
using UnityEngine;
using Newtonsoft.Json.Linq;
using System.Runtime.CompilerServices;
using System.Reflection;
using System.Data;
using System.Collections.Generic;
using System.Reflection.Emit;

#if NET6_0
using Il2Cpp;
using Il2CppSteamworks;
#else
using Steamworks;
#endif

namespace SilicaAdminMod
{
    public static class Event_Structures
    {
        public static event EventHandler<OnRequestSellStructureArgs> OnRequestSellStructure = delegate { };
        public static event EventHandler<OnCommanderSoldStructureArgs> OnCommanderSoldStructure = delegate { };

        [HarmonyPatch(typeof(StructureSellComponent), nameof(StructureSellComponent.GetCanSell))]
        static class ApplyPatch_StructureSellComponent_GetCanSell
        {
            public static void Postfix(StructureSellComponent __instance, ref bool __result, Structure __0)
            {
                try
                {
                    OnRequestSellStructureArgs onRequestSellStructureArgs = FireOnRequestSellStructureEvent(__0, __0.Team, __result);

                    if (onRequestSellStructureArgs.Override)
                    {
                        // overriding a sale
                        if (__result)
                        {
                            if (SiAdminMod.Pref_Admin_DebugLogMessages.Value)
                            {
                                MelonLogger.Msg("Blocking structure (" + __0.name + ") from being sold on team " + __0.Team.TeamShortName);
                            }

                            __result = false;
                            return;
                        }
                        // overriding a blocked sale
                        else
                        {
                            __result = true;
                        }
                    }

                    if (SiAdminMod.Pref_Admin_DebugLogMessages.Value)
                    {
                        MelonLogger.Msg("Allowing structure (" + __0.name + ") to be sold on team " + __0.Team.TeamShortName);
                    }

                    OnCommanderSoldStructureArgs onCommanderSoldStructureArgs = FireOnCommanderSoldStructure(__0, __0.Team);

                    if (SiAdminMod.Pref_Admin_DebugLogMessages.Value)
                    {
                        MelonLogger.Msg("Structure (" + __0.name + ") sold by commander on team " + __0.Team.TeamShortName);
                    }
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run StructureSellComponent::GetCanSell");
                }
            }
        }

        public static OnRequestSellStructureArgs FireOnRequestSellStructureEvent(Structure structure, Team team, bool gameDecision)
        {
            OnRequestSellStructureArgs onRequestSellStructureArgs = new OnRequestSellStructureArgs();
            onRequestSellStructureArgs.Structure = structure;
            onRequestSellStructureArgs.Team = team;
            onRequestSellStructureArgs.GameDecision = gameDecision;
            EventHandler<OnRequestSellStructureArgs> requestSellStructureEvent = OnRequestSellStructure;
            if (requestSellStructureEvent != null)
            {
                requestSellStructureEvent(null, onRequestSellStructureArgs);
            }

            return onRequestSellStructureArgs;
        }

        public static OnCommanderSoldStructureArgs FireOnCommanderSoldStructure(Structure structure, Team team)
        {
            OnCommanderSoldStructureArgs onCommanderSoldStructureArgs = new OnCommanderSoldStructureArgs();
            onCommanderSoldStructureArgs.Structure = structure;
            onCommanderSoldStructureArgs.Team = team;
            EventHandler<OnCommanderSoldStructureArgs> commanderDestroyedStructureEvent = OnCommanderSoldStructure;
            if (commanderDestroyedStructureEvent != null)
            {
                commanderDestroyedStructureEvent(null, onCommanderSoldStructureArgs);
            }

            return onCommanderSoldStructureArgs;
        }
    }
}
