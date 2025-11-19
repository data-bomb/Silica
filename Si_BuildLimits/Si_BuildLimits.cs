/*
Silica Build Limits
Copyright (C) 2025 by databomb

* Description *
Allows servers to enforce limitations on number of structures.

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
#else
using System.Reflection;
#endif

using HarmonyLib;
using MelonLoader;
using SilicaAdminMod;
using System;
using Si_BuildLimits;
using System.Linq;

[assembly: MelonInfo(typeof(BuildLimits), "Build Limits", "0.9.8", "databomb", "https://github.com/data-bomb/Silica")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]
[assembly: MelonOptionalDependencies("Admin Mod")]

namespace Si_BuildLimits
{
    public class BuildLimits : MelonMod
    {
        static MelonPreferences_Category _modCategory = null!;
        static MelonPreferences_Entry<bool> _Pref_Block_BotCommanders = null!;
        static MelonPreferences_Entry<int> _Pref_Limit_Bases = null!;
        static MelonPreferences_Entry<int> _Pref_Limit_Turrets = null!;
        static MelonPreferences_Entry<int> _Pref_Limit_Research = null!;
        static MelonPreferences_Entry<int> _Pref_Limit_Nodes = null!;
        static MelonPreferences_Entry<int> _Pref_Limit_Silos = null!;
        static MelonPreferences_Entry<int> _Pref_Limit_Deposits = null!;
        static MelonPreferences_Entry<int> _Pref_Limit_Prod1 = null!;
        static MelonPreferences_Entry<int> _Pref_Limit_Prod2 = null!;
        static MelonPreferences_Entry<int> _Pref_Limit_Prod3 = null!;
        static MelonPreferences_Entry<int> _Pref_Limit_Prod4 = null!;
        static MelonPreferences_Entry<int> _Pref_Limit_Prod5 = null!;

        public override void OnInitializeMelon()
        {
            _modCategory ??= MelonPreferences.CreateCategory("Silica");
            _Pref_Block_BotCommanders ??= _modCategory.CreateEntry<bool>("BuildLimits_EnforceLimitsOnAI", true);
            _Pref_Limit_Bases ??= _modCategory.CreateEntry<int>("BuildLimits_Bases", -1);
            _Pref_Limit_Turrets ??= _modCategory.CreateEntry<int>("BuildLimits_Turrets", 20);
            _Pref_Limit_Research ??= _modCategory.CreateEntry<int>("BuildLimits_Research", -1);
            _Pref_Limit_Nodes ??= _modCategory.CreateEntry<int>("BuildLimits_Nodes", -1);
            _Pref_Limit_Silos ??= _modCategory.CreateEntry<int>("BuildLimits_Silos", -1);
            _Pref_Limit_Deposits ??= _modCategory.CreateEntry<int>("BuildLimits_Deposits", -1);
            _Pref_Limit_Prod1 ??= _modCategory.CreateEntry<int>("BuildLimits_Production_Level1", -1);
            _Pref_Limit_Prod2 ??= _modCategory.CreateEntry<int>("BuildLimits_Production_Level2", -1);
            _Pref_Limit_Prod3 ??= _modCategory.CreateEntry<int>("BuildLimits_Production_Level3", -1);
            _Pref_Limit_Prod4 ??= _modCategory.CreateEntry<int>("BuildLimits_Production_Level4", -1);
            _Pref_Limit_Prod5 ??= _modCategory.CreateEntry<int>("BuildLimits_Production_Level5", -1);
        }

        public override void OnLateInitializeMelon()
        {
            //subscribing to the events
            Event_Construction.OnRequestBuildStructure += OnRequestBuildStructure_LimitCheck;
        }

        public void NotifyLimitsEnforced(Team team, int maxStructures, string type)
        {
            // find if team has commander
            Player? commander = null;
            if (GameMode.CurrentGameMode is GameModeExt gameModeExt)
            {
                commander = gameModeExt.GetCommanderForTeam(team);
            }

            if (commander != null)
            {
                string response = $"{type} structure limit ({maxStructures}) exceeded" + (maxStructures > 0 ? ". Sell/destroy before building again." : ".");
                HelperMethods.SendConsoleMessageToPlayer(commander, response);
                HelperMethods.ReplyToCommand_Player(commander, response);
            }
        }

        public void OnRequestBuildStructure_LimitCheck(object? sender, OnRequestBuildStructureArgs args)
        {
            try
            {
                if (args == null)
                {
                    return;
                }

                // ignore AI build depending on preferences
                if (!_Pref_Block_BotCommanders.Value && !args.PlayerInitiated)
                {
                    return;
                }

                ConstructionData constructionData = args.ConstructionData;
                Structure parentStructure = args.ParentStructure;
                if (!constructionData || !constructionData.IsStructure || !constructionData.ObjectToBuild || !parentStructure || !parentStructure.Team)
                {
                    return;
                }

                // check for base limits (Headquarters, Nest)
                if (_Pref_Limit_Bases.Value >= 0 && parentStructure.Team.BaseStructure == constructionData.ObjectInfo)
                {
                    int baseStructureCount = parentStructure.Team.GetStructureCount(constructionData.ObjectInfo);
                    if (baseStructureCount >= _Pref_Limit_Bases.Value)
                    {
                        NotifyLimitsEnforced(parentStructure.Team, _Pref_Limit_Bases.Value, "Base");
                        args.Block = true;
                    }

                    return;
                }

                
                // check for turret limits (HeavyTurret, ThornSpire, HiveSpire, etc.) [note: includes RadarStation]
                if (_Pref_Limit_Turrets.Value >= 0 && constructionData.ObjectInfo.StructureType == StructureType.Defense)
                {
                    int structureTypeCount = GetStructureTypeCount(parentStructure.Team, constructionData.ObjectInfo);
                    if (structureTypeCount >= _Pref_Limit_Turrets.Value)
                    {
                        NotifyLimitsEnforced(parentStructure.Team, _Pref_Limit_Turrets.Value, "Turret");
                        args.Block = true;
                    }

                    return;
                }

                // check for production limits
                if (constructionData.ObjectInfo.StructureType == StructureType.Production)
                {
                    if (_Pref_Limit_Prod1.Value >= 0 && constructionData.ObjectInfo.StructureSelectionType == StructureSelectionType.Units1)
                    {
                        int structureSelectionTypeCount = GetStructureSelectionTypeCount(parentStructure.Team, constructionData.ObjectInfo);
                        if (structureSelectionTypeCount >= _Pref_Limit_Prod1.Value)
                        {
                            NotifyLimitsEnforced(parentStructure.Team, _Pref_Limit_Prod1.Value, (parentStructure.Team.Index == (int)SiConstants.ETeam.Alien ? "Lesser Spawning Cyst" : "Barracks"));
                            args.Block = true;
                        }

                        return;
                    }

                    if (_Pref_Limit_Prod2.Value >= 0 && constructionData.ObjectInfo.StructureSelectionType == StructureSelectionType.Units2)
                    {
                        int structureSelectionTypeCount = GetStructureSelectionTypeCount(parentStructure.Team, constructionData.ObjectInfo);
                        if (structureSelectionTypeCount >= _Pref_Limit_Prod2.Value)
                        {
                            NotifyLimitsEnforced(parentStructure.Team, _Pref_Limit_Prod2.Value, (parentStructure.Team.Index == (int)SiConstants.ETeam.Alien ? "Greater Spawning Cyst" : "Light Vehicle Factory"));
                            args.Block = true;
                        }

                        return;
                    }

                    if (_Pref_Limit_Prod3.Value >= 0 && constructionData.ObjectInfo.StructureSelectionType == StructureSelectionType.Units3)
                    {
                        int structureSelectionTypeCount = GetStructureSelectionTypeCount(parentStructure.Team, constructionData.ObjectInfo);
                        if (structureSelectionTypeCount >= _Pref_Limit_Prod3.Value)
                        {
                            NotifyLimitsEnforced(parentStructure.Team, _Pref_Limit_Prod3.Value, (parentStructure.Team.Index == (int)SiConstants.ETeam.Alien ? "Grand Spawning Cyst" : "Heavy Vehicle Factory"));
                            args.Block = true;
                        }

                        return;
                    }

                    if (_Pref_Limit_Prod4.Value >= 0 && constructionData.ObjectInfo.StructureSelectionType == StructureSelectionType.Units4)
                    {
                        int structureSelectionTypeCount = GetStructureSelectionTypeCount(parentStructure.Team, constructionData.ObjectInfo);
                        if (structureSelectionTypeCount >= _Pref_Limit_Prod4.Value)
                        {
                            NotifyLimitsEnforced(parentStructure.Team, _Pref_Limit_Prod4.Value, (parentStructure.Team.Index == (int)SiConstants.ETeam.Alien ? "Colossal Spawning Cyst" : "Ultra Heavy Vehicle Factory"));
                            args.Block = true;
                        }

                        return;
                    }

                    if (_Pref_Limit_Prod5.Value >= 0 && constructionData.ObjectInfo.StructureSelectionType == StructureSelectionType.Units5)
                    {
                        int structureSelectionTypeCount = GetStructureSelectionTypeCount(parentStructure.Team, constructionData.ObjectInfo);
                        if (structureSelectionTypeCount >= _Pref_Limit_Prod5.Value)
                        {
                            NotifyLimitsEnforced(parentStructure.Team, _Pref_Limit_Prod5.Value, (parentStructure.Team.Index == (int)SiConstants.ETeam.Alien ? "tbd" : "Air Factory"));
                            args.Block = true;
                        }
                    }

                    return;
                }

                // check for research (QuantumCortex, ResearchFacility)
                if (_Pref_Limit_Research.Value >= 0 && constructionData.ObjectInfo.StructureType == StructureType.Research)
                {
                    int structureTypeCount = GetStructureTypeCount(parentStructure.Team, constructionData.ObjectInfo);
                    if (structureTypeCount >= _Pref_Limit_Research.Value)
                    {
                        NotifyLimitsEnforced(parentStructure.Team, _Pref_Limit_Research.Value, "Research");
                        args.Block = true;
                    }

                    return;
                }

                // check for resource deposits (BioCache, Refinery)
                if (_Pref_Limit_Deposits.Value >= 0 && constructionData.ObjectInfo.StructureType == StructureType.Resource &&
                        constructionData.ObjectInfo.HasResourceDeposit && constructionData.ObjectInfo.HasResourceStorage)
                {
                    int depositStructureCount = parentStructure.Team.GetStructureCount(constructionData.ObjectInfo);
                    if (depositStructureCount >= _Pref_Limit_Deposits.Value)
                    {
                        NotifyLimitsEnforced(parentStructure.Team, _Pref_Limit_Deposits.Value, (parentStructure.Team.Index == (int)SiConstants.ETeam.Alien ? "BioCache" : "Refinery"));
                        args.Block = true;
                    }

                    return;
                }

                // check for Silos
                if (_Pref_Limit_Silos.Value >= 0 && constructionData.ObjectInfo.StructureType == StructureType.Resource &&
                        !constructionData.ObjectInfo.HasResourceDeposit && constructionData.ObjectInfo.HasResourceStorage)
                {
                    int siloStructureCount = parentStructure.Team.GetStructureCount(constructionData.ObjectInfo);
                    if (siloStructureCount >= _Pref_Limit_Silos.Value)
                    {
                        NotifyLimitsEnforced(parentStructure.Team, _Pref_Limit_Silos.Value, "Silo");
                        args.Block = true;
                    }

                    return;
                }

                // check for Nodes
                if (_Pref_Limit_Nodes.Value >= 0 && constructionData.ObjectInfo.StructureType == StructureType.Resource && 
                        !constructionData.ObjectInfo.HasResourceDeposit && !constructionData.ObjectInfo.HasResourceStorage)
                {
                    int nodeStructureCount = parentStructure.Team.GetStructureCount(constructionData.ObjectInfo);
                    if (nodeStructureCount >= _Pref_Limit_Nodes.Value)
                    {
                        NotifyLimitsEnforced(parentStructure.Team, _Pref_Limit_Nodes.Value, "Node");
                        args.Block = true;
                    }

                    return;
                }
            }
            catch (Exception error)
            {
                HelperMethods.PrintError(error, "Failed to run OnRequestBuildStructure_LimitCheck");
            }
        }
        public int GetStructureTypeCount(Team team, ObjectInfo structureInfo)
        {
            int num = 0;
            foreach (Structure structure in team.Structures)
            {
                if (!structure)
                {
                    MelonLogger.Error("GetStructureTypeCount: A structure is NULL for team '" + team.TeamShortName + "', skipping it...");
                }
                else if ((structure.ObjectInfo.StructureType == structureInfo.StructureType) && !structure.IsDestroyed)
                {
                    num++;
                }
            }
            return num;
        }

        public int GetStructureSelectionTypeCount(Team team, ObjectInfo structureInfo)
        {
            int num = 0;
            foreach (Structure structure in team.Structures)
            {
                if (!structure)
                {
                    MelonLogger.Error("GetStructureSelectionTypeCount: A structure is NULL for team '" + team.TeamShortName + "', skipping it...");
                }
                else if ((structure.ObjectInfo.StructureSelectionType == structureInfo.StructureSelectionType) && !structure.IsDestroyed)
                {
                    num++;
                }
            }
            return num;
        }
    }
}