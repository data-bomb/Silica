﻿/*
 Silica Logging Mod
 Copyright (C) 2023-2025 by databomb
 
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

using HarmonyLib;
using MelonLoader;
using MelonLoader.Utils;
using Si_Logging;
using UnityEngine;
using System;
using SilicaAdminMod;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.IO;

[assembly: MelonInfo(typeof(HL_Logging), "Half-Life Logger", "1.5.5", "databomb&zawedcvg", "https://github.com/data-bomb/Silica")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]
[assembly: MelonOptionalDependencies("Admin Mod")]

namespace Si_Logging
{
    // https://developer.valvesoftware.com/wiki/HL_Log_Standard
    public partial class HL_Logging : MelonMod
    {
        static int[] teamResourcesCollected = new int[SiConstants.MaxPlayableTeams + 1];
        static Player?[]? lastCommander;

        static MelonPreferences_Category _modCategory = null!;
        static MelonPreferences_Entry<bool> Pref_Log_Damage = null!;
        static MelonPreferences_Entry<bool> Pref_Log_Kills_Include_AI_vs_Player = null!;
        static MelonPreferences_Entry<string> Pref_Log_ParserExe = null!;
        public static MelonPreferences_Entry<float> Pref_Log_PerfMonitor_Interval = null!;
        public static MelonPreferences_Entry<bool> Pref_Log_PerfMonitor_Enable = null!;
        public static MelonPreferences_Entry<bool> Pref_Log_PlayerConsole_Enable = null!;

        public static bool ParserExePresent()
        {
            return System.IO.File.Exists(GetParserPath());
        }
        public static string GetParserPath()
        {
            return System.IO.Path.Combine(MelonEnvironment.UserDataDirectory, Pref_Log_ParserExe.Value);
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

        public override void OnInitializeMelon()
        {
            try
            {
                _modCategory ??= MelonPreferences.CreateCategory("Silica");
                Pref_Log_Damage ??= _modCategory.CreateEntry<bool>("Logging_LogDamage", false);
                Pref_Log_Kills_Include_AI_vs_Player ??= _modCategory.CreateEntry<bool>("Logging_LogKills_IncludeAIvsPlayer", true);
                Pref_Log_ParserExe ??= _modCategory.CreateEntry<string>("Logging_ParserExePath", "parser.exe");
                Pref_Log_PerfMonitor_Interval ??= _modCategory.CreateEntry<float>("Logging_PerfMonitor_LogInterval", 60f);
                Pref_Log_PerfMonitor_Enable ??= _modCategory.CreateEntry<bool>("Logging_PerfMonitor_Enable", true);
                Pref_Log_PlayerConsole_Enable ??= _modCategory.CreateEntry<bool>("Logging_PlayerConsole_Enable", true);

                if (!System.IO.Directory.Exists(GetLogFileDirectory()))
                {
                    MelonLogger.Msg("Creating log file directory at: " + GetLogFileDirectory());
                    System.IO.Directory.CreateDirectory(GetLogFileDirectory());
                }

                if (!System.IO.File.Exists(CurrentLogFile))
                {
                    AddFirstLogLine();
                }

                lastCommander = new Player?[SiConstants.MaxPlayableTeams];
                for (int i = 0; i < SiConstants.MaxPlayableTeams; i++)
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

                if (Pref_Log_PlayerConsole_Enable.Value)
                {
                    string ConsoleLine = "<b>Loading map \"" + sceneName + "\"</b>";
                    HelperMethods.SendConsoleMessage(ConsoleLine);
                }
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
                        int userID = GetUserId(__0);
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
                        int userID = GetUserId(__0);
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

                        if (Pref_Log_PlayerConsole_Enable.Value)
                        {
                            string ConsoleLine = "<b>" + HelperMethods.GetTeamColor(__0) + __0.PlayerName + "</color></b> disconnected.";
                            HelperMethods.SendConsoleMessage(ConsoleLine);
                        }
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
                    int userID = GetUserId(__0);
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
            public static void Postfix(StrategyMode __instance, Unit __0, UnityEngine.GameObject __1)
            {
                try
                {
                    if (__0 == null || __1 == null)
                    {
                        return;
                    }

                    // Attacker
                    BaseGameObject attackerBase = GameFuncs.GetBaseGameObject(__1);
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

                        int victimUserID = GetUserId(victimPlayer);

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

                                string LogLine = "\"" + victimPlayer.PlayerName + "<" + victimUserID + "><" + GetPlayerID(victimPlayer) + "><" + victimPlayer.Team.TeamShortName + ">\" committed suicide with \"" + __1.ToString().Split('(')[0] + "\" (dmgtype \"\")";
                                PrintLogLine(LogLine);

                                if (Pref_Log_PlayerConsole_Enable.Value)
                                {
                                    string ConsoleLine = "<b>" + HelperMethods.GetTeamColor(victimPlayer) + victimPlayer.PlayerName + "</color></b> (" + __1.ToString().Split('(')[0] + ") committed suicide";
                                    HelperMethods.SendConsoleMessage(ConsoleLine);
                                }
                            }
                            // human-controlled player killed another human-controlled player
                            else
                            {
                                string attackerEntry = AddPlayerLogEntry(attackerPlayer);
                                string victimEntry = AddPlayerLogEntry(victimPlayer);
                                string withEntry = AddKilledWithEntry(__0, __1);

                                string LogLine = $"{attackerEntry} killed {victimEntry} with {withEntry}";
                                PrintLogLine(LogLine);

                                if (Pref_Log_PlayerConsole_Enable.Value)
                                {
                                    string attackerConsoleEntry = AddPlayerConsoleEntry(attackerPlayer);
                                    string victimConsoleEntry = AddPlayerConsoleEntry(victimPlayer);

                                    string ConsoleLine = $"{attackerConsoleEntry} ({GetNameFromObject(__1)}) killed {victimConsoleEntry} ({GetNameFromUnit(__0)})";
                                    HelperMethods.SendConsoleMessage(ConsoleLine);
                                }
                            }
                        }
                        else if (Pref_Log_Kills_Include_AI_vs_Player.Value)
                        // Attacker is an AI, Victim is a human
                        {
                            string LogLine = "\"" + __1.ToString().Split('(')[0] + "<><><" + attackerBase.Team.TeamShortName + ">\" killed \"" + victimPlayer.PlayerName + "<" + victimUserID + "><" + GetPlayerID(victimPlayer) + "><" + victimPlayer.Team.TeamShortName + ">\" with \"" + __1.ToString().Split('(')[0] + "\" (dmgtype \"\") (victim \"" + __0.ToString().Split('(')[0] + "\")";
                            PrintLogLine(LogLine);

                            if (Pref_Log_PlayerConsole_Enable.Value)
                            {
                                string ConsoleLine = "<b>AI</b> (" + __1.ToString().Split('(')[0] + ")" + " killed <b>" + HelperMethods.GetTeamColor(victimPlayer) + victimPlayer.PlayerName + "</color></b> (" + __0.ToString().Split('(')[0] + ")";
                                HelperMethods.SendConsoleMessage(ConsoleLine);
                            }
                        }
                    }
                    else if (isAttackerHuman && Pref_Log_Kills_Include_AI_vs_Player.Value)
                    // Attacker is a human, Victim is an AI
                    {
                        int attackerUserID = GetUserId(attackerPlayer);
                        string LogLine = "\"" + attackerPlayer.PlayerName + "<" + attackerUserID + "><" + GetPlayerID(attackerPlayer) + "><" + attackerPlayer.Team.TeamShortName + ">\" killed \"" + __0.ToString().Split('(')[0] + "<><><" + __0.Team.TeamShortName + ">\" with \"" + __1.ToString().Split('(')[0] + "\" (dmgtype \"\") (victim \"" + __0.ToString().Split('(')[0] + "\")";
                        PrintLogLine(LogLine);

                        if (Pref_Log_PlayerConsole_Enable.Value)
                        {
                            string ConsoleLine = "<b>" + HelperMethods.GetTeamColor(attackerPlayer) + attackerPlayer.PlayerName + "</color></b> (" + __1.ToString().Split('(')[0] + ") killed " + "<b>AI</b> (" + __0.ToString().Split('(')[0] + ")";
                            HelperMethods.SendConsoleMessage(ConsoleLine);
                        }
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
        public static void ProcessOnPlayerChangedTeam(Player player, Team oldTeam, Team newTeam)
        {
            string theOldTeamName;
            if (oldTeam == null)
            {
                theOldTeamName = "";
            }
            else
            {
                theOldTeamName = oldTeam.TeamShortName;
            }

            if (newTeam == null)
            {
                // this happens at the begginning of each map and isn't useful logging information
                return;
            }

            if (player == null)
            {
                return;
            }


            int userID = GetUserId(player);
            string LogLine = "\"" + player.PlayerName + "<" + userID + "><" + GetPlayerID(player) + "><" + theOldTeamName + ">\" joined team \"" + newTeam.TeamShortName + "\"";
            PrintLogLine(LogLine);

            if (Pref_Log_PlayerConsole_Enable.Value)
            {
                string ConsoleLine = string.Empty;
                ConsoleLine = "<b>" + HelperMethods.GetTeamColor(player) + player.PlayerName + "</color></b> " + (oldTeam == null ? "joined team " : "changed to team ") + HelperMethods.GetTeamColor(newTeam) + newTeam.TeamShortName + "</color>";

                HelperMethods.SendConsoleMessage(ConsoleLine);
            }
        }

        [HarmonyPatch(typeof(MP_TowerDefense), nameof(MP_TowerDefense.OnPlayerChangedTeam))]
        private static class ApplyPatch_MPTowerDefense_OnPlayerChangedTeam
        {
            public static void Postfix (MP_TowerDefense __instance, Player __0, Team __1, Team __2)
            {
                try
                {
                    ProcessOnPlayerChangedTeam(__0, __1, __2);
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run MP_TowerDefense::OnPlayerChangedTeam");
                }
            }
        }

        [HarmonyPatch(typeof(MP_Strategy), nameof(MP_Strategy.OnPlayerChangedTeam))]
        private static class ApplyPatch_MPStrategy_OnPlayerChangedTeam
        {
            public static void Postfix(MP_Strategy __instance, Player __0, Team __1, Team __2)
            {
                try
                {
                    ProcessOnPlayerChangedTeam(__0, __1, __2);
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run MP_Strategy::OnPlayerChangedTeam");
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
                int userID = GetUserId(player);
                string role;

                if (args.Role == GameModeExt.ETeamRole.COMMANDER)
                {
                    role = "Commander";
                }
                else if (args.Role == GameModeExt.ETeamRole.INFANTRY)
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
            public static void Postfix(NetworkID __0, int __1, string __2)
            {
                try
                {
                    // Player player = GetPlayerFromSteamID(__0);
                    // TODO: grab old name and current team
                    string LogLine = "\"...<><" + __0.SteamID.ToString() + "><>\" changed name to \"" + __2 + "\"";
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
            public static void Postfix(DamageManager __instance, float __0, GameObject __1, byte __2, bool __3)
            {
                // should we log the damage?
                if (!Pref_Log_Damage.Value)
                {
                    return;
                }

                // was it a non-human-controlled instigator?
                if (__1 == null)
                {
                    return;
                }

                BaseGameObject attackerBase = GameFuncs.GetBaseGameObject(__1);
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
                int damage = (int)Math.Ceiling(__0);
                if (damage <= 1)
                {
                    return;
                }

                int attackerUserID = GetUserId(attackerPlayer);
                int victimUserID = GetUserId(victimPlayer);
                string LogLine = "\"" + attackerPlayer.PlayerName + "<" + attackerUserID + "><" + GetPlayerID(attackerPlayer) + "><" + attackerPlayer.Team.TeamShortName + ">\" attacked \"" + victimPlayer.PlayerName + "<" + victimUserID + "><" + GetPlayerID(victimPlayer) + "><" + victimPlayer.Team.TeamShortName + ">\" with \"" + __1.ToString().Split('(')[0] + "\"" + " (damage \"" + damage.ToString() + "\")";
                PrintLogLine(LogLine, true);
            }
        }

        // 059. Player-Player Actions
        // None for now. Re-evaluate with updates

        // 060. Player Objectives/Actions - Structure Kill
        [HarmonyPatch(typeof(Player), nameof(Player.OnTargetDestroyed))]
        private static class ApplyPatchOnTargetDestroyed
        {
            public static void Postfix(Target __0, GameObject __1)
            {
                try
                {
                    if (__0 == null || __1 == null)
                    {
                        return;
                    }

                    BaseGameObject attackerBase = GameFuncs.GetBaseGameObject(__1);
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

                    if (__0.Team == null || attackerPlayer.Team == null)
                    {
                        return;
                    }

                    // under construction buildings still appear as ObjectInfoType.Structure
                    if (__0.ObjectInfo == null || __0.ObjectInfo.ObjectType != ObjectInfoType.Structure)
                    {
                        return;
                    }

                    int userID = GetUserId(attackerPlayer);

                    string structTeam = __0.Team.TeamShortName;

                    string structName = GetStructureName(__0);

                    string LogLine = "\"" + attackerPlayer.PlayerName + "<" + userID + "><" + GetPlayerID(attackerPlayer) + "><" + attackerPlayer.Team.TeamShortName + ">\" triggered \"structure_kill\" (structure \"" + structName + "\") (weapon \"" + __1.ToString().Split('(')[0] + "\") (struct_team \"" + structTeam + "\") (construction \"" + (__0.OwnerConstructionSite == null ? "no" : "yes") + "\")";
                    PrintLogLine(LogLine);

                    if (Pref_Log_PlayerConsole_Enable.Value)
                    {
                        string ConsoleLine = "<b>" + HelperMethods.GetTeamColor(attackerPlayer) + attackerPlayer.PlayerName + "</color></b> (" + __1.ToString().Split('(')[0] + ") destroyed a " + (__0.OwnerConstructionSite == null ? "structure" : "construction site") + " (" + HelperMethods.GetTeamColor(__0.Team) + structName + "</color>)";
                        HelperMethods.SendConsoleMessageToTeam(attackerPlayer.Team, ConsoleLine);
                    }
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run OnTargetDestroyed");
                }
            }
        }

        [HarmonyPatch(typeof(MP_Strategy), nameof(MP_Strategy.OnStructureDestroyed))]
        private static class ApplyPatchOnStructureDestroyed
        {
            public static void Postfix(MP_Strategy __instance, Structure __0, GameObject __1)
            {
                try
                {
                    if (__0 == null)
                    {
                        return;
                    }

                    //check if the destruction affects the tech tier.
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
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run OnStructureDestroyed");
                }
            }
        }
        
        public static string GetStructureName(Target target)
        {
            if (target == null || target.ObjectInfo == null)
            {
                return "";
            }

            // remove any spaces or dashes from the display name
            // this is still slightly different than calling ToString() but this should be more reliable with game updates
            return target.ObjectInfo.DisplayName.Replace(" ", "").Replace("-", "");
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

                        GameModeExt gameModeInstance = GameObject.FindObjectOfType<GameModeExt>();

                        if (gameModeInstance == null)
                        {
                            return;
                        }

                        GameModeExt.ETeamsVersus versusMode = gameModeInstance.TeamsVersus;

                        if (__1 == null)
                        {
                            string RoundWinLogEarlyLine = "World triggered \"Round_Win\" (gamemode \"" + GameMode.CurrentGameMode.ToString().Split(' ')[0] + "\") (gametype \"" + versusMode.ToString() + "\")";
                            PrintLogLine(RoundWinLogEarlyLine);

                            if (Pref_Log_PlayerConsole_Enable.Value)
                            {
                                string ConsoleLine = "<b>Round is over. Game is a draw.</b>";
                                HelperMethods.SendConsoleMessage(ConsoleLine);
                            }

                            return;
                        }

                        string VictoryLogLine = "Team \"" + __1.TeamShortName + "\" triggered \"Victory\"";
                        PrintLogLine(VictoryLogLine);

                        if (Pref_Log_PlayerConsole_Enable.Value)
                        {
                            string ConsoleLine = "<b>Team " + HelperMethods.GetTeamColor(__1) + __1.TeamShortName + "</color> is victorious!</b>";
                            HelperMethods.SendConsoleMessage(ConsoleLine);
                        }

                        for (int i = 0; i < SiConstants.MaxPlayableTeams; i++)
                        {
                            Team? thisTeam = Team.Teams[i];
                            // skip GameMaster team
                            if (thisTeam == null || thisTeam.IsSpecial)
                            {
                                continue;
                            }

                            if (versusMode == GameModeExt.ETeamsVersus.HUMANS_VS_HUMANS && i == (int)SiConstants.ETeam.Alien)
                            {
                                continue;
                            }
                            else if (versusMode == GameModeExt.ETeamsVersus.HUMANS_VS_ALIENS && i == (int)SiConstants.ETeam.Centauri)
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
                                int userID = GetUserId(thisPlayer);

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
                        if (!ParserExePresent())
                        {
                            MelonLogger.Msg("Parser file not present.");
                            return;
                        }

                        // launch parser
                        MelonLogger.Msg("Launching parser.");
                        ProcessStartInfo start = new ProcessStartInfo();
                        start.FileName = Path.GetFullPath(GetParserPath());
                        string arguments = string.Format("\"{0}", GetLogFileDirectory());
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
                    GameModeExt gameModeInstance = GameObject.FindObjectOfType<GameModeExt>();
                    GameModeExt.ETeamsVersus versusMode = gameModeInstance.TeamsVersus;

                    string RoundStartLogLine = "World triggered \"Round_Start\" (gamemode \"" + GameMode.CurrentGameMode.ToString().Split(' ')[0] + "\") (gametype \"" + versusMode.ToString() + "\")";
                    
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
                        int userID = GetUserId(__0);

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
