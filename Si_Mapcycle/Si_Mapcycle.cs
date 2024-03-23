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
using Il2CppDebugTools;
#else
using DebugTools;
#endif

using HarmonyLib;
using MelonLoader;
using Si_Mapcycle;
using MelonLoader.Utils;
using System;
using System.IO;
using System.Collections.Generic;
using SilicaAdminMod;
using System.Linq;
using UnityEngine;
using System.ComponentModel.Design;

[assembly: MelonInfo(typeof(MapCycleMod), "Mapcycle", "1.5.3", "databomb", "https://github.com/data-bomb/Silica")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]
[assembly: MelonOptionalDependencies("Admin Mod")]

namespace Si_Mapcycle
{
    public class MapCycleMod : MelonMod
    {
        static String mapName = "";
        static int iMapLoadCount;
        static bool firedRoundEndOnce;
        static int roundsOnSameMap;
        static List<Player> rockers = null!;
        static List<String> mapNominations = null!;
        static string[]? sMapCycle;
        static string rockthevoteWinningMap = "";

        static MelonPreferences_Category _modCategory = null!;
        static MelonPreferences_Entry<int> Pref_Mapcycle_RoundsBeforeChange = null!;
        static MelonPreferences_Entry<int> Pref_Mapcycle_EndgameDelay = null!;

        static float Timer_EndRoundDelay = HelperMethods.Timer_Inactive;
        static float Timer_InitialPostVoteDelay = HelperMethods.Timer_Inactive;
        static float Timer_FinalPostVoteDelay = HelperMethods.Timer_Inactive;

        public override void OnInitializeMelon()
        {
            try
            {
                _modCategory ??= MelonPreferences.CreateCategory("Silica");
                Pref_Mapcycle_RoundsBeforeChange ??= _modCategory.CreateEntry<int>("Mapcycle_RoundsBeforeMapChange", 4);
                Pref_Mapcycle_EndgameDelay ??= _modCategory.CreateEntry<int>("Mapcycle_DelayBeforeEndgameMapChange_Seconds", 33);

                String mapCycleFile = MelonEnvironment.UserDataDirectory + "\\mapcycle.txt";

                rockers = new List<Player>();
                mapNominations = new List<String>();

                if (!File.Exists(mapCycleFile))
                {
                    // Create simple mapcycle.txt file
                    using FileStream mapcycleFileStream = File.Create(mapCycleFile);
                    mapcycleFileStream.Close();
                    File.WriteAllText(mapCycleFile, "RiftBasin\nGreatErg\nBadlands\nNarakaCity\n");
                }

                // Open the stream and read it back.
                using StreamReader mapcycleStreamReader = File.OpenText(mapCycleFile);
                List<string> sMapList = new List<string>();
                string? sMap = "";

                while ((sMap = mapcycleStreamReader.ReadLine()) != null)
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
            HelperMethods.RegisterPlayerCommand("rockthevote", rockthevoteCallback, true);

            HelperMethods.CommandCallback nominateCallback = Command_Nominate;
            HelperMethods.RegisterPlayerCommand("nominate", nominateCallback, true);

            HelperMethods.CommandCallback currentmapCallback = Command_CurrentMap;
            HelperMethods.RegisterPlayerPhrase("currentmap", currentmapCallback, true);

            HelperMethods.CommandCallback nextmapCallback = Command_NextMap;
            HelperMethods.RegisterPlayerPhrase("nextmap", nextmapCallback, true);

            // validate contents of mapcycle now that game database is available
            if (sMapCycle == null)
            {
                return;
            }

            foreach (string mapName in sMapCycle)
            {
                if (!IsMapNameValid(mapName))
                {
                    MelonLogger.Error("Invalid map found in mapcycle.txt: " + mapName);
                }
            }
        }

        public static void Command_RockTheVote(Player? callerPlayer, String args)
        {
			if (callerPlayer == null)
			{
				HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, " Console not supported.");
				return;
			}
			
            // check if game on-going
            if (!GameMode.CurrentGameMode.GameOngoing)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, " Can't rock the vote. Game not started.");
                return;
            }

            // is the end-game timer already preparing to switch
            if (HelperMethods.IsTimerActive(Timer_EndRoundDelay))
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, " Can't rock the vote. Map change already in progress.");
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
                HelperMethods.ReplyToCommand_Player(callerPlayer, "wants to rock the vote. Another vote is in progress. Wait before trying again.");
                return;
            }

            ChatVoteBallot? rtvBallot = CreateRTVBallot();
            if (rtvBallot == null)
            {
                HelperMethods.ReplyToCommand_Player(callerPlayer, "rocked the vote. Currently unavailable. Wait before trying again.");
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

            int rtvIndex = 0;

            // add nominated maps first
            if (mapNominations.Count > 0)
            {
                foreach (String mapName in mapNominations)
                {
                    rtvOptions[rtvIndex] = new OptionPair
                    {
                        Command = (rtvIndex + 1).ToString(),
                        Description = mapName
                    };

                    rtvIndex++;
                }
            }

            // then remaining from the mapcycle, if any
            for (int i = rtvIndex; i < 3; i++)
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
                Description = "Keep Current Map"
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

            // should we continue or has the game already ended or is in the progress of ending?
            if (!GameMode.CurrentGameMode.GameOngoing || HelperMethods.IsTimerActive(Timer_EndRoundDelay))
            {
                mapNominations.Clear();
                MelonLogger.Warning("Cancelling Rock the Vote handling. Round is not currently active.");
                return;
            }

            if (sMapCycle == null)
            {
                mapNominations.Clear();
                MelonLogger.Error("mapcycle is null");
                return;
            }

            int winningIndex = int.Parse(results.WinningCommand);
            MelonLogger.Msg("Reached vote handler for Rock the Vote. Winning result was: " + winningIndex.ToString());
            if (winningIndex == 4)
            {
                rockthevoteWinningMap = "";
            }
            else if (winningIndex <= mapNominations.Count)
            {
                // winning map was a nomination
                rockthevoteWinningMap = mapNominations[winningIndex-1];
            }
            else
            {
                // winning map wasn't a nomination or "Keep" option
                rockthevoteWinningMap = sMapCycle[(iMapLoadCount + winningIndex) % (sMapCycle.Length - 1)];
            }

            mapNominations.Clear();
            MelonLogger.Msg("Winning map name: " + rockthevoteWinningMap);

            HelperMethods.StartTimer(ref Timer_InitialPostVoteDelay);
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

        public static void Command_Nominate(Player? callerPlayer, String args)
        {
			if (callerPlayer == null)
			{
				HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, " Console not supported.");
				return;
			}
			
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
            if (!IsMapNameValid(targetMapName))
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Invalid map name");
                return;
            }

            bool alreadyNominated = mapNominations.Any(k => k == targetMapName);
            if (alreadyNominated)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Map already nominated");
                return;
            }

            // do we already have enough nominations?
            if (mapNominations.Count > 2)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Too many map nominations already received");
                return;
            }

            if (ChatVotes.IsVoteInProgress())
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, " Can't nominate a map because a vote is in progress.");
                return;
            }

            if (!GameMode.CurrentGameMode.GameOngoing)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, " Can't nominate a map yet. Game not started.");
                return;
            }

            HelperMethods.ReplyToCommand_Player(callerPlayer, "nominated " + targetMapName + " as a map for the rock the vote list.");
            mapNominations.Add(targetMapName);
        }

        public static void Command_CurrentMap(Player? callerPlayer, String args)
        {
			if (callerPlayer == null)
			{
				HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, " Console not supported.");
				return;
			}
			
            HelperMethods.ReplyToCommand_Player(callerPlayer, ": The current map is " + mapName);
        }
        
        public static void Command_NextMap(Player? callerPlayer, String args)
        {
			if (callerPlayer == null)
			{
				HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, " Console not supported.");
				return;
			}
			
            if (sMapCycle == null)
            {
                MelonLogger.Warning("sMapCycle is null. Skipping nextmap handling.");
                return;
            }

            int roundsLeft = Pref_Mapcycle_RoundsBeforeChange.Value - roundsOnSameMap;
            HelperMethods.ReplyToCommand_Player(callerPlayer, ": The next map is " + sMapCycle[(iMapLoadCount + 1) % (sMapCycle.Length - 1)] + ". " + roundsLeft.ToString() + " more round" + (roundsLeft == 1 ? "" : "s") + " before map changes.");
        }

        public static void Command_ChangeMap(Player? callerPlayer, String args)
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
            if (!IsMapNameValid(targetMapName))
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Invalid map name");
                return;
            }

            // if any rock-the-vote actions are pending then they should be cancelled
            if (HelperMethods.IsTimerActive(Timer_FinalPostVoteDelay))
            {
                Timer_FinalPostVoteDelay = HelperMethods.Timer_Inactive;
                MelonLogger.Warning("Admin changed map while final RTV timer was in progress. Forcing timer to expire.");
            }

            if (HelperMethods.IsTimerActive(Timer_InitialPostVoteDelay))
            {
                Timer_InitialPostVoteDelay = HelperMethods.Timer_Inactive;
                MelonLogger.Warning("Admin changed map while initial RTV timer was in progress. Forcing timer to expire.");
            }

            HelperMethods.AlertAdminAction(callerPlayer, "changing map to " + targetMapName + "...");
            MelonLogger.Msg("Changing map to " + targetMapName + "...");

            ChangeMap(targetMapName);
        }

        public static void ChangeMap(string mapName)
        {
            iMapLoadCount++;
            string mapChangeCommand = "map " + mapName + " MP_Strategy";
            MelonLogger.Msg("Sending command to server console: " + mapChangeCommand);

            #if NET6_0
            Il2CppDebugTools.DebugConsole.TryExecuteCommand(mapChangeCommand, false, null);
            #else
            DebugTools.DebugConsole.TryExecuteCommand(mapChangeCommand, false, null);
            #endif

            /*
            LevelInfo? levelInfo = GetLevelInfo(mapName);
            if (levelInfo == null)
            {
                MelonLogger.Warning("Could not find LevelInfo for map name: " + mapName);
                return;
            }

            GameModeInfo? gameModeInfo = GetGameModeInfo(levelInfo);
            if (gameModeInfo == null)
            {
                MelonLogger.Warning("Could not find GameModeInfo for map name: " + mapName);
                return;
            }

            NetworkGameServer.LoadLevel(levelInfo.FileName, gameModeInfo);
            */
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            rockers.Clear();
            mapNominations.Clear();
            roundsOnSameMap = 0;
            mapName = sceneName;

            if (sceneName == "Intro" || sceneName == "MainMenu" || sceneName.Length < 2)
            {
                return;
            }

            // re-index the mapycle so the same maps don't appear on the next vote
            IndexToMapInCycle(sceneName);
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
                try
                {
                    if (HelperMethods.IsTimerActive(Timer_EndRoundDelay))
                    {
                        Timer_EndRoundDelay += Time.deltaTime;

                        if (Timer_EndRoundDelay > Pref_Mapcycle_EndgameDelay.Value)
                        {
                            Timer_EndRoundDelay = HelperMethods.Timer_Inactive;

                            if (sMapCycle == null)
                            {
                                return;
                            }

                            String sNextMap = sMapCycle[iMapLoadCount % (sMapCycle.Length - 1)];

                            MelonLogger.Msg("Changing map to " + sNextMap + ".....");
                            ChangeMap(sNextMap);
                            return;
                        }
                    }

                    if (HelperMethods.IsTimerActive(Timer_InitialPostVoteDelay))
                    {
                        Timer_InitialPostVoteDelay += Time.deltaTime;

                        if (Timer_InitialPostVoteDelay > 2.0f)
                        {
                            Timer_InitialPostVoteDelay = HelperMethods.Timer_Inactive;

                            HelperMethods.ReplyToCommand("Rock the vote finished.");

                            if (rockthevoteWinningMap == "")
                            {
                                HelperMethods.ReplyToCommand("Staying on current map.");
                                return;
                            }

                            HelperMethods.ReplyToCommand("Preparing to change map to " + rockthevoteWinningMap + "...");
                            HelperMethods.StartTimer(ref Timer_FinalPostVoteDelay);
                            return;
                        }
                    }

                    if (HelperMethods.IsTimerActive(Timer_FinalPostVoteDelay))
                    {
                        Timer_FinalPostVoteDelay += Time.deltaTime;

                        if (Timer_FinalPostVoteDelay > 6.0f)
                        {
                            Timer_FinalPostVoteDelay = HelperMethods.Timer_Inactive;

                            MelonLogger.Msg("Changing map to " + rockthevoteWinningMap + "....");
                            ChangeMap(rockthevoteWinningMap);
                            return;
                        }
                    }
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run MusicJukeboxHandler::Update");
                }
            }
        }

        [HarmonyPatch(typeof(MusicJukeboxHandler), nameof(MusicJukeboxHandler.OnGameEnded))]
        private static class ApplyPatch_OnGameEnded
        {
            public static void Postfix(MusicJukeboxHandler __instance, GameMode __0, Team __1)
            {
                try
                {
                    if (firedRoundEndOnce)
                    {
                        return;
                    }

                    firedRoundEndOnce = true;

                    if (sMapCycle == null)
                    {
                        MelonLogger.Warning("sMapCycle is null. Skipping end-round routines.");
                        return;
                    }

                    roundsOnSameMap++;
                    if (Pref_Mapcycle_RoundsBeforeChange.Value > roundsOnSameMap)
                    {
                        int roundsLeft = Pref_Mapcycle_RoundsBeforeChange.Value - roundsOnSameMap;
                        HelperMethods.ReplyToCommand("Current map will change after " + roundsLeft.ToString() + " more round" + (roundsLeft == 1 ? "." : "s."));
                        return;
                    }

                    if (HelperMethods.IsTimerActive(Timer_EndRoundDelay))
                    {
                        MelonLogger.Warning("End round delay timer already started.");
                        return;
                    }

                    // if any rock-the-vote actions are pending then they should be cancelled
                    if (HelperMethods.IsTimerActive(Timer_FinalPostVoteDelay))
                    {
                        Timer_FinalPostVoteDelay = HelperMethods.Timer_Inactive;
                        MelonLogger.Warning("Game ended while final RTV timer was in progress. Forcing timer to expire.");
                    }

                    if (HelperMethods.IsTimerActive(Timer_InitialPostVoteDelay))
                    {
                        Timer_InitialPostVoteDelay = HelperMethods.Timer_Inactive;
                        MelonLogger.Warning("Game ended while initial RTV timer was in progress. Forcing timer to expire.");
                    }

                    HelperMethods.ReplyToCommand("Preparing to change map to " + sMapCycle[(iMapLoadCount + 1) % (sMapCycle.Length - 1)] + "....");
                    HelperMethods.StartTimer(ref Timer_EndRoundDelay);
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run MusicJukeboxHandler::OnGameEnded");
                }
            }
        }

        [HarmonyPatch(typeof(MusicJukeboxHandler), nameof(MusicJukeboxHandler.OnGameStarted))]
        private static class ApplyPatch_OnGameStarted
        {
            public static void Prefix(MusicJukeboxHandler __instance, GameMode __0)
            {
                try
                {
                    firedRoundEndOnce = false;
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run MusicJukeboxHandler::OnGameStarted");
                }
            }
        }

        private static GameModeInfo? GetGameModeInfo(LevelInfo levelInfo)
        {
            foreach (GameModeInfo gameModeInfo in levelInfo.GameModes)
            {
                if (gameModeInfo == null)
                {
                    continue;
                }

                if (!gameModeInfo.Enabled)
                {
                    continue;
                }

                MelonLogger.Msg("Found gameModeInfo name: " + gameModeInfo.ObjectName);

                if (String.Equals("MP_Strategy", gameModeInfo.ObjectName, StringComparison.OrdinalIgnoreCase))
                {
                    return gameModeInfo;
                }
            }

            return null;
        }

        private static bool IsMapNameValid(string mapName)
        {
            if (GetLevelInfo(mapName) != null)
            {
                return true;
            }

            return false;
        }

        private static LevelInfo? GetLevelInfo(string mapName)
        {
            if (GameDatabase.Database == null || GameDatabase.Database.AllLevels == null)
            {
                MelonLogger.Warning("Found game database null.");
                return null;
            }

            foreach (LevelInfo? levelInfo in GameDatabase.Database.AllLevels)
            {
                if (levelInfo == null)
                {
                    continue;
                }

                if (!levelInfo.Enabled)
                {
                    continue;
                }

                if (!levelInfo.IsMultiplayer)
                {
                    continue;
                }

                if (String.Equals(mapName, levelInfo.FileName, StringComparison.OrdinalIgnoreCase))
                {
                    return levelInfo;
                }
            }

            return null;
        }

        private static void IndexToMapInCycle(string mapName)
        {
            if (sMapCycle == null || mapName.Length < 2)
            {
                return;
            }

            int matchIndex = Array.IndexOf(sMapCycle, mapName);
            if (matchIndex == -1)
            {
                MelonLogger.Warning("Could not find map in mapcycle array: ", mapName);
                return;
            }

            // increase iMapLoadCount by the amount needed to reach the current array index of the current map
            int currentArrayIndex = iMapLoadCount % (sMapCycle.Length - 1);
            if (matchIndex > currentArrayIndex)
            {
                iMapLoadCount += (matchIndex - currentArrayIndex);
            }
            else if (matchIndex < currentArrayIndex)
            {
                iMapLoadCount += (sMapCycle.Length) - (currentArrayIndex - matchIndex); 
            }
            // if matchIndex == currentArrayIndex, no need to do anything
        }
    }
}