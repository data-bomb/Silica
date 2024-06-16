/*
Silica Admin Mod
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

using HarmonyLib;
using System;
using System.Linq;
using MelonLoader;
using UnityEngine;
using Newtonsoft.Json.Linq;
using MelonLoader.ICSharpCode.SharpZipLib.Core;
using System.Runtime.CompilerServices;
using System.Reflection;
using System.Data;

#if NET6_0
using Il2Cpp;
using Il2CppSteamworks;
#else
using Steamworks;
#endif

namespace SilicaAdminMod
{
    public static class Event_Roles
    {
        public static event EventHandler<OnRequestCommanderArgs> OnRequestCommander = delegate { };
        public static event EventHandler<OnRoleChangedArgs> OnRoleChanged = delegate { };

        [HarmonyPatch(typeof(MP_Strategy), nameof(MP_Strategy.ProcessNetRPC))]
        static class ApplyPatch_MPStrategy_RequestRole
        {
            public static bool Prefix(MP_Strategy __instance, ref GameByteStreamReader __0, byte __1)
            {
                try
                {
                    if (__instance == null || __0 == null)
                    {
                        return true;
                    }

                    // only look at RPC_RequestRole
                    if (__1 != (byte)MP_Strategy.ERPCs.REQUEST_ROLE)
                    {
                        return true;
                    }

                    Player requestingPlayer = Player.FindPlayer((CSteamID)__0.ReadUInt64(), (int)__0.ReadByte());
                    MP_Strategy.ETeamRole eRole = (MP_Strategy.ETeamRole)__0.ReadByte();

                    if (requestingPlayer == null)
                    {
                        MelonLogger.Warning("Cannot find player in role request.");
                        return false;
                    }

                    // would the game code treat it as an infantry/no role request?
                    if (eRole != MP_Strategy.ETeamRole.COMMANDER || __instance.GetCommanderForTeam(requestingPlayer.Team))
                    {
                        __0 = RestoreRPC_RequestRoleReader(requestingPlayer, eRole);
                        FireOnRoleChangedEvent(requestingPlayer, eRole);
                        return true;
                    }

                    StrategyTeamSetup strategyTeamSetup = __instance.GetStrategyTeamSetup(requestingPlayer.Team);
                    if (strategyTeamSetup == null)
                    {
                        return false;
                    }

                    OnRequestCommanderArgs onRequestCommanderArgs = FireOnRequestCommanderEvent(requestingPlayer);

                    if (onRequestCommanderArgs.Block)
                    {
                        MelonLogger.Msg("Blocking commander role request for " + onRequestCommanderArgs.Requester.PlayerName);
                        if (!onRequestCommanderArgs.PreventSpawnWhenBlocked)
                        {
                            MelonLogger.Msg("Preventing Spawn");
                            __instance.SpawnUnitForPlayer(requestingPlayer, requestingPlayer.Team);
                            FireOnRoleChangedEvent(requestingPlayer, MP_Strategy.ETeamRole.INFANTRY);
                        }

                        return false;
                    }

                    MelonLogger.Msg("Allowing to join commander");
                    #if NET6_0
                    __instance.SetCommander(strategyTeamSetup.Team, requestingPlayer);
                    __instance.RPC_SynchCommander(strategyTeamSetup.Team);
                    #else
                    Type strategyType = typeof(MP_Strategy);
                    MethodInfo setCommanderMethod = strategyType.GetMethod("SetCommander", BindingFlags.Instance | BindingFlags.NonPublic);
                    setCommanderMethod.Invoke(__instance, parameters: new object?[] { strategyTeamSetup.Team, requestingPlayer });

                    MethodInfo synchCommanderMethod = strategyType.GetMethod("RPC_SynchCommander", BindingFlags.Instance | BindingFlags.NonPublic);
                    synchCommanderMethod.Invoke(__instance, new object[] { strategyTeamSetup.Team });
                    #endif

                    FireOnRoleChangedEvent(requestingPlayer, MP_Strategy.ETeamRole.COMMANDER);

                    return false;
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run MP_Strategy::ProcessNetRPC");
                }

                return true;
            }
        }

        public static GameByteStreamReader RestoreRPC_RequestRoleReader(Player requestingPlayer, MP_Strategy.ETeamRole role)
        {
            GameByteStreamWriter gameByteStreamWriter = GameByteStreamWriter.GetGameByteStreamWriter(0U, "Si_AdminMod::RestoreRPC_RequestRoleReader", true);
            gameByteStreamWriter.WriteByte((byte)ENetworkPacketType.GameModeRPC);
            gameByteStreamWriter.WriteByte(0);
            gameByteStreamWriter.WriteByte((byte)MP_Strategy.ERPCs.REQUEST_ROLE);
            gameByteStreamWriter.WriteUInt64((ulong)requestingPlayer.PlayerID);
            gameByteStreamWriter.WriteByte((byte)requestingPlayer.PlayerChannel);
            gameByteStreamWriter.WriteByte((byte)role);
            GameByteStreamReader gameByteStreamReader = GameByteStreamReader.GetGameByteStreamReader(gameByteStreamWriter.GetByteData(), gameByteStreamWriter.GetByteDataSize(), 0U, "Si_AdminMod::RestoreRPC_RequestRoleReader", true);
            gameByteStreamReader.ReadByte();
            gameByteStreamReader.ReadByte();
            gameByteStreamReader.ReadByte();
            return gameByteStreamReader;
        }

        public static void FireOnRoleChangedEvent(Player player, MP_Strategy.ETeamRole role)
        {
            MelonLogger.Msg("Firing Role Change Event for " + player.PlayerName + " to role " + role.ToString());

            OnRoleChangedArgs onRoleChangedArgs = new OnRoleChangedArgs
            {
                Player = player,
                Role = role
            };
            OnRoleChanged?.Invoke(null, onRoleChangedArgs);
        }

        public static OnRequestCommanderArgs FireOnRequestCommanderEvent(Player player)
        {
            OnRequestCommanderArgs onRequestCommanderArgs = new OnRequestCommanderArgs
            {
                Requester = player,
                Block = false,
                PreventSpawnWhenBlocked = false
            };
            OnRequestCommander?.Invoke(null, onRequestCommanderArgs);

            return onRequestCommanderArgs;
        }
    }
}
