﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SyncMLViewer.Executer
{
    internal class Program
    {
        private static bool argSyncMlFile = false;
        private static string argSyncMlFileValue = string.Empty;
        private static bool argSyncMl = false;
        private static string argSyncMlValue = string.Empty;
        private static bool argOmaUri = false;
        private static string argOmaUriValue = string.Empty;
        private static bool argCommand = false;
        private static string argCommandValue = string.Empty;
        private static bool argData = false;
        private static string argDataValue = string.Empty;
        private static bool argFormat = false;
        private static string argFormatValue = string.Empty;
        private static bool argType = false;
        private static string argTypeValue = string.Empty;
        private static bool argKeepLocalMDMEnrollment = false; 
        private static bool argKeepLocalMDMEnrollmentValue = false;
        private static bool argVerbose = false;
        private static bool argQuiet = false;
        private static bool argSetEmbeddedMode = false;
        private static bool argSetEmbeddedModeValue = false;
        private static bool argUnregisterLocalMDMEnrollment = false;
        private static bool argUnregisterLocalMDMEnrollmentValue = false;
        private static bool argRedirectLocalMDMEnrollment = false;
        private static bool argRedirectLocalMDMEnrollmentValue = false;
        private static bool argCleanupLocalMDMEnrollment = false;
        private static bool argCleanupLocalMDMEnrollmentValue = false;

        // Important, MTA is necessary otherwise the Local MDM API will not work!
        [MTAThread]
        static void Main(string[] args)
        {
            if (Helper.IsElevated() == false)
            { 
                Console.WriteLine("Please run this tool with elevated permissions!");
                return;
            }

            // SyncMLViewer.Executer -Command GET -OmaUri "./DevDetail/Ext/DeviceHardwareData"
            // SyncMLViewer.Executer -SyncMLInputFile "C:\Code\GitHubRepos\SyncMLViewer\SyncMLViewer\bin\x64\Debug\SyncMlViewer.SyncMl.txt"
            // SyncMLViewer.Executer -SyncML "<SyncBody><GET><CmdID>1</CmdID><Item><Target><LocURI>./DevDetail/Ext/DeviceHardwareData</LocURI></Target><Meta><Format xmlns=`"syncml:metinf`">chr</Format><Type xmlns=`"syncml:metinf`">text/plain</Type></Meta><Data></Data></Item></GET></SyncBody>"

            ParseCommandlineArgs(args);

            string prog = Assembly.GetExecutingAssembly().GetName().Name;
            bool inputFile = false;
            string syncMl = string.Empty;
            string syncMLResult = string.Empty;
            string dataDefault = string.Empty;
            string commandDefault = "GET";
            string formatDefault = "int";
            string typeDefault = "text/plain";
            bool keepLocalMDMEnrollmentDefault = false;
            bool redirectLocalMDMEnrollmentDefault = false;

            if (args.Length == 1)
            {
                // sometimes [-SyncMLInputFile "C:\Code\GitHubRepos\SyncMLViewer\SyncMLViewer\bin\x64\Debug\SyncMlViewer.SyncMl.txt"] is passed in as a single argument
                // rare special case , but we handle it

                if (argSyncMlFile)
                {
                    Debug.WriteLine($"[{prog}] Received: {args[0]}");
                    try
                    {
                        int startIndex = "-syncmlfile".Length + 1;
                        var filePath = args[0].ToLower().Substring(startIndex, args[0].Length - startIndex);
                        syncMl = File.ReadAllText(filePath);
                        inputFile = true;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[{prog}] Failed to read file, ex = {ex}");
                    }
                }
                if (argUnregisterLocalMDMEnrollment)
                {
                    Debug.WriteLine($"[{prog}] Received {nameof(argUnregisterLocalMDMEnrollment)}: {argUnregisterLocalMDMEnrollmentValue}");
                    MdmLocalManagement.SetEmbeddedMode();
                    MdmLocalManagement.UnregisterLocalMDM();
                    MdmLocalManagement.ClearEmbeddedMode();
                    return;
                }
                if (argCleanupLocalMDMEnrollment)
                {
                    Debug.WriteLine($"[{prog}] Received {nameof(argCleanupLocalMDMEnrollment)}: {argCleanupLocalMDMEnrollmentValue}");
                    var count = MdmLocalManagement.ClenaupEnrollments();
                    if (count > 0)
                    {
                        Console.WriteLine($"Cleaned up {count} orphaned local MDM enrollment entries");
                    }
                    else
                    {
                        Console.WriteLine($"No orphaned local MDM enrollment entries found");
                    }   
                    return;
                }
            }
            else if (args.Length >= 2)
            {
                if (argVerbose)
                {
                    Debug.WriteLine($"[{prog}] Received {nameof(argVerbose)}: {argVerbose}");
                    Debug.Listeners.Add(new TextWriterTraceListener(Console.Out));
                    Debug.AutoFlush = true;
                }
                if (argSetEmbeddedMode)
                {
                    Debug.WriteLine($"[{prog}] Received {nameof(argSetEmbeddedMode)}: {argSetEmbeddedModeValue}");
                    if (argSetEmbeddedModeValue == true)
                    {
                        MdmLocalManagement.SetEmbeddedMode();
                    }
                    else
                    {
                        MdmLocalManagement.ClearEmbeddedMode();
                    }
                    return;
                }
                if (argKeepLocalMDMEnrollment)
                {
                    Debug.WriteLine($"[{prog}] Received {nameof(argKeepLocalMDMEnrollment)}: {argKeepLocalMDMEnrollmentValue}");
                    keepLocalMDMEnrollmentDefault = argKeepLocalMDMEnrollmentValue;
                }
                if (argRedirectLocalMDMEnrollment)
                {
                    Debug.WriteLine($"[{prog}] Received {nameof(argRedirectLocalMDMEnrollment)}: {argRedirectLocalMDMEnrollmentValue}");
                    redirectLocalMDMEnrollmentDefault = argRedirectLocalMDMEnrollmentValue;
                }
                if (argSyncMlFile)
                {
                    try
                    {
                        Debug.WriteLine($"[{prog}] Received {nameof(argSyncMlFile)}: {argSyncMlFileValue}");
                        syncMl = File.ReadAllText(argSyncMlFileValue);
                        inputFile = true;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[{prog}] Failed to read file {argSyncMlFileValue}, ex = {ex}");
                    }

                    // Call the Local MDM API
                    syncMLResult = MdmLocalManagement.SendRequestProcedure(syncMl, keepLocalMDMEnrollment: keepLocalMDMEnrollmentDefault, redirectLocalMDMEnrollment: redirectLocalMDMEnrollmentDefault);
                }
                if (argSyncMl)
                {
                    Debug.WriteLine($"[{prog}] Received {nameof(argSyncMl)}: {argSyncMlValue}");

                    // Call the Local MDM API
                    syncMLResult = MdmLocalManagement.SendRequestProcedure(argSyncMlValue, keepLocalMDMEnrollment: keepLocalMDMEnrollmentDefault, redirectLocalMDMEnrollment: redirectLocalMDMEnrollmentDefault);
                }
                if (argOmaUri)
                {
                    Debug.WriteLine($"[{prog}] Received {nameof(argOmaUri)}: {argOmaUriValue}");

                    if (argCommand)
                    {
                        Debug.WriteLine($"[{prog}] Received {nameof(argCommand)}: {argCommandValue}");
                        commandDefault = argCommandValue;
                    }
                    if (argData)
                    {
                        Debug.WriteLine($"[{prog}] Received {nameof(argData)}: {argDataValue}");
                        dataDefault = argDataValue;
                    }
                    if (argFormat)
                    {
                        Debug.WriteLine($"[{prog}] Received {nameof(argFormat)}: {argFormatValue}");
                        formatDefault = argFormatValue;
                    }
                    if (argType)
                    {
                        Debug.WriteLine($"[{prog}] Received {nameof(argType)}: {argTypeValue}");
                        typeDefault = argTypeValue;
                    }

                    // Call the Local MDM API
                    syncMLResult = MdmLocalManagement.SendRequestProcedure(argOmaUriValue, commandDefault, dataDefault, formatDefault, typeDefault, keepLocalMDMEnrollmentDefault, redirectLocalMDMEnrollment: redirectLocalMDMEnrollmentDefault);
                }
            }

            // if input file was specified, write output to disk
            if (inputFile)
            {
                var assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

                string syncMlOutputFile = "SyncMlViewer.SyncMLOutput.txt";
                var syncMlOutputFilePath = Path.Combine(assemblyPath, syncMlOutputFile);
                try
                {
                    File.WriteAllText(syncMlOutputFilePath, MdmLocalManagement.TryPrettyXml(syncMLResult));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[{prog}] Failed to write {syncMlOutputFilePath} to disk, ex = {ex}");
                }
            }

            if (!argQuiet)
            {
                // write output to console
                Console.WriteLine(MdmLocalManagement.TryPrettyXml(syncMLResult));
            }
        }

        private static void ParseCommandlineArgs(string[] args)
        {
            if (args.Any())
            {
                for (int i = 0; i < args.Length; i++)
                {
                    string arg = args[i];
                    if (arg.StartsWith("-"))
                    {
                        switch (arg.TrimStart(new[] { '-' }).ToLower())
                        {
                            case "syncmlfile":
                                argSyncMlFile = true;
                                argSyncMlFileValue = args[i + 1];
                                break;
                            case "syncml":
                                argSyncMl = true;
                                argSyncMlValue = args[i + 1];
                                break;
                            case "omauri":
                                argOmaUri = true;
                                argOmaUriValue = args[i + 1];
                                break;
                            case "command":
                                argCommand = true;
                                argCommandValue = args[i + 1];
                                break;
                            case "data":
                                argData = true;
                                argDataValue = args[i + 1];
                                break;
                            case "format":
                                argFormat = true;
                                argFormatValue = args[i + 1];
                                break;
                            case "type":
                                argType = true;
                                argTypeValue = args[i + 1];
                                break;
                            case "keeplocalmdmenrollment":
                                argKeepLocalMDMEnrollment = true;
                                argKeepLocalMDMEnrollmentValue = true;
                                break;
                            case "unregisterlocalmdmenrollment":
                                argUnregisterLocalMDMEnrollment = true;
                                argUnregisterLocalMDMEnrollmentValue = true;
                                break;
                            case "setembeddedmode":
                                argSetEmbeddedMode = true;
                                try
                                {
                                    argSetEmbeddedModeValue = bool.Parse(args[i + 1]);
                                }
                                catch (Exception)
                                {
                                    Console.WriteLine($"ERROR: {arg} requires a boolean value (true|false)");
                                }
                                break;
                            case "redirectlocalmdmenrollment":
                                argRedirectLocalMDMEnrollment = true;
                                argRedirectLocalMDMEnrollmentValue = true;
                                break;
                            case "cleanuplocalmdmenrollment":
                                argCleanupLocalMDMEnrollment = true;
                                argCleanupLocalMDMEnrollmentValue = true;
                                break;
                            case "verbose":
                                argVerbose = true;
                                break;
                            case "quiet":
                                argQuiet = true;
                                break;
                            case "?":
                            case "h":
                                Console.WriteLine($"2024 by Oliver Kieselbach (oliverkieselbach.com)");
                                Console.WriteLine($"The tools uses the Local MDM management API (mdmlocalmanagement.dll) to register, execute, and unregister.");
                                Console.WriteLine("");
                                Console.WriteLine($"USAGE: {Assembly.GetExecutingAssembly().GetName().Name} [options...]");
                                Console.WriteLine("");
                                Console.WriteLine($"-SyncMLFile <filepath>          path to input file with SyncML data: e.g. <SyncBody><GET>...");
                                Console.WriteLine($"                                parameter -syncMLFile <filepath> can only be combined with -KeepLocalMDMEnrollment");
                                Console.WriteLine($"-SyncML <syncml>                one liner SyncML data: e.g. <SyncBody><GET>... escape quotes \" accordingly!");
                                Console.WriteLine($"                                parameter -syncML <syncml> can only be combined with -KeepLocalMDMEnrollment");
                                Console.WriteLine($"-OMAURI <omauri>                specify OMA-URI: e.g. ./DevDetail/Ext/Microsoft/DeviceName");
                                Console.WriteLine($"-Command <command>              specify Command: GET, ADD, ATOMIC, DELETE, EXEC, REPLACE, RESULT");
                                Console.WriteLine($"-Data <data>                    specify Data: e.g. 123, ABC, base64");
                                Console.WriteLine($"-Format <format>                specify Format: int, char, bool, b64, null, xml");
                                Console.WriteLine($"-Type <type>                    specify Type: e.g. text/plain, b64, registered MIME content-type");
                                Console.WriteLine($"-SetEmbeddedMode <true|false>   specify SetEmbeddedMode, must be used without other parameters");
                                Console.WriteLine($"-KeepLocalMDMEnrollment         specify KeepLocalMDMEnrollment, prevent call of Unregister API to revert most changes");
                                Console.WriteLine($"-UnregisterLocalMDMEnrollment   specify UnregisterLocalMDMEnrollment, call Unregister API to revert most changes");
                                Console.WriteLine($"-RedirectLocalMDMEnrollment     specify RedirectLocalMDMEnrollment, redirect local MDM requests to real MDM enrollment");
                                Console.WriteLine($"                                redirection will be the MDM enrollment not MMP-C enrollment");
                                Console.WriteLine($"-CleanupLocalMDMEnrollment      specify CleanupLocalMDMEnrollment, cleanup orphaned local MDM registry entires");
                                Console.WriteLine($"                                can only be used as single parameter");
                                Console.WriteLine($"-Verbose                        specify Verbose, generate verbose/debug output");
                                Console.WriteLine($"-Quiet                          specify Quiet, surpress any ouput");
                                Environment.Exit(0);
                                break;
                            default:
                                break;
                        }
                    }
                }
            }
        }
    }
}
