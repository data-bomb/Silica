/*
Silica Chat Silence
Copyright (C) 2023-2024 by databomb

* Description *
Provides an admin command to silence a player, which prevents that 
player from talking in the server.

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
using Il2CppSteamworks;
#else
using Steamworks;
#endif

using HarmonyLib;
using MelonLoader;
using Si_ChatSilence;
using System.Collections.Generic;
using SilicaAdminMod;
using System;
using System.Linq;


[assembly: MelonInfo(typeof(ChatSilence), "Chat Silence", "2.0.1", "databomb", "https://github.com/data-bomb/Silica")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]
[assembly: MelonOptionalDependencies("Admin Mod")]

namespace Si_ChatSilence
{
    public class ChatSilence : MelonMod
    {
        static List<NetworkID> silencedPlayers = null!;
        static List<NetworkID> mutedPlayers = null!;

        public override void OnInitializeMelon()
        {
            silencedPlayers = new List<NetworkID>();
            mutedPlayers = new List<NetworkID>();
        }

        public override void OnLateInitializeMelon()
        {
            // register commands
            HelperMethods.CommandCallback silenceCallback = Command_Silence;
            HelperMethods.RegisterAdminCommand("silence", silenceCallback, Power.Mute, "Prevents target player from sending chat messages. Usage: !silence <player>");

            HelperMethods.CommandCallback unSilenceCallback = Command_UnSilence;
            HelperMethods.RegisterAdminCommand("unsilence", unSilenceCallback, Power.Mute, "Allows target player to send chat messages. Usage: !unsilence <player>");

            HelperMethods.CommandCallback muteCallback = Command_Mute;
            HelperMethods.RegisterAdminCommand("mute", muteCallback, Power.Mute, "Prevents target player from sending voice messages. Usage: !mute <player>");

            HelperMethods.CommandCallback unMuteCallback = Command_UnMute;
            HelperMethods.RegisterAdminCommand("unmute", unMuteCallback, Power.Mute, "Allows target player to send voice messages. Usage: !unmute <player>");


            // subscribe to the OnRequestPlayerChat event
            Event_Netcode.OnRequestPlayerChat += OnRequestPlayerChat;
        }

        public static void Command_Silence(Player? callerPlayer, String args)
        {
            string commandName = args.Split(' ')[0];

            // validate argument count
            int argumentCount = args.Split(' ').Length - 1;
            if (argumentCount > 1)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Too many arguments");
                return;
            }
            else if (argumentCount < 1)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Too few arguments");
                return;
            }

            // validate argument contents
            String sTarget = args.Split(' ')[1];
            Player? playerTarget = HelperMethods.FindTargetPlayer(sTarget);

            if (playerTarget == null)
            {
                HelperMethods.ReplyToCommand(args.Split(' ')[0] + ": Ambiguous or invalid target");
                return;
            }

            if (callerPlayer != null && !callerPlayer.CanAdminTarget(playerTarget))
            {
                HelperMethods.ReplyToCommand_Player(playerTarget, "is immune due to level");
                return;
            }

            if (IsPlayerSilenced(playerTarget))
            {
                HelperMethods.ReplyToCommand(args.Split(' ')[0] + ": Target player already silenced");
            }
            else
            {
                SilencePlayer(playerTarget);
                HelperMethods.AlertAdminActivity(callerPlayer, playerTarget, "silenced");
            }
        }

        public static void Command_UnSilence(Player? callerPlayer, String args)
        {
            string commandName = args.Split(' ')[0];

            // validate argument count
            int argumentCount = args.Split(' ').Length - 1;
            if (argumentCount > 1)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Too many arguments");
                return;
            }
            else if (argumentCount < 1)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Too few arguments");
                return;
            }

            // validate argument contents
            String sTarget = args.Split(' ')[1];
            Player? playerTarget = HelperMethods.FindTargetPlayer(sTarget);

            if (playerTarget == null)
            {
                HelperMethods.ReplyToCommand(args.Split(' ')[0] + ": Ambiguous or invalid target");
                return;
            }

            if (callerPlayer != null && !callerPlayer.CanAdminTarget(playerTarget))
            {
                HelperMethods.ReplyToCommand_Player(playerTarget, "is immune due to level");
                return;
            }

            if (IsPlayerSilenced(playerTarget))
            {
                UnSilencePlayer(playerTarget);
                HelperMethods.AlertAdminActivity(callerPlayer, playerTarget, "unsilenced");
            }
            else
            {
                HelperMethods.ReplyToCommand(args.Split(' ')[0] + ": Target player not silenced");
            }
        }

        public static bool IsPlayerSilenced(Player player)
        {
            return silencedPlayers.Any(s => s == player.PlayerID);
        }

        public static bool IsSteamSilenced(NetworkID steamID)
        {
            return silencedPlayers.Any(s => s == steamID);
        }

        public static void SilencePlayer(Player playerTarget)
        {
            silencedPlayers.Add(playerTarget.PlayerID);
        }

        public static void UnSilencePlayer(Player playerTarget)
        {
            silencedPlayers.Remove(playerTarget.PlayerID);
        }

        public void OnRequestPlayerChat(object? sender, OnRequestPlayerChatArgs args)
        {
            if (args.Player == null)
            {
                return;
            }

            MelonLogger.Msg("Checking if player is silenced.");

            // check if player is allowed to chat
            if (IsSteamSilenced(args.Player.PlayerID))
            {
                MelonLogger.Msg("Preventing " + args.Player.PlayerName + " from talking in chat.");
                args.Block = true;
            }
        }

        [HarmonyPatch(typeof(GameMode), nameof(GameMode.OnPlayerLeftBase))]
        private static class Silence_Patch_GameMode_OnPlayerLeftBase
        {
            public static void Prefix(GameMode __instance, Player __0)
            {
                try
                {
                    if (__0 != null)
                    {
                        if (IsPlayerSilenced(__0))
                        {
                            silencedPlayers.Remove(__0.PlayerID);
                        }
                    }
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run GameMode::OnPlayerLeftBase");
                }
            }
        }

        public static void Command_Mute(Player? callerPlayer, String args)
        {
            string commandName = args.Split(' ')[0];

            // validate argument count
            int argumentCount = args.Split(' ').Length - 1;
            if (argumentCount > 1)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Too many arguments");
                return;
            }
            else if (argumentCount < 1)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Too few arguments");
                return;
            }

            // validate argument contents
            String sTarget = args.Split(' ')[1];
            Player? playerTarget = HelperMethods.FindTargetPlayer(sTarget);

            if (playerTarget == null)
            {
                HelperMethods.ReplyToCommand(args.Split(' ')[0] + ": Ambiguous or invalid target");
                return;
            }

            if (callerPlayer != null && !callerPlayer.CanAdminTarget(playerTarget))
            {
                HelperMethods.ReplyToCommand_Player(playerTarget, "is immune due to level");
                return;
            }

            if (IsPlayerMuted(playerTarget))
            {
                HelperMethods.ReplyToCommand(args.Split(' ')[0] + ": Target player already muted");
            }
            else
            {
                MutePlayer(playerTarget);
                HelperMethods.AlertAdminActivity(callerPlayer, playerTarget, "muted");
            }
        }

        public static void Command_UnMute(Player? callerPlayer, String args)
        {
            string commandName = args.Split(' ')[0];

            // validate argument count
            int argumentCount = args.Split(' ').Length - 1;
            if (argumentCount > 1)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Too many arguments");
                return;
            }
            else if (argumentCount < 1)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Too few arguments");
                return;
            }

            // validate argument contents
            String sTarget = args.Split(' ')[1];
            Player? playerTarget = HelperMethods.FindTargetPlayer(sTarget);

            if (playerTarget == null)
            {
                HelperMethods.ReplyToCommand(args.Split(' ')[0] + ": Ambiguous or invalid target");
                return;
            }

            if (callerPlayer != null && !callerPlayer.CanAdminTarget(playerTarget))
            {
                HelperMethods.ReplyToCommand_Player(playerTarget, "is immune due to level");
                return;
            }

            if (IsPlayerMuted(playerTarget))
            {
                UnMutePlayer(playerTarget);
                HelperMethods.AlertAdminActivity(callerPlayer, playerTarget, "unmuted");
            }
            else
            {
                HelperMethods.ReplyToCommand(args.Split(' ')[0] + ": Target player not muted");
            }
        }

        [HarmonyPatch(typeof(NetworkLayer), nameof(NetworkLayer.RelayVoiceStreamPacket))]
        private static class Mute_Patch_GameMode_RelayVoiceStreamPacket
        {
            #if NET6_0
            public static bool Prefix(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte> __0, uint __1, EP2PSend __2, Player __3, bool __4)
            #else
            public static bool Prefix(byte[] __0, uint __1, EP2PSend __2, Player __3, bool __4)
            #endif
            {
                if (IsPlayerMuted(__3))
                {
                    return false;
                }

                return true;
            }
        }

        public static bool IsPlayerMuted(Player player)
        {
            return mutedPlayers.Any(s => s == player.PlayerID);
        }

        public static bool IsSteamMuted(NetworkID steamID)
        {
            return mutedPlayers.Any(s => s == steamID);
        }

        public static void MutePlayer(Player playerTarget)
        {
            mutedPlayers.Add(playerTarget.PlayerID);
        }

        public static void UnMutePlayer(Player playerTarget)
        {
            mutedPlayers.Remove(playerTarget.PlayerID);
        }
    }
}