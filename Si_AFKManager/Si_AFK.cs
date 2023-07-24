/*
Silica Commander Management Mod
Copyright (C) 2023 by databomb

* Description *
For Silica listen servers, allows hosts to use the !kick or !afk command
to disconnect a player without a session ban.

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
using Il2Cpp;
using MelonLoader;
using Si_AFKManager;
using AdminExtension;
using System.Timers;
using UnityEngine;
using System.Timers;

[assembly: MelonInfo(typeof(AwayFromKeyboard), "AFK Manager", "1.1.3", "databomb")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]

namespace Si_AFKManager
{
    public class AwayFromKeyboard : MelonMod
    {
        public class AFKCount
        {
            public Il2Cpp.Player Player { get; set; }
            public uint Minutes { get; set; }
        }

        static System.Timers.Timer afkTimer;
        static bool AdminModAvailable = false;
        static List<AFKCount> AFKTracker;
        static bool oneMinuteCheckTime;

        public override void OnLateInitializeMelon()
        {
            AdminModAvailable = RegisteredMelons.Any(m => m.Info.Name == "Admin Mod");

            AFKTracker = new List<AFKCount>();

            if (AdminModAvailable)
            {
                HelperMethods.CommandCallback kickCallback = Command_Kick;
                HelperMethods.CommandCallback afkCallback = Command_AFK;
                HelperMethods.RegisterAdminCommand("!kick", kickCallback, Power.Kick);
                HelperMethods.RegisterAdminCommand("!afk", afkCallback, Power.Kick);
            }
            else
            {
                MelonLogger.Warning("Dependency missing: Admin Mod");
            }

            double interval = 60000.0f;
            afkTimer = new System.Timers.Timer(interval);
            afkTimer.Elapsed += new ElapsedEventHandler(timerCallbackOneMinute);
            afkTimer.AutoReset = true;
            afkTimer.Enabled = true;
        }

        public void Command_Kick(Il2Cpp.Player callerPlayer, String args)
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
            Il2Cpp.Player? playerToKick = HelperMethods.FindTargetPlayer(sTarget);

            if (playerToKick == null)
            {
                HelperMethods.ReplyToCommand(args.Split(' ')[0] + ": Ambiguous or invalid target");
                return;
            }

            if (callerPlayer.CanAdminTarget(playerToKick))
            {
                if (HelperMethods.KickPlayer(playerToKick))
                {
                    HelperMethods.AlertAdminActivity(callerPlayer, playerToKick, "kicked");
                }
                else
                {
                    HelperMethods.ReplyToCommand_Player(playerToKick, "is the host and cannot be targeted");
                }
            }
            else
            {
                HelperMethods.ReplyToCommand_Player(playerToKick, "is immune due to level");
            }
        }

        public void Command_AFK(Il2Cpp.Player callerPlayer, String args)
        {
            // validate argument count
            int argumentCount = args.Split(' ').Count() - 1;
            if (argumentCount > 0)
            {
                HelperMethods.ReplyToCommand(args.Split(' ')[0] + ": Too many arguments");
                return;
            }

            HelperMethods.ReplyToCommand(args.Split(' ')[0] + ": " + AFKTracker.Count.ToString() + " players with AFK counters");
        }

        private static void timerCallbackOneMinute(object source, ElapsedEventArgs e)
        {
            oneMinuteCheckTime = true;
        }

        [HarmonyPatch(typeof(Il2Cpp.MusicJukeboxHandler), nameof(Il2Cpp.MusicJukeboxHandler.Update))]
        private static class ApplyPatch_MusicJukeboxHandlerUpdate
        {
            private static void Postfix(Il2Cpp.MusicJukeboxHandler __instance)
            {
                try
                {
                    // check if timer expired while the game is in-progress
                    if (Il2Cpp.GameMode.CurrentGameMode.GameOngoing == true && oneMinuteCheckTime == true)
                    {
                        MelonLogger.Msg("AFK Timer fired");

                        oneMinuteCheckTime = false;
                        
                        foreach (Il2Cpp.Player player in Il2Cpp.Player.Players)
                        {
                            if (player != null)
                            {
                                if (player.Team == null)
                                {
                                    MelonLogger.Msg("Found " + player.PlayerName + " didn't pick a team yet");
                                    int afkIndex = AFKTracker.FindIndex(p => p.Player == player);
                                    // they were AFK for another minute
                                    if (afkIndex >= 0)
                                    {
                                        AFKTracker[afkIndex].Minutes += 1;
                                    }
                                    // they weren't being tracked yet
                                    else
                                    {
                                        AFKCount afkPlayer = new AFKCount();
                                        afkPlayer.Player = player;
                                        afkPlayer.Minutes = 1;

                                        AFKTracker.Add(afkPlayer);
                                    }

                                    // TODO: Check if they were AFK for too long
                                }
                            }
                        }
                    }
                }
                catch (Exception exception)
                {
                    HelperMethods.PrintError(exception, "Failed in MusicJukeboxHandler::Update");
                }
            }
        }

        // clear all AFK counters
        [HarmonyPatch(typeof(Il2Cpp.MusicJukeboxHandler), nameof(Il2Cpp.MusicJukeboxHandler.OnGameEnded))]
        private static class ApplyPatchOnGameEnded
        {
            public static void Postfix(Il2Cpp.MusicJukeboxHandler __instance, Il2Cpp.GameMode __0, Il2Cpp.Team __1)
            {
                try
                {
                    AFKTracker.Clear();
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run MusicJukeboxHandler::OnGameEnded");
                }
            }
        }
    }
}