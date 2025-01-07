﻿/*
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
using System.Reflection;
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
using Harmony;
using static SilicaAdminMod.SiConstants;
using System.Numerics;


[assembly: MelonInfo(typeof(MapCycleMod), "Mapcycle", "1.7.0", "databomb", "https://github.com/data-bomb/Silica")]
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
        static List<Player> rockers = new List<Player>()!;
        static List<VotingOption> mapNominations = new List<VotingOption>();
        static List<VotingOption> defaultRtvStrategyList = new List<VotingOption>();
        static List<string> gameModes = new List<string>();
        static string rockthevoteWinningMap = "";
        // Global or static variable to save the map index before RTV
        static int savedMapIndex = -1;  // Initialize to -1 to indicate it's unset initially

        static MelonPreferences_Category _modCategory = null!;
        static MelonPreferences_Entry<int> Pref_Mapcycle_RoundsBeforeChange = null!;
        static MelonPreferences_Entry<int> Pref_Mapcycle_EndgameDelay = null!;
        static MelonPreferences_Entry<float> Pref_Mapcycle_RockTheVote_Percent = null!;

        static float Timer_EndRoundDelay = HelperMethods.Timer_Inactive;
        static float Timer_InitialPostVoteDelay = HelperMethods.Timer_Inactive;
        static float Timer_FinalPostVoteDelay = HelperMethods.Timer_Inactive;

        static List<VotingOption> mapCycleEntries = new List<VotingOption>();

        static List<string> strategyMaps = new List<string>
            {
                "CrimsonPeak"
            };

        public override void OnLateInitializeMelon()
        {
            string mapCycleFile = MelonEnvironment.UserDataDirectory + "\\mapcycle.txt";

            if (!File.Exists(mapCycleFile))
            {
                CreateDefaultMapcycle(mapCycleFile);
            }

            ParseMapcycle(mapCycleFile);

            defaultRtvStrategyList = CreateVotingOptions();

            foreach (var votingOption in mapCycleEntries)
            {
                if (!IsMapNameValid(votingOption.MapName))
                {
                    MelonLogger.Error("Invalid map found in mapcycle.txt: " + votingOption.MapName);
                }
            }

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

        }

        public static List<string> InitializeGameModeList(bool enabledModesOnly = false)
        {
            if (GameDatabase.Database == null || GameDatabase.Database.AllGameModes == null)
            {
                MelonLogger.Msg("InitializeGameModeList: GameDatabase.Database or .AllGameModes is null");
                return null;
            }

            gameModes = new List<string>();
            foreach (var gameModeInfo in GameDatabase.Database?.AllGameModes)
            {
                if (enabledModesOnly && !gameModeInfo.Enabled)
                {
                    continue;
                }
                gameModes.Add(gameModeInfo.ObjectName);

            }
            return gameModes;
        }

        public static string NormalizeGameMode(string gameMode)
        {
            var gameModeList = InitializeGameModeList();

            if (string.IsNullOrWhiteSpace(gameMode))
                return gameMode;

            // Replace spaces with underscores to standardize the input for comparison
            string standardizedInput = gameMode.Replace(" ", "_");



            // Find a matching game mode ignoring case
            var correctGameMode = gameModeList.FirstOrDefault(gm => string.Equals(gm, standardizedInput, StringComparison.OrdinalIgnoreCase));
            if (correctGameMode != null)
            {
                return correctGameMode; // Return the correctly cased version
            }

            MelonLogger.Warning($"No matching correct game mode found for: {gameMode}");

            return gameMode; // Return the original if no match was found
        }

        public override void OnInitializeMelon()
        {
            try
            {
                _modCategory ??= MelonPreferences.CreateCategory("Silica");
                Pref_Mapcycle_RoundsBeforeChange ??= _modCategory.CreateEntry<int>("Mapcycle_RoundsBeforeMapChange", 2);
                Pref_Mapcycle_EndgameDelay ??= _modCategory.CreateEntry<int>("Mapcycle_DelayBeforeEndgameMapChange_Seconds", 9);
                Pref_Mapcycle_RockTheVote_Percent ??= _modCategory.CreateEntry<float>("Mapcycle_RockTheVote_PercentNeeded", 0.31f);


            }
            catch (Exception exception)
            {
                MelonLogger.Msg(exception.ToString());
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



        public static List<VotingOption> CreateVotingOptions()
        {
            List<VotingOption> currentVotingOptions = new List<VotingOption>();

            var distinctMapCycle =
              mapCycleEntries
              .GroupBy(s => s.MapName, StringComparer.OrdinalIgnoreCase)
              .Select(g => g.First())
              .ToList();

            foreach (var entry in distinctMapCycle)
            {
                string gameMode;
                if (strategyMaps.Contains(entry.MapName, StringComparer.OrdinalIgnoreCase))
                {
                    gameMode = "MP_Strategy";
                }
                else
                {
                    var gameModeInfo = GetGameModeInfo(entry.MapName);  // Assume this method returns the highest priority game mode
                    gameMode = gameModeInfo?.ObjectName ?? "MP_Strategy";  // Ensure fallback if null
                }

                currentVotingOptions.Add(new VotingOption(entry.MapName, gameMode));
            }

            foreach (VotingOption nominatedMap in mapNominations)
            {
                // Create a new instance for each nomination
                var existingOption = currentVotingOptions.FirstOrDefault(option =>
                    option.MapName.Equals(nominatedMap.MapName, StringComparison.OrdinalIgnoreCase) &&
                    option.GameMode.Equals(nominatedMap.GameMode, StringComparison.OrdinalIgnoreCase));

                if (existingOption == null)  // Ensure unique
                {
                    currentVotingOptions.Add(new VotingOption(nominatedMap.MapName, nominatedMap.GameMode));
                }
            }

            return currentVotingOptions;
        }

        public static ChatVoteBallot? CreateRTVBallot()
        {
            // Retrieve the voting options which should already include map names and game modes
            List<VotingOption> currentVotingOptions = CreateVotingOptions();

            if (currentVotingOptions == null || currentVotingOptions.Count == 0)
            {
                MelonLogger.Warning("No valid voting options available.");
                return null;
            }

            // Create voting options, starting with 'Keep Current Map'
            OptionPair[] rtvOptions = new OptionPair[currentVotingOptions.Count + 1];
            rtvOptions[0] = new OptionPair
            {
                Command = "1",
                Description = "Keep Current Map"
            };

            // Populate the rest of the voting options with the maps and their game modes
            for (int i = 0; i < currentVotingOptions.Count; i++)
            {
                VotingOption option = currentVotingOptions[i];
                string description = option.GameMode.Equals("MP_Strategy", StringComparison.OrdinalIgnoreCase)
                                     ? option.MapName
                                     : $"{option.MapName} - {option.GameMode}";

                rtvOptions[i + 1] = new OptionPair
                {
                    Command = (i + 2).ToString(),  // Adjust the command indices accordingly
                    Description = description
                };
            }

            // Log the RTV ballot maps to the console
            MelonLogger.Msg("RTV Ballot Maps:");
            MelonLogger.Msg("1 - Keep Current Map");  // Ensure this is always logged
            foreach (var option in rtvOptions.Skip(1))  // Skip the first option as it's already logged
            {
                MelonLogger.Msg($"{option.Command} - {option.Description}");
            }

            ChatVoteBallot rtvBallot = new ChatVoteBallot
            {
                Question = "Vote for a map:",
                VoteHandler = (results) => RockTheVote_Handler(results, currentVotingOptions),
                Options = rtvOptions
            };

            return rtvBallot;
        }

        public static void RockTheVote_Handler(ChatVoteResults results, List<VotingOption> currentVotingOptions)
        {
            int winningIndex = int.Parse(results.WinningCommand) - 1;  // Zero-based index

            // Handle 'Keep Current Map' specifically
            if (winningIndex == 0)
            {
                HelperMethods.SendChatMessageToAll("Vote concluded: The current map will be kept. RTV is now reset and available.");
                ResetRTVStatus();
                mapNominations.Clear();
                currentVotingOptions.Clear();
                return;
            }

            // Adjusting index for actual maps due to 'Keep Current Map' being option 1
            winningIndex -= 1;

            if (winningIndex < 0 || winningIndex >= currentVotingOptions.Count)
            {
                MelonLogger.Error("Invalid map selection index from RTV.");
                return;
            }

            // Extract both map name and game mode from the selected voting option
            VotingOption selectedOption = currentVotingOptions[winningIndex];
            string selectedMapName = selectedOption.MapName;
            string selectedGameMode = selectedOption.GameMode;

            HelperMethods.SendChatMessageToAll($"Winning RTV Map Option: {winningIndex + 2} - {selectedMapName} ({selectedGameMode})");
            MelonLogger.Msg("Selected map name: " + selectedMapName + ", Game Mode: " + selectedGameMode);

            // Assume QueueChangeMap can now accept a game mode parameter
            QueueChangeMap(selectedMapName, true, selectedGameMode);

            currentVotingOptions.Clear();
            mapNominations.Clear();


            ResetRTVStatus();
            MelonLogger.Msg("RTV Ballot has been reset.");
        }

        public static void ResetRTVStatus()
        {
            rockers.Clear();  // Clear list of players who have initiated RTV
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
            int rocksNeeded = (int)Math.Ceiling(totalPlayers * Pref_Mapcycle_RockTheVote_Percent.Value);
            if (rocksNeeded < 1)
            {
                return 1;
            }
            return rocksNeeded;
        }

        public static void Command_Nominate(Player? callerPlayer, string args)
        {
            if (callerPlayer == null)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, "Console not supported.");
                return;
            }

            var parts = args.Split(new[] { ' ' }, 3);

            if (parts == null || parts.Length < 2)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, "Too few arguments.");
                return;
            }

            string mapName = parts[1].Trim();
            string wsParts2 = parts.Length > 2 ? parts[2].Trim() : string.Empty;
            var gameMode = parts.Length > 2 ? NormalizeGameMode(wsParts2) : string.Empty;  // Will determine game mode later if not specified


            if (!IsMapNameValid(mapName))
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, "Invalid map name.");
                return;
            }

            if (string.IsNullOrEmpty(gameMode))
            {
                gameMode = GetGameModeInfo(mapName)?.ObjectName;
            }


            if (mapCycleEntries.Count == 0)
            {
                MelonLogger.Warning("Map cycle is invalid or not loaded.");
                return;
            }

            bool alreadyNominated = mapNominations.Any(entry => entry.MapName.Equals(mapName, StringComparison.OrdinalIgnoreCase)
                && entry.GameMode.Equals(gameMode, System.StringComparison.OrdinalIgnoreCase));

            if (alreadyNominated)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, mapName + ": Map already nominated");
                return;
            }

            var existingOption = defaultRtvStrategyList.FirstOrDefault(option =>
                   option.MapName.Equals(mapName, StringComparison.OrdinalIgnoreCase) &&
                   option.GameMode.Equals(gameMode, StringComparison.OrdinalIgnoreCase));

            if (existingOption != null)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, "Map already nominated.");
                return;
            }

            if (mapNominations.Count >= 3)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, "Too many map nominations already received.");
                return;
            }

            if (ChatVotes.IsVoteInProgress())
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, "Can't nominate a map because a vote is in progress.");
                return;
            }

            if (!GameMode.CurrentGameMode.GameOngoing)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, "Can't nominate a map yet. Game not started.");
                return;
            }

            if (String.IsNullOrWhiteSpace(gameMode))
                return;

            // Add the nomination
            mapNominations.Add(new VotingOption(mapName, gameMode));
            HelperMethods.ReplyToCommand_Player(callerPlayer, "Nominated " + mapName + " with game mode " + gameMode + ".");
        }


        public static void Command_CurrentMap(Player? callerPlayer, String args)
        {
            if (callerPlayer == null)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, " Console not supported.");
                return;
            }

            HelperMethods.ReplyToCommand_Player(callerPlayer, ": The current map is " + GetDisplayName(mapName));
        }

        public static void Command_NextMap(Player? callerPlayer, String args)
        {
            if (callerPlayer == null)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, " Console not supported.");
                return;
            }

            if (mapCycleEntries == null)
            {
                MelonLogger.Warning("sMapCycle is null. Skipping nextmap handling.");
                return;
            }

            int roundsLeft = Pref_Mapcycle_RoundsBeforeChange.Value - roundsOnSameMap;
            HelperMethods.ReplyToCommand_Player(callerPlayer, ": The next map is " + GetDisplayName(GetNextMap()) + ". " + roundsLeft.ToString() + " more round" + (roundsLeft == 1 ? "" : "s") + " before map changes.");
        }

        public static string? GetNextMap()
        {
            // check for empty mapcycle
            if (mapCycleEntries == null || !mapCycleEntries.Any())
            {
                MelonLogger.Warning("Mapcycle Mod has empty mapcycle file.");
                return string.Empty;
            }

            return mapCycleEntries[(iMapLoadCount + 1) % (mapCycleEntries.Count)]?.MapName;
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
            if (mapCycleEntries == null || !mapCycleEntries.Any())
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

            HelperMethods.AlertAdminAction(callerPlayer, "changing map to " + GetDisplayName(targetMapName) + "...");
            MelonLogger.Msg("Changing map to " + targetMapName + "...");

            QueueChangeMap(targetMapName);
        }

        public static GameModeInfo? GetByName(string gameMode)
        {
            if (GameDatabase.Database.AllGameModes == null)
            {
                MelonLogger.Warning("No game modes available in the database.");
                return null;
            }

            MelonLogger.Msg("Available game modes:");
            foreach (GameModeInfo mode in GameDatabase.Database.AllGameModes)
            {
                MelonLogger.Msg("Game Mode: " + mode.ObjectName);
            }

            foreach (GameModeInfo mode in GameDatabase.Database.AllGameModes)
            {
                if (string.Equals(mode.ObjectName, gameMode, StringComparison.OrdinalIgnoreCase))
                {
                    return mode;
                }
            }

            return null;
        }

        public static void QueueChangeMap(string? mapName, bool fromRTV = false, string gamemodertv = "MP_Strategy")
        {
            if (mapCycleEntries == null || !mapCycleEntries.Any() || mapName == null)
                return;

            GameModeInfo? gameModeInfo = null;

            if (fromRTV)
            {

                gameModeInfo = GameModeInfo.GetByName(gamemodertv);
                savedMapIndex = iMapLoadCount; // Save the current index

                HelperMethods.SendChatMessageToAll($"Changing to Winning map: {mapName} with Gamemode {gameModeInfo}.");
            }
            else
            {


                if (savedMapIndex != -1)
                {
                    iMapLoadCount = (savedMapIndex + 1) % mapCycleEntries.Count; ; // Set next map to be the one after the saved index

                    MelonLogger.Msg($"Post-RTV: Resuming map cycle from index {iMapLoadCount}, Map: {mapCycleEntries[iMapLoadCount % mapCycleEntries.Count]}");
                    savedMapIndex = -1; // Reset saved index after changing to the correct map

                }
                else
                {
                    iMapLoadCount = iMapLoadCount + 1;
                    MelonLogger.Msg($"Regular map cycle load. Checking game mode settings for map: {mapName}");
                }

                string selectedGameMode = mapCycleEntries[iMapLoadCount].GameMode;
                gameModeInfo = GameModeInfo.GetByName(selectedGameMode);



                if (gameModeInfo == null)
                {

                    gameModeInfo = GetGameModeInfo(mapName);
                }


                MelonLogger.Msg($"Queueing next map: {mapCycleEntries[iMapLoadCount % mapCycleEntries.Count]?.MapName}");

            }


            // Set the game server's queue depending on the runtime environment
#if NET6_0
            NetworkGameServer.Instance.m_QueueGameMode = gameModeInfo;
            NetworkGameServer.Instance.m_QueueMap = mapName;
            MelonLogger.Msg($"[NET6] Queued map: {mapName} with game mode: {gameModeInfo?.ObjectName}");
#else
            FieldInfo queueMapField = typeof(NetworkGameServer).GetField("m_QueueMap", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo queueGameModeInfoField = typeof(NetworkGameServer).GetField("m_QueueGameMode", BindingFlags.NonPublic | BindingFlags.Instance);

            queueGameModeInfoField.SetValue(NetworkGameServer.Instance, gameModeInfo);
            queueMapField.SetValue(NetworkGameServer.Instance, mapName);
            MelonLogger.Msg($"[NET Framework] Queued map: {mapName} with game mode: {gameModeInfo?.ObjectName}");
#endif
        }



        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            rockers.Clear();
            mapNominations.Clear();
            roundsOnSameMap = 0;
            mapName = sceneName;

            if (sceneName == "Intro" || sceneName == "MainMenu" || sceneName == "Loading" || sceneName.Length < 2)
            {
                return;
            }

            HelperMethods.SendChatMessageToAll("Current Mapcycle:");
            foreach (var entry in mapCycleEntries)
            {

                HelperMethods.SendChatMessageToAll($"{entry.MapName} {entry.GameMode}");

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

                            if (mapCycleEntries == null || !mapCycleEntries.Any())
                            {
                                return;
                            }

                            string? nextMap = GetNextMap();

                            MelonLogger.Msg($"Changing map to {nextMap} .....");
                            QueueChangeMap(nextMap);
                            return;
                        }
                    }

                    if (HelperMethods.IsTimerActive(Timer_InitialPostVoteDelay))
                    {
                        Timer_InitialPostVoteDelay += Time.deltaTime;

                        if (Timer_InitialPostVoteDelay > 2.0f)
                        {
                            Timer_InitialPostVoteDelay = HelperMethods.Timer_Inactive;

                            HelperMethods.SendChatMessageToAll("Rock the vote finished.");

                            if (rockthevoteWinningMap == "")
                            {
                                HelperMethods.SendChatMessageToAll("Staying on current map.");
                                return;
                            }

                            HelperMethods.SendChatMessageToAll($"Preparing to change map to {GetDisplayName(rockthevoteWinningMap)} ...");
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

                            MelonLogger.Msg($"Changing map to {rockthevoteWinningMap} ...");
                            QueueChangeMap(rockthevoteWinningMap);
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

                    if (mapCycleEntries == null || !mapCycleEntries.Any())
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

                    HelperMethods.ReplyToCommand("Preparing to change map to " + GetNextMap() + "....");
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

        private static GameModeInfo? GetGameModeInfo(string mapName)
        {
            // the highest priority for any given level is the preferred mode
            // currently this should resolve to MP_Strategy or MP_Siege for most maps
            LevelInfo levelInfo = GetLevelInfo(mapName);

            if (levelInfo == null)
            {
                return null;
            }

            int highestPriority = -1;
            GameModeInfo? priorityGameMode = null;

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

                if (highestPriority < gameModeInfo.Priority)
                {
                    highestPriority = gameModeInfo.Priority;
                    priorityGameMode = gameModeInfo;
                }
            }

            return priorityGameMode;
        }

        private static bool IsMapNameValid(string mapName) =>
        GetLevelInfo(mapName) != null;


        private static LevelInfo GetLevelInfo(string name, bool displayNameSearch = false)
        {
            if (GameDatabase.Database == null || GameDatabase.Database.AllLevels == null)
            {
                MelonLogger.Warning("Found game database null.");
                return new LevelInfo();
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

                if (!displayNameSearch)
                {
                    if (String.Equals(name, levelInfo.FileName, StringComparison.OrdinalIgnoreCase))
                    {
                        return levelInfo;
                    }
                }
                else
                {
                    if (String.Equals(name, levelInfo.DisplayName, StringComparison.OrdinalIgnoreCase))
                    {
                        return levelInfo;
                    }
                }
            }

            return new LevelInfo();
        }

        private static void IndexToMapInCycle(string mapName)
        {
            if (mapCycleEntries == null || !mapCycleEntries.Any() || String.IsNullOrWhiteSpace(mapName))
            {
                MelonLogger.Warning("Mapcycle is empty or map name is not provided.");
                return;
            }

            int currentArrayIndex = iMapLoadCount % mapCycleEntries.Count;
            int matchIndex = -1;
            for (int i = 0; i < mapCycleEntries.Count; i++)
            {
                if (string.Equals(mapCycleEntries[i]?.MapName, mapName, StringComparison.OrdinalIgnoreCase))
                {
                    if (i >= currentArrayIndex)
                    {
                        matchIndex = i;
                        break;
                    }
                }
            }

            if (matchIndex == -1)
            {
                MelonLogger.Warning($"Could not find map '{mapName}' in mapcycle array. Checking for case-sensitive mismatch...");
                // Check for case-sensitive mismatches and log them
                foreach (var entry in mapCycleEntries)
                {
                    if (entry.MapName.Equals(mapName, StringComparison.Ordinal))
                    {
                        MelonLogger.Msg($"Case-sensitive mismatch found. Map in array: '{entry}', Searched map name: '{mapName}'");
                    }
                }
                return;
            }
        }

        private static string? GetDisplayName(string? mapName) =>
            mapName == null ? string.Empty : GetLevelInfo(mapName)?.DisplayName ?? mapName;

        private static void CreateDefaultMapcycle(string mapCycleFile)
        {
            // Create simple mapcycle.txt file
            using (var mapcycleFileStream = File.Create(mapCycleFile))
            {
                mapcycleFileStream.Close();
                File.WriteAllText(mapCycleFile, "RiftBasin\nNorthPolarCap\nTheMaw\nGreatErg\nBadlands\nNarakaCity\n");
            }
        }


        private static void ParseMapcycle(string mapCycleFile)
        {
            try
            {
                using (var mapcycleStreamReader = File.OpenText(mapCycleFile))
                {
                    string? line;

                    while ((line = mapcycleStreamReader.ReadLine()) != null)
                    {
                        var parts = line.Split(new[] { ' ' }, 2);
                        var mapName = parts[0].Trim();
                        var gameMode = parts.Length > 1 ? NormalizeGameMode(parts[1].Trim()) : "";

                        mapCycleEntries.Add(new VotingOption(mapName, gameMode));
                    }

                    // Log the loaded mapcycle list
                    MelonLogger.Msg("Current Mapcycle:");
                    foreach (var entry in mapCycleEntries)
                    {
                        MelonLogger.Msg($"Map: {entry.MapName}, Game Mode: " + (String.IsNullOrWhiteSpace(entry.GameMode) ? "Default" : entry.GameMode));
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error(ex.ToString());
            }
        }

    }
}

