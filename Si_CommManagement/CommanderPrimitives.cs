/*
Silica Commander Management Mod
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
        public static void DemoteTeamsCommander(MP_Strategy strategyInstance, Team TargetTeam)
        {
            Player DemotedCommander = strategyInstance.GetCommanderForTeam(TargetTeam);

            #if NET6_0
            strategyInstance.SetCommander(TargetTeam, null);
            strategyInstance.RPC_SynchCommander(TargetTeam);
            #else
            Type strategyType = typeof(MP_Strategy);
            MethodInfo setCommanderMethod = strategyType.GetMethod("SetCommander", BindingFlags.Instance | BindingFlags.NonPublic);
            setCommanderMethod.Invoke(strategyInstance, parameters: new object?[] { TargetTeam, null });

            MethodInfo synchCommanderMethod = strategyType.GetMethod("RPC_SynchCommander", BindingFlags.Instance | BindingFlags.NonPublic);
            synchCommanderMethod.Invoke(strategyInstance, new object[] { TargetTeam });
            #endif

            // need to get the player back to Infantry and not stuck in no-clip
            SendToRole(DemotedCommander, GameModeExt.ETeamRole.INFANTRY);
            // respawn
            GameMode.CurrentGameMode.SpawnUnitForPlayer(DemotedCommander, TargetTeam);
        }

        public static void PromoteToCommander(Player CommanderPlayer)
        {
            MP_Strategy strategyInstance = GameObject.FindObjectOfType<MP_Strategy>();

            // now mimic switching to COMMANDER role
            BaseTeamSetup strategyTeamInstance = strategyInstance.GetTeamSetup(CommanderPlayer.Team);
            MelonLogger.Msg("Trying to promote " + CommanderPlayer.PlayerName + " on team " + CommanderPlayer.Team.TeamShortName);


            #if NET6_0
            strategyInstance.SetCommander(strategyTeamInstance.Team, CommanderPlayer);
            strategyInstance.RPC_SynchCommander(strategyTeamInstance.Team);
            #else
            Type strategyType = typeof(MP_Strategy);
            MethodInfo setCommanderMethod = strategyType.GetMethod("SetCommander", BindingFlags.Instance | BindingFlags.NonPublic);
            setCommanderMethod.Invoke(strategyInstance, parameters: new object?[] { strategyTeamInstance.Team, CommanderPlayer });

            MethodInfo synchCommanderMethod = strategyType.GetMethod("RPC_SynchCommander", BindingFlags.Instance | BindingFlags.NonPublic);
            synchCommanderMethod.Invoke(strategyInstance, new object[] { strategyTeamInstance.Team });
            #endif

            // make a log entry of this role change
            Event_Roles.FireOnRoleChangedEvent(CommanderPlayer, GameModeExt.ETeamRole.COMMANDER);
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