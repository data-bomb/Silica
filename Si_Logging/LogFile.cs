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
#endif

using System;
using SilicaAdminMod;
using MelonLoader.Utils;
using MelonLoader;

namespace Si_Logging
{
    public partial class HL_Logging
    {
        static String CurrentLogFile = GetLogFilePath();

        public static bool ParserExePresent()
        {
            return System.IO.File.Exists(GetParserPath());
        }
        public static string GetParserPath()
        {
            return System.IO.Path.Combine(MelonEnvironment.UserDataDirectory, Pref_Log_ParserExe.Value);
        }

        public static bool VideoGeneratorExePresent()
        {
            return System.IO.File.Exists(GetVideoGeneratorPath());
        }
        public static string GetVideoGeneratorPath()
        {
            return System.IO.Path.Combine(MelonEnvironment.UserDataDirectory, Pref_Log_VideoExe.Value);
        }

        public static void PrintLogLine(string LogMessage, bool suppressConsoleOutput = false)
        {
            if (LogMessage != null)
            {
                string TempLogFile = GetLogFilePath();
                if (TempLogFile != CurrentLogFile)
                {
                    System.IO.File.AppendAllText(CurrentLogFile, GetLogPrefix() + "Log file closed" + Environment.NewLine);
                    CurrentLogFile = TempLogFile;
                    AddFirstLogLine();
                }
                string LogLine = GetLogPrefix() + LogMessage;

                if (!suppressConsoleOutput)
                {
                    MelonLogger.Msg(LogLine);
                }

                System.IO.File.AppendAllText(CurrentLogFile, LogLine + Environment.NewLine);
            }
        }

        public static void AddFirstLogLine()
        {
            string FirstLine = "Log file started (file \"" + GetLogSubPath() + "\") (game \"" + MelonEnvironment.GameExecutablePath + "\") (version \"" + MelonLoader.InternalUtils.UnityInformationHandler.GameVersion + "\") (hostid \"" + NetworkGameServer.GetServerID().ToString() + "\")";
            System.IO.File.AppendAllText(CurrentLogFile, GetLogPrefix() + FirstLine + Environment.NewLine);
        }

        public static string GetLogFileDirectory()
        {
            return System.IO.Path.Combine(MelonEnvironment.UserDataDirectory, @"logs\");
        }

        public static string GetLogSubPath()
        {
            return @"logs\" + GetLogName();
        }

        public static string GetLogFilePath()
        {
            string LogSubPath = GetLogSubPath();
            return System.IO.Path.Combine(MelonEnvironment.UserDataDirectory, LogSubPath);
        }

        public static string GetLogName()
        {
            DateTime currentDateTime = DateTime.Now;
            return "L" + currentDateTime.ToString("yyyyMMdd") + ".log";
        }
    }
}
