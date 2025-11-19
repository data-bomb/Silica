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
using System.Reflection;
using System.Reflection.Emit;
using System.Data;
using System.Collections.Generic;
using static MelonLoader.MelonLogger;


#if NET6_0
using Il2Cpp;
using Il2CppSilica;
using Il2CppSteamworks;
#else
using Silica;
using Steamworks;
#endif

namespace SilicaAdminMod
{
    public static class Event_Construction
    {
        public static event EventHandler<OnRequestBuildStructureArgs> OnRequestBuildStructure = delegate { };

        [HarmonyPatch(typeof(Structure), nameof(Structure.Construct))]
        static class ApplyPatch_Structure_Construct
        {
            static bool Detour_BeforeRequestConstructionSite(ConstructionData constructionData, Structure parentStructure, Vector3 worldPosition, Quaternion rotation, bool isClientRequest)
            {
                if (SiAdminMod.Pref_Admin_DebugLogMessages.Value)
                {
                    MelonLogger.Msg("Structure::Construct detour hit");
                }

                OnRequestBuildStructureArgs onRequestBuildStructureArgs = FireOnRequestBuildStructureEvent(constructionData, parentStructure, worldPosition, rotation, isClientRequest);

                if (onRequestBuildStructureArgs.Block)
                {
                    if (SiAdminMod.Pref_Admin_DebugLogMessages.Value)
                    {
                        MelonLogger.Msg("Blocking construction of structure (" + constructionData.name + ") on Team " + parentStructure.Team.TeamShortName);
                    }

                    return true;
                }

                return false;
            }

            static int FindEntryPoint_Structure_Construct(List<CodeInstruction> opCodes)
            {
                // find the last instance of calling RequestConstructionSite method
                var methodRequestConstructionSite = AccessTools.Method(typeof(ConstructionData), "RequestConstructionSite");
                if (methodRequestConstructionSite == null)
                {
                    MelonLogger.Warning("Transpiler failure of Structure::Construct. Cannot locate RequestConstructionSite method. Structure events will not function correctly.");
                    return -1;
                }

                int lastConstructionSiteCall = -1;
                for (int i = 0; i < opCodes.Count; i++)
                {
                    if (opCodes[i].Calls(methodRequestConstructionSite))
                    {
                        lastConstructionSiteCall = i;
                    }
                }
                
                MelonLogger.Msg("(Structure::Construct Transpiler) Found last call of ReqConSite at opCode[" + lastConstructionSiteCall + "]");
                
                // if we couldn't find any calls then the game was updated in an unexpected way
                if (lastConstructionSiteCall < 0)
                {
                    MelonLogger.Warning("Transpiler failure of Structure::Construct. Cannot locate RequestConstructionSite call. Structure events will not function correctly.");
                    return -1;
                }
                
                // scan upwards to the beginning of the call stack for the RequestConstructionSite method
                int insertionPoint = lastConstructionSiteCall;
                for (int i = lastConstructionSiteCall; i >= 0; i--)
                {
                    if (opCodes[i].IsLdarg(1)) // first argument (ConstructionData constructionData)
                    {
                        insertionPoint = i;
                        break;
                    }
                }
                
                // if we're at 0 then the game was updated in an unexpected way
                if (insertionPoint == 0)
                {
                    MelonLogger.Warning("Transpiler failure of Structure::Construct. Cannot locate ldarg1. Structure events will not function correctly.");
                    return -1;
                }

                MelonLogger.Msg("(Structure::Construct Transpiler) Found insertion point at opCode[" + insertionPoint + "]");

                return insertionPoint;
            }

            static List<CodeInstruction> GenerateILPatch_Structure_Construct(ILGenerator generator)
            {
                Label skipExit = generator.DefineLabel();
                var detourStructureConstruct = AccessTools.Method(typeof(ApplyPatch_Structure_Construct), nameof(ApplyPatch_Structure_Construct.Detour_BeforeRequestConstructionSite));

                return new List<CodeInstruction>()
                {
                    new CodeInstruction(OpCodes.Ldarg_1),           // ConstructionData constructionData
                    new CodeInstruction(OpCodes.Ldarg_0),           // Structure parentStructure
                    new CodeInstruction(OpCodes.Ldarg_2),           // Vector3 worldPosition
                    new CodeInstruction(OpCodes.Ldarg_3),           // Quarternion rotation
                    new CodeInstruction(OpCodes.Ldarg_S, (byte)4),  // bool isClientRequest
                    new CodeInstruction(OpCodes.Call, detourStructureConstruct), // call detour method
                    new CodeInstruction(OpCodes.Brfalse_S, skipExit), // exit on true
                    new CodeInstruction(OpCodes.Ldc_I4_7), // ProductionActionResult.UnspecifiedError
                    new CodeInstruction(OpCodes.Ret), // return from Structure::Construct
                    new CodeInstruction(OpCodes.Nop, null) { labels = new List<Label>() { skipExit } }
                }; // else.. continue to call ConstructionData::RequestConstructionSite
            }

            // an alternative approach would be needed for il2cpp
            #if !NET6_0
            [HarmonyTranspiler]
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                var opCodes = instructions.ToList();
                int insertionPoint = FindEntryPoint_Structure_Construct(opCodes);

                // don't make any modifications without confidence in insertionPoint
                if (insertionPoint < 0)
                {
                    return instructions;
                }

                // generate the IL we need to insert before the last RequestConstructionSite call
                var inlineOpCodes = GenerateILPatch_Structure_Construct(generator);

                // insert code before the stack adjustments start for RequestConstructionSite
                opCodes.InsertRange(insertionPoint, inlineOpCodes);

                return opCodes.AsEnumerable();
            }
            #endif
        }

        public static OnRequestBuildStructureArgs FireOnRequestBuildStructureEvent(ConstructionData constructionData, Structure parentStructure, Vector3 position, Quaternion rotation, bool playerInitiated)
        {
            OnRequestBuildStructureArgs onRequestBuildStructureArgs = new OnRequestBuildStructureArgs();
            onRequestBuildStructureArgs.ConstructionData = constructionData;
            onRequestBuildStructureArgs.ParentStructure = parentStructure;
            onRequestBuildStructureArgs.Position = position;
            onRequestBuildStructureArgs.Rotation = rotation;
            onRequestBuildStructureArgs.PlayerInitiated = playerInitiated;
            EventHandler<OnRequestBuildStructureArgs> requestBuildStructureEvent = OnRequestBuildStructure;
            if (requestBuildStructureEvent != null)
            {
                requestBuildStructureEvent(null, onRequestBuildStructureArgs);
            }

            return onRequestBuildStructureArgs;
        }
    }
}
