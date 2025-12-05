/*
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
using Si_Logging;
using UnityEngine;
using System;
using SilicaAdminMod;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

[assembly: MelonInfo(typeof(HL_Logging), "Half-Life Logger", "1.8.10", "databomb&zawedcvg", "https://github.com/data-bomb/Silica")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]
#if NET6_0
[assembly: MelonOptionalDependencies("Admin Mod", "QList")]
#else
[assembly: MelonOptionalDependencies("Admin Mod")]
#endif

namespace Si_Logging
{
    // https://developer.valvesoftware.com/wiki/HL_Log_Standard
    public partial class HL_Logging : MelonMod
    {
        static int[] teamResourcesCollected = new int[SiConstants.MaxPlayableTeams + 1];
        static Player?[]? lastCommander;

        static MelonPreferences_Category _modCategory = null!;
        public static MelonPreferences_Entry<bool> Pref_Log_Damage = null!;
        public static MelonPreferences_Entry<bool> Pref_Display_Damage = null!;
        public static MelonPreferences_Entry<bool> Pref_Log_Kills_Include_AI_vs_Player = null!;
        public static MelonPreferences_Entry<string> Pref_Log_ParserExe = null!;
        public static MelonPreferences_Entry<string> Pref_Log_VideoExe = null!;
        public static MelonPreferences_Entry<int> Pref_Log_VideoPlayerThreshold = null!;
        public static MelonPreferences_Entry<float> Pref_Log_PerfMonitor_Interval = null!;
        public static MelonPreferences_Entry<bool> Pref_Log_PerfMonitor_Enable = null!;
        public static MelonPreferences_Entry<bool> Pref_Log_PlayerConsole_Enable = null!;
        public static MelonPreferences_Entry<int> Pref_Log_MinDamageCutoff = null!;

        public override void OnInitializeMelon()
        {
            try
            {
                _modCategory ??= MelonPreferences.CreateCategory("Silica");
                Pref_Log_Damage ??= _modCategory.CreateEntry<bool>("Logging_LogDamage", false);
                Pref_Display_Damage ??= _modCategory.CreateEntry<bool>("Logging_DisplayDamage", true);
                Pref_Log_MinDamageCutoff ??= _modCategory.CreateEntry<int>("Logging_LogDamage_MinDmgCutoff", 1);
                Pref_Log_Kills_Include_AI_vs_Player ??= _modCategory.CreateEntry<bool>("Logging_LogKills_IncludeAIvsPlayer", true);
                Pref_Log_ParserExe ??= _modCategory.CreateEntry<string>("Logging_ParserExePath", "parser.exe");
                Pref_Log_VideoExe ??= _modCategory.CreateEntry<string>("Logging_Video_ExePath", "video-generator.exe");
                Pref_Log_VideoPlayerThreshold ??= _modCategory.CreateEntry<int>("Logging_Video_MinimumPlayersNeeded", 2);
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

        [MethodImpl(MethodImplOptions.NoOptimization)]
        public override void OnLateInitializeMelon()
        {
            HelperMethods.StartTimer(ref Timer_PerfMonitorLog);

            //subscribing to the event
            Event_Roles.OnRoleChanged += OnRoleChanged;
            Event_Chat.OnRequestPlayerChat += OnRequestPlayerChat;
            Event_Structures.OnCommanderDestroyedStructure += OnCommanderDestroyedStructure_Log;

            #if NET6_0
            bool QListLoaded = RegisteredMelons.Any(m => m.Info.Name == "QList");
            if (QListLoaded)
            {
                QListRegistration();
            }
            #endif
        }

        #if NET6_0
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void QListRegistration()
        {
            QList.Options.RegisterMod(this);

            QList.OptionTypes.BoolOption logDamage = new(Pref_Log_Damage, Pref_Log_Damage.Value);
            QList.OptionTypes.BoolOption logAllKills = new(Pref_Log_Kills_Include_AI_vs_Player, Pref_Log_Kills_Include_AI_vs_Player.Value);
            QList.Options.AddOption(logDamage);

            QList.Options.AddOption(logAllKills);
        }
        #endif

        // 003. Change Map
        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            try
            {
                if (sceneName == "Intro" || sceneName == "MainMenu" || sceneName == "Loading")
                {
                    return;
                }
                
                PrintLogLine($"Loading map \"{sceneName}\"");

                DamageDatabase.ResetRound();

                if (Pref_Log_PlayerConsole_Enable.Value)
                {
                    HelperMethods.SendConsoleMessage($"<b>Loading map \"{sceneName}\"</b>");
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
                    string connectionID = GetConnectionString(__0);

                    PrintLogLine($"{connectionID} connected, address \"127.0.0.1:27015\"");
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
                    if (__0 == null)
                    {
                        return;
                    }

                    // TODO: Find current name
                    string connectingPlayer = AddPlayerLogEntry(__0);

                    PrintLogLine($"{connectingPlayer} entered the game");
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
                    if (__0 == null)
                    {
                        return;
                    }

                    string disconnectingPlayer = AddPlayerLogEntry(__0);

                    PrintLogLine($"{disconnectingPlayer} disconnected");

                    if (Pref_Log_PlayerConsole_Enable.Value)
                    {
                        string disconnectingPretty = AddPlayerConsoleEntry(__0);
                        HelperMethods.SendConsoleMessage($"{disconnectingPretty} disconnected.");
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
                    string kickedPlayer = AddPlayerLogEntry(__0);

                    PrintLogLine($"Kick: {kickedPlayer} was kicked by \"Console\" (message \"\")");
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

                    #pragma warning disable CS8604 // Dereference of a possibly null reference.
                    if (isVictimHuman)
                    {
                        DamageDatabase.OnPlayerDeath(victimPlayer);

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
                                string victim = AddPlayerLogEntry(victimPlayer);
                                string instigator = GetNameFromObject(__1);
                                string position = GetPlayerPosition(__0);

                                PrintLogLine($"{victim} committed suicide with \"{instigator}\" (dmgtype \"\") {position}");

                                if (Pref_Log_PlayerConsole_Enable.Value)
                                {
                                    string victimPretty = AddPlayerConsoleEntry(victimPlayer);

                                    HelperMethods.SendConsoleMessage($"{victimPretty} ({instigator}) committed suicide");
                                }
                            }
                            // human-controlled player killed another human-controlled player
                            else
                            {
                                string attacker = AddPlayerLogEntry(attackerPlayer);
                                string victim = AddPlayerLogEntry(victimPlayer);
                                string weapon = AddKilledWithEntry(__0, __1);
                                string position = GetPlayerPosition(__0, __1);

                                PrintLogLine($"{attacker} killed {victim} with {weapon} {position}");

                                if (Pref_Log_PlayerConsole_Enable.Value)
                                {
                                    string attackerPretty = AddPlayerConsoleEntry(attackerPlayer);
                                    string victimPretty = AddPlayerConsoleEntry(victimPlayer);
                                    string attackerWeapon = GetNameFromObject(__1);
                                    string victimUnit = GetNameFromUnit(__0);

                                    HelperMethods.SendConsoleMessage($"{attackerPretty} ({attackerWeapon}) killed {victimPretty} ({victimUnit})");
                                }
                            }
                        }
                        else if (Pref_Log_Kills_Include_AI_vs_Player.Value)
                        // Attacker is an AI, Victim is a human
                        {
                            string attacker = AddAIAttackerLogEntry(__1, attackerBase.Team);
                            string victim = AddPlayerLogEntry(victimPlayer);
                            string weapon = AddKilledWithEntry(__0, __1);
                            string victimUnit = GetNameFromUnit(__0);
                            string position = GetPlayerPosition(__0, __1);

                            PrintLogLine($"{attacker} killed {victim} with {weapon} {position}");

                            if (Pref_Log_PlayerConsole_Enable.Value)
                            {
                                string AIPretty = AddAIConsoleEntry();
                                string instigator = GetNameFromObject(__1);
                                string victimPretty = AddPlayerConsoleEntry(victimPlayer);

                                HelperMethods.SendConsoleMessage($"{AIPretty} ({instigator}) killed {victimPretty} ({victimUnit})");
                            }
                        }
                    }
                    else if (isAttackerHuman && Pref_Log_Kills_Include_AI_vs_Player.Value)
                    // Attacker is a human, Victim is an AI
                    {
                        string attacker = AddPlayerLogEntry(attackerPlayer);
                        string victim = AddAIVictimLogEntry(__0);
                        string weapon = AddKilledWithEntry(__0, __1);
                        string position = GetPlayerPosition(__0, __1);

                        PrintLogLine($"{attacker} killed {victim} with {weapon} {position}");

                        if (Pref_Log_PlayerConsole_Enable.Value)
                        {
                            string AIPretty = AddAIConsoleEntry();
                            string attackerPretty = AddPlayerConsoleEntry(attackerPlayer);
                            string instigator = GetNameFromObject(__1);
                            string victimUnit = GetNameFromUnit(__0);

                            HelperMethods.SendConsoleMessage($"{attackerPretty} ({instigator}) killed {AIPretty} ({victimUnit})");
                        }
                    }
                    #pragma warning restore CS8604 // Dereference of a possibly null reference.
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
                theOldTeamName = string.Empty;
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

            string playerEntry = AddPlayerLogEntry(player, theOldTeamName);
            
            PrintLogLine($"{playerEntry} joined team \"{newTeam.TeamShortName}\"");

            if (Pref_Log_PlayerConsole_Enable.Value)
            {
                string playerPretty = AddPlayerConsoleEntry(player);
                string action = (oldTeam == null ? "joined team " : "changed to team ");

                HelperMethods.SendConsoleMessage($"{playerPretty} {action} {HelperMethods.GetTeamColor(newTeam)}{newTeam.TeamShortName}</color>");
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
                if (args == null)
                {
                    return;
                }

                Player player = args.Player;

                if (player == null || player.Team == null)
                {
                    return;
                }

                string role = GetRoleName(args.Role);
                string playerEntry = AddPlayerLogEntry(player);

                PrintLogLine($"{playerEntry} changed role to \"{role}\"");
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
                    string steamID = GetPlayerID(__0);
                    PrintLogLine($"\"...<><{steamID}><>\" changed name to \"{__2}\"");
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run SendPlayerChangeName");
                }
            }
        }

        public static Player? GetPlayer(BaseGameObject? baseGameObject)
        {
            if (baseGameObject == null)
            {
                return null;
            }

            NetworkComponent? networkComponent = baseGameObject.NetworkComponent;
            if (networkComponent == null)
            {
                return null;
            }

            Player? player = networkComponent.OwnerPlayer;
            if (player == null)
            {
                return null;
            }

            return player;
        }

        public static Player? GetPlayer(DamageManager? damageManager)
        {
            if (damageManager == null)
            {
                return null;
            }

            BaseGameObject baseObject = damageManager.Owner;
            if (baseObject == null)
            {
                return null;
            }

            return GetPlayer(baseObject);
        }

        public static Player? GetPlayer(GameObject? gameObject)
        {
            // was it a non-player-controlled object?
            if (gameObject == null)
            {
                return null;
            }

            BaseGameObject? baseObject = GameFuncs.GetBaseGameObject(gameObject);
            if (baseObject == null)
            {
                return null;
            }

            return GetPlayer(baseObject);
        }

        public static bool ShouldHandleDamage(float damage)
        {
            // was damage less than (or equal to) the minimum cut-off value?
            int damageRounded = (int)Math.Ceiling(damage);
            if (damageRounded <= Pref_Log_MinDamageCutoff.Value)
            {
                return false;
            }

            return true;
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
                // are there any damage-related settings that are enabled?
                if (!Pref_Log_Damage.Value && !Pref_Display_Damage.Value)
                {
                    return;
                }

                // should we continue handling the damage?
                if (!ShouldHandleDamage(__0))
                {
                    return;
                }

                Player? attackerPlayer = GetPlayer(__1);
                Player? victimPlayer = GetPlayer(__instance);

                // was it a non-player-controlled instigator?
                if (attackerPlayer == null)
                {
                    return;
                }

                // was it a non-player-controlled victim?
                if (victimPlayer == null)
                {
                    return;
                }

                // at this point we have confirmed it was PvP damage
                if (Pref_Display_Damage.Value)
                {
                    DamageDatabase.AddDamage(victimPlayer, attackerPlayer, __0);
                }

                if (Pref_Log_Damage.Value)
                {
                    string attacker = AddPlayerLogEntry(attackerPlayer);
                    string victim = AddPlayerLogEntry(victimPlayer);
                    string weapon = GetNameFromObject(__1);

                    PrintLogLine($"{attackerPlayer} attacked {victimPlayer} with \"{weapon}\" (damage \"{__0.ToString("#.#")}\")", true);
                }
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
                    bool isAttackerHuman = (attackerPlayer != null);

                    if (__0.Team == null || attackerBase.Team == null)
                    {
                        return;
                    }

                    // under construction buildings still appear as ObjectInfoType.Structure
                    if (__0.ObjectInfo == null || __0.ObjectInfo.ObjectType != ObjectInfoType.Structure)
                    {
                        return;
                    }
                    
                    string playerEntry = (isAttackerHuman ? AddPlayerLogEntry(attackerPlayer) : AddAIAttackerLogEntry(__1, attackerBase.Team));
                    string structName = GetStructureName(__0);
                    string structTeam = __0.Team.TeamShortName;
                    string weapon = GetNameFromObject(__1);
                    string construction = (__0.OwnerConstructionSite == null ? "no" : "yes");
                    string position = GetLogPosition(__0.gameObject.transform.position);

                    PrintLogLine($"{playerEntry} triggered \"structure_kill\" (structure \"{structName}\") (weapon \"{weapon}\") (struct_team \"{structTeam}\") (construction \"{construction}\") (building_position \"{position}\")");

                    if (Pref_Log_PlayerConsole_Enable.Value)
                    {
                        string playerPretty = (isAttackerHuman ? AddPlayerConsoleEntry(attackerPlayer) : AddAIConsoleEntry());
                        string type = (__0.OwnerConstructionSite == null ? "structure" : "construction site");

                        HelperMethods.SendConsoleMessageToTeam(attackerBase.Team, $"{playerPretty} ({weapon}) destroyed a {type} ({HelperMethods.GetTeamColor(__0.Team)}{structName}</color>)");
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
                        if (tier != currentTechTier[structureTeam.Index])
                        {
                            currentTechTier[structureTeam.Index] = tier;
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

        // 061. Team Objectives/Actions - Structure Deletion
        public void OnCommanderDestroyedStructure_Log(object? sender, OnCommanderDestroyedStructureArgs args)
        {
            try
            {
                if (args == null)
                {
                    return;
                }

                Structure structure = args.Structure;
                Team team = args.Team;

                if (structure == null || team == null)
                {
                    return;
                }

                string teamName = GetTeamName(team);
                string structName = GetStructureName(structure);
                string position = GetLogPosition(structure.transform.position);

                PrintLogLine($"Team \"{teamName}\" triggered \"structure_sold\" (building_name \"{structName}\") (building_position \"{position}\")");

                if (Pref_Log_PlayerConsole_Enable.Value)
                {
                    string teamPretty = GetTeamName(team);

                    HelperMethods.SendConsoleMessageToTeam(team, $"{teamPretty} sold a structure ({HelperMethods.GetTeamColor(team)}{structName}</color>)");
                }
            }
            catch (Exception error)
            {
                HelperMethods.PrintError(error, "Failed to run OnCommanderDestroyedStructure_Log");
            }
        }

        // 061. Team Objectives/Actions - Structure Placement
        [HarmonyPatch(typeof(ConstructionData), nameof(ConstructionData.RequestConstructionSite))]
        private static class ApplyPatchRequestConstructionSite
        {
            public static void Postfix(ConstructionData __instance, ConstructionSite __result, Structure __0, Vector3 __1, Quaternion __2, Team __3, Transform __4, float __5)
            {
                if (__instance == null || __result == null)
                {
                    return;
                }

                if (__instance.ObjectInfo.ObjectType != ObjectInfoType.Structure)
                {
                    return;
                }

                string teamName = GetTeamName(__3);
                string structName = GetStructureName(__result);
                string position = GetLogPosition(__1);

                PrintLogLine($"Team \"{teamName}\" triggered \"construction_start\" (building_name \"{structName}\") (building_position \"{position}\")");
            }
        }

        // 061. Team Objectives/Actions - Structure Complete
        #if NET6_0
        [HarmonyPatch(typeof(ConstructionSite), nameof(ConstructionSite.SpawnObject))]
        #else
        [HarmonyPatch(typeof(ConstructionSite), "SpawnObject")]
        #endif
        private static class ApplyPatchConstructionSpawned
        {
            public static void Postfix(ConstructionSite __instance)
            {
                if (__instance == null)
                {
                    return;
                }

                if (__instance.ObjectInfo.ObjectType != ObjectInfoType.Structure)
                {
                    return;
                }

                string teamName = GetTeamName(__instance.Team);
                string structName = GetStructureName(__instance);
                string position = GetLogPosition(__instance.transform.position);

                PrintLogLine($"Team \"{teamName}\" triggered \"construction_complete\" (building_name \"{structName}\") (building_position \"{position}\")");
            }
        }

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

                        if (VictimDamage != null)
                        {
                            DamageDatabase.ResetRound();
                        }

                        GameModeExt gameModeInstance = GameObject.FindFirstObjectByType<GameModeExt>();

                        if (gameModeInstance == null)
                        {
                            return;
                        }

                        string gametype = GetGameType(gameModeInstance);

                        if (__1 == null)
                        {
                            string gamemode = GetGameMode();

                            PrintLogLine($"World triggered \"Round_Win\" (gamemode \"{gamemode}\") (gametype \"{gametype}\")");

                            if (Pref_Log_PlayerConsole_Enable.Value)
                            {
                                HelperMethods.SendConsoleMessage($"<b>Round is over. Game is a draw.</b>");
                            }

                            return;
                        }

                        string teamName = GetTeamName(__1);
                        PrintLogLine($"Team \"{teamName}\" triggered \"Victory\"");

                        if (Pref_Log_PlayerConsole_Enable.Value)
                        {
                            HelperMethods.SendConsoleMessage($"<b>Team {HelperMethods.GetTeamColor(__1)}{teamName}</color> is victorious!</b>");
                        }

                        GameModeExt.ETeamsVersus versusMode = gameModeInstance.TeamsVersus;

                        for (int i = 0; i < SiConstants.MaxPlayableTeams; i++)
                        {
                            // skip GameMaster and Wildlife teams
                            if (i == (int)SiConstants.ETeam.Wildlife || i == (int)SiConstants.ETeam.Gamemaster)
                            {
                                continue;
                            }

                            Team? thisTeam = Team.Teams[i];
                            if (thisTeam == null || thisTeam.IsSpecial)
                            {
                                continue;
                            }

                            // skip disabled teams
                            if (versusMode == GameModeExt.ETeamsVersus.HUMANS_VS_HUMANS && i == (int)SiConstants.ETeam.Alien)
                            {
                                continue;
                            }
                            else if (versusMode == GameModeExt.ETeamsVersus.HUMANS_VS_ALIENS && i == (int)SiConstants.ETeam.Centauri)
                            {
                                continue;
                            }

                            string resourcesCollected = teamResourcesCollected[thisTeam.Index].ToString();
                            string playerCount = thisTeam.GetNumPlayers().ToString();
                            teamName = GetTeamName(i);

                            PrintLogLine($"Team \"{teamName}\" scored \"{resourcesCollected}\" with \"{playerCount}\" players");
                        }

                        int roundPlayers = 0;

                        for (int i = 0; i < Player.Players.Count; i++)
                        {
                            if (Player.Players[i] == null)
                            {
                                continue;
                            }

                            Player thisPlayer = Player.Players[i];
                            string playerEntry = AddPlayerLogEntry(thisPlayer);

                            PrintLogLine($"Player {playerEntry} scored \"{thisPlayer.Score}\" (kills \"{thisPlayer.Kills}\") (deaths \"{thisPlayer.Deaths}\")");

                            roundPlayers++;
                        }

                        PrintLogLine($"World triggered \"Round_Win\" (gametype \"{gametype}\")");

                        // try and call parser
                        if (ParserExePresent())
                        {
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
                        else
                        {
                            MelonLogger.Msg("Parser file not present.");
                        }

                        // try and make round recap video
                        if (VideoGeneratorExePresent())
                        {
                            // check if the round reached the player threshold
                            if (roundPlayers >= Pref_Log_VideoPlayerThreshold.Value)
                            {
                                // launch video generator
                                MelonLogger.Msg("Launching video generator.");
                                ProcessStartInfo start = new ProcessStartInfo();
                                start.FileName = Path.GetFullPath(GetVideoGeneratorPath());
                                string arguments = string.Format("\"{0}", GetLogFileDirectory());
                                start.Arguments = arguments;
                                start.UseShellExecute = false;
                                start.RedirectStandardOutput = false;
                                using Process? process = Process.Start(start);
                            }
                            else
                            {
                                MelonLogger.Msg("Video generator not called due to low player count.");
                            }
                        }
                        else
                        {
                            MelonLogger.Msg("Video generator file not present.");
                        }
                    }
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run OnGameEnded");
                }
            }
        }

        // 061. Team Objectives/Actions - Research Tier
        public static int[] currentTechTier = new int[SiConstants.MaxPlayableTeams];
        public static int getHighestTechTier(Team team)
        {
            return team.TechnologyTier;
        }

        public static void initializeRound(ref int[] tiers)
        {
            for (int i = 0; i < Team.NumTeams; i++)
            {
                tiers[Team.Teams[i].Index] = 0;
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
                    if (tier != currentTechTier[siteTeam.Index])
                    {
                        currentTechTier[siteTeam.Index] = tier;
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
            string teamName = GetTeamName(team);
            PrintLogLine($"Team \"{teamName}\" triggered \"technology_change\" (tier \"{tier}\")");
        }
        
        // 062. World Objectives/Actions - Round_Start
        [HarmonyPatch(typeof(MusicJukeboxHandler), nameof(MusicJukeboxHandler.OnGameStarted))]
        private static class ApplyPatchOnGameStarted
        {
            public static void Prefix(MusicJukeboxHandler __instance, GameMode __0)
            {
                try
                {
                    GameModeExt gameModeInstance = GameObject.FindFirstObjectByType<GameModeExt>();

                    string gamemode = GetGameMode();
                    string gametype = GetGameType(gameModeInstance);
                    
                    PrintLogLine($"World triggered \"Round_Start\" (gamemode \"{gamemode}\") (gametype \"{gametype}\")");

                    initializeRound(ref currentTechTier);
                    firedRoundEndOnce = false;
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run OnGameStarted");
                }
            }
        }

        // 063. Chat
        public void OnRequestPlayerChat(object? sender, OnRequestPlayerChatArgs args)
        {
            if (args.Player == null)
            {
                return;
            }

            string playerEntry = AddPlayerLogEntry(args.Player);
            string action = (args.TeamOnly == false ? "say" : "say_team");

            PrintLogLine($"{playerEntry} {action} \"{args.Text}\"");
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
