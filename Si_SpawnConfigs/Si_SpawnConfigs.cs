/*
Silica Spawn Configuration System
Copyright (C) 2024 by databomb

* Description *
For Silica listen servers, allows admins to build configs of pre-built
spawned prefabs to support events or missions.

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

using MelonLoader;
using Si_SpawnConfigs;
using UnityEngine;
using MelonLoader.Utils;
using System;
using System.Collections.Generic;
using SilicaAdminMod;
using System.Linq;
using System.IO;
using Newtonsoft.Json;

[assembly: MelonInfo(typeof(SpawnConfigs), "Admin Spawn Configs", "0.9.2", "databomb", "https://github.com/data-bomb/Silica")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]
[assembly: MelonOptionalDependencies("Admin Mod")]

namespace Si_SpawnConfigs
{
    public class SpawnConfigs : MelonMod
    {
        static bool AdminModAvailable = false;
        static GameObject? lastSpawnedObject;

        public class SpawnSetup
        {
            public String? Map
            {
                get;
                set;
            }

            public String? VersusMode
            {
                get;
                set;
            }

            public List<TeamSpawn>? Teams
            {
                get;
                set;
            }

            public List<StructureSpawn>? Structures
            {
                get;
                set;
            }

            public List<UnitSpawn>? Units
            {
                get;
                set;
            }
        }

        public class TeamSpawn
        {
            public int TeamIndex
            {
                get;
                set;
            }

            public int Resources
            {
                get;
                set;
            }
        }

        public class ObjectSpawn
        {
            private string _classname = null!;

            public String Classname
            {
                get => _classname;
                set => _classname = value ?? throw new ArgumentNullException("Classname name is required.");
            }

            private float[] _position = null!;

            public float[] Position
            {
                get => _position;
                set => _position = value ?? throw new ArgumentNullException("Position is required.");
            }

            private float[] _rotation = null!;

            public float[] Rotation
            {
                get => _rotation;
                set => _rotation = value ?? throw new ArgumentNullException("Rotation is required.");
            }

            public uint? NetID
            {
                get;
                set;
            }
        }

        public class StructureSpawn : ObjectSpawn
        {
            public int TeamIndex
            {
                get;
                set;
            }

            public int? TechTier
            {
                get;
                set;
            }

            public float? Health
            {
                get;
                set;
            }

            public int? Resources
            {
                get;
                set;
            }
        }

        public class UnitSpawn : ObjectSpawn
        {
            public int TeamIndex
            {
                get;
                set;
            }

            public float? Health
            {
                get;
                set;
            }

            public int? Resources
            {
                get;
                set;
            }
        }

        public override void OnLateInitializeMelon()
        {
            AdminModAvailable = RegisteredMelons.Any(m => m.Info.Name == "Admin Mod");

            if (AdminModAvailable)
            {
                HelperMethods.CommandCallback spawnCallback = Command_Spawn;
                HelperMethods.RegisterAdminCommand("spawn", spawnCallback, Power.Cheat);

                HelperMethods.CommandCallback undoSpawnCallback = Command_UndoSpawn;
                HelperMethods.RegisterAdminCommand("undospawn", undoSpawnCallback, Power.Cheat);

                HelperMethods.CommandCallback saveCallback = Command_SaveSetup;
                HelperMethods.RegisterAdminCommand("saveconfig", saveCallback, Power.Cheat);

                HelperMethods.CommandCallback loadCallback = Command_LoadSetup;
                HelperMethods.RegisterAdminCommand("loadconfig", loadCallback, Power.Cheat);

                HelperMethods.CommandCallback addCallback = Command_AddSetup;
                HelperMethods.RegisterAdminCommand("addconfig", addCallback, Power.Cheat);
            }
            else
            {
                MelonLogger.Warning("Dependency missing: Admin Mod");
            }
        }

        public static void Command_UndoSpawn(Player callerPlayer, String args)
        {
            // validate argument count
            int argumentCount = args.Split(' ').Length - 1;
            if (argumentCount > 0)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Too many arguments");
                return;
            }

            if (lastSpawnedObject == null)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Nothing to undo");
                return;
            }

            BaseGameObject baseObject = lastSpawnedObject.GetBaseGameObject();
            String name = baseObject.ToString();
            baseObject.DamageManager.SetHealth01(0.0f);
            lastSpawnedObject = null;

            HelperMethods.AlertAdminAction(callerPlayer, "destroyed last spawned item (" + name + ")");
        }

        public static void Command_Spawn(Player callerPlayer, String args)
        {
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

            Vector3 playerPosition = callerPlayer.ControlledUnit.WorldPhysicalCenter;
            Quaternion playerRotation = callerPlayer.ControlledUnit.GetFacingRotation();
            String spawnName = args.Split(' ')[1];

            int teamIndex = callerPlayer.Team.Index;
            GameObject? spawnedObject = HelperMethods.SpawnAtLocation(spawnName, playerPosition, playerRotation, teamIndex);
            if (spawnedObject == null)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Failed to spawn");
                return;
            }

            HelperMethods.AlertAdminAction(callerPlayer, "spawned " + spawnName);
        }


        public static void Command_SaveSetup(Player callerPlayer, String args)
        {
            String commandName = args.Split(' ')[0];

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

            String configFile = args.Split(' ')[1];

            try
            {
                // check if UserData\SpawnConfigs\ directory exists
                String spawnConfigDir = GetSpawnConfigsDirectory();
                if (!System.IO.Directory.Exists(spawnConfigDir))
                {
                    MelonLogger.Msg("Creating SpawnConfigs directory at: " + spawnConfigDir);
                    System.IO.Directory.CreateDirectory(spawnConfigDir);
                }

                // check if file extension is valid
                if (configFile.Contains('.') && !configFile.EndsWith("json"))
                {
                    HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Invalid save name (not .json)");
                    return;
                }
                
                // add .json if it's not already there
                if (!configFile.Contains('.'))
                {
                    configFile += ".json";
                }

                // final check on filename
                if (configFile.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Cannot use input as filename");
                    return;
                }

                // don't overwrite an existing file
                String configFileFullPath = Path.Combine(spawnConfigDir, configFile);
                if (File.Exists(configFileFullPath))
                {
                    HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": configuration already exists");
                    return;
                }

                // is there anything to save right now?
                if (!GameMode.CurrentGameMode.GameOngoing)
                {
                    HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Nothing to save with current game state");
                    return;
                }

                SpawnSetup spawnSetup = GenerateSpawnSetup();

                // save to file
                String JsonRaw = JsonConvert.SerializeObject(spawnSetup, Newtonsoft.Json.Formatting.Indented);

                File.WriteAllText(configFileFullPath, JsonRaw);

                HelperMethods.ReplyToCommand(commandName + ": Saved config to file");
            }
            catch (Exception error)
            {
                HelperMethods.PrintError(error, "Command_SaveSetup failed");
            }
        }

        public static void Command_AddSetup(Player callerPlayer, String args)
        {
            String commandName = args.Split(' ')[0];

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

            String configFile = args.Split(' ')[1];

            try
            {
                String spawnConfigDir = GetSpawnConfigsDirectory();

                // check if file extension is valid
                if (configFile.Contains('.') && !configFile.EndsWith("json"))
                {
                    HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Invalid save name (not .json)");
                    return;
                }

                // add .json if it's not already there
                if (!configFile.Contains('.'))
                {
                    configFile += ".json";
                }

                // final check on filename
                if (configFile.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Cannot use input as filename");
                    return;
                }

                // do we have anything to load here?
                String configFileFullPath = System.IO.Path.Combine(spawnConfigDir, configFile);
                if (!File.Exists(configFileFullPath))
                {
                    HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": configuration not found");
                    return;
                }

                // check global config options
                String JsonRaw = File.ReadAllText(configFileFullPath);
                SpawnSetup? spawnSetup = JsonConvert.DeserializeObject<SpawnSetup>(JsonRaw);
                if (spawnSetup == null) 
                {
                    HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": json error in configuration file");
                    return;
                }

                if (spawnSetup.Map != null && spawnSetup.Map != NetworkGameServer.GetServerMap())
                {
                    HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": incompatible map specified");
                    return;
                }

                MP_Strategy strategyInstance = GameObject.FindObjectOfType<MP_Strategy>();
                if (spawnSetup.VersusMode != null && spawnSetup.VersusMode != strategyInstance.TeamsVersus.ToString())
                {
                    HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": incompatible mode specified");
                    return;
                }

                if (!ExecuteBatchSpawn(spawnSetup))
                {
                    HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": bad name in config file")
                    return;
                }

                HelperMethods.ReplyToCommand(commandName + ": Added config from file");
            }
            catch (Exception error)
            {
                HelperMethods.PrintError(error, "Command_AddSetup failed");
            }
        }

        public static void Command_LoadSetup(Player callerPlayer, String args)
        {
            String commandName = args.Split(' ')[0];

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

            String configFile = args.Split(' ')[1];

            try
            {
                String spawnConfigDir = GetSpawnConfigsDirectory();

                // check if file extension is valid
                if (configFile.Contains('.') && !configFile.EndsWith("json"))
                {
                    HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Invalid save name (not .json)");
                    return;
                }

                // add .json if it's not already there
                if (!configFile.Contains('.'))
                {
                    configFile += ".json";
                }

                // final check on filename
                if (configFile.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Cannot use input as filename");
                    return;
                }

                // do we have anything to load here?
                String configFileFullPath = System.IO.Path.Combine(spawnConfigDir, configFile);
                if (!File.Exists(configFileFullPath))
                {
                    HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": configuration not found");
                    return;
                }

                // check global config options
                String JsonRaw = File.ReadAllText(configFileFullPath);
                SpawnSetup? spawnSetup = JsonConvert.DeserializeObject<SpawnSetup>(JsonRaw);
                if (spawnSetup == null)
                {
                    HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": json error in configuration file");
                    return;
                }

                if (spawnSetup.Map != null && spawnSetup.Map != NetworkGameServer.GetServerMap())
                {
                    HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": incompatible map specified");
                    return;
                }

                MP_Strategy strategyInstance = GameObject.FindObjectOfType<MP_Strategy>();
                if (spawnSetup.VersusMode != null && spawnSetup.VersusMode != strategyInstance.TeamsVersus.ToString())
                {
                    HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": incompatible mode specified");
                    return;
                }

                SpawnSetup originalSpawnSetup = GenerateSpawnSetup(true);

                // remove all construction sites (units + structures)
                RemoveConstructionSites();

                // load structures so there is still an HQ/Nest
                if (!LoadStructures(spawnSetup))
                {
                    HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": invalid structure in config file");
                    return;
                }

                // remove original structures (incl HQ+Nest)
                RemoveStructures(originalSpawnSetup);

                // remove original units to avoid likely collisions if reloaded
                RemoveUnits(originalSpawnSetup);

                // load new units
                if (!LoadUnits(spawnSetup))
                {
                    HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": invalid unit in config file");
                    return;
                }

                // set anything team-specific
                LoadTeams(spawnSetup);

                HelperMethods.ReplyToCommand(commandName + ": Loaded config from file");
            }
            catch (Exception error)
            {
                HelperMethods.PrintError(error, "Command_LoadSetup failed");
            }
        }

        static string GetSpawnConfigsDirectory()
        {
            return Path.Combine(MelonEnvironment.UserDataDirectory, @"SpawnConfigs\");
        }

        public static void RemoveConstructionSites()
        {
            MelonLogger.Msg("Removing all construciton sites");
            ConstructionSite.ClearAllConstructionSites();
            /*foreach (ConstructionSite constructionSite in ConstructionSite.ConstructionSites)
            {
                if (constructionSite == null)
                {
                    continue;
                }

                MelonLogger.Msg("Removing construction " + constructionSite.ToString());

                //constructionSite.gameObject.GetDamageManager().SetHealth01(0.0f);
                constructionSite.DamageManager.SetHealth01(0.0f);
            }*/
        }

        public static void RemoveStructures(SpawnSetup removeSetup)
        {
            if (removeSetup.Structures != null)
            {
                foreach (StructureSpawn spawnEntry in removeSetup.Structures)
                {
                    if (spawnEntry.NetID == null)
                    {
                        continue;
                    }

                    MelonLogger.Msg("Removing structure " + spawnEntry.Classname);

                    Structure thisStructure = Structure.GetStructureByNetID((uint)spawnEntry.NetID);
                    thisStructure.DamageManager.SetHealth01(0.0f);
                }
            }
        }

        public static void RemoveUnits(SpawnSetup removeSetup)
        {
            if (removeSetup.Units != null)
            {
                foreach (UnitSpawn spawnEntry in removeSetup.Units)
                {
                    if (spawnEntry.NetID == null)
                    {
                        continue;
                    }

                    MelonLogger.Msg("Removing unit " + spawnEntry.Classname);

                    Unit thisUnit = Unit.GetUnitByNetID((uint)spawnEntry.NetID);
                    thisUnit.DamageManager.SetHealth01(0.0f);
                }
            }
        }

        public static void ExecuteBatchRemoval(SpawnSetup removeSetup)
        {
            // remove all pending construction first (units being built are ConstructionSites) to avoid de-referencing null
            RemoveConstructionSites();

            RemoveStructures(removeSetup);

            RemoveUnits(removeSetup);
        }

        public static bool LoadUnits(SpawnSetup addSetup)
        {
            // load all units
            if (addSetup.Units != null)
            {
                foreach (UnitSpawn spawnEntry in addSetup.Units)
                {
                    MelonLogger.Msg("Adding unit " + spawnEntry.Classname);

                    Vector3 position = new Vector3
                    {
                        x = spawnEntry.Position[0],
                        y = spawnEntry.Position[1],
                        z = spawnEntry.Position[2]
                    };
                    Quaternion rotation = new Quaternion
                    {
                        x = spawnEntry.Rotation[0],
                        y = spawnEntry.Rotation[1],
                        z = spawnEntry.Rotation[2],
                        w = spawnEntry.Rotation[3]
                    };
                    GameObject? spawnedObject = HelperMethods.SpawnAtLocation(spawnEntry.Classname, position, rotation, spawnEntry.TeamIndex);
                    if (spawnedObject == null)
                    {
                        return false;
                    }

                    BaseGameObject baseObject = spawnedObject.GetBaseGameObject();
                    if (spawnEntry.Health != null)
                    {
                        baseObject.DamageManager.SetHealth((float)spawnEntry.Health);
                    }

                    if (baseObject.IsResourceHolder && spawnEntry.Resources != null)
                    {
                        // assign biotics (Resources[1]) to alien team and balterium (Resources[0]) to human teams
                        Resource resource = spawnEntry.TeamIndex == 0 ? Resource.Resources[1] : Resource.Resources[0];
                        baseObject.StoreResource(resource, (int)spawnEntry.Resources);
                    }
                }
            }

            return true;
        }

        public static bool LoadStructures(SpawnSetup addSetup)
        {
            // load all structures
            if (addSetup.Structures != null)
            {
                foreach (StructureSpawn spawnEntry in addSetup.Structures)
                {
                    MelonLogger.Msg("Adding structure " + spawnEntry.Classname);

                    Vector3 position = new Vector3
                    {
                        x = spawnEntry.Position[0],
                        y = spawnEntry.Position[1],
                        z = spawnEntry.Position[2]
                    };
                    Quaternion rotation = new Quaternion
                    {
                        x = spawnEntry.Rotation[0],
                        y = spawnEntry.Rotation[1],
                        z = spawnEntry.Rotation[2],
                        w = spawnEntry.Rotation[3]
                    };
                    GameObject? spawnedObject = HelperMethods.SpawnAtLocation(spawnEntry.Classname, position, rotation, spawnEntry.TeamIndex);
                    if (spawnedObject == null)
                    {
                        return false;
                    }

                    BaseGameObject baseObject = spawnedObject.GetBaseGameObject();
                    if (spawnEntry.Health != null)
                    {
                        baseObject.DamageManager.SetHealth((float)spawnEntry.Health);
                    }

                    if (spawnEntry.TechTier != null)
                    {
                        uint netID = spawnedObject.GetNetworkComponent().NetID;
                        Structure thisStructure = Structure.GetStructureByNetID(netID);
                        if (thisStructure != null)
                        {
                            thisStructure.StructureTechnologyTier = (int)spawnEntry.TechTier;

#if NET6_0
                            thisStructure.RPC_SynchTechnologyTier();
#else
                            Type structureType = typeof(Structure);
                            MethodInfo synchTechMethod = structureType.GetMethod("RPC_SynchTechnologyTier");

                            synchTechMethod.Invoke(thisStructure, null);
#endif
                        }
                    }

                    if (baseObject.IsResourceHolder && spawnEntry.Resources != null)
                    {
                        // assign biotics (Resources[1]) to alien team and balterium (Resources[0]) to human teams
                        Resource resource = spawnEntry.TeamIndex == 0 ? Resource.Resources[1] : Resource.Resources[0];
                        baseObject.StoreResource(resource, (int)spawnEntry.Resources);
                    }
                }
            }

            return true;
        }
        
        public static void LoadTeams(SpawnSetup addSetup)
        {
            // team-specific info
            if (addSetup.Teams != null)
            {
                foreach (TeamSpawn spawnEntry in addSetup.Teams)
                {
                    Team.Teams[spawnEntry.TeamIndex].StartingResources = spawnEntry.Resources;
                    //Team.Teams[spawnEntry.TeamIndex].m_StartingResources = spawnEntry.Resources;
                }
            }
        }

        public static bool ExecuteBatchSpawn(SpawnSetup spawnSetup)
        {
            bool loadStatus = LoadStructures(spawnSetup);
            if (!loadStatus)
            {
                return false;
            }

            loadStatus = LoadUnits(spawnSetup);
            if (!loadStatus)
            {
                return false;
            }

            LoadTeams(spawnSetup);

            return true;
        }

        public static SpawnSetup GenerateSpawnSetup(bool includeNetIDs = false)
        {
            // set global config options
            SpawnSetup spawnSetup = new SpawnSetup
            {
                Map = NetworkGameServer.GetServerMap()
            };
            MP_Strategy strategyInstance = GameObject.FindObjectOfType<MP_Strategy>();
            spawnSetup.VersusMode = strategyInstance.TeamsVersus.ToString();

            // create a list of all structures and units
            spawnSetup.Structures = new List<StructureSpawn>();
            spawnSetup.Units = new List<UnitSpawn>();
            spawnSetup.Teams = new List<TeamSpawn>();

            foreach (Team team in Team.Teams)
            {
                if (team == null)
                {
                    continue;
                }

                TeamSpawn thisTeamSpawn = new TeamSpawn
                {
                    Resources = team.StartingResources,
                    TeamIndex = team.Index
                };
                spawnSetup.Teams.Add(thisTeamSpawn);

                foreach (Structure structure in team.Structures)
                {
                    // skip bunkers for now. TODO: address how to safely add bunkers
                    if (structure.ToString().StartsWith("Bunk"))
                    {
                        continue;
                    }

                    StructureSpawn thisSpawnEntry = new StructureSpawn();

                    float[] position = new float[]
                    {
                            structure.transform.position.x,
                            structure.transform.position.y,
                            structure.transform.position.z
                    };
                    thisSpawnEntry.Position = position;

                    float[] rotation = new float[]
                    {
                            structure.transform.rotation.x,
                            structure.transform.rotation.y,
                            structure.transform.rotation.z,
                            structure.transform.rotation.w
                    };
                    thisSpawnEntry.Rotation = rotation;

                    thisSpawnEntry.TeamIndex = structure.Team.Index;
                    thisSpawnEntry.Classname = structure.ToString().Split('(')[0];

                    // only record health if damaged
                    if (structure.DamageManager.Health01 < 0.99f)
                    {
                        thisSpawnEntry.Health = structure.DamageManager.Health;
                    }

                    // if there's a non-default tech tier
                    if (structure.StructureTechnologyTier > 0)
                    {
                        thisSpawnEntry.TechTier = structure.StructureTechnologyTier;
                    }

                    if (structure.IsResourceHolder)
                    {
                        thisSpawnEntry.Resources = structure.GetStoredResources();
                    }

                    if (includeNetIDs)
                    {
                        thisSpawnEntry.NetID = structure.NetworkComponent.NetID;
                    }

                    spawnSetup.Structures.Add(thisSpawnEntry);
                }

                foreach (Unit unit in team.Units)
                {
                    UnitSpawn thisSpawnEntry = new UnitSpawn();

                    float[] position = new float[]
                    {
                        unit.transform.position.x,
                        unit.transform.position.y,
                        unit.transform.position.z
                    };
                    thisSpawnEntry.Position = position;

                    Quaternion facingRotation = unit.GetFacingRotation();
                    float[] rotation = new float[]
                    {
                        facingRotation.x,
                        facingRotation.y,
                        facingRotation.z,
                        facingRotation.w
                    };
                    thisSpawnEntry.Rotation = rotation;

                    thisSpawnEntry.TeamIndex = unit.Team.Index;
                    thisSpawnEntry.Classname = unit.ToString().Split('(')[0];

                    // only record health if damaged
                    if (unit.DamageManager.Health01 < 0.99f)
                    {
                        thisSpawnEntry.Health = unit.DamageManager.Health;
                    }

                    if (unit.IsResourceHolder)
                    {
                        thisSpawnEntry.Resources = unit.GetResourceCapacity();
                    }

                    if (includeNetIDs)
                    {
                        thisSpawnEntry.NetID = unit.NetworkComponent.NetID;
                    }

                    spawnSetup.Units.Add(thisSpawnEntry);
                }
            }

            return spawnSetup;
        }

        // balterium and biotics ResourceArea s will need to be destroyed and respawned into the game with ResourceArea.DistributeResources

        // for changing a bunker to an outpost or spawning a vehicle nearby

        /*
         * Il2Cpp.CapturePoint.AllCapturePoints.Count.ToString()
            int stuff = Il2Cpp.GameDatabase.GetSpawnablePrefabIndex("Outpost_01");
            UnityEngine.GameObject gameobject = Il2Cpp.GameDatabase.GetSpawnablePrefab(stuff);
         * [17:17:02.185] [UnityExplorer] Bunker_01 (UnityEngine.GameObject)
        [17:17:02.187] [UnityExplorer] --------------------
        static UnityEngine.GameObject Il2Cpp.Game::SpawnPrefab(UnityEngine.GameObject objectPrefab, Il2Cpp.Player ownerPlayer, Il2Cpp.Team team, UnityEngine.Vector3 position, UnityEngine.Quaternion rotation, bool sendNetSpawn, bool sendNetInit)
        - Parameter 0 'objectPrefab': Bunker_01 (UnityEngine.GameObject)
        - Parameter 1 'ownerPlayer': null
        - Parameter 2 'team': null
        - Parameter 3 'position': (-2465.00, 125.00, -2378.00)
        - Parameter 4 'rotation': (0.00000, 0.40674, 0.00000, -0.91355)
        - Parameter 5 'sendNetSpawn': True
        - Parameter 6 'sendNetInit': True
        - Return value: null

        [17:17:02.190] [UnityExplorer] 11
        [17:17:02.196] [UnityExplorer] --------------------
        void Il2Cpp.CapturePoint::OnEnable()
        - __instance: CaptureZone_Cuboid_1 (CapturePoint)

        [17:17:02.198] [UnityExplorer] --------------------
        void Il2Cpp.CapturePoint::OnSendNetInit(Il2Cpp.GameByteStreamWriter packetWriter)
        - __instance: CaptureZone_Cuboid_1 (CapturePoint)
        - Parameter 0 'packetWriter': GameByteStreamWriter

                void Il2Cpp.PrefabsToPointsElement::SpawnPrefabs(Il2Cpp.Team forTeam, float probabilityScale)
        - __instance: PrefabsToPointsElement
        - Parameter 0 'forTeam': null
        - Parameter 1 'probabilityScale': 1
        */

        /*
         * 
         *  for setting a config on round restart:
         static bool Prefix(Il2Cpp.MP_Strategy __instance)
{
    try {
       StringBuilder sb = new StringBuilder();
       sb.AppendLine("--------------------");
       sb.AppendLine("void Il2Cpp.MP_Strategy::SpawnBaseStructures()");
       sb.Append("- __instance: ").AppendLine(__instance.ToString());
       UnityExplorer.ExplorerCore.Log(sb.ToString());
    }
    catch (System.Exception ex) {
        UnityExplorer.ExplorerCore.LogWarning($"Exception in patch of void Il2Cpp.MP_Strategy::SpawnBaseStructures():\n{ex}");
    }

UnityExplorer.ExplorerCore.Log(__instance.TeamsVersus.ToString());
if (__instance.TeamsVersus != Il2Cpp.MP_Strategy.ETeamsVersus.NONE)
{
UnityExplorer.ExplorerCore.Log("block");
return false;
}

return true;
}

         */
    }
}