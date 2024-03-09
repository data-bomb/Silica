/*
Silica Map Cycle
Copyright (C) 2023-2024 by databomb

* Description *
Provides map management and cycles to a server.

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
# endif

using HarmonyLib;
using MelonLoader;
using Si_Mapcycle;
using System.Timers;
using MelonLoader.Utils;
using System;
using System.IO;
using System.Collections.Generic;
using SilicaAdminMod;
using System.Linq;

[assembly: MelonInfo(typeof(MapCycleMod), "Mapcycle", "1.4.1", "databomb", "https://github.com/data-bomb/Silica")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]
[assembly: MelonOptionalDependencies("Admin Mod")]

namespace Si_Mapcycle
{
    public class MapCycleMod : MelonMod
    {
        static String mapName = "";
        static bool bEndRound;
        static bool bTimerExpired;
        static int iMapLoadCount;
        static List<Player> rockers = null!;
        static string[]? sMapCycle;

        private static System.Timers.Timer? DelayTimer;

        public override void OnInitializeMelon()
        {
            String mapCycleFile = MelonEnvironment.UserDataDirectory + "\\mapcycle.txt";

            try
            {
                rockers = new List<Player>();

                if (!File.Exists(mapCycleFile))
                {
                    // Create simple mapcycle.txt file
                    using (FileStream fs = File.Create(mapCycleFile))
                    {
                        fs.Close();
                        System.IO.File.WriteAllText(mapCycleFile, "RiftBasin\nGreatErg\nBadlands\nNarakaCity\n");
                    }
                }

                // Open the stream and read it back.
                using StreamReader mapFileStream = File.OpenText(mapCycleFile);
                List<string> sMapList = new List<string>();
                string? sMap = "";

                while ((sMap = mapFileStream.ReadLine()) != null)
                {
                    sMapList.Add(sMap);
                }
                sMapCycle = sMapList.ToArray();
            }
            catch (Exception exception)
            {
                MelonLogger.Msg(exception.ToString());
            }
        }

        public override void OnLateInitializeMelon()
        {
            HelperMethods.CommandCallback mapCallback = Command_ChangeMap;
            HelperMethods.RegisterAdminCommand("map", mapCallback, Power.Map);

            HelperMethods.CommandCallback rockthevoteCallback = Command_RockTheVote;
            HelperMethods.RegisterPlayerPhrase("rtv", rockthevoteCallback, true);
            HelperMethods.RegisterPlayerPhrase("rockthevote", rockthevoteCallback, true);

            HelperMethods.CommandCallback currentmapCallback = Command_CurrentMap;
            HelperMethods.RegisterPlayerPhrase("currentmap", currentmapCallback, false);
        }

        public static void Command_RockTheVote(Player callerPlayer, String args)
        {
            // check if game on-going
            if (!GameMode.CurrentGameMode.GameOngoing)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, " Can't rock the vote. Game not started.");
                return;
            }

            // did we already RTV
            if (rockers.Contains(callerPlayer))
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, " Already rocked the vote. ", MoreRocksNeededForVote().ToString(), " more needed.");
                return;
            }

            rockers.Add(callerPlayer);
            if (rockers.Count() < RocksNeededForVote())
            {
                HelperMethods.ReplyToCommand_Player(callerPlayer, "rocked the vote. ", MoreRocksNeededForVote().ToString(), " more needed.");
                return;
            }
            
            if (ChatVotes.IsVoteInProgress())
            {
                HelperMethods.ReplyToCommand_Player(callerPlayer, "rocked the vote. Another vote in progress. ", MoreRocksNeededForVote().ToString(), " more needed later.");
                return;
            }

            ChatVoteBallot? rtvBallot = CreateRTVBallot();
            if (rtvBallot == null)
            {
                HelperMethods.ReplyToCommand_Player(callerPlayer, "rocked the vote. Currently unavailable. ", MoreRocksNeededForVote().ToString(), " more needed later.");
                return;
            }

            ChatVotes.HoldVote(rtvBallot);
        }

        public static ChatVoteBallot? CreateRTVBallot()
        {
            if (sMapCycle == null)
            {
                return null;
            }

            OptionPair[] rtvOptions = new OptionPair[4];
            for (int i = 0; i < 3; i++)
            {
                rtvOptions[i] = new OptionPair();

                rtvOptions[i].Command = (i+1).ToString();
                rtvOptions[i].Description = sMapCycle[(iMapLoadCount + 1 + i) % (sMapCycle.Length - 1)];
            }

            rtvOptions[3] = new OptionPair
            {
                Command = "4",
                Description = "Keep current map"
            };

            ChatVoteBallot rtvBallot = new ChatVoteBallot
            {
                Question = "Select the next map:",
                VoteHandler = RockTheVote_Handler,
                Options = rtvOptions
            };

            return rtvBallot;
        }

        public static void RockTheVote_Handler(ChatVoteResults results)
        {
            try
            {
                MelonLogger.Msg("Reached vote handler for Rock the Vote. Winning result was: " + results.WinningCommand);
                if (sMapCycle == null)
                {
                    return;
                }

                HelperMethods.ReplyToCommand("rtv voting now closed");
                int winningNumber = int.Parse(results.WinningCommand);
                if (winningNumber == 4)
                {
                    HelperMethods.ReplyToCommand("Staying on current map.");
                    return;
                }

                string winningMap = sMapCycle[(iMapLoadCount + winningNumber) % (sMapCycle.Length - 1)];
                HelperMethods.ReplyToCommand("Switching to " + winningMap);

                MelonLogger.Msg("Changing map to " + winningMap + "...");

                NetworkGameServer.LoadLevel(winningMap, GameMode.CurrentGameMode.GameModeInfo);
            }
            catch (Exception exception)
            {
                MelonLogger.Msg(exception.ToString());
            }
        }

        public static int MoreRocksNeededForVote()
        {
            int rocksNeeded = RocksNeededForVote();
            int moreNeeded = rocksNeeded - rockers.Count();
            if (moreNeeded < 1)
            {
                return 1;
            }

            return moreNeeded;
        }

        public static int RocksNeededForVote()
        {
            int totalPlayers = Player.Players.Count;
            int rocksNeeded = (int)Math.Ceiling(totalPlayers * 0.31f);
            if (rocksNeeded < 1)
            {
                return 1;
            }

            return rocksNeeded;
        }

        public static void Command_CurrentMap(Player callerPlayer, String args)
        {
            HelperMethods.ReplyToCommand("Current map is " + mapName);
        }
        
        public static void Command_NextMap(Player callerPlayer, String args)
        {
            if (sMapCycle == null)
            {
                return;
            }

            HelperMethods.ReplyToCommand("Next map is " + sMapCycle[(iMapLoadCount + 1) % (sMapCycle.Length - 1)]);
        }

        public static void Command_ChangeMap(Player callerPlayer, String args)
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

            // validate argument
            if (sMapCycle == null)
            {
                return;
            }

            String targetMapName = args.Split(' ')[1];
            bool validMap = sMapCycle.Any(k => k == targetMapName);
            if (!validMap)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Invalid map name");
                return;
            }

            HelperMethods.AlertAdminAction(callerPlayer, "changing map to " + targetMapName + "...");


            MelonLogger.Msg("Changing map to " + targetMapName + "...");

            NetworkGameServer.LoadLevel(targetMapName, GameMode.CurrentGameMode.GameModeInfo);
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            rockers.Clear();
            mapName = sceneName;
        }

        private static void HandleTimerChangeLevel(object? source, ElapsedEventArgs e)
        {
            MapCycleMod.bTimerExpired = true;
        }

        #if NET6_0
        [HarmonyPatch(typeof(MusicJukeboxHandler), nameof(MusicJukeboxHandler.Update))]
        #else
        [HarmonyPatch(typeof(MusicJukeboxHandler), "Update")]
        #endif
        private static class ApplyPatch_MusicJukeboxHandlerUpdate
        {
            private static void Postfix(MusicJukeboxHandler __instance)
            {
                // check if timer expired
                if (MapCycleMod.bEndRound == true && MapCycleMod.bTimerExpired == true && sMapCycle != null)
                {
                    MapCycleMod.bEndRound = false;
                    MapCycleMod.iMapLoadCount++;

                    String sNextMap = sMapCycle[iMapLoadCount % (sMapCycle.Length-1)];

                    MelonLogger.Msg("Changing map to " + sNextMap + "...");
                    NetworkGameServer.LoadLevel(sNextMap, GameMode.CurrentGameMode.GameModeInfo);
                }
            }
        }

        [HarmonyPatch(typeof(MusicJukeboxHandler), nameof(MusicJukeboxHandler.OnGameEnded))]
        private static class ApplyPatch_OnGameEnded
        {
            public static void Postfix(MusicJukeboxHandler __instance, GameMode __0, Team __1)
            {
                if (sMapCycle != null)
                {
                    HelperMethods.ReplyToCommand("Preparing to change map to " + sMapCycle[(iMapLoadCount + 1) % (sMapCycle.Length - 1)] + "...");
                }
                MapCycleMod.bEndRound = true;
                MapCycleMod.bTimerExpired = false;

                double interval = 12000.0;
                MapCycleMod.DelayTimer = new System.Timers.Timer(interval);
                MapCycleMod.DelayTimer.Elapsed += new ElapsedEventHandler(HandleTimerChangeLevel);
                MapCycleMod.DelayTimer.AutoReset = false;
                MapCycleMod.DelayTimer.Enabled = true;
            }
        }
    }
}