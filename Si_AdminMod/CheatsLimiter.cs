/*
Silica Admin Mod
Copyright (C) 2024 by databomb

* Description *
Provides basic admin mod system to allow additional admins beyond
the host.

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
using Il2CppDebugTools;
using Il2CppSilica;
using Il2CppSteamworks;
#else
using DebugTools;
using Silica;
using System.Reflection;
using Steamworks;
#endif

using HarmonyLib;
using MelonLoader;
using System;
using System.Text;
using UnityEngine;

namespace SilicaAdminMod
{
    public class CheatsEventLimiter
    {
        [HarmonyPatch(typeof(Game), nameof(Game.CheatsEnabled), MethodType.Setter)]
        private static class ApplyPatch_Game_CheatsEnabled_Setter_Pre
        {
            public static bool Prefix(bool __0)
            {
                try
                {
                    // if we're turning cheats off then send this to everyone
                    if (!__0)
                    {
                        return true;
                    }

                    // check if we're supposed to limit this cheats event to admins
                    if (!SiAdminMod.Pref_Admin_StopNonAdminCheats.Value)
                    {
                        return true;
                    }

                    #if NET6_0
                    Game.m_CheatsEnabled = true;
                    #else
                    FieldInfo cheatsEnabledField = typeof(Game).GetField("m_CheatsEnabled", BindingFlags.NonPublic | BindingFlags.Static);
                    cheatsEnabledField.SetValue(null, true);
                    #endif

                    // send modified cheats event only to admins
                    SendLimitedCheatsEvent(true);

                    GameInput.SetActionMapEnableOverride("Cheats", true);

                    // skip the main setter method
                    return false;
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run Game::CheatsEnabled::Setter(Pre)");
                }

                return true;
            }
        }

        public static void SendLimitedCheatsEvent(bool cheatsStatus)
        {
            GameByteStreamWriter gameByteStreamWriter = GameByteStreamWriter.GetGameByteStreamWriter(0U, "NetworkLayer::SendLimitedCheatsEvent", true);
            gameByteStreamWriter.WriteByte((byte)ENetworkPacketType.Cheats);
            gameByteStreamWriter.WriteBool(cheatsStatus);
            byte[] byteData = gameByteStreamWriter.GetByteData();
            uint byteDataSize = (uint)gameByteStreamWriter.GetByteDataSize();
            for (int i = 0; i < Player.Players.Count; i++)
            {
                Player player = Player.Players[i];
                if (player == null)
                {
                    continue;
                }

                if (player == NetworkGameServer.GetServerPlayer())
                {
                    continue;
                }

                // does the player have standard admin powers or higher?
                Power modPowers = player.GetAdminPowers();
                if (player.AdminLevel >= EAdminLevel.STANDARD || AdminMethods.PowerInPowers(Power.Cheat, modPowers))
                {
                    #if NET6_0
                    NetworkLayer.NetBitsSent += byteDataSize * 8U;
                    #else
                    FieldInfo netBitsSentField = typeof(NetworkLayer).GetField("NetBitsSent", BindingFlags.NonPublic | BindingFlags.Static);
                    uint bitsSent = (uint)netBitsSentField.GetValue(null);
                    bitsSent += byteDataSize * 8U;
                    netBitsSentField.SetValue(null, bitsSent);
                    #endif

                    #if NET6_0
                    NetworkLayer.SendServerPacket(player.PlayerID, gameByteStreamWriter.GetByteData(), byteDataSize, ENetworkPacketSend.Reliable, player.PlayerChannel);
                    #else
                    Type networkLayerType = typeof(NetworkLayer);
                    MethodInfo sendServerPacketMethod = networkLayerType.GetMethod("SendServerPacket", BindingFlags.NonPublic | BindingFlags.Static);
                    sendServerPacketMethod.Invoke(null, new object[] { player.PlayerID, gameByteStreamWriter.GetByteData(), byteDataSize, ENetworkPacketSend.Reliable, player.PlayerChannel });
                    #endif
                }
            }
        }
    }
}
