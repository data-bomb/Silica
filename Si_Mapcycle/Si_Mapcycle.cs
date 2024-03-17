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

[assembly: MelonInfo(typeof(MapCycleMod), "Mapcycle", "1.4.5", "databomb", "https://github.com/data-bomb/Silica")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]
[assembly: MelonOptionalDependencies("Admin Mod")]

namespace Si_Mapcycle
{
    public class MapCycleMod : MelonMod
    {
        static String mapName = "";
        static bool bEndRound;
        static bool endroundChangeLevelTimerExpired;
        static bool rtvFinalChangeTimerExpired;
        static bool rtvChangeLevelTimerExpired;
        static int iMapLoadCount;
        static int roundsOnSameMap;
        static List<Player> rockers = null!;
        static string[]? sMapCycle;
        static string rockthevoteWinningMap = "";

        static MelonPreferences_Category _modCategory = null!;
        static MelonPreferences_Entry<int> Pref_Mapcycle_RoundsBeforeChange = null!;
        static MelonPreferences_Entry<int> Pref_Mapcycle_EndgameDelay = null!;

        static System.Timers.Timer? EndRoundDelayTimer;
        static System.Timers.Timer? InitialPostVoteDelayTimer;
        static System.Timers.Timer? FinalPostVoteDelayTimer;

        public override void OnInitializeMelon()
        {
            _modCategory ??= MelonPreferences.CreateCategory("Silica");
            Pref_Mapcycle_RoundsBeforeChange ??= _modCategory.CreateEntry<int>("Mapcycle_RoundsBeforeMapChange", 4);
            Pref_Mapcycle_EndgameDelay ??= _modCategory.CreateEntry<int>("Mapcycle_DelayBeforeEndgameMapChange_Seconds", 7);

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
            HelperMethods.RegisterPlayerCommand("rtv", rockthevoteCallback, true);
            HelperMethods.RegisterPlayerPhrase("rockthevote", rockthevoteCallback, true);

            HelperMethods.CommandCallback currentmapCallback = Command_CurrentMap;
            HelperMethods.RegisterPlayerPhrase("currentmap", currentmapCallback, true);

            HelperMethods.CommandCallback nextmapCallback = Command_NextMap;
            HelperMethods.RegisterPlayerPhrase("nextmap", nextmapCallback, true);
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
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, " Already used rtv. ", MoreRocksNeededForVote().ToString(), " more needed.");
                return;
            }

            rockers.Add(callerPlayer);
            if (rockers.Count < RocksNeededForVote())
            {
                HelperMethods.ReplyToCommand_Player(callerPlayer, "wants to rock the vote. ", MoreRocksNeededForVote().ToString(), " more needed.");
                return;
            }
            
            if (ChatVotes.IsVoteInProgress())
            {
                HelperMethods.ReplyToCommand_Player(callerPlayer, "wants to rock the vote. Another vote already in progress. Wait before trying again.");
                return;
            }

            ChatVoteBallot? rtvBallot = CreateRTVBallot();
            if (rtvBallot == null)
            {
                HelperMethods.ReplyToCommand_Player(callerPlayer, "rocked the vote. Currently unavailable. ", MoreRocksNeededForVote().ToString(), " more needed later.");
                return;
            }

            HelperMethods.ReplyToCommand_Player(callerPlayer, "rocked the vote.");
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
                rtvOptions[i] = new OptionPair
                {
                    Command = (i + 1).ToString(),
                    Description = sMapCycle[(iMapLoadCount + 1 + i) % (sMapCycle.Length - 1)]
                };
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
            MelonLogger.Msg("Reached vote handler for Rock the Vote. Winning result was: " + results.WinningCommand);
            rockers.Clear();

            if (sMapCycle == null)
            {
                MelonLogger.Error("mapcycle is null");
                return;
            }

            switch (results.WinningCommand)
            {
                case "1":
                {
                    rockthevoteWinningMap = sMapCycle[(iMapLoadCount + 1) % (sMapCycle.Length - 1)];
                    break;
                }
                case "2":
                {
                    rockthevoteWinningMap = sMapCycle[(iMapLoadCount + 2) % (sMapCycle.Length - 1)];
                    break;
                }
                case "3":
                {
                    rockthevoteWinningMap = sMapCycle[(iMapLoadCount + 3) % (sMapCycle.Length - 1)];
                    break;
                }
                case "4":
                {
                    rockthevoteWinningMap = "";
                    break;
                }
                default:
                {
                    rockthevoteWinningMap = "";
                    MelonLogger.Warning("Reached invalid result");
                    break;
                }
            }

            double interval = 1000.0;
            MapCycleMod.InitialPostVoteDelayTimer = new System.Timers.Timer(interval);
            MapCycleMod.InitialPostVoteDelayTimer.Elapsed += new ElapsedEventHandler(HandleTimerRockTheVote);
            MapCycleMod.InitialPostVoteDelayTimer.AutoReset = false;
            MapCycleMod.InitialPostVoteDelayTimer.Enabled = true;
        }

        public static int MoreRocksNeededForVote()
        {
            int rocksNeeded = RocksNeededForVote();
            int moreNeeded = rocksNeeded - rockers.Count;
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
            HelperMethods.ReplyToCommand_Player(callerPlayer, ": The current map is " + mapName);
        }
        
        public static void Command_NextMap(Player callerPlayer, String args)
        {
            if (sMapCycle == null)
            {
                return;
            }

            int roundsLeft = Pref_Mapcycle_RoundsBeforeChange.Value - roundsOnSameMap;
            HelperMethods.ReplyToCommand_Player(callerPlayer, ": The next map is " + sMapCycle[(iMapLoadCount + 1) % (sMapCycle.Length - 1)] + ". " + roundsLeft.ToString() + " more round" + (roundsLeft == 1 ? "" : "s") + " before map changes.");
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
            roundsOnSameMap = 0;
            mapName = sceneName;
        }

        private static void HandleTimerChangeLevel(object? source, ElapsedEventArgs e)
        {
            endroundChangeLevelTimerExpired = true;
        }

        private static void HandleTimerRockTheVote(object? source, ElapsedEventArgs e)
        {
            rtvChangeLevelTimerExpired = true;
        }

        private static void HandleTimerRockTheVoteFinal(object? source, ElapsedEventArgs e)
        {
            rtvFinalChangeTimerExpired = true;
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
                // check if timers expired
                if (rtvChangeLevelTimerExpired)
                {
                    rtvChangeLevelTimerExpired = false;

                    HelperMethods.ReplyToCommand("Rock the vote finished.");
                    if (rockthevoteWinningMap == "")
                    {
                        HelperMethods.ReplyToCommand("Staying on current map.");
                        return;
                    }

                    roundsOnSameMap = 0;
                    HelperMethods.ReplyToCommand("Preparing to change map to " + rockthevoteWinningMap + "...");

                    double interval = 6000.0;
                    MapCycleMod.FinalPostVoteDelayTimer = new System.Timers.Timer(interval);
                    MapCycleMod.FinalPostVoteDelayTimer.Elapsed += new ElapsedEventHandler(HandleTimerRockTheVoteFinal);
                    MapCycleMod.FinalPostVoteDelayTimer.AutoReset = false;
                    MapCycleMod.FinalPostVoteDelayTimer.Enabled = true;
                    
                    return;
                }

                if (rtvFinalChangeTimerExpired)
                {
                    rtvFinalChangeTimerExpired = false;
                    MelonLogger.Msg("Changing map to " + rockthevoteWinningMap + "...");
                    NetworkGameServer.LoadLevel(rockthevoteWinningMap, GameMode.CurrentGameMode.GameModeInfo);
                    return;
                }

                if (sMapCycle == null)
                {
                    return;
                }

                if (bEndRound)
                {
                    // if rtv timers are running then don't call the endround timer
                    if ((FinalPostVoteDelayTimer != null && FinalPostVoteDelayTimer.Enabled) ||
                        (InitialPostVoteDelayTimer != null && InitialPostVoteDelayTimer.Enabled))
                    {
                        MelonLogger.Warning("RTV timers running when end round vote would have happened.");
                        bEndRound = false;
                        return;
                    }

                    if (!endroundChangeLevelTimerExpired)
                    {
                        return;
                    }

                    bEndRound = false;
                    iMapLoadCount++;

                    String sNextMap = sMapCycle[iMapLoadCount % (sMapCycle.Length-1)];

                    MelonLogger.Msg("Changing map to " + sNextMap + "...");
                    NetworkGameServer.LoadLevel(sNextMap, GameMode.CurrentGameMode.GameModeInfo);

                    return;
                }
            }
        }

        [HarmonyPatch(typeof(MusicJukeboxHandler), nameof(MusicJukeboxHandler.OnGameEnded))]
        private static class ApplyPatch_OnGameEnded
        {
            public static void Postfix(MusicJukeboxHandler __instance, GameMode __0, Team __1)
            {
                if (sMapCycle == null)
                {
                    return;
                }

                if (bEndRound)
                {
                    return;
                }

                bEndRound = true;

                roundsOnSameMap++;
                if (Pref_Mapcycle_RoundsBeforeChange.Value > roundsOnSameMap)
                {
                    int roundsLeft = Pref_Mapcycle_RoundsBeforeChange.Value - roundsOnSameMap;
                    HelperMethods.ReplyToCommand("Current map will change after " + roundsLeft.ToString() + " more round" + (roundsLeft == 1 ? "." : "s."));
                    return;
                }

                if (EndRoundDelayTimer != null && EndRoundDelayTimer.Enabled)
                {
                    MelonLogger.Warning("End round delay timer already started.");
                    return;
                }

                HelperMethods.ReplyToCommand("Preparing to change map to " + sMapCycle[(iMapLoadCount + 1) % (sMapCycle.Length - 1)] + "....");
                endroundChangeLevelTimerExpired = false;

                double interval = Pref_Mapcycle_EndgameDelay.Value * 1000.0f;
                MapCycleMod.EndRoundDelayTimer = new System.Timers.Timer(interval);
                MapCycleMod.EndRoundDelayTimer.Elapsed += new ElapsedEventHandler(HandleTimerChangeLevel);
                MapCycleMod.EndRoundDelayTimer.AutoReset = false;
                MapCycleMod.EndRoundDelayTimer.Enabled = true;
            }
        }
    }
}