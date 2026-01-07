/*
Silica Admin Mod
Copyright (C) 2023-2025 by databomb

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

using MelonLoader;
using System;
using UnityEngine;

#if NET6_0
using Il2Cpp;
using Il2CppSteamworks;
using Il2CppDebugTools;
#else
using DebugTools;
using System.Collections.Generic;
using Steamworks;
using System.Reflection;
#endif

namespace SilicaAdminMod
{
    public static class HelperMethods
    {
        public static readonly float Timer_Inactive = -123.0f;
        public const string defaultColor = "<color=#DDE98C>";
        public const string chatPrefix = "<b>" + defaultColor + "[</color><color=#DFA725>SAM</color>" + defaultColor + "]</color></b> ";

        public delegate void CommandCallback(Player? callerPlayer, String args);

        public static void RegisterAdminCommand(String adminCommand, CommandCallback adminCallback, Power adminPower, String? adminDescription = null)
        {
            AdminMethods.RegisterAdminCommand(adminCommand, adminCallback, adminPower, adminDescription);
        }

        public static void RegisterPlayerCommand(String playerCommand, CommandCallback commandCallback, bool hideFromChat)
        {
            PlayerMethods.RegisterPlayerCommand(playerCommand, commandCallback, hideFromChat);
        }
        public static void RegisterPlayerPhrase(String playerCommand, CommandCallback commandCallback, bool hideFromChat)
        {
            PlayerMethods.RegisterPlayerPhrase(playerCommand, commandCallback, hideFromChat);
        }

        public static bool UnregisterAdminCommand(String adminCommand)
        {
            return AdminMethods.UnregisterAdminCommand(adminCommand);
        }

        public static bool UnregisterPlayerCommand(String playerCommand)
        {
            return PlayerMethods.UnregisterPlayerCommand(playerCommand);
        }

        public static bool UnregisterPlayerPhrase(String playerPhrase)
        {
            return PlayerMethods.UnregisterPlayerPhrase(playerPhrase);
        }

        public static bool IsValidCommandPrefix(char commandFirstCharacter)
        {
            if (commandFirstCharacter == '!' || commandFirstCharacter == '/' || commandFirstCharacter == '.')
            {
                return true;
            }

            return false;
        }

        public static AdminCommand? GetAdminCommand(string commandString)
        {
            String thisCommandText = commandString.Split(' ')[0];
            // trim first character
            thisCommandText = thisCommandText[1..];
            return AdminMethods.FindAdminCommandFromString(thisCommandText);
        }

        public static PlayerCommand? GetPlayerCommand(string commandString)
        {
            String thisCommandText = commandString.Split(' ')[0];
            // trim first character
            thisCommandText = thisCommandText[1..];
            return PlayerMethods.FindPlayerCommandFromString(thisCommandText);
        }

        public static PlayerCommand? GetPlayerPhrase(string phraseString)
        {
            String thisPhrase = phraseString.Split(' ')[0];
            return PlayerMethods.FindPlayerPhraseFromString(thisPhrase);
        }

        public static void ReplyToCommand(params string[] messages)
        {
            SendChatMessageToAll(chatPrefix + String.Concat(messages));
        }

        public static void ReplyToCommand_Player(Player player, params string[] messages)
        {
            SendChatMessageToAll(chatPrefix + GetTeamColor(player) + player.PlayerName + "</color> " + String.Concat(messages));
        }

        public static void AlertAdminActivity(Player? adminPlayer, Player targetPlayer, string action)
        {
            string adminName = (adminPlayer == null) ? "CONSOLE" : adminPlayer.PlayerName;
            SendChatMessageToAll(chatPrefix + GetAdminColor() + adminName + "</color> " + action + " " + GetTeamColor(targetPlayer) + targetPlayer.PlayerName + "</color>");
        }

        public static void AlertAdminAction(Player? adminPlayer, string action)
        {
            string adminName = (adminPlayer == null) ? "CONSOLE" : adminPlayer.PlayerName;
            SendChatMessageToAll(chatPrefix + GetAdminColor() + adminName + "</color> " + action);
        }

        public static void SendChatMessageToAll(params string[] messages)
        {
            for (int i = 0; i < Player.Players.Count; i++)
            {
                Player? player = Player.Players[i];
                if (player == null)
                {
                    continue;
                }

                NetworkSendChat(player, false, messages);
            }
        }

        public static void SendChatMessageToTeam(Team team, params string[] messages)
        {
            for (int i = 0; i < Player.Players.Count; i++)
            {
                Player? player = Player.Players[i];
                if (player == null)
                {
                    continue;
                }

                if (player.Team == team)
                {
                    NetworkSendChat(player, false, messages);
                }
            }
        }

        public static void SendChatMessageToTeamNoCommander(Team team, params string[] messages)
        {
            for (int i = 0; i < Player.Players.Count; i++)
            {
                Player? player = Player.Players[i];
                if (player == null)
                {
                    continue;
                }

                if (player.Team == team && !player.IsCommander)
                {
                    NetworkSendChat(player, false, messages);
                }
            }
        }

        public static void SendConsoleMessage(params string[] messages)
        {
            for (int i = 0; i < Player.Players.Count; i++)
            {
                Player? player = Player.Players[i];
                if (player == null)
                {
                    continue;
                }

                NetworkSendConsole(player, messages);
            }
        }

        public static void SendConsoleMessageToTeam(Team team, params string[] messages)
        {
            for (int i = 0; i < Player.Players.Count; i++)
            {
                Player? player = Player.Players[i];
                if (player == null)
                {
                    continue;
                }

                if (player.Team == team)
                {
                    NetworkSendConsole(player, messages);
                }
            }
        }

        public static void SendChatMessageToPlayer(Player? player, params string[] messages)
        {
            // send to server console if null
            if (player == null)
            {
                DebugConsole.Log(String.Concat(messages), DebugConsole.LogLevel.Log);
                DebugConsole.Log("", DebugConsole.LogLevel.Log);
                return;
            }

            NetworkSendChat(player, false, messages);
        }
        public static void SendConsoleMessageToPlayer(Player? player, params string[] messages)
        {
            // send to server console if null
            if (player == null)
            {
                DebugConsole.Log(String.Concat(messages), DebugConsole.LogLevel.Log);
                DebugConsole.Log("", DebugConsole.LogLevel.Log);
                return;
            }

            NetworkSendConsole(player, messages);
        }
        
        private static void NetworkSendChat(Player recipient, bool teamOnly, params string[] messages)
        {
            #if NET6_0
            if (recipient == NetworkGameServer.GetServerPlayer())
            {
                Player.SendServerChatMessage(String.Concat(messages));
                return;
            }
            #endif

            GameByteStreamWriter gameByteStreamWriter = GameByteStreamWriter.GetGameByteStreamWriter(0U, "Si_AdminMod::NetworkSendChat", true);
            gameByteStreamWriter.WriteByte((byte)ENetworkPacketType.ChatMessage);
            gameByteStreamWriter.WriteUInt64((ulong)NetworkID.CurrentUserID);
            gameByteStreamWriter.WriteByte((byte)0);
            gameByteStreamWriter.WriteString(String.Concat(messages));
            gameByteStreamWriter.WriteBool(teamOnly); // teamOnly
            gameByteStreamWriter.WriteBool(true); // isServerMessage

            uint byteDataSize = (uint)gameByteStreamWriter.GetByteDataSize();
            #if NET6_0
            NetworkLayer.NetBitsSent += byteDataSize * 8U;
            #else
            FieldInfo netBitsSentField = typeof(NetworkLayer).GetField("NetBitsSent", BindingFlags.NonPublic | BindingFlags.Static);
            uint bitsSent = (uint)netBitsSentField.GetValue(null);
            bitsSent += byteDataSize * 8U;
            netBitsSentField.SetValue(null, bitsSent);
            #endif

            #if NET6_0
            NetworkLayer.SendServerPacket(recipient.PlayerID, gameByteStreamWriter.GetByteData(), byteDataSize, ENetworkPacketSend.Reliable, recipient.PlayerChannel);
            #else
            Type networkLayerType = typeof(NetworkLayer);
            MethodInfo sendServerPacketMethod = networkLayerType.GetMethod("SendServerPacket", BindingFlags.NonPublic | BindingFlags.Static);
            sendServerPacketMethod.Invoke(null, new object[] { recipient.PlayerID, gameByteStreamWriter.GetByteData(), byteDataSize, ENetworkPacketSend.Reliable, recipient.PlayerChannel });
            #endif
        }

        private static void NetworkSendConsole(Player recipient, params string[] messages)
        {
            #if NET6_0
            if (recipient == NetworkGameServer.GetServerPlayer())
            {
                DebugConsole.Log(String.Concat(messages));
                return;
            }
            #endif

            GameByteStreamWriter gameByteStreamWriter = GameByteStreamWriter.GetGameByteStreamWriter(0U, "Si_AdminMod::NetworkSendConsole", true);
            gameByteStreamWriter.WriteByte((byte)ENetworkPacketType.RemoteCommandResult);
            gameByteStreamWriter.WriteUInt64((ulong)recipient.PlayerID);
            gameByteStreamWriter.WriteByte((byte)recipient.PlayerChannel);
            short stringCount = (short)messages.Length;
            gameByteStreamWriter.WriteInt16(stringCount);
            int i = 0;
            while (i < stringCount)
            {
                gameByteStreamWriter.WriteString(messages[i]);
                gameByteStreamWriter.WriteByte((byte)DebugConsole.LogLevel.NoFormat);
                i++;
            }
            uint byteDataSize = (uint)gameByteStreamWriter.GetByteDataSize();
            #if NET6_0
            NetworkLayer.NetBitsSent += byteDataSize * 8U;
            #else
            FieldInfo netBitsSentField = typeof(NetworkLayer).GetField("NetBitsSent", BindingFlags.NonPublic | BindingFlags.Static);
            uint bitsSent = (uint)netBitsSentField.GetValue(null);
            bitsSent += byteDataSize * 8U;
            netBitsSentField.SetValue(null, bitsSent);
            #endif

            #if NET6_0
            NetworkLayer.SendServerPacket(recipient.PlayerID, gameByteStreamWriter.GetByteData(), byteDataSize, ENetworkPacketSend.Reliable, recipient.PlayerChannel);
            #else
            Type networkLayerType = typeof(NetworkLayer);
            MethodInfo sendServerPacketMethod = networkLayerType.GetMethod("SendServerPacket", BindingFlags.NonPublic | BindingFlags.Static);
            sendServerPacketMethod.Invoke(null, new object[] { recipient.PlayerID, gameByteStreamWriter.GetByteData(), byteDataSize, ENetworkPacketSend.Reliable, recipient.PlayerChannel });
            #endif
        }

        public static void PrintError(Exception exception, string? message = null)
        {
            if (message != null)
            {
                MelonLogger.Msg(message);
            }
            string error = exception.Message;
            error += "\n" + exception.TargetSite;
            error += "\n" + exception.StackTrace;
            MelonLogger.Error(error);
        }

        public static void SetCommander(Team team, Player? player)
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
                synchCommanderMethod.Invoke(strategyInstance, new object[] { team });
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
                synchCommanderMethod.Invoke(defenseInstance, new object[] { team });
                #endif
            }
        }

        public static Player? FindTargetPlayer(String sTarget)
        {
            Player? targetPlayer = null;
            int iTargetCount = 0;

            // loop through all players
            #if NET6_0
            Il2CppSystem.Collections.Generic.List<Player> players = Player.Players;
            #else
            List<Player> players = Player.Players;
            #endif

            int iPlayerCount = players.Count;

            for (int i = 0; i < iPlayerCount; i++)
            {
                if (players[i].PlayerName.Contains(sTarget))
                {
                    iTargetCount++;
                    targetPlayer = players[i];
                }
            }

            if (iTargetCount != 1)
            {
                targetPlayer = null;
            }

            return targetPlayer;
        }

        public static string GetAdminColor()
        {
            return "<color=#ff54ff>";
        }

        private static string TeamColorTextFromIndex(SiConstants.ETeam teamIndex)
        {
            return teamIndex switch
            {
                SiConstants.ETeam.Alien => "<color=#54CC54>",
                SiConstants.ETeam.Centauri => "<color=#FF5454>",
                SiConstants.ETeam.Sol => "<color=#548DFF>",
                SiConstants.ETeam.Wildlife => "<color=#cc8800>",
                _ => "<color=#FFFFFF>",
            };
        }

        public static string GetTeamColor(Team team)
        {
            if (team == null)
            {
                return "<color=#FFFFFF>";
            }

            return TeamColorTextFromIndex((SiConstants.ETeam)team.Index);
        }

        public static string GetTeamColor(Player player)
        {
            if (player == null)
            {
                return "<color=#FFFFFF>";
            }

            Team? team = player.Team;
            if (team == null)
            {
                return "<color=#FFFFFF>";
            }

            int teamIndex = team.Index;
            return TeamColorTextFromIndex((SiConstants.ETeam)teamIndex);
        }

        public static Power PowerTextToPower(String powerText)
        {
            Power powers = Power.None;
            if (powerText.Contains('a'))
            {
                powers |= Power.Slot;
            }
            if (powerText.Contains('b'))
            {
                powers |= Power.Vote;
            }
            if (powerText.Contains('c'))
            {
                powers |= Power.Kick;
            }
            if (powerText.Contains('d'))
            {
                powers |= Power.Ban;
            }
            if (powerText.Contains('e'))
            {
                powers |= Power.Unban;
            }
            if (powerText.Contains('f'))
            {
                powers |= Power.Slay;
            }
            if (powerText.Contains('g'))
            {
                powers |= Power.Map;
            }
            if (powerText.Contains('h'))
            {
                powers |= Power.Cheat;
            }
            if (powerText.Contains('i'))
            {
                powers |= Power.Commander;
            }
            if (powerText.Contains('j'))
            {
                powers |= Power.Skip;
            }
            if (powerText.Contains('k'))
            {
                powers |= Power.End;
            }
            if (powerText.Contains('l'))
            {
                powers |= Power.Eject;
            }
            if (powerText.Contains('m'))
            {
                powers |= Power.Mute;
            }
            if (powerText.Contains('n'))
            {
                powers |= Power.MuteForever;
            }
            if (powerText.Contains('o'))
            {
                powers |= Power.Generic;
            }
            if (powerText.Contains('p'))
            {
                powers |= Power.Teams;
            }
            if (powerText.Contains('q'))
            {
                powers |= Power.Custom1;
            }
            if (powerText.Contains('r'))
            {
                powers |= Power.Custom2;
            }
            if (powerText.Contains('s'))
            {
                powers |= Power.Custom3;
            }
            if (powerText.Contains('t'))
            {
                powers |= Power.Custom4;
            }
            if (powerText.Contains('u'))
            {
                powers |= Power.Reserved1;
            }
            if (powerText.Contains('v'))
            {
                powers |= Power.Reserved2;
            }
            if (powerText.Contains('w'))
            {
                powers |= Power.Reserved3;
            }
            if (powerText.Contains('x'))
            {
                powers |= Power.Reserved4;
            }
            if (powerText.Contains('y'))
            {
                powers |= Power.Rcon;
            }
            if (powerText.Contains('z'))
            {
                powers |= Power.Root;
            }

            return powers;
        }

        public static void DestroyAllStructures(Team team)
        {
            if (team == null)
            {
                return;
            }

            for (int i = 0; i < team.Structures.Count; i++)
            {
                team.Structures[i].DamageManager.SetHealth01(0.0f);
            }
        }

        public static bool KickPlayer(Player playerToKick)
        {
            Player serverPlayer = NetworkGameServer.GetServerPlayer();

            if (playerToKick == serverPlayer)
            {
                return false;
            }

            NetworkID serverSteam = NetworkGameServer.GetServerID();
            NetworkID playerSteam = playerToKick.PlayerID;

            int playerChannel = playerToKick.PlayerChannel;

            NetworkLayer.SendPlayerConnectResponse(ENetworkPlayerConnectType.Kicked, playerSteam, playerChannel, serverSteam);
            Player.RemovePlayer(playerSteam);
            NetworkLayer.SendPlayerConnect(ENetworkPlayerConnectType.Disconnected, playerSteam, playerChannel);

            return true;
        }

        public static GameObject? SpawnAtLocation(String name, Vector3 position, Quaternion rotation, int teamIndex = -1)
        {
            int prefabIndex = GameDatabase.GetSpawnablePrefabIndex(name);
            if (prefabIndex <= -1)
            {
                DebugConsole.Log("SpawnAtLocation: Could not find prefab '" + name + "' in network spawn lists!", DebugConsole.LogLevel.Warning);
                return null;
            }

            GameObject prefabObject = GameDatabase.GetSpawnablePrefab(prefabIndex);
            GameObject? spawnedObject;
            if (teamIndex > -1)
            {
                spawnedObject = Game.SpawnPrefab(prefabObject, null, Team.Teams[teamIndex], position, rotation, true, true);
            }
            else
            {
                BaseGameObject baseGameObject = prefabObject.GetBaseGameObject();
                if (baseGameObject)
                {
                    teamIndex = baseGameObject.DefaultTeam.Index;
                }

                spawnedObject = Game.SpawnPrefab(prefabObject, null, Team.Teams[teamIndex], position, rotation, true, true);
            }

            return spawnedObject;
        }

        #if !NET6_0
        public static byte FindByteValueInEnum(Type parentType, string enumName, string byteName)
        {
                Type enumType = parentType.GetNestedType(enumName, BindingFlags.NonPublic);
                var enumValues = enumType.GetEnumValues();
                foreach (var enumValue in enumValues)
                {
                    if (string.Compare(enumValue.ToString(), byteName) == 0)
                    {
                        return (byte)enumValue;
                    }
                }

                return byte.MaxValue;
        }
        #endif

        public static bool IsTimerActive(float time)
        {
            if (time >= 0.0f)
            {
                return true;
            }

            return false;
        }

        public static void StartTimer(ref float timer)
        {
            timer = 0.0f;
        }
    }
}