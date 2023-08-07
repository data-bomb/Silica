/*
Silica Spawn Configuration System
Copyright (C) 2023 by databomb

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

using Il2Cpp;
using MelonLoader;
using Si_SpawnConfigs;
using UnityEngine;
using AdminExtension;
using MelonLoader.Utils;
using System.Text.Json;

[assembly: MelonInfo(typeof(SpawnConfigs), "Admin Spawn Configs", "0.8.7", "databomb")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]

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

            public List<SpawnEntry> SpawnEntries
            {
                get;
                set;
            }
        }

        public class SpawnEntry
        {
            public String Classname
            {
                get;
                set;
            }

            public float[] Position
            {
                get;
                set;
            }

            public float[] Rotation
            { 
                get; 
                set;
            }

            public int TeamIndex
            {
                get;
                set;
            }

            public bool IsStructure
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

            public uint? NetID
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
                HelperMethods.RegisterAdminCommand("!spawn", spawnCallback, Power.Cheat);

                HelperMethods.CommandCallback undoSpawnCallback = Command_UndoSpawn;
                HelperMethods.RegisterAdminCommand("!undospawn", undoSpawnCallback, Power.Cheat);

                HelperMethods.CommandCallback saveCallback = Command_SaveSetup;
                HelperMethods.RegisterAdminCommand("!saveconfig", saveCallback, Power.Cheat);

                HelperMethods.CommandCallback loadCallback = Command_LoadSetup;
                HelperMethods.RegisterAdminCommand("!loadconfig", loadCallback, Power.Cheat);

                HelperMethods.CommandCallback addCallback = Command_AddSetup;
                HelperMethods.RegisterAdminCommand("!addconfig", addCallback, Power.Cheat);
            }
            else
            {
                MelonLogger.Warning("Dependency missing: Admin Mod");
            }
        }

        public void Command_UndoSpawn(Il2Cpp.Player callerPlayer, String args)
        {
            // validate argument count
            int argumentCount = args.Split(' ').Length - 1;
            if (argumentCount > 0)
            {
                HelperMethods.ReplyToCommand(args.Split(' ')[0] + ": Too many arguments");
                return;
            }

            if (lastSpawnedObject == null)
            {
                HelperMethods.ReplyToCommand(args.Split(' ')[0] + ": Nothing to undo");
                return;
            }

            BaseGameObject baseObject = lastSpawnedObject.GetBaseGameObject();
            String name = baseObject.ToString();
            baseObject.DamageManager.SetHealth01(0.0f);
            lastSpawnedObject = null;

            HelperMethods.AlertAdminAction(callerPlayer, "destroyed last spawned item (" + name + ")");
        }

        public void Command_Spawn(Il2Cpp.Player callerPlayer, String args)
        {
            // validate argument count
            int argumentCount = args.Split(' ').Length - 1;
            if (argumentCount > 1)
            {
                HelperMethods.ReplyToCommand(args.Split(' ')[0] + ": Too many arguments");
                return;
            }
            else if (argumentCount < 1)
            {
                HelperMethods.ReplyToCommand(args.Split(' ')[0] + ": Too few arguments");
                return;
            }

            Vector3 playerPosition = callerPlayer.m_ControlledUnit.WorldPhysicalCenter;
            Quaternion playerRotation = callerPlayer.m_ControlledUnit.GetFacingRotation();
            String spawnName = args.Split(' ')[1];

            int teamIndex = callerPlayer.m_Team.Index;
            GameObject? spawnedObject = SpawnAtLocation(spawnName, playerPosition, playerRotation, teamIndex);
            if (spawnedObject == null)
            {
                HelperMethods.ReplyToCommand(args.Split(' ')[0] + ": Failed to spawn");
                return;
            }

            HelperMethods.AlertAdminAction(callerPlayer, "spawned " + spawnName);
        }

        public static GameObject? SpawnAtLocation(String name, Vector3 position, Quaternion rotation, int teamIndex = -1)
        {
            int prefabIndex = GameDatabase.GetSpawnablePrefabIndex(name);
            if (prefabIndex <= -1)
            {
                return null;
            }

            GameObject prefabObject = GameDatabase.GetSpawnablePrefab(prefabIndex);
            GameObject spawnedObject = Game.SpawnPrefab(prefabObject, null, true, true);

            if (spawnedObject == null)
            {
                return null;
            }

            lastSpawnedObject = spawnedObject;

            Unit testUnit = spawnedObject.GetComponent<Unit>();
            // unit
            if (testUnit != null)
            {
                position.y += 3f;
                spawnedObject.transform.position = position;
                spawnedObject.transform.rotation = rotation;

                spawnedObject.transform.GetBaseGameObject().Teleport(position, rotation);
            }
            // structure
            else
            {
                spawnedObject.transform.position = position;
                spawnedObject.transform.rotation = rotation;
            }

            if (teamIndex > -1)
            {
                // set team information
                BaseGameObject baseObject = spawnedObject.GetBaseGameObject();
                if (baseObject.m_Team.Index != teamIndex)
                {
                    baseObject.Team = Team.Teams[teamIndex];
                    baseObject.m_Team = Team.Teams[teamIndex];
                    baseObject.UpdateToCurrentTeam();
                }
            }

            return spawnedObject;
        }
        public void Command_SaveSetup(Il2Cpp.Player callerPlayer, String args)
        {
            String commandName = args.Split(' ')[0];

            // validate argument count
            int argumentCount = args.Split(' ').Length - 1;
            if (argumentCount > 1)
            {
                HelperMethods.ReplyToCommand(commandName + ": Too many arguments");
                return;
            }
            else if (argumentCount < 1)
            {
                HelperMethods.ReplyToCommand(commandName + ": Too few arguments");
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
                    HelperMethods.ReplyToCommand(commandName + ": Invalid save name (not .json)");
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
                    HelperMethods.ReplyToCommand(commandName + ": Cannot use input as filename");
                    return;
                }

                // don't overwrite an existing file
                String configFileFullPath = Path.Combine(spawnConfigDir, configFile);
                if (File.Exists(configFileFullPath))
                {
                    HelperMethods.ReplyToCommand(commandName + ": configuration already exists");
                    return;
                }

                // is there anything to save right now?
                if (!GameMode.CurrentGameMode.GameOngoing)
                {
                    HelperMethods.ReplyToCommand(commandName + ": Nothing to save with current game state");
                    return;
                }

                SpawnSetup spawnSetup = GenerateSpawnSetup();

                // save to file
                String JsonRaw = JsonSerializer.Serialize(
                    spawnSetup, 
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

                File.WriteAllText(configFileFullPath, JsonRaw);

                HelperMethods.ReplyToCommand(commandName + ": Saved config to file");
            }
            catch (Exception error)
            {
                HelperMethods.PrintError(error, "Command_SaveSetup failed");
            }
        }

        public void Command_AddSetup(Il2Cpp.Player callerPlayer, String args)
        {
            String commandName = args.Split(' ')[0];

            // validate argument count
            int argumentCount = args.Split(' ').Length - 1;
            if (argumentCount > 1)
            {
                HelperMethods.ReplyToCommand(commandName + ": Too many arguments");
                return;
            }
            else if (argumentCount < 1)
            {
                HelperMethods.ReplyToCommand(commandName + ": Too few arguments");
                return;
            }

            String configFile = args.Split(' ')[1];

            try
            {
                String spawnConfigDir = GetSpawnConfigsDirectory();

                // check if file extension is valid
                if (configFile.Contains('.') && !configFile.EndsWith("json"))
                {
                    HelperMethods.ReplyToCommand(commandName + ": Invalid save name (not .json)");
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
                    HelperMethods.ReplyToCommand(commandName + ": Cannot use input as filename");
                    return;
                }

                // do we have anything to load here?
                String configFileFullPath = System.IO.Path.Combine(spawnConfigDir, configFile);
                if (!File.Exists(configFileFullPath))
                {
                    HelperMethods.ReplyToCommand(commandName + ": configuration not found");
                    return;
                }

                // check global config options
                String JsonRaw = File.ReadAllText(configFileFullPath);
                SpawnSetup? spawnSetup = JsonSerializer.Deserialize<SpawnSetup>(JsonRaw);
                if (spawnSetup == null) 
                {
                    HelperMethods.ReplyToCommand(commandName + ": json error in configuration file");
                    return;
                }

                if (spawnSetup.Map != null && spawnSetup.Map != NetworkGameServer.GetServerMapName())
                {
                    HelperMethods.ReplyToCommand(commandName + ": incompatible map specified");
                    return;
                }

                MP_Strategy strategyInstance = GameObject.FindObjectOfType<Il2Cpp.MP_Strategy>();
                if (spawnSetup.VersusMode != null && spawnSetup.VersusMode != strategyInstance.TeamsVersus.ToString())
                {
                    HelperMethods.ReplyToCommand(commandName + ": incompatible mode specified");
                    return;
                }

                if (!ExecuteBatchSpawn(spawnSetup))
                {
                    HelperMethods.ReplyToCommand(commandName + ": bad name in config file");
                    return;
                }

                HelperMethods.ReplyToCommand(commandName + ": Added config from file");
            }
            catch (Exception error)
            {
                HelperMethods.PrintError(error, "Command_AddSetup failed");
            }
        }

        public void Command_LoadSetup(Il2Cpp.Player callerPlayer, String args)
        {
            String commandName = args.Split(' ')[0];

            // validate argument count
            int argumentCount = args.Split(' ').Length - 1;
            if (argumentCount > 1)
            {
                HelperMethods.ReplyToCommand(commandName + ": Too many arguments");
                return;
            }
            else if (argumentCount < 1)
            {
                HelperMethods.ReplyToCommand(commandName + ": Too few arguments");
                return;
            }

            String configFile = args.Split(' ')[1];

            try
            {
                String spawnConfigDir = GetSpawnConfigsDirectory();

                // check if file extension is valid
                if (configFile.Contains('.') && !configFile.EndsWith("json"))
                {
                    HelperMethods.ReplyToCommand(commandName + ": Invalid save name (not .json)");
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
                    HelperMethods.ReplyToCommand(commandName + ": Cannot use input as filename");
                    return;
                }

                // do we have anything to load here?
                String configFileFullPath = System.IO.Path.Combine(spawnConfigDir, configFile);
                if (!File.Exists(configFileFullPath))
                {
                    HelperMethods.ReplyToCommand(commandName + ": configuration not found");
                    return;
                }

                // check global config options
                String JsonRaw = File.ReadAllText(configFileFullPath);
                SpawnSetup? spawnSetup = JsonSerializer.Deserialize<SpawnSetup>(JsonRaw);
                if (spawnSetup == null)
                {
                    HelperMethods.ReplyToCommand(commandName + ": json error in configuration file");
                    return;
                }

                if (spawnSetup.Map != null && spawnSetup.Map != NetworkGameServer.GetServerMapName())
                {
                    HelperMethods.ReplyToCommand(commandName + ": incompatible map specified");
                    return;
                }

                MP_Strategy strategyInstance = GameObject.FindObjectOfType<Il2Cpp.MP_Strategy>();
                if (spawnSetup.VersusMode != null && spawnSetup.VersusMode != strategyInstance.TeamsVersus.ToString())
                {
                    HelperMethods.ReplyToCommand(commandName + ": incompatible mode specified");
                    return;
                }

                SpawnSetup originalSpawnSetup = GenerateSpawnSetup(true);

                if (!ExecuteBatchSpawn(spawnSetup))
                {
                    HelperMethods.ReplyToCommand(commandName + ": bad name in config file");
                    return;
                }

                // remove the originals
                ExecuteBatchRemoval(originalSpawnSetup);

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
        public static void ExecuteBatchRemoval(SpawnSetup removeSetup)
        {
            foreach (SpawnEntry spawnEntry in removeSetup.SpawnEntries)
            {
                MelonLogger.Msg("Removing " + spawnEntry.Classname);

                if (spawnEntry.NetID == null)
                {
                    continue;
                }

                if (spawnEntry.IsStructure)
                {
                    Structure thisStructure = Structure.GetStructureByNetID((uint)spawnEntry.NetID);
                    thisStructure.DamageManager.SetHealth01(0.0f);
                }
                else
                {
                    Unit thisUnit = Unit.GetUnitByNetID((uint)spawnEntry.NetID);
                    thisUnit.DamageManager.SetHealth01(0.0f);
                }
            }

            foreach (ConstructionSite constructionSite in ConstructionSite.ConstructionSites)
            {
                constructionSite.DamageManager.SetHealth01(0.0f);
            }
        }

        public static bool ExecuteBatchSpawn(SpawnSetup spawnSetup)
        {
            // load all structures and units
            foreach (SpawnEntry spawnEntry in spawnSetup.SpawnEntries)
            {
                MelonLogger.Msg("Adding " + spawnEntry.Classname);

                Vector3 position = new(spawnEntry.Position[0], spawnEntry.Position[1], spawnEntry.Position[2]);
                Quaternion rotation = new(spawnEntry.Rotation[0], spawnEntry.Rotation[1], spawnEntry.Rotation[2], spawnEntry.Rotation[3]);
                GameObject? spawnedObject = SpawnAtLocation(spawnEntry.Classname, position, rotation, spawnEntry.TeamIndex);
                if (spawnedObject == null)
                {
                    return false;
                }

                if (spawnEntry.Health != null)
                {
                    BaseGameObject baseObject = spawnedObject.GetBaseGameObject();
                    baseObject.DamageManager.SetHealth((float)spawnEntry.Health);
                }

                if (spawnEntry.IsStructure && spawnEntry.TechTier != null)
                {
                    uint netID = spawnedObject.GetNetworkComponent().NetID;
                    Structure thisStructure = Structure.GetStructureByNetID(netID);
                    if (thisStructure != null)
                    {
                        thisStructure.StructureTechnologyTier = (int)spawnEntry.TechTier;
                        thisStructure.RPC_SynchTechnologyTier();
                    }
                }
            }

            return true;
        }

        public static SpawnSetup GenerateSpawnSetup(bool includeNetIDs = false)
        {
            // set global config options
            SpawnSetup spawnSetup = new();
            spawnSetup.Map = NetworkGameServer.GetServerMapName();
            MP_Strategy strategyInstance = GameObject.FindObjectOfType<Il2Cpp.MP_Strategy>();
            spawnSetup.VersusMode = strategyInstance.TeamsVersus.ToString();

            // create a list of all structures and units
            spawnSetup.SpawnEntries = new List<SpawnEntry>();
            foreach (Team team in Team.Teams)
            {
                if (team == null)
                {
                    continue;
                }

                foreach (Structure structure in team.Structures)
                {
                    // skip bunkers for now. TODO: address how to safely add bunkers
                    if (structure.ToString().StartsWith("Bunk"))
                    {
                        continue;
                    }

                    SpawnEntry thisSpawnEntry = new();

                    BaseGameObject structureBaseObject = structure.gameObject.GetBaseGameObject();
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
                    thisSpawnEntry.IsStructure = true;

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

                    if (includeNetIDs)
                    {
                        thisSpawnEntry.NetID = structure.NetworkComponent.NetID;
                    }

                    spawnSetup.SpawnEntries.Add(thisSpawnEntry);
                }

                foreach (Unit unit in team.Units)
                {
                    SpawnEntry thisSpawnEntry = new();

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
                    thisSpawnEntry.IsStructure = false;

                    // only record health if damaged
                    if (unit.DamageManager.Health01 < 0.99f)
                    {
                        thisSpawnEntry.Health = unit.DamageManager.Health;
                    }

                    if (includeNetIDs)
                    {
                        thisSpawnEntry.NetID = unit.NetworkComponent.NetID;
                    }

                    spawnSetup.SpawnEntries.Add(thisSpawnEntry);
                }
            }

            return spawnSetup;
        }

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