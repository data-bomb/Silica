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
using System.Data;
using static MelonLoader.MelonLogger;

#if NET6_0
using Il2Cpp;
using Il2CppSteamworks;
#else
using Steamworks;
#endif

namespace SilicaAdminMod
{
    public static class Event_Netcode
    {
        public static event EventHandler<OnRequestPlayerChatArgs> OnRequestPlayerChat = delegate { };

        [HarmonyPatch(typeof(GameByteStreamReader), nameof(GameByteStreamReader.GetGameByteStreamReader))]
        static class ApplyPatch_GameByteStreamReader_Events
        {
            #if NET6_0
            static void Prefix(GameByteStreamReader __result, ref Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte> __0, int __1, bool __2)
            #else
            static void Prefix(GameByteStreamReader __result, ref byte[] __0, int __1, bool __2)
            #endif
            {
                try
                {
                    GameByteStreamReader tempReader = new GameByteStreamReader();

                    #if NET6_0
                    tempReader.m_ReadDebug = __2;
                    #else
                    FieldInfo readDebugField = typeof(GameByteStreamReader).GetField("m_ReadDebug", BindingFlags.NonPublic | BindingFlags.Instance);
                    readDebugField.SetValue(tempReader, __2);
                    #endif
                    tempReader.Reset(__0, __1);

                    byte packetType = tempReader.ReadByte();

                    switch (packetType)
                    {
                        case (byte)ENetworkPacketType.ChatMessage:
                        {
                            CSteamID chatterSteamId = (CSteamID)tempReader.ReadUInt64();
                            int chatterChannel = (int)tempReader.ReadByte();
                            string chatText = tempReader.ReadString();
                            bool chatTeamOnly = tempReader.ReadBool();
                            Player chatterPlayer = Player.FindPlayer(chatterSteamId, chatterChannel);

                            if (!chatterPlayer)
                            {
                                return;
                            }

                            MelonLogger.Msg("Firing OnRequestPlayerChatEvent for player: " + chatterPlayer.PlayerName);
                            OnRequestPlayerChatArgs onRequestPlayerChatArgs = FireOnRequestPlayerChatEvent(chatterPlayer, chatText, chatTeamOnly);

                            if (onRequestPlayerChatArgs.Block)
                            {
                                MelonLogger.Msg("Blocking chat message for player: " + chatterPlayer.PlayerName);
                                __0[1] = (byte)127;
                            }

                            break;
                        }
                    }
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run GameByteStreamReader::GetGameByteStreamReader");
                }
            }
        }

        public static OnRequestPlayerChatArgs FireOnRequestPlayerChatEvent(Player player, string message, bool teamOnly)
        {
            OnRequestPlayerChatArgs onRequestPlayerChatArgs = new OnRequestPlayerChatArgs
            {
                Player = player,
                Text = message,
                TeamOnly = teamOnly,
                Block = false
            };
            OnRequestPlayerChat?.Invoke(null, onRequestPlayerChatArgs);

            return onRequestPlayerChatArgs;
        }
    }
}
