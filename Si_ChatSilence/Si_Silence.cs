/*
Silica Chat Silence
Copyright (C) 2024 by databomb

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


[assembly: MelonInfo(typeof(ChatSilence), "Silence Admin Command", "1.1.1", "databomb", "https://github.com/data-bomb/Silica")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]
[assembly: MelonOptionalDependencies("Admin Mod")]

namespace Si_ChatSilence
{
    public class ChatSilence : MelonMod
    {
        static bool adminModAvailable = false;
        static List<CSteamID> silencedPlayers = null!;

        public override void OnInitializeMelon()
        {
            silencedPlayers = new List<CSteamID>();
        }

        public override void OnLateInitializeMelon()
        {
            adminModAvailable = RegisteredMelons.Any(m => m.Info.Name == "Admin Mod");

            if (adminModAvailable)
            {
                HelperMethods.CommandCallback silenceCallback = Command_Silence;
                HelperMethods.RegisterAdminCommand("!silence", silenceCallback, Power.Mute);

                HelperMethods.CommandCallback unSilenceCallback = Command_UnSilence;
                HelperMethods.RegisterAdminCommand("!unsilence", unSilenceCallback, Power.Mute);
            }
            else
            {
                MelonLogger.Warning("Dependency missing: Admin Mod");
            }
        }

        public static void Command_Silence(Player callerPlayer, String args)
        {
            // validate argument count
            int argumentCount = args.Split(' ').Count() - 1;
            if (argumentCount > 1)
            {
                HelperMethods.ReplyToCommand(args.Split(' ')[0] + ": Too many arguments");
                return;
            }
            else if (argumentCount < 1)
            {
                HelperMethods.ReplyToCommand(args.Split(' ')[0] + ": Too few arguments");
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

            if (callerPlayer.CanAdminTarget(playerTarget))
            {
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
            else
            {
                HelperMethods.ReplyToCommand_Player(playerTarget, "is immune due to level");
            }
        }

        public void Command_UnSilence(Player callerPlayer, String args)
        {
            // validate argument count
            int argumentCount = args.Split(' ').Count() - 1;
            if (argumentCount > 1)
            {
                HelperMethods.ReplyToCommand(args.Split(' ')[0] + ": Too many arguments");
                return;
            }
            else if (argumentCount < 1)
            {
                HelperMethods.ReplyToCommand(args.Split(' ')[0] + ": Too few arguments");
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

            if (callerPlayer.CanAdminTarget(playerTarget))
            {
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
            else
            {
                HelperMethods.ReplyToCommand_Player(playerTarget, "is immune due to level");
            }
        }

        public static bool IsPlayerSilenced(Player player)
        {
            return silencedPlayers.Any(s => s == player.PlayerID);
        }

        public static bool IsSteamSilenced(CSteamID steamID)
        {
            return silencedPlayers.Any(s => s ==  steamID);
        }

        public static void SilencePlayer(Player playerTarget)
        {
            silencedPlayers.Add(playerTarget.PlayerID);
        }

        public static void UnSilencePlayer(Player playerTarget)
        {
            silencedPlayers.Remove(playerTarget.PlayerID);
        }

        [HarmonyPatch(typeof(GameByteStreamReader), nameof(GameByteStreamReader.GetGameByteStreamReader))]
        static class GetGameByteStreamReaderPrePatch
        {
            #if NET6_0
            public static void Prefix(GameByteStreamReader __result, Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte> __0, int __1, bool __2)
            #else
            public static void Prefix(GameByteStreamReader __result, byte[] __0, int __1, bool __2)
            #endif
            {
                try
                {
                    // byte[0] = (2) Byte
                    // byte[1] = ENetworkPacketType
                    ENetworkPacketType packetType = (ENetworkPacketType)__0[1];
                    if (packetType == ENetworkPacketType.ChatMessage)
                    {
                        // byte [2] = UInt64
                        // byte [3:10] = CSteamID
                        CSteamID cSteamID = (CSteamID)BitConverter.ToUInt64(__0, 3);

                        if (IsSteamSilenced(cSteamID))
                        {
                            // null out bytes in player
                            for (int i = 0; i < sizeof(UInt64); i++)
                            {
                                __0[3 + i] = 0;
                            }

                            return;
                        }
                    }
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run GameByteStreamReader::GetGameByteStreamReader");
                }
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
    }
}