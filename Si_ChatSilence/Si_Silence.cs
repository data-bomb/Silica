using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using Si_ChatSilence;
using UnityEngine;
using AdminExtension;
using Il2CppBehaviorTrees;
using Il2CppSteamworks;

[assembly: MelonInfo(typeof(ChatSilence), "Silence Admin Command", "1.0.0", "databomb")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]

namespace Si_ChatSilence
{
    public class ChatSilence : MelonMod
    {
        static bool adminModAvailable = false;
        static List<CSteamID> silencedPlayers;

        public override void OnInitializeMelon()
        {
            silencedPlayers = new List<CSteamID>();
        }

        public override void OnLateInitializeMelon()
        {
            adminModAvailable = RegisteredMelons.Any(m => m.Info.Name == "Admin Mod");

            if (adminModAvailable)
            {
                HelperMethods.CommandCallback silenceCallback = Command_Silence;
                HelperMethods.RegisterAdminCommand("!silence", silenceCallback, Power.Mute);

                HelperMethods.CommandCallback unSilenceCallback = Command_UnSilence;
                HelperMethods.RegisterAdminCommand("!unsilence", unSilenceCallback, Power.Mute);
            }
            else
            {
                MelonLogger.Warning("Dependency missing: Admin Mod");
            }
        }

        public void Command_Silence(Il2Cpp.Player callerPlayer, String args)
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

            // validate argument contents
            String sTarget = args.Split(' ')[1];
            Il2Cpp.Player? playerTarget = HelperMethods.FindTargetPlayer(sTarget);

            if (playerTarget == null)
            {
                HelperMethods.ReplyToCommand(args.Split(' ')[0] + ": Ambiguous or invalid target");
                return;
            }

            if (callerPlayer.CanAdminTarget(playerTarget))
            {
                if (IsPlayerSilenced(playerTarget))
                {
                    HelperMethods.ReplyToCommand(args.Split(' ')[0] + ": Target player already silenced");
                }
                else
                {
                    SilencePlayer(playerTarget);
                    HelperMethods.AlertAdminActivity(callerPlayer, playerTarget, "silenced");
                }
            }
            else
            {
                HelperMethods.ReplyToCommand_Player(playerTarget, "is immune due to level");
            }
        }

        public void Command_UnSilence(Il2Cpp.Player callerPlayer, String args)
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

            // validate argument contents
            String sTarget = args.Split(' ')[1];
            Il2Cpp.Player? playerTarget = HelperMethods.FindTargetPlayer(sTarget);

            if (playerTarget == null)
            {
                HelperMethods.ReplyToCommand(args.Split(' ')[0] + ": Ambiguous or invalid target");
                return;
            }

            if (callerPlayer.CanAdminTarget(playerTarget))
            {
                if (IsPlayerSilenced(playerTarget))
                {
                    UnSilencePlayer(playerTarget);
                    HelperMethods.AlertAdminActivity(callerPlayer, playerTarget, "unsilenced");
                }
                else
                {
                    HelperMethods.ReplyToCommand(args.Split(' ')[0] + ": Target player not silenced");
                }
            }
            else
            {
                HelperMethods.ReplyToCommand_Player(playerTarget, "is immune due to level");
            }
        }

        public static bool IsPlayerSilenced(Il2Cpp.Player player)
        {
            return silencedPlayers.Any(s => s == player.PlayerID);
        }

        public static bool IsSteamSilenced(Il2CppSteamworks.CSteamID steamID)
        {
            return silencedPlayers.Any(s => s ==  steamID);
        }

        public static void SilencePlayer(Il2Cpp.Player playerTarget)
        {
            silencedPlayers.Add(playerTarget.PlayerID);
        }

        public static void UnSilencePlayer(Il2Cpp.Player playerTarget)
        {
            silencedPlayers.Remove(playerTarget.PlayerID);
        }

        [HarmonyPatch(typeof(Il2Cpp.GameByteStreamReader), nameof(Il2Cpp.GameByteStreamReader.GetGameByteStreamReader))]
        static class GetGameByteStreamReaderPrePatch
        {
            public static void Prefix(Il2Cpp.GameByteStreamReader __result, Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte> __0, int __1, bool __2)
            {
                try
                {
                    // byte[0] = (2) Byte
                    // byte[1] = ENetworkPacketType
                    Il2Cpp.ENetworkPacketType packetType = (Il2Cpp.ENetworkPacketType)__0[1];
                    if (packetType == Il2Cpp.ENetworkPacketType.ChatMessage)
                    {
                        // byte [2] = UInt64
                        // byte [3:10] = CSteamID
                        CSteamID cSteamID = (CSteamID)BitConverter.ToUInt64(__0, 3);

                        if (IsSteamSilenced(cSteamID))
                        {
                            // null out bytes in player
                            for (int i = 0; i < sizeof(UInt64); i++)
                            {
                                __0[3 + i] = 0;
                            }

                            return;
                        }
                    }
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run GameByteStreamReader::GetGameByteStreamReader");
                }
            }
        }

        [HarmonyPatch(typeof(Il2Cpp.GameMode), nameof(Il2Cpp.GameMode.OnPlayerLeftBase))]
        private static class Silence_Patch_GameMode_OnPlayerLeftBase
        {
            public static void Prefix(Il2Cpp.GameMode __instance, Il2Cpp.Player __0)
            {
                try
                {
                    if (__0 != null)
                    {
                        if (IsPlayerSilenced(__0))
                        {
                            silencedPlayers.Remove(__0.PlayerID);
                        }
                    }
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run GameMode::OnPlayerLeftBase");
                }
            }
        }
    }
}