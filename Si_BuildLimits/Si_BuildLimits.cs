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

[assembly: MelonInfo(typeof(BuildLimits), "Build Limits", "1.0.1", "databomb", "https://github.com/data-bomb/Silica")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]
[assembly: MelonOptionalDependencies("Admin Mod")]

namespace Si_BuildLimits
{
    public class BuildLimits : MelonMod
    {
        const int nolimit = -1;

        static MelonPreferences_Category _modCategory = null!;
        static MelonPreferences_Entry<bool> _Pref_Block_BotCommanders = null!;
        static MelonPreferences_Entry<int> _Pref_Limit_Bases_Humans = null!;
        static MelonPreferences_Entry<int> _Pref_Limit_Bases_Aliens = null!;
        static MelonPreferences_Entry<int> _Pref_Limit_Turrets_Humans = null!;
        static MelonPreferences_Entry<int> _Pref_Limit_Turrets_Aliens = null!;
        static MelonPreferences_Entry<int> _Pref_Limit_Research_Humans = null!;
        static MelonPreferences_Entry<int> _Pref_Limit_Research_Alien = null!;
        static MelonPreferences_Entry<int> _Pref_Limit_Nodes = null!;
        static MelonPreferences_Entry<int> _Pref_Limit_Silos = null!;
        static MelonPreferences_Entry<int> _Pref_Limit_Deposits_Humans = null!;
        static MelonPreferences_Entry<int> _Pref_Limit_Deposits_Aliens = null!;
        static MelonPreferences_Entry<int> _Pref_Limit_Prod1_Humans = null!;
        static MelonPreferences_Entry<int> _Pref_Limit_Prod1_Aliens = null!;
        static MelonPreferences_Entry<int> _Pref_Limit_Prod2_Humans = null!;
        static MelonPreferences_Entry<int> _Pref_Limit_Prod2_Aliens = null!;
        static MelonPreferences_Entry<int> _Pref_Limit_Prod3_Humans = null!;
        static MelonPreferences_Entry<int> _Pref_Limit_Prod3_Aliens = null!;
        static MelonPreferences_Entry<int> _Pref_Limit_Prod4_Humans = null!;
        static MelonPreferences_Entry<int> _Pref_Limit_Prod4_Aliens = null!;
        static MelonPreferences_Entry<int> _Pref_Limit_Prod5_Humans = null!;
        static MelonPreferences_Entry<int> _Pref_Limit_Prod5_Aliens = null!;

        public override void OnInitializeMelon()
        {
            _modCategory ??= MelonPreferences.CreateCategory("Silica");
            _Pref_Block_BotCommanders ??= _modCategory.CreateEntry<bool>("BuildLimits_EnforceLimitsOnAI", true);
            _Pref_Limit_Bases_Humans ??= _modCategory.CreateEntry<int>("BuildLimits_Humans_Bases",         nolimit); // HQ
            _Pref_Limit_Bases_Aliens ??= _modCategory.CreateEntry<int>("BuildLimits_Aliens_Bases",         nolimit); // Nest
            _Pref_Limit_Turrets_Humans ??= _modCategory.CreateEntry<int>("BuildLimits_Humans_Turrets",          15); // Turrets + RadarStations
            _Pref_Limit_Turrets_Aliens ??= _modCategory.CreateEntry<int>("BuildLimits_Aliens_Turrets",          25); // Spires
            _Pref_Limit_Research_Humans ??= _modCategory.CreateEntry<int>("BuildLimits_Humans_Research",   nolimit); // ResearchFactory
            _Pref_Limit_Research_Alien ??= _modCategory.CreateEntry<int>("BuildLimits_Aliens_Research",    nolimit); // QuantumCortex
            _Pref_Limit_Nodes ??= _modCategory.CreateEntry<int>("BuildLimits_Nodes",                       nolimit); // Nodes
            _Pref_Limit_Silos ??= _modCategory.CreateEntry<int>("BuildLimits_Silos",                       nolimit); // Silos
            _Pref_Limit_Deposits_Humans ??= _modCategory.CreateEntry<int>("BuildLimits_Humans_Deposits",   nolimit); // Refinery
            _Pref_Limit_Deposits_Aliens ??= _modCategory.CreateEntry<int>("BuildLimits_Aliens_Deposits",   nolimit); // BioCache
            _Pref_Limit_Prod1_Humans ??= _modCategory.CreateEntry<int>("BuildLimits_Humans_Production_L1", nolimit); // Barracks
            _Pref_Limit_Prod1_Aliens ??= _modCategory.CreateEntry<int>("BuildLimits_Aliens_Production_L1", nolimit); // Lesser Spawning Cyst
            _Pref_Limit_Prod2_Humans ??= _modCategory.CreateEntry<int>("BuildLimits_Humans_Production_L2", nolimit); // LVF
            _Pref_Limit_Prod2_Aliens ??= _modCategory.CreateEntry<int>("BuildLimits_Aliens_Production_L2", nolimit); // Greater Spawning Cyst
            _Pref_Limit_Prod3_Humans ??= _modCategory.CreateEntry<int>("BuildLimits_Humans_Production_L3", nolimit); // HVF
            _Pref_Limit_Prod3_Aliens ??= _modCategory.CreateEntry<int>("BuildLimits_Aliens_Production_L3", nolimit); // Grand Spawning Cyst
            _Pref_Limit_Prod4_Humans ??= _modCategory.CreateEntry<int>("BuildLimits_Humans_Production_L4", nolimit); // UHVF
            _Pref_Limit_Prod4_Aliens ??= _modCategory.CreateEntry<int>("BuildLimits_Aliens_Production_L4", nolimit); // Colossal Spawning Cyst
            _Pref_Limit_Prod5_Humans ??= _modCategory.CreateEntry<int>("BuildLimits_Humans_Production_L5", nolimit); // Air Factory
            _Pref_Limit_Prod5_Aliens ??= _modCategory.CreateEntry<int>("BuildLimits_Aliens_Production_L5", nolimit); // (undefined)
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

        public void OnRequestBuildStructure_LimitCheck(object? sender, OnRequestBuildArgs args)
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

                // check for Nodes
                if (_Pref_Limit_Nodes.Value > nolimit && constructionData.ObjectInfo.StructureType == StructureType.Resource &&
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
                
                // check for turret limits (HeavyTurret, ThornSpire, HiveSpire, etc.) [note: includes RadarStation]
                if (constructionData.ObjectInfo.StructureType == StructureType.Defense)
                {
                    int defenseStructureLimit = (parentStructure.Team.Index == (int)SiConstants.ETeam.Alien ? _Pref_Limit_Turrets_Aliens.Value : _Pref_Limit_Turrets_Humans.Value);

                    if (defenseStructureLimit <= nolimit)
                    {
                        return;
                    }

                    int structureTypeCount = GetStructureTypeCount(parentStructure.Team, constructionData.ObjectInfo);
                    if (structureTypeCount >= defenseStructureLimit)
                    {
                        NotifyLimitsEnforced(parentStructure.Team, defenseStructureLimit, "Turret");
                        args.Block = true;
                    }

                    return;
                }

                // check for production limits
                if (constructionData.ObjectInfo.StructureType == StructureType.Production)
                {
                    if (constructionData.ObjectInfo.StructureSelectionType == StructureSelectionType.Units1)
                    {
                        int productionStructureLimit = (parentStructure.Team.Index == (int)SiConstants.ETeam.Alien ? _Pref_Limit_Prod1_Aliens.Value : _Pref_Limit_Prod1_Humans.Value);

                        if (productionStructureLimit <= nolimit)
                        {
                            return;
                        }

                        int structureSelectionTypeCount = GetStructureSelectionTypeCount(parentStructure.Team, constructionData.ObjectInfo);
                        if (structureSelectionTypeCount >= productionStructureLimit)
                        {
                            NotifyLimitsEnforced(parentStructure.Team, productionStructureLimit, (parentStructure.Team.Index == (int)SiConstants.ETeam.Alien ? "Lesser Spawning Cyst" : "Barracks"));
                            args.Block = true;
                        }

                        return;
                    }

                    if (constructionData.ObjectInfo.StructureSelectionType == StructureSelectionType.Units2)
                    {
                        int productionStructureLimit = (parentStructure.Team.Index == (int)SiConstants.ETeam.Alien ? _Pref_Limit_Prod2_Aliens.Value : _Pref_Limit_Prod2_Humans.Value);

                        if (productionStructureLimit <= nolimit)
                        {
                            return;
                        }

                        int structureSelectionTypeCount = GetStructureSelectionTypeCount(parentStructure.Team, constructionData.ObjectInfo);
                        if (structureSelectionTypeCount >= productionStructureLimit)
                        {
                            NotifyLimitsEnforced(parentStructure.Team, productionStructureLimit, (parentStructure.Team.Index == (int)SiConstants.ETeam.Alien ? "Greater Spawning Cyst" : "Light Vehicle Factory"));
                            args.Block = true;
                        }

                        return;
                    }

                    if (constructionData.ObjectInfo.StructureSelectionType == StructureSelectionType.Units3)
                    {
                        int productionStructureLimit = (parentStructure.Team.Index == (int)SiConstants.ETeam.Alien ? _Pref_Limit_Prod3_Aliens.Value : _Pref_Limit_Prod3_Humans.Value);

                        if (productionStructureLimit <= nolimit)
                        {
                            return;
                        }

                        int structureSelectionTypeCount = GetStructureSelectionTypeCount(parentStructure.Team, constructionData.ObjectInfo);
                        if (structureSelectionTypeCount >= productionStructureLimit)
                        {
                            NotifyLimitsEnforced(parentStructure.Team, productionStructureLimit, (parentStructure.Team.Index == (int)SiConstants.ETeam.Alien ? "Grand Spawning Cyst" : "Heavy Vehicle Factory"));
                            args.Block = true;
                        }

                        return;
                    }

                    if (constructionData.ObjectInfo.StructureSelectionType == StructureSelectionType.Units4)
                    {
                        int productionStructureLimit = (parentStructure.Team.Index == (int)SiConstants.ETeam.Alien ? _Pref_Limit_Prod4_Aliens.Value : _Pref_Limit_Prod4_Humans.Value);

                        if (productionStructureLimit <= nolimit)
                        {
                            return;
                        }

                        int structureSelectionTypeCount = GetStructureSelectionTypeCount(parentStructure.Team, constructionData.ObjectInfo);
                        if (structureSelectionTypeCount >= productionStructureLimit)
                        {
                            NotifyLimitsEnforced(parentStructure.Team, productionStructureLimit, (parentStructure.Team.Index == (int)SiConstants.ETeam.Alien ? "Colossal Spawning Cyst" : "Ultra Heavy Vehicle Factory"));
                            args.Block = true;
                        }

                        return;
                    }

                    if (constructionData.ObjectInfo.StructureSelectionType == StructureSelectionType.Units5)
                    {
                        int productionStructureLimit = (parentStructure.Team.Index == (int)SiConstants.ETeam.Alien ? _Pref_Limit_Prod5_Aliens.Value : _Pref_Limit_Prod5_Humans.Value);

                        if (productionStructureLimit <= nolimit)
                        {
                            return;
                        }

                        int structureSelectionTypeCount = GetStructureSelectionTypeCount(parentStructure.Team, constructionData.ObjectInfo);
                        if (structureSelectionTypeCount >= productionStructureLimit)
                        {
                            NotifyLimitsEnforced(parentStructure.Team, productionStructureLimit, (parentStructure.Team.Index == (int)SiConstants.ETeam.Alien ? "tbd" : "Air Factory"));
                            args.Block = true;
                        }
                    }

                    return;
                }

                // check for resource deposits (BioCache, Refinery)
                if (constructionData.ObjectInfo.StructureType == StructureType.Resource &&
                        constructionData.ObjectInfo.HasResourceDeposit && constructionData.ObjectInfo.HasResourceStorage)
                {
                    int depositStructureLimit = (parentStructure.Team.Index == (int)SiConstants.ETeam.Alien ? _Pref_Limit_Deposits_Aliens.Value : _Pref_Limit_Deposits_Humans.Value);

                    if (depositStructureLimit <= nolimit)
                    {
                        return;
                    }

                    int depositStructureCount = parentStructure.Team.GetStructureCount(constructionData.ObjectInfo);
                    if (depositStructureCount >= depositStructureLimit)
                    {
                        NotifyLimitsEnforced(parentStructure.Team, depositStructureLimit, (parentStructure.Team.Index == (int)SiConstants.ETeam.Alien ? "BioCache" : "Refinery"));
                        args.Block = true;
                    }

                    return;
                }

                // check for research (QuantumCortex, ResearchFacility)
                if (constructionData.ObjectInfo.StructureType == StructureType.Research)
                {
                    int researchStructureLimit = (parentStructure.Team.Index == (int)SiConstants.ETeam.Alien ? _Pref_Limit_Research_Alien.Value : _Pref_Limit_Research_Humans.Value);

                    if (researchStructureLimit <= nolimit)
                    {
                        return;
                    }

                    int structureTypeCount = GetStructureTypeCount(parentStructure.Team, constructionData.ObjectInfo);
                    if (structureTypeCount >= researchStructureLimit)
                    {
                        NotifyLimitsEnforced(parentStructure.Team, researchStructureLimit, "Research");
                        args.Block = true;
                    }

                    return;
                }

                // check for Silos
                if (_Pref_Limit_Silos.Value > nolimit && constructionData.ObjectInfo.StructureType == StructureType.Resource &&
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

                // check for base limits (Headquarters, Nest)
                if (parentStructure.Team.BaseStructure == constructionData.ObjectInfo)
                {
                    int baseStructureLimit = (parentStructure.Team.Index == (int)SiConstants.ETeam.Alien ? _Pref_Limit_Bases_Aliens.Value : _Pref_Limit_Bases_Humans.Value);

                    if (baseStructureLimit <= nolimit)
                    {
                        return;
                    }

                    int baseStructureCount = parentStructure.Team.GetStructureCount(constructionData.ObjectInfo);
                    if (baseStructureCount >= baseStructureLimit)
                    {
                        NotifyLimitsEnforced(parentStructure.Team, baseStructureLimit, "Base");
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