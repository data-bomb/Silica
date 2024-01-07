/*
Silica Admin Mod
Copyright (C) 2024 by databomb

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

#if NET6_0
using Il2Cpp;
using Il2CppSteamworks;
#else
using Steamworks;
#endif

namespace SilicaAdminMod
{
    public static class Event_OnRequestCommander
    {
        public static event EventHandler<OnRequestCommanderArgs>? OnRequestCommander = delegate { };

        [HarmonyPatch(typeof(MP_Strategy), nameof(MP_Strategy.ProcessNetRPC))]
        static class ApplyPatch_MPStrategy_RequestRole
        {
            public static bool Prefix(MP_Strategy __instance, GameByteStreamReader __0, byte __1)
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

                    // would the game code treat it as an infantry/no role request?
                    if (eRole != MP_Strategy.ETeamRole.COMMANDER || __instance.GetCommanderForTeam(requestingPlayer.Team))
                    {
                        GameByteStreamWriter gameByteStreamWriter = GameByteStreamWriter.GetGameByteStreamWriter(true);
                        gameByteStreamWriter.WriteUInt64((ulong)requestingPlayer.PlayerID);
                        gameByteStreamWriter.WriteByte((byte)requestingPlayer.PlayerChannel);
                        gameByteStreamWriter.WriteByte((byte)eRole);
                        __0 = GameByteStreamReader.GetGameByteStreamReader(gameByteStreamWriter.GetByteData(), gameByteStreamWriter.GetByteDataSize(), true);
                        return true;
                    }

                    StrategyTeamSetup strategyTeamSetup = __instance.GetStrategyTeamSetup(requestingPlayer.Team);
                    if (strategyTeamSetup == null)
                    {
                        return false;
                    }

                    OnRequestCommanderArgs onRequestCommanderArgs = new OnRequestCommanderArgs();
                    onRequestCommanderArgs.Requester = requestingPlayer;
                    onRequestCommanderArgs.Block = false;
                    EventHandler<OnRequestCommanderArgs>? requestCommanderEvent = OnRequestCommander;
                    if (requestCommanderEvent != null)
                    {
                        requestCommanderEvent(null, onRequestCommanderArgs);
                    }

                    if (onRequestCommanderArgs.Block)
                    {
                        MelonLogger.Msg("Blocking commander role request");
                        __instance.SpawnUnitForPlayer(requestingPlayer, requestingPlayer.Team);
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

                    return false;
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run MP_Strategy::ProcessNetRPC");
                }

                return true;
            }
        }
    }
}
