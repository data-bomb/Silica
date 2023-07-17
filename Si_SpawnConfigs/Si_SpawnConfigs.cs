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

[assembly: MelonInfo(typeof(SpawnConfigs), "Admin Spawn Configs", "0.8.0", "databomb")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]

namespace Si_SpawnConfigs
{
    public class SpawnConfigs : MelonMod
    {
        static bool AdminModAvailable = false;
        static GameObject? lastSpawnedObject;

        public override void OnLateInitializeMelon()
        {
            AdminModAvailable = RegisteredMelons.Any(m => m.Info.Name == "Admin Mod");

            if (AdminModAvailable)
            {
                HelperMethods.CommandCallback spawnCallback = Command_Spawn;
                HelperMethods.RegisterAdminCommand("!spawn", spawnCallback, Power.Cheat);

                HelperMethods.CommandCallback undoSpawnCallback = Command_UndoSpawn;
                HelperMethods.RegisterAdminCommand("!undospawn", undoSpawnCallback, Power.Cheat);
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

            if (SpawnAtLocation(args.Split(' ')[1], playerPosition, playerRotation))
            {
                HelperMethods.AlertAdminAction(callerPlayer, "spawned " + args.Split(' ')[1]);
            }
            else
            {
                HelperMethods.ReplyToCommand(args.Split(' ')[0] + ": Failed to spawn");
            }
        }

        public static bool SpawnAtLocation(String name, Vector3 position, Quaternion rotation)
        {
            int prefabIndex = GameDatabase.GetSpawnablePrefabIndex(name);
            if (prefabIndex <= -1)
            {
                return false;
            }

            GameObject prefabObject = GameDatabase.GetSpawnablePrefab(prefabIndex);
            GameObject spawnedObject = Game.SpawnPrefab(prefabObject, null, true, true);

            if (spawnedObject == null)
            {
                return false;
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

            return true;
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
    }
}