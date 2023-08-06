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

using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using Si_SpawnConfigs;
using UnityEngine;
using AdminExtension;
using MelonLoader.Utils;
using System.Text.Json;
using Il2CppSilica.UI;

[assembly: MelonInfo(typeof(SpawnConfigs), "Admin Spawn Configs", "0.8.3", "databomb")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]

namespace Si_SpawnConfigs
{
    public class SpawnConfigs : MelonMod
    {
        static bool AdminModAvailable = false;
        static GameObject? lastSpawnedObject;

        public class SpawnSetup
        {
            public String Map
            {
                get;
                set;
            }

            public String VersusMode
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

            public float Position_X
            {
                get;
                set;
            }
            public float Position_Y
            {
                get;
                set;
            }
            public float Position_Z
            {
                get;
                set;
            }

            public float Rotation_X
            { 
                get; 
                set;
            }
            public float Rotation_Y
            {
                get;
                set;
            }
            public float Rotation_Z
            {
                get;
                set;
            }
            public float Rotation_W
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
                HelperMethods.RegisterAdminCommand("!savesetup", saveCallback, Power.Cheat);

                HelperMethods.CommandCallback loadCallback = Command_LoadSetup;
                HelperMethods.RegisterAdminCommand("!loadsetup", loadCallback, Power.Cheat);
            }
            else
            {
                MelonLogger.Warning("Dependency missing: Admin Mod");
            }
        }

        public void Command_UndoSpawn(Il2Cpp.Player callerPlayer, String args)
        {
            // validate argument count
            int argumentCount = args.Split(' ').Count() - 1;
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
            int argumentCount = args.Split(' ').Count() - 1;
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

            GameObject? spawnedObject = SpawnAtLocation(spawnName, playerPosition, playerRotation);
            if (spawnedObject == null)
            {
                HelperMethods.ReplyToCommand(args.Split(' ')[0] + ": Failed to spawn");
                return;
            }

            // check if team is correct
            BaseGameObject baseObject = spawnedObject.GetBaseGameObject();
            if (baseObject.m_Team != callerPlayer.m_Team)
            {
                baseObject.Team = callerPlayer.Team;
                baseObject.m_Team = callerPlayer.m_Team;
                baseObject.UpdateToCurrentTeam();
            }

            HelperMethods.AlertAdminAction(callerPlayer, "spawned " + spawnName);
        }

        public static GameObject? SpawnAtLocation(String name, Vector3 position, Quaternion rotation)
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

            //MelonLogger.Msg("pos[x]:" + playerPosition.x.ToString() + " pos[y]:" + playerPosition.y.ToString() + " pos[z]:" + playerPosition.z.ToString());

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

            return spawnedObject;
        }
        public void Command_SaveSetup(Il2Cpp.Player callerPlayer, String args)
        {
            String commandName = args.Split(' ')[0];

            // validate argument count
            int argumentCount = args.Split(' ').Count() - 1;
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
                if (configFile.Contains(".") && !configFile.EndsWith("json"))
                {
                    HelperMethods.ReplyToCommand(commandName + ": Invalid save name (not .json)");
                    return;
                }
                
                // add .json if it's not already there
                if (!configFile.Contains("."))
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

                // set global config options
                SpawnSetup spawnSetup = new SpawnSetup();
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
                        SpawnEntry thisSpawnEntry = new SpawnEntry();

                        thisSpawnEntry.Classname = structure.ToString().Split('(')[0];
                        thisSpawnEntry.Position_X = structure.gameObject.GetBaseGameObject().WorldPhysicalCenter.x;
                        thisSpawnEntry.Position_Y = structure.gameObject.GetBaseGameObject().WorldPhysicalCenter.y;
                        thisSpawnEntry.Position_Z = structure.gameObject.GetBaseGameObject().WorldPhysicalCenter.z;
                        thisSpawnEntry.Rotation_X = structure.transform.rotation.x;
                        thisSpawnEntry.Rotation_Y = structure.transform.rotation.y;
                        thisSpawnEntry.Rotation_Z = structure.transform.rotation.z;
                        thisSpawnEntry.Rotation_W = structure.transform.rotation.w;
                        thisSpawnEntry.TeamIndex = structure.Team.Index;
                        thisSpawnEntry.IsStructure = true;

                        spawnSetup.SpawnEntries.Add(thisSpawnEntry);
                    }

                    foreach (Unit unit in team.Units)
                    {
                        SpawnEntry thisSpawnEntry = new SpawnEntry();

                        thisSpawnEntry.Classname = unit.ToString().Split('(')[0];
                        thisSpawnEntry.Position_X = unit.gameObject.GetBaseGameObject().WorldPhysicalCenter.x;
                        thisSpawnEntry.Position_Y = unit.gameObject.GetBaseGameObject().WorldPhysicalCenter.y;
                        thisSpawnEntry.Position_Z = unit.gameObject.GetBaseGameObject().WorldPhysicalCenter.z;
                        thisSpawnEntry.Rotation_X = unit.GetFacingRotation().x;
                        thisSpawnEntry.Rotation_Y = unit.GetFacingRotation().y;
                        thisSpawnEntry.Rotation_Z = unit.GetFacingRotation().z;
                        thisSpawnEntry.Rotation_W = unit.GetFacingRotation().w;
                        thisSpawnEntry.TeamIndex = unit.Team.Index;
                        thisSpawnEntry.IsStructure = false;

                        spawnSetup.SpawnEntries.Add(thisSpawnEntry);
                    }
                }

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

        public void Command_LoadSetup(Il2Cpp.Player callerPlayer, String args)
        {
            String commandName = args.Split(' ')[0];

            // validate argument count
            int argumentCount = args.Split(' ').Count() - 1;
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
                if (configFile.Contains(".") && !configFile.EndsWith("json"))
                {
                    HelperMethods.ReplyToCommand(commandName + ": Invalid save name (not .json)");
                    return;
                }

                // add .json if it's not already there
                if (!configFile.Contains("."))
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

                if (spawnSetup.Map != NetworkGameServer.GetServerMapName())
                {
                    HelperMethods.ReplyToCommand(commandName + ": incompatible map specified");
                    return;
                }

                MP_Strategy strategyInstance = GameObject.FindObjectOfType<Il2Cpp.MP_Strategy>();
                if (spawnSetup.VersusMode != strategyInstance.TeamsVersus.ToString())
                {
                    HelperMethods.ReplyToCommand(commandName + ": incompatible mode specified");
                    return;
                }

                // load all structures and units
                foreach (SpawnEntry spawnEntry in spawnSetup.SpawnEntries)
                {
                    MelonLogger.Msg(spawnEntry.Classname);

                    Vector3 position = new Vector3(spawnEntry.Position_X, spawnEntry.Position_Y, spawnEntry.Position_Z);
                    Quaternion rotation = new Quaternion(spawnEntry.Rotation_X, spawnEntry.Rotation_Y, spawnEntry.Rotation_Z, spawnEntry.Rotation_W);
                    GameObject? spawnedObject = SpawnAtLocation(spawnEntry.Classname, position, rotation);
                    if (spawnedObject == null)
                    {
                        HelperMethods.ReplyToCommand(commandName + ": bad name in config file");
                        return;
                    }

                    // check if team is correct
                    BaseGameObject baseObject = spawnedObject.GetBaseGameObject();
                    if (baseObject.m_Team.Index != spawnEntry.TeamIndex)
                    {
                        baseObject.Team = Team.Teams[spawnEntry.TeamIndex];
                        baseObject.m_Team = Team.Teams[spawnEntry.TeamIndex];
                        baseObject.UpdateToCurrentTeam();
                    }
                }

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