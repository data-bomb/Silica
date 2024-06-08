/*
Silica AFK Manager
Copyright (C) 2023-2024 by databomb

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

#if NET6_0
using Il2Cpp;
#endif

using HarmonyLib;
using MelonLoader;
using Si_AFKManager;
using System.Collections.Generic;
using System.Linq;
using SilicaAdminMod;
using System;
using System.Collections;
using UnityEngine;

[assembly: MelonInfo(typeof(AwayFromKeyboard), "AFK Manager", "1.3.2", "databomb", "https://github.com/data-bomb/Silica")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]
[assembly: MelonOptionalDependencies("Admin Mod")]

namespace Si_AFKManager
{
    public class AwayFromKeyboard : MelonMod
    {
        public class AFKCount
        {
            private Player _player = null!;

            public Player Player
            { 
                get => _player;
                set => _player = value ?? throw new ArgumentNullException("Player is required.");
            }
            public uint Minutes
            {
                get; 
                set;
            }
        }

        static float Timer_AFKCheck = HelperMethods.Timer_Inactive;
        static bool AdminModAvailable = false;
        static List<AFKCount> AFKTracker = null!;
        static bool skippedFirstCheck;

        static MelonPreferences_Category _modCategory = null!;
        static MelonPreferences_Entry<bool> Pref_AFK_KickIfServerNotFull = null!;
        static MelonPreferences_Entry<int> Pref_AFK_MinutesBeforeKick = null!;

        public override void OnInitializeMelon()
        {
            _modCategory ??= MelonPreferences.CreateCategory("Silica");
            Pref_AFK_KickIfServerNotFull ??= _modCategory.CreateEntry<bool>("AFK_KickIfServerNotFull", false);
            Pref_AFK_MinutesBeforeKick ??= _modCategory.CreateEntry<int>("AFK_MinutesBeforeKick", 7);
        }

        public override void OnLateInitializeMelon()
        {
            HelperMethods.StartTimer(ref Timer_AFKCheck);

            AdminModAvailable = RegisteredMelons.Any(m => m.Info.Name == "Admin Mod");

            AFKTracker = new List<AFKCount>();

            HelperMethods.CommandCallback kickCallback = Command_Kick;
            HelperMethods.CommandCallback afkCallback = Command_AFK;
            HelperMethods.RegisterAdminCommand("kick", kickCallback, Power.Kick, "Kicks target player. Usage: !kick <player>");
            HelperMethods.RegisterAdminCommand("afk", afkCallback, Power.Kick, "Kicks any AFK players immediately. Usage: !afk");

            #if NET6_0
            bool QListLoaded = RegisteredMelons.Any(m => m.Info.Name == "QList");
            if (!QListLoaded)
            {
                return;
            }

            QList.Options.RegisterMod(this);

            QList.OptionTypes.BoolOption kickWhenNotFull = new(Pref_AFK_KickIfServerNotFull, Pref_AFK_KickIfServerNotFull.Value);
            QList.OptionTypes.IntOption minutesBeforeKick = new(Pref_AFK_MinutesBeforeKick, true, Pref_AFK_MinutesBeforeKick.Value, 1, 60);

            QList.Options.AddOption(kickWhenNotFull);
            QList.Options.AddOption(minutesBeforeKick);
            #endif
        }

        public static bool ServerAlmostFull()
        {
            if (Player.Players.Count + 2 >= NetworkGameServer.GetPlayersMax())
            {
                return true;
            }

            return false;
        }

        public static void Command_Kick(Player? callerPlayer, String args)
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
            Player? playerToKick = HelperMethods.FindTargetPlayer(sTarget);

            if (playerToKick == null)
            {
                HelperMethods.ReplyToCommand(args.Split(' ')[0] + ": Ambiguous or invalid target");
                return;
            }

            if (callerPlayer != null && !callerPlayer.CanAdminTarget(playerToKick))
            {
                HelperMethods.ReplyToCommand_Player(playerToKick, "is immune due to level");
                return;
            }

            if (HelperMethods.KickPlayer(playerToKick))
            {
                HelperMethods.AlertAdminActivity(callerPlayer, playerToKick, "kicked");
            }
            else
            {
                HelperMethods.ReplyToCommand_Player(playerToKick, "is the host and cannot be targeted");
            }
        }

        public static void Command_AFK(Player? callerPlayer, String args)
        {
            string commandName = args.Split(' ')[0];
            
            // validate argument count
            int argumentCount = args.Split(' ').Length - 1;
            if (argumentCount > 0)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Too many arguments");
                return;
            }

            if (Pref_AFK_KickIfServerNotFull == null || Pref_AFK_MinutesBeforeKick == null)
            {
                return;
            }

            // force kick now
            if (Pref_AFK_KickIfServerNotFull.Value)
            {
                // track if any players need to be removed from the AFKTracker list after we've finished iterating
                // we can't kick inside the foreach Players iterator because it modifies the list
                List<Player>? playersToKick = new List<Player>();

                foreach (Player player in Player.Players)
                {
                    if (player == null)
                    {
                        continue;
                    }

                    if (player.Team != null)
                    {
                        continue;
                    }

                    int afkIndex = AFKTracker.FindIndex(p => p.Player == player);
                    // they were AFK for some time
                    if (afkIndex >= 0)
                    {
                        MelonLogger.Msg("Found player " + AFKTracker[afkIndex].Player.PlayerName + " AFK for " + AFKTracker[afkIndex].Minutes.ToString());

                        if (AFKTracker[afkIndex].Minutes >= Pref_AFK_MinutesBeforeKick.Value)
                        {
                            // kick immediately
                            playersToKick.Add(player);
                        }
                    }
                }

                if(playersToKick.Count <= 0)
                {
                    HelperMethods.ReplyToCommand(args.Split(' ')[0] + ": no players were AFK for too long");
                }
                else
                {
                    foreach (Player playerToKick in playersToKick)
                    {
                        if (playerToKick == null)
                        {
                            continue;
                        }

                        HelperMethods.KickPlayer(playerToKick);
                        HelperMethods.ReplyToCommand_Player(playerToKick, "was kicked for being AFK");
                        int afkIndexToRemove = AFKTracker.FindIndex(p => p.Player == playerToKick);
                        if (afkIndexToRemove >= 0)
                        {
                            AFKTracker.RemoveAt(afkIndexToRemove);
                        }
                    }
                }
            }
            else
            {
                HelperMethods.ReplyToCommand(args.Split(' ')[0] + ": server already immediately kicks AFK players");
            }
        }

        #if NET6_0
        [HarmonyPatch(typeof(MusicJukeboxHandler), nameof(MusicJukeboxHandler.Update))]
        #else
        [HarmonyPatch(typeof(MusicJukeboxHandler), "Update")]
        #endif
        private static class ApplyPatch_MusicJukeboxHandler_Update
        {
            private static void Postfix(MusicJukeboxHandler __instance)
            {
                try
                {
                    if (Pref_AFK_MinutesBeforeKick == null || Pref_AFK_KickIfServerNotFull == null)
                    {
                        return;
                    }

                    // check if timer expired while the game is in-progress
                    Timer_AFKCheck += Time.deltaTime;
                    if (Timer_AFKCheck >= 60.0f)
                    {
                        Timer_AFKCheck = 0.0f;

                        if (!GameMode.CurrentGameMode.GameOngoing)
                        {
                            return;
                        }

                        // skip the first timer expiration so we're at least a minute into the round
                        if (!skippedFirstCheck)
                        {
                            skippedFirstCheck = true;
                            return;
                        }

                        // track if any players need to be removed from the AFKTracker list after we've finished iterating
                        // we can't kick inside the foreach Players iterator because it modifies the list
                        List<Player>? playersToKick = new List<Player>();

                        // remove players in a seperate loop
                        foreach (Player player in Player.Players)
                        {
                            if (player == null)
                            {
                                continue;
                            }

                            // for now, we'll only care about people who idle and don't join a team
                            if (player.Team == null)
                            {
                                continue;
                            }

                            int afkIndex = AFKTracker.FindIndex(p => p.Player == player);
                            // if they've joined a team then remove them from the AFK tracker
                            if (afkIndex >= 0)
                            {
                                MelonLogger.Msg("Removing " + player.PlayerName + " from AFK list for being on a team.");
                                AFKTracker.RemoveAt(afkIndex);
                            }
                        }

                        foreach (Player player in Player.Players)
                        {
                            if (player == null)
                            {
                                continue;
                            }

                            // for now, we'll only care about people who idle and don't join a team
                            if (player.Team != null)
                            {
                                continue;
                            }

                            int afkIndex = AFKTracker.FindIndex(p => p.Player == player);

                            Player serverPlayer = NetworkGameServer.GetServerPlayer();
                            if (player == serverPlayer)
                            {
                                continue;
                            }

                            // they were AFK for another minute
                            if (afkIndex >= 0)
                            {
                                AFKTracker[afkIndex].Minutes += 1;

                                if (AFKTracker[afkIndex].Minutes >= Pref_AFK_MinutesBeforeKick.Value)
                                {
                                    // kick immediately
                                    if (Pref_AFK_KickIfServerNotFull.Value)
                                    {
                                        playersToKick.Add(player);
                                    }
                                    // only kick if server is almost full
                                    else
                                    {
                                        if (ServerAlmostFull())
                                        {
                                            playersToKick.Add(player);
                                        }
                                    }
                                }
                            }
                            // they weren't being tracked yet
                            else
                            {
                                AFKCount afkPlayer = new AFKCount();
                                afkPlayer.Player = player;
                                afkPlayer.Minutes = 1;

                                AFKTracker.Add(afkPlayer);
                            }
                        }

                        foreach (Player playerToKick in playersToKick)
                        {
                            if (playerToKick == null)
                            {
                                continue;
                            }

                            HelperMethods.KickPlayer(playerToKick);
                            HelperMethods.ReplyToCommand_Player(playerToKick, "was kicked for being AFK");
                            int afkIndexToRemove = AFKTracker.FindIndex(p => p.Player == playerToKick);
                            if (afkIndexToRemove >= 0)
                            {
                                AFKTracker.RemoveAt(afkIndexToRemove);
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

        [HarmonyPatch(typeof(MusicJukeboxHandler), nameof(MusicJukeboxHandler.OnGameStarted))]
        private static class ApplyPatch_MusicJukeboxHandler_OnGameStarted
        {
            public static void Postfix(MusicJukeboxHandler __instance, GameMode __0)
            {
                try
                {
                    skippedFirstCheck = false;
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run MusicJukeboxHandler::OnGameStarted");
                }
            }
        }

        // clear all AFK counters
        [HarmonyPatch(typeof(MusicJukeboxHandler), nameof(MusicJukeboxHandler.OnGameEnded))]
        private static class ApplyPatch_MusicJukeboxHandler_OnGameEnded
        {
            public static void Postfix(MusicJukeboxHandler __instance, GameMode __0, Team __1)
            {
                try
                {
                    skippedFirstCheck = false;
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