/*
Silica Commander Management Mod
Copyright (C) 2023 by databomb

* Description *
For Silica listen servers, allows hosts to use the !kick or !afk command
to disconnect a player without a session ban.

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
using Il2CppSystem.Runtime.Remoting.Channels;
using MelonLoader;
using Si_AFKManager;
using AdminExtension;
using Il2CppBehaviorDesigner.Runtime.Formations.Tasks;

[assembly: MelonInfo(typeof(AwayFromKeyboard), "AFK Manager", "1.0.0", "databomb")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]

namespace Si_AFKManager
{
    public class AwayFromKeyboard : MelonMod
    {
        static bool AdminModAvailable = false;

        public override void OnLateInitializeMelon()
        {
            AdminModAvailable = RegisteredMelons.Any(m => m.Info.Name == "Admin Mod");
        }

        public static bool KickPlayer(Il2Cpp.Player playerToKick)
        {
            Il2Cpp.Player serverPlayer = Il2Cpp.NetworkGameServer.GetServerPlayer();

            if (playerToKick == serverPlayer)
            {
                return false;
            }

            Il2CppSteamworks.CSteamID serverSteam = NetworkGameServer.GetServerID();
            int playerChannel = playerToKick.PlayerChannel;
            Il2CppSteamworks.CSteamID playerSteam = playerToKick.PlayerID;

            Il2Cpp.NetworkLayer.SendPlayerConnectResponse(ENetworkPlayerConnectType.Kicked, playerSteam, playerChannel, serverSteam);
            Il2Cpp.Player.RemovePlayer(playerSteam, playerChannel);
            NetworkLayer.SendPlayerConnect(ENetworkPlayerConnectType.Disconnected, playerSteam, playerChannel);

            return true;
        }

        [HarmonyPatch(typeof(Il2Cpp.MusicJukeboxHandler), nameof(Il2Cpp.MusicJukeboxHandler.OnGameStarted))]
        private static class ApplyPatchOnGameStarted
        {
            public static void Postfix(Il2Cpp.MusicJukeboxHandler __instance, Il2Cpp.GameMode __0)
            {
                try
                {
                    // TODO: Begin timer to track AFK players every 30 seconds
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run OnGameStarted");
                }
            }
        }

        [HarmonyPatch(typeof(Il2CppSilica.UI.Chat), nameof(Il2CppSilica.UI.Chat.MessageReceived))]
        private static class ApplyReceiveChatKickCommandPatch
        {
            private static void HandleChat(object sender, System.EventArgs e)
            {
                // Add your form load event handling code here.
            }

            public static void Postfix(Il2CppSilica.UI.Chat __instance, Il2Cpp.Player __0, string __1, bool __2)
            {
                try
                {
                    // each faction has its own chat manager but by looking at alien and only global messages this catches commands only once
                    if (__instance.ToString().Contains("alien") && __2 == false)
                    {
                        bool bIsKickCommand = (String.Equals(__1.Split(' ')[0], "!kick", StringComparison.OrdinalIgnoreCase) ||
                                                    String.Equals(__1.Split(' ')[0], "!afk", StringComparison.OrdinalIgnoreCase));
                        if (bIsKickCommand)
                        {
                            // check for authorized
                            if (!AdminModAvailable)
                            {
                                // default to only server operator is considered authorized
                                Il2Cpp.Player serverPlayer = Il2Cpp.NetworkGameServer.GetServerPlayer();
                                if (serverPlayer != __0)
                                {
                                    HelperMethods.ReplyToCommand_Player(__0, "cannot use " + __1.Split(' ')[0]);
                                    return;
                                }
                            }
                            else
                            {
                                if (!__0.CanAdminExecute(Power.Kick))
                                {
                                    HelperMethods.ReplyToCommand_Player(__0, "cannot use " + __1.Split(' ')[0]);
                                    return;
                                }
                            }

                            // validate argument count
                            int argumentCount = __1.Split(' ').Count() - 1;
                            if (argumentCount > 1)
                            {
                                HelperMethods.ReplyToCommand(__1.Split(' ')[0] + ": Too many arguments");
                                return;
                            }
                            else if (argumentCount < 1)
                            {
                                HelperMethods.ReplyToCommand(__1.Split(' ')[0] + ": Too few arguments");
                                return;
                            }

                            // validate argument contents
                            String sTarget = __1.Split(' ')[1];
                            Il2Cpp.Player? playerToKick = HelperMethods.FindTargetPlayer(sTarget);

                            if (playerToKick == null)
                            {
                                HelperMethods.ReplyToCommand(__1.Split(' ')[0] + ": Ambiguous or invalid target");
                                return;
                            }

                            if (__0.CanAdminTarget(playerToKick))
                            {
                                if (KickPlayer(playerToKick))
                                {
                                    HelperMethods.AlertAdminActivity(__0, playerToKick, "kicked");
                                }
                                else
                                {
                                    HelperMethods.ReplyToCommand_Player(playerToKick, "is the host and cannot be targeted");
                                }
                            }
                            else
                            {
                                HelperMethods.ReplyToCommand_Player(playerToKick, "is immune due to level");
                            }
                        }
                    }
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run MessageReceived");
                }

                return;
            }
        }
    }
}