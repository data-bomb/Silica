/*
Silica Commander Management Mod
Copyright (C) 2023-2026 by databomb

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

using MelonLoader;
using SilicaAdminMod;
using System;
using UnityEngine;
using System.Collections.Generic;

namespace Si_CommanderManagement
{
    public class CommanderPrimitives
    {
        public static Player? GetCommander(Team team)
        {
            if (GameMode.CurrentGameMode)
            {
                if (GameMode.CurrentGameMode is MP_Strategy strategyInstance)
                {
                    return strategyInstance.GetCommanderForTeam(team);
                }
                else if (GameMode.CurrentGameMode is MP_TowerDefense defenseInstance)
                {
                    return defenseInstance.GetCommanderForTeam(team);
                }
            }

            return null;
        }

        public static void DemoteTeamsCommander(Team TargetTeam)
        {
            Player? DemotedCommander = GetCommander(TargetTeam);
            if (DemotedCommander == null)
            {
                throw new ArgumentException("Team has no commander");
            }

            SetCommander(TargetTeam, null);

            // need to get the player back to Infantry and not stuck in no-clip
            SendToRole(DemotedCommander, GameModeExt.ETeamRole.UNIT);
            // respawn
            GameMode.CurrentGameMode.TrySpawnPlayerAtSpawnPoint(DemotedCommander, TargetTeam);
        }

        #if !NET6_0
        // 0.9.47: MP_Strategy.RPC_SynchCommander(Team team, bool kicked = false) grew an optional
        // second parameter. MethodInfo.Invoke does not fill optional parameters, so invoking it
        // with one argument threw "Number of parameters specified does not match the expected
        // number" after SetCommander had already run: the commander was set server-side but
        // never synced to clients. Fill any trailing optional parameters from their defaults
        // instead of assuming the arity (MP_TowerDefense still has the single-parameter form).
        private static object?[] FillOptionalParameters(MethodInfo method, params object?[] args)
        {
            ParameterInfo[] parameters = method.GetParameters();
            object?[] filled = new object?[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                if (i < args.Length)
                {
                    filled[i] = args[i];
                }
                else
                {
                    filled[i] = parameters[i].HasDefaultValue ? parameters[i].DefaultValue : null;
                }
            }
            return filled;
        }
        #endif

        private static void SetCommander(Team team, Player? player)
        {
            if (GameMode.CurrentGameMode is MP_Strategy strategyInstance)
            {
                #if NET6_0
                strategyInstance.SetCommander(team, player);
                strategyInstance.RPC_SynchCommander(team);
                #else
                Type strategyModeType = strategyInstance.GetType();
                MethodInfo setCommanderMethod = strategyModeType.GetMethod("SetCommander", BindingFlags.Instance | BindingFlags.NonPublic);
                setCommanderMethod.Invoke(strategyInstance, parameters: new object?[] { team, player });

                MethodInfo synchCommanderMethod = strategyModeType.GetMethod("RPC_SynchCommander", BindingFlags.Instance | BindingFlags.NonPublic);
                synchCommanderMethod.Invoke(strategyInstance, FillOptionalParameters(synchCommanderMethod, team));
                #endif
            }
            else if (GameMode.CurrentGameMode is MP_TowerDefense defenseInstance)
            {
                #if NET6_0
                defenseInstance.SetCommander(team, player);
                defenseInstance.RPC_SynchCommander(team);
                #else
                Type defenseModeType = defenseInstance.GetType();
                MethodInfo setCommanderMethod = defenseModeType.GetMethod("SetCommander", BindingFlags.Instance | BindingFlags.NonPublic);
                setCommanderMethod.Invoke(defenseInstance, parameters: new object?[] { team, player });

                MethodInfo synchCommanderMethod = defenseModeType.GetMethod("RPC_SynchCommander", BindingFlags.Instance | BindingFlags.NonPublic);
                synchCommanderMethod.Invoke(defenseInstance, FillOptionalParameters(synchCommanderMethod, team));
                #endif
            }
        }

        // mimic switching to COMMANDER role
        public static void PromoteToCommander(Player commander)
        {
            MelonLogger.Msg("Trying to promote " + commander.PlayerName + " on team " + commander.Team.TeamShortName);
            SetCommander(commander.Team, commander);

            // make a log entry of this role change
            Event_Roles.FireOnRoleChangedEvent(commander, GameModeExt.ETeamRole.COMMANDER);
        }

        public static void SendToRole(Player FormerCommander, GameModeExt.ETeamRole role)
        {
            GameByteStreamWriter theRoleStream;
            theRoleStream = GameMode.CurrentGameMode.CreateRPCPacket(2);
            if (theRoleStream == null)
            {
                return;
            }

            theRoleStream.WriteUInt64((ulong)FormerCommander.PlayerID);
            theRoleStream.WriteByte((byte)FormerCommander.PlayerChannel);
            theRoleStream.WriteByte((byte)role);
            GameMode.CurrentGameMode.SendRPCPacket(theRoleStream);
        }
    }
}