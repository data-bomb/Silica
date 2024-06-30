/*
 Silica Logging Mod
 Copyright (C) 2023-2024 by databomb
 
 * Description *
 For Silica servers, creates a log file with console replication
 in the Half-Life log standard format.

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

using System.Threading;
using HarmonyLib;
using MelonLoader;
using MelonLoader.Utils;
using Si_Logging;
using UnityEngine;
using System;
using SilicaAdminMod;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Text;

[assembly: MelonInfo(typeof(HL_Logging), "Half-Life Logger", "1.3.0", "databomb&zawedcvg", "https://github.com/data-bomb/Silica")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]
[assembly: MelonOptionalDependencies("Admin Mod")]

namespace Si_Logging
{
    // https://developer.valvesoftware.com/wiki/HL_Log_Standard
    public class HL_Logging : MelonMod
    {
        const int MaxPlayableTeams = 3;
        static int[] teamResourcesCollected = new int[MaxPlayableTeams + 1];
        static Player?[]? lastCommander;

        static MelonPreferences_Category _modCategory = null!;
        static MelonPreferences_Entry<bool> Pref_Log_Damage = null!;
        static MelonPreferences_Entry<bool> Pref_Log_Kills_Include_AI_vs_Player = null!;
        static MelonPreferences_Entry<string> Pref_Log_ParserFile = null!;
        static MelonPreferences_Entry<string> Pref_Log_PythonExe = null!;
        public static MelonPreferences_Entry<float> Pref_Log_PerfMonitor_Interval = null!;

        public static bool ParserFilePresent()
        {
            return System.IO.File.Exists(GetParserPath());
        }

        public static string GetParserPath()
        {
            return System.IO.Path.Combine(MelonEnvironment.UserDataDirectory, Pref_Log_ParserFile.Value);
        }

        public static void PrintLogLine(string LogMessage, bool suppressConsoleOutput = false)
        {
            if (LogMessage != null)
            {
                string TempLogFile = GetLogFilePath();
                if (TempLogFile != CurrentLogFile)
                {
                    System.IO.File.AppendAllText(CurrentLogFile, GetLogPrefix() + "Log file closed" + Environment.NewLine);
                    CurrentLogFile = TempLogFile;
                    AddFirstLogLine();
                }
                string LogLine = GetLogPrefix() + LogMessage;

                if (!suppressConsoleOutput)
                {
                    MelonLogger.Msg(LogLine);
                }

                System.IO.File.AppendAllText(CurrentLogFile, LogLine + Environment.NewLine);
            }
        }

        public static void AddFirstLogLine()
        {
            string FirstLine = "Log file started (file \"" + GetLogSubPath() + "\") (game \"" + MelonEnvironment.GameExecutablePath + "\") (version \"" + MelonLoader.InternalUtils.UnityInformationHandler.GameVersion + "\") (hostid \"" + NetworkGameServer.GetServerID().ToString() + "\")";
            System.IO.File.AppendAllText(CurrentLogFile, GetLogPrefix() + FirstLine + Environment.NewLine);
        }

        // Generate prefix in format "L mm/dd/yyyy - hh:mm:ss:"
        public static string GetLogPrefix()
        {
            DateTime currentDateTime = DateTime.Now;
            string LogPrefix = "L " + currentDateTime.ToString("MM/dd/yyyy - HH:mm:ss: ");
            return LogPrefix;
        }

        public static string GetLogFileDirectory()
        {
            return System.IO.Path.Combine(MelonEnvironment.UserDataDirectory, @"logs\");
        }

        public static string GetLogSubPath()
        {
            return @"logs\" + GetLogName();
        }

        public static string GetLogFilePath()
        {
            string LogSubPath = GetLogSubPath();
            return System.IO.Path.Combine(MelonEnvironment.UserDataDirectory, LogSubPath);
        }

        public static string GetLogName()
        {
            DateTime currentDateTime = DateTime.Now;
            return "L" + currentDateTime.ToString("yyyyMMdd") + ".log";
        }

        static String CurrentLogFile = GetLogFilePath();

        public static string GetPlayerID(Player player)
        {
            return player.ToString().Split('_')[1];
        }

        public override void OnInitializeMelon()
        {
            try
            {
                _modCategory ??= MelonPreferences.CreateCategory("Silica");
                Pref_Log_Damage ??= _modCategory.CreateEntry<bool>("Logging_LogDamage", false);
                Pref_Log_Kills_Include_AI_vs_Player ??= _modCategory.CreateEntry<bool>("Logging_LogKills_IncludeAIvsPlayer", true);
                Pref_Log_ParserFile ??= _modCategory.CreateEntry<string>("Logging_LogParserPath", "inserting_info.py");
                Pref_Log_PythonExe ??= _modCategory.CreateEntry<string>("Logging_PythonExePath", "C:\\Users\\A\\Mods\\Silica\\ranked\\venv\\Scripts\\python.exe");
                Pref_Log_PerfMonitor_Interval ??= _modCategory.CreateEntry<float>("Logging_PerfMonitor_LogInterval", 60f);

                if (!System.IO.Directory.Exists(GetLogFileDirectory()))
                {
                    MelonLogger.Msg("Creating log file directory at: " + GetLogFileDirectory());
                    System.IO.Directory.CreateDirectory(GetLogFileDirectory());
                }

                if (!System.IO.File.Exists(CurrentLogFile))
                {
                    AddFirstLogLine();
                }

                lastCommander = new Player?[MaxPlayableTeams];
                for (int i = 0; i < MaxPlayableTeams; i++)
                {
                    lastCommander[i] = null;
                }
            }
            catch (Exception error)
            {
                HelperMethods.PrintError(error, "Failed to initialize log directories or files");
            }
        }

        public override void OnLateInitializeMelon()
        {
            HelperMethods.StartTimer(ref ServerPerfLogger.Timer_PerfMonitorLog);

            //subscribing to the event
            Event_Roles.OnRoleChanged += OnRoleChanged;
            #if NET6_0
            bool QListLoaded = RegisteredMelons.Any(m => m.Info.Name == "QList");
            if (!QListLoaded)
            {
                return;
            }

            QList.Options.RegisterMod(this);

            QList.OptionTypes.BoolOption logDamage = new(Pref_Log_Damage, Pref_Log_Damage.Value);
            QList.OptionTypes.BoolOption logAllKills = new(Pref_Log_Kills_Include_AI_vs_Player, Pref_Log_Kills_Include_AI_vs_Player.Value);
            QList.Options.AddOption(logDamage);

            QList.Options.AddOption(logAllKills);
            #endif
        }

        // 003. Change Map
        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            try
            {
                if (sceneName == "Intro" || sceneName == "MainMenu" || sceneName == "Loading")
                {
                    return;
                }

                string LogLine = "Loading map \"" + sceneName + "\"";
                PrintLogLine(LogLine);
            }
            catch (Exception error)
            {
                HelperMethods.PrintError(error);
            }
        }

        // 050. Connection
        #if NET6_0
        [HarmonyPatch(typeof(NetworkGameServer), nameof(NetworkGameServer.OnP2PSessionRequest))]
        #else
        [HarmonyPatch(typeof(NetworkGameServer), "OnP2PSessionRequest")]
        #endif
        private static class ApplyPatchOnP2PSessionRequest
        {
            public static void Postfix(NetworkGameServer __instance, P2PSessionRequest_t __0)
            {
                try
                {
                    // TODO: Find IP address and port of client
                    string LogLine = "\"...<><" + __0.m_steamIDRemote.ToString() + "><> connected, address \"127.0.0.1:27015\"";
                    PrintLogLine(LogLine);
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run OnP2PSessionRequest");
                }
            }
        }

        // TODO: 050b. Validation

        // 051. Enter Game
        [HarmonyPatch(typeof(GameMode), nameof(GameMode.OnPlayerJoinedBase))]
        private static class ApplyPatchOnPlayerJoinedBase
        {
            public static void Postfix(GameMode __instance, Player __0)
            {
                try
                {
                    if (__0 != null)
                    {
                        // TODO: Find current name
                        int userID = Math.Abs(__0.GetInstanceID());
                        string LogLine = "\"" + __0.PlayerName + "<" + userID + "><" + GetPlayerID(__0) + "><>\" entered the game";
                        PrintLogLine(LogLine);
                    }
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run OnPlayerJoinedBase");
                }
            }
        }

        // 052. Disconnection
        [HarmonyPatch(typeof(GameMode), nameof(GameMode.OnPlayerLeftBase))]
        private static class ApplyPatchOnPlayerLeftBase
        {
            public static void Prefix(GameMode __instance, Player __0)
            {
                try
                {
                    if (__0 != null)
                    {
                        int userID = Math.Abs(__0.GetInstanceID());
                        string teamName;
                        if (__0.Team == null)
                        {
                            teamName = "";
                        }
                        else
                        {
                            teamName = __0.Team.TeamShortName;
                        }

                        string LogLine = "\"" + __0.PlayerName + "<" + userID + "><" + GetPlayerID(__0) + "><" + teamName + ">\" disconnected";
                        PrintLogLine(LogLine);
                    }
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run OnPlayerLeftBase");
                }
            }
        }

        // 052b. Kick
        [HarmonyPatch(typeof(NetworkGameServer), nameof(NetworkGameServer.KickPlayer))]
        private static class ApplyPatchKickPlayer
        {
            public static void Postfix(Player __0)
            {
                try
                {
                    int userID = Math.Abs(__0.GetInstanceID());
                    string teamName;
                    if ( __0.Team == null)
                    {
                        teamName = "";
                    }
                    else
                    {
                        teamName = __0.Team.TeamShortName;
                    }
                    string LogLine = "Kick: \"" + __0.PlayerName + "<" + userID + "><" + GetPlayerID(__0) + "><" + teamName + "\" was kicked by \"Console\" (message \"\")";
                    PrintLogLine(LogLine);
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run KickPlayer");
                }
            }
        }

        // 053. Suicides
        // 057. Kills
        [HarmonyPatch(typeof(StrategyMode), nameof(StrategyMode.OnUnitDestroyed))]
        private static class ApplyPatch_StrategyMode_OnUnitDestroyed
        {
            public static void Postfix(StrategyMode __instance, Unit __0, EDamageType __1, UnityEngine.GameObject __2)
            {
                try
                {
                    if (__0 == null || __2 == null)
                    {
                        return;
                    }

                    // Attacker
                    BaseGameObject attackerBase = GameFuncs.GetBaseGameObject(__2);
                    if (attackerBase == null)
                    {
                        return;
                    }
                    NetworkComponent attackerNetComp = attackerBase.NetworkComponent;
                    Player attackerPlayer = attackerNetComp.OwnerPlayer;

                    // Victim
                    Player victimPlayer = __0.ControlledBy;

                    bool isVictimHuman = (victimPlayer != null);
                    bool isAttackerHuman = (attackerPlayer != null);

                    #pragma warning disable CS8602 // Dereference of a possibly null reference.
                    if (isVictimHuman)
                    {

                        int victimUserID = Math.Abs(victimPlayer.GetInstanceID());

                        if (attackerNetComp == null)
                        {
                            return;
                        }

                        // Attacker and Victim are both humans
                        if (isAttackerHuman)
                        {
                            bool isSuicide = (attackerPlayer == victimPlayer);
                            if (isSuicide)
                            {

                                string LogLine = "\"" + victimPlayer.PlayerName + "<" + victimUserID + "><" + GetPlayerID(victimPlayer) + "><" + victimPlayer.Team.TeamShortName + ">\" committed suicide with \"" + __2.ToString().Split('(')[0] + "\" (dmgtype \"" + __1.ToString() + "\")";
                                PrintLogLine(LogLine);
                            }
                            // human-controlled player killed another human-controlled player
                            else
                            {
                                int attackerUserID = Math.Abs(attackerPlayer.GetInstanceID());
                                string LogLine = "\"" + attackerPlayer.PlayerName + "<" + attackerUserID + "><" + GetPlayerID(attackerPlayer) + "><" + attackerPlayer.Team.TeamShortName + ">\" killed \"" + victimPlayer.PlayerName + "<" + victimUserID + "><" + GetPlayerID(victimPlayer) + "><" + victimPlayer.Team.TeamShortName + ">\" with \"" + __2.ToString().Split('(')[0] + "\" (dmgtype \"" + __1.ToString() + "\") (victim \"" + __0.ToString().Split('(')[0] + "\")";
                                PrintLogLine(LogLine);
                            }
                        }
                        else if (Pref_Log_Kills_Include_AI_vs_Player.Value)
                        // Attacker is an AI, Victim is a human
                        {
                            string LogLine = "\"" + __2.ToString().Split('(')[0] + "<><><" + attackerBase.Team.TeamShortName + ">\" killed \"" + victimPlayer.PlayerName + "<" + victimUserID + "><" + GetPlayerID(victimPlayer) + "><" + victimPlayer.Team.TeamShortName + ">\" with \"" + __2.ToString().Split('(')[0] + "\" (dmgtype \"" + __1.ToString() + "\") (victim \"" + __0.ToString().Split('(')[0] + "\")";
                            PrintLogLine(LogLine);
                        }
                    }
                    else if (isAttackerHuman && Pref_Log_Kills_Include_AI_vs_Player.Value)
                    // Attacker is a human, Victim is an AI
                    {
                        int attackerUserID = Math.Abs(attackerPlayer.GetInstanceID());
                        string LogLine = "\"" + attackerPlayer.PlayerName + "<" + attackerUserID + "><" + GetPlayerID(attackerPlayer) + "><" + attackerPlayer.Team.TeamShortName + ">\" killed \"" + __0.ToString().Split('(')[0] + "<><><" + __0.Team.TeamShortName + ">\" with \"" + __2.ToString().Split('(')[0] + "\" (dmgtype \"" + __1.ToString() + "\") (victim \"" + __0.ToString().Split('(')[0] + "\")";
                        PrintLogLine(LogLine);
                    }
                    #pragma warning restore CS8602 // Dereference of a possibly null reference.
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run OnUnitDestroyed");
                }
            }
        }

        // 054. Team Selection
        [HarmonyPatch(typeof(MP_Strategy), nameof(MP_Strategy.OnPlayerChangedTeam))]
        private static class ApplyPatchOnPlayerChangedTeam
        {
            public static void Postfix(MP_Strategy __instance, Player __0, Team __1, Team __2)
            {
                try
                {
                    string theOldTeamName;
                    if (__1 == null)
                    {
                        theOldTeamName = "";
                    }
                    else
                    {
                        theOldTeamName = __1.TeamShortName;
                    }

                    string theNewTeamName;
                    if (__2 == null)
                    {
                        // this happens at the begginning of each map and isn't useful logging information
                        return;
                    }
                    else
                    {
                        theNewTeamName = __2.TeamShortName;
                    }

                    if (__0 != null)
                    {
                        int userID = Math.Abs(__0.GetInstanceID());
                        string LogLine = "\"" + __0.PlayerName + "<" + userID + "><" + GetPlayerID(__0) + "><" + theOldTeamName + ">\" joined team \"" + theNewTeamName + "\"";
                        PrintLogLine(LogLine);
                    }
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run OnPlayerChangedTeam");
                }
            }
        }

        // 055. Role Selection - Commander Change
        public void OnRoleChanged(object? sender, OnRoleChangedArgs args)
        {
            try
            {
                if (args == null) return;

                Player player = args.Player;
                
                if (player == null || player.Team == null) return;
                int userID = Math.Abs(player.GetInstanceID());
                string role;

                if (args.Role == MP_Strategy.ETeamRole.COMMANDER)
                {
                    role = "Commander";
                }
                else if (args.Role == MP_Strategy.ETeamRole.INFANTRY)
                {
                    role = "Infantry";
                } else
                {
                    role = "None";
                }

                string LogLine = "\"" + player.PlayerName + "<" + userID + "><" + GetPlayerID(player) + "><" + player.Team.TeamShortName + ">\" changed role to \"" + role + "\"";
                PrintLogLine(LogLine);
            }
            catch (Exception error)
            {
                HelperMethods.PrintError(error, "Failed to run OnRoleChanged");
            }

        }

        // 056. Change Name
        [HarmonyPatch(typeof(NetworkLayer), nameof(NetworkLayer.SendPlayerChangeName))]
        private static class ApplyPatchSendPlayerChangeName
        {
            public static void Postfix(CSteamID __0, int __1, string __2)
            {
                try
                {
                    // Player player = GetPlayerFromSteamID(__0);
                    // TODO: grab old name and current team
                    string LogLine = "\"...<><" + __0.ToString() + "><>\" changed name to \"" + __2 + "\"";
                    PrintLogLine(LogLine);
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run SendPlayerChangeName");
                }
            }
        }

        // 058. Injuring
        #if NET6_0
        [HarmonyPatch(typeof(DamageManager), nameof(DamageManager.OnDamageReceived))]
        #else
        [HarmonyPatch(typeof(DamageManager), "OnDamageReceived")]
        #endif
        private static class ApplyPatchOnDamageReceived
        {
            public static void Postfix(DamageManager __instance, UnityEngine.Collider __0, float __1, EDamageType __2, UnityEngine.GameObject __3, UnityEngine.Vector3 __4)
            {
                // should we log the damage?
                if (!Pref_Log_Damage.Value)
                {
                    return;
                }

                // was it a non-human-controlled instigator?
                if (__3 == null)
                {
                    return;
                }

                BaseGameObject attackerBase = GameFuncs.GetBaseGameObject(__3);
                if (attackerBase == null)
                {
                    return;
                }

                NetworkComponent attackerNetComp = attackerBase.NetworkComponent;
                if (attackerNetComp == null)
                {
                    return;
                }

                Player attackerPlayer = attackerNetComp.OwnerPlayer;
                if (attackerPlayer == null)
                {
                    return;
                }

                // was it a non-human-controlled victim?
                BaseGameObject victimBase = __instance.Owner;
                if (victimBase == null)
                {
                    return;
                }

                NetworkComponent victimNetComp = victimBase.NetworkComponent;
                if (victimNetComp == null)
                {
                    return;
                }

                Player victimPlayer = victimNetComp.OwnerPlayer;
                if (victimPlayer == null)
                {
                    return;
                }

                // was damage less than (or equal to) 1?
                int damage = (int)Math.Ceiling(__1);
                if (damage <= 1)
                {
                    return;
                }

                int attackerUserID = Math.Abs(attackerPlayer.GetInstanceID());
                int victimUserID = Math.Abs(victimPlayer.GetInstanceID());
                string LogLine = "\"" + attackerPlayer.PlayerName + "<" + attackerUserID + "><" + GetPlayerID(attackerPlayer) + "><" + attackerPlayer.Team.TeamShortName + ">\" attacked \"" + victimPlayer.PlayerName + "<" + victimUserID + "><" + GetPlayerID(victimPlayer) + "><" + victimPlayer.Team.TeamShortName + ">\" with \"" + __3.ToString().Split('(')[0] + "\"" + " (damage \"" + damage.ToString() + "\")";
                PrintLogLine(LogLine, true);
            }
        }

        // 059. Player-Player Actions
        // None for now. Re-evaluate with updates

        // 060. Player Objectives/Actions - Structure Kill
        [HarmonyPatch(typeof(MP_Strategy), nameof(MP_Strategy.OnStructureDestroyed))]
        private static class ApplyPatchOnStructureDestroyed
        {
            public static void Postfix(MP_Strategy __instance, Structure __0, EDamageType __1, UnityEngine.GameObject __2)
            {
                try
                {
                    //check if the destruction affects the tech tier.
                    if (__0 == null) return;

                    if (__0.Team != null)
                    {
                        Team structureTeam = __0.Team;
                        int tier = getHighestTechTier(structureTeam);
                        if (tier != currTiers[structureTeam.name])
                        {
                            currTiers[structureTeam.name] = tier;
                            LogTierChange(structureTeam, tier);
                        }
                    }
                    if (__2 == null) return;

                        // Attacker
                    BaseGameObject attackerBase = GameFuncs.GetBaseGameObject(__2);

                    if (attackerBase != null)
                    {
                        NetworkComponent attackerNetComp = attackerBase.NetworkComponent;
                        // was teamkiller a playable character?
                        if (attackerNetComp != null)
                        {
                            Player attackerPlayer = attackerNetComp.OwnerPlayer;

                            if (attackerPlayer != null)
                            {
                                int userID = Math.Abs(attackerPlayer.GetInstanceID());
                                string structName;
                                if (__0.ToString().Contains('_'))
                                {
                                    structName = __0.ToString().Split('_')[0];
                                }
                                else if (__0.ToString().Contains('('))
                                {
                                    structName = __0.ToString().Split('(')[0];
                                }
                                else
                                {
                                    structName = __0.ToString();
                                }

                                string attackerPlayerTeam;
                                if (attackerPlayer.Team == null)
                                {
                                    attackerPlayerTeam = "";
                                }
                                else
                                {
                                    attackerPlayerTeam = attackerPlayer.Team.TeamShortName;
                                }

                                string structTeam;
                                if (__0.Team == null)
                                {
                                    structTeam = "";
                                }
                                else
                                {
                                    structTeam = __0.Team.TeamShortName;
                                }

                                string LogLine = "\"" + attackerPlayer.PlayerName + "<" + userID + "><" + GetPlayerID(attackerPlayer) + "><" + attackerPlayerTeam + ">\" triggered \"structure_kill\" (structure \"" + structName + "\") (struct_team \"" + structTeam + "\")";
                                PrintLogLine(LogLine);
                            }

                        }
                    }
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run OnStructureDestroyed");
                }
            }
        }

        // TODO: 060. Player Objectives/Actions
        // Ideas: Enter/exit vehicle. Take control of bug.

        // Round-End Scoring based on Resources Collected
        [HarmonyPatch(typeof(Team), nameof(Team.OnResourcesChanged))]
        private static class ApplyPatchOnResourcesChanged
        {
            public static void Postfix(Team __instance, Structure __0, ResourceHolder __1, int __2)
            {
                try
                {
                    if (__instance == null)
                    {
                        return;
                    }

                    if (__2 <= 0)
                    {
                        return;
                    }

                    teamResourcesCollected[__instance.Index] += __2;
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run OnResourcesChanged");
                }
            }
        }

        // 061. Team Objectives/Actions - Victory
        // 062. World Objectives/Actions - Round_Win
        // 065. Round-End Team Score Report
        // 067. Round-End Player Score Report

        static bool firedRoundEndOnce;

        [HarmonyPatch(typeof(MusicJukeboxHandler), nameof(MusicJukeboxHandler.OnGameEnded))]
        private static class ApplyPatchOnGameEnded
        {
            public static void Postfix(MusicJukeboxHandler __instance, GameMode __0, Team __1)
            {
                try
                {
                    if (!firedRoundEndOnce)
                    {
                        firedRoundEndOnce = true;

                        MP_Strategy strategyInstance = GameObject.FindObjectOfType<MP_Strategy>();
                        MP_Strategy.ETeamsVersus versusMode = strategyInstance.TeamsVersus;

                        if (strategyInstance == null)
                        {
                            return;
                        }

                        if (__1 == null)
                        {
                            string RoundWinLogEarlyLine = "World triggered \"Round_Win\" (gametype \"" + versusMode.ToString() + "\")";
                            PrintLogLine(RoundWinLogEarlyLine);
                            return;
                        }

                        string VictoryLogLine = "Team \"" + __1.TeamShortName + "\" triggered \"Victory\"";
                        PrintLogLine(VictoryLogLine);

                        for (int i = 0; i < SiConstants.MaxPlayableTeams; i++)
                        {
                            Team? thisTeam = Team.Teams[i];
                            if (versusMode == MP_Strategy.ETeamsVersus.HUMANS_VS_HUMANS && i == (int)SiConstants.ETeam.Alien)
                            {
                                continue;
                            }
                            else if (versusMode == MP_Strategy.ETeamsVersus.HUMANS_VS_ALIENS && i == (int)SiConstants.ETeam.Centauri)
                            {
                                continue;
                            }
                            
                            string TeamLogLine = "Team \"" + thisTeam.TeamShortName + "\" scored \"" + teamResourcesCollected[thisTeam.Index].ToString() + "\" with \"" + thisTeam.GetNumPlayers().ToString() + "\" players";
                            PrintLogLine(TeamLogLine);
                        }

                        for (int i = 0; i < Player.Players.Count; i++)
                        {
                            if (Player.Players[i] != null)
                            {
                                Player thisPlayer = Player.Players[i];
                                int userID = Math.Abs(thisPlayer.GetInstanceID());

                                string playerTeam;
                                if (thisPlayer.Team == null)
                                {
                                    playerTeam = "";
                                }
                                else
                                {
                                    playerTeam = thisPlayer.Team.TeamShortName;
                                }

                                string PlayerLogLine = "Player \"" + thisPlayer.PlayerName + "<" + userID + "><" + GetPlayerID(thisPlayer) + "><" + playerTeam + ">\" scored \"" + thisPlayer.Score + "\" (kills \"" + thisPlayer.Kills + "\") (deaths \"" + thisPlayer.Deaths + "\")";
                                PrintLogLine(PlayerLogLine);
                            }
                        }

                        string RoundWinLogLine = "World triggered \"Round_Win\" (gametype \"" + versusMode.ToString() + "\")";
                        PrintLogLine(RoundWinLogLine);

                        // call parser
                        if (!ParserFilePresent())
                        {
                            MelonLogger.Msg("Parser file not present.");
                            return;
                        }

                        // launch parser
                        MelonLogger.Msg("Launching parser.");
                        ProcessStartInfo start = new ProcessStartInfo();
                        start.FileName = Pref_Log_PythonExe.Value;
                        string arguments = string.Format("\"{0}\" \"{1}\"", GetParserPath(), GetLogFileDirectory());
                        start.Arguments = arguments;
                        start.UseShellExecute = false;
                        start.RedirectStandardOutput = false;
                        using Process? process = Process.Start(start);
                    }
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run OnGameEnded");
                }
            }
        }

        // 061. Team Objectives/Actions - Research Tier
        public static Dictionary<string, int> currTiers = new Dictionary<string, int>();
        public static int getHighestTechTier(Team team)
        {
            for (int i = 4; i > 0; i--)
            {
                int count = team.GetTechnologyTierStructureCount(i);
                if (count > 0) { return i; }
            }

            return 0;
        }

        public static void initializeRound(ref Dictionary<string, int> tiers)
        {
            for (int i = 0; i < Team.NumTeams; i++)
            {
                tiers[Team.Teams[i].name] = 0;
                teamResourcesCollected[i] = 0;
            }
        }

        #if NET6_0
        [HarmonyPatch(typeof(BarksHandler), nameof(BarksHandler.OnConstructionCompleted))]
        #else
        [HarmonyPatch(typeof(BarksHandler), "OnConstructionCompleted")]
        #endif
        private static class ApplyPatchOnSetTechnologyTier
        {
            public static void Postfix(ConstructionSite constructionSite, bool wasCompleted)
            {
                try
                {
                    Team siteTeam = constructionSite.Team;
                    int tier = getHighestTechTier(siteTeam);
                    if (tier != currTiers[siteTeam.name])
                    {
                        currTiers[siteTeam.name] = tier;
                        LogTierChange(siteTeam, tier);

                    }
                } 

                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run OnSetConstructionSite");
                }

            }
        }

        public static void LogTierChange(Team team, int tier)
        {
                string LogLine = "Team \"" + team.TeamShortName + "\" triggered \"technology_change\" (tier \"" + tier.ToString() + "\")";
                PrintLogLine(LogLine);
        }
        
        // 062. World Objectives/Actions - Round_Start
        [HarmonyPatch(typeof(MusicJukeboxHandler), nameof(MusicJukeboxHandler.OnGameStarted))]
        private static class ApplyPatchOnGameStarted
        {
            public static void Prefix(MusicJukeboxHandler __instance, GameMode __0)
            {
                try
                {
                    MP_Strategy strategyInstance = GameObject.FindObjectOfType<MP_Strategy>();
                    MP_Strategy.ETeamsVersus versusMode = strategyInstance.TeamsVersus;

                    string RoundStartLogLine = "World triggered \"Round_Start\" (gametype \"" + versusMode.ToString() + "\")";
                    PrintLogLine(RoundStartLogLine);
                    initializeRound(ref currTiers);

                    firedRoundEndOnce = false;
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run OnGameStarted");
                }
            }
        }

        // 063. Chat
        #if NET6_0
        [HarmonyPatch(typeof(Il2CppSilica.UI.Chat), nameof(Il2CppSilica.UI.Chat.MessageReceived))]
        #else
        [HarmonyPatch(typeof(Silica.UI.Chat), "MessageReceived")]
        #endif
        private static class ApplyPatchMessageReceived
        {
            #if NET6_0
            public static void Postfix(Il2CppSilica.UI.Chat __instance, Player __0, string __1, bool __2)
            #else
            public static void Postfix(Silica.UI.Chat __instance, Player __0, string __1, bool __2)
            #endif
            {
                try
                {
                    // each faction has its own chat manager but by looking at alien and only global messages this catches commands only once
                    if (__instance != null && __0 != null && __instance.ToString().Contains("alien"))
                    {
                        int userID = Math.Abs(__0.GetInstanceID());

                        string teamName;
                        if (__0.Team == null)
                        {
                            teamName = "";
                        }
                        else
                        {
                            teamName = __0.Team.TeamShortName;
                        }

                        // __2 true = team-only message
                        if (__2 == false)
                        {
                            string LogLine = "\"" + __0.PlayerName + "<" + userID + "><" + GetPlayerID(__0) + "><" + teamName + ">\" say \"" + __1 + "\"";
                            PrintLogLine(LogLine);
                            
                        }
                        else
                        {
                            string LogLine = "\"" + __0.PlayerName + "<" + userID + "><" + GetPlayerID(__0) + "><" + teamName + ">\" say_team \"" + __1 + "\"";
                            PrintLogLine(LogLine);
                        }
                    }

                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run Chat::MessageReceived");
                }
            }
        }

        // 064. Team Alliances
        // None for now. Re-evaluate with updates

        // 066. Private Chat
        // None for now. Re-evaluate with updates

        // 068. Weapon Selection
        // None for now. Re-evaluate with updates

        // 069. Weapon Pickup
        // None for now. Re-evaluate with updates
    }
}
