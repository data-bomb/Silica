using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using MelonLoader.Utils;
using Newtonsoft.Json;
using Si_BasicBanlist;
using UnityEngine;

[assembly: MelonInfo(typeof(BasicBanlist), "[Si] Basic Banlist", "0.9.0", "databomb")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]

namespace Si_BasicBanlist
{
    public class BasicBanlist : MelonMod
    {
        public class BanEntry
        {
            public long OffenderSteamId
            {
                get;
                set;
            }
            public String OffenderName
            {
                get;
                set;
            }
            public int UnixBanTime
            {
                get;
                set;
            }
            public String Comments
            {
                get;
                set;
            }
        }

        static List<BanEntry> MasterBanList;
        static String banListFile = MelonEnvironment.UserDataDirectory + "\\banned_users.json";

        public override void OnInitializeMelon()
        {
            try
            {
                if (File.Exists(BasicBanlist.banListFile))
                {
                    // Open the stream and read it back.
                    using (StreamReader banFileStream = File.OpenText(BasicBanlist.banListFile))
                    {
                        String JsonRaw = banFileStream.ReadToEnd();

                        MelonLogger.Msg(JsonRaw);

                        MasterBanList = JsonConvert.DeserializeObject<List<BanEntry>>(JsonRaw);
                    }
                }
                else
                {
                    MasterBanList = new List<BanEntry>();
                }
            }
            catch (Exception exception)
            {
                MelonLogger.Msg(exception.ToString());
            }
        }

        [HarmonyPatch(typeof(Il2Cpp.NetworkGameServer), nameof(Il2Cpp.NetworkGameServer.KickPlayer))]
        private static class ApplyPatchKickPlayer
        {
            public static bool Prefix(Il2Cpp.Player __0, bool __1)
            {
                // gather information to log in the banlist
                BasicBanlist.BanEntry thisBan = new BanEntry();
                thisBan.OffenderSteamId = long.Parse(__0.ToString().Split('_')[1]);
                thisBan.OffenderName = __0.PlayerName;
                thisBan.UnixBanTime = (int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
                thisBan.Comments = "banned by " + Il2Cpp.NetworkGameServer.GetServerPlayer().PlayerName;
                
                // are we already banned?
                if (BasicBanlist.MasterBanList.Find(i => i.OffenderSteamId ==  thisBan.OffenderSteamId) != null)
                {
                    MelonLogger.Msg("Player name (" + thisBan.OffenderName + ") SteamID (" + thisBan.OffenderSteamId.ToString() + ") already on banlist.");
                }
                else
                {
                    MelonLogger.Msg("Added player name (" + thisBan.OffenderName + ") SteamID (" + thisBan.OffenderSteamId.ToString() + ") to the banlist.");
                    BasicBanlist.MasterBanList.Add(thisBan);

                    // convert back to json string
                    String JsonRaw = JsonConvert.SerializeObject(BasicBanlist.MasterBanList, Formatting.Indented);
                    MelonLogger.Msg(JsonRaw);
                    // write to file
                    try
                    {
                        if (File.Exists(BasicBanlist.banListFile))
                        {
                            File.WriteAllText(BasicBanlist.banListFile, JsonRaw);
                        }
                    }
                    catch (Exception exception)
                    {
                        MelonLogger.Msg(exception.ToString());
                    }
                    
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(Il2Cpp.GameMode), nameof(Il2Cpp.GameMode.OnPlayerJoinedBase))]
        private static class ApplyPatchOnPlayerJoinedBase
        {
            public static void Postfix(Il2Cpp.GameMode __instance, Il2Cpp.Player __0)
            {
                // check if player was previously banned
                long JoiningPlayerSteamId = long.Parse(__0.ToString().Split('_')[1]);
                if (BasicBanlist.MasterBanList.Find(i => i.OffenderSteamId == JoiningPlayerSteamId) != null)
                {
                    MelonLogger.Msg("Found match in ban database for: " + __0.ToString());
                    // perform kick
                    //Il2Cpp.NetworkGameServer.KickPlayer(__0);
                }
            }
        }
    }
}