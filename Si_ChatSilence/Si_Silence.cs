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


[assembly: MelonInfo(typeof(ChatSilence), "Silence Admin Command", "1.2.1", "databomb", "https://github.com/data-bomb/Silica")]
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
            // register commands
            HelperMethods.CommandCallback silenceCallback = Command_Silence;
            HelperMethods.RegisterAdminCommand("!silence", silenceCallback, Power.Mute);

            HelperMethods.CommandCallback unSilenceCallback = Command_UnSilence;
            HelperMethods.RegisterAdminCommand("!unsilence", unSilenceCallback, Power.Mute);


            // subscribe to the OnRequestPlayerChat event
            Event_Netcode.OnRequestPlayerChat += OnRequestPlayerChat;
        }

        public static void Command_Silence(Player callerPlayer, String args)
        {
            // validate argument count
            int argumentCount = args.Split(' ').Length - 1;
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

        public static void Command_UnSilence(Player callerPlayer, String args)
        {
            // validate argument count
            int argumentCount = args.Split(' ').Length - 1;
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
    }
}