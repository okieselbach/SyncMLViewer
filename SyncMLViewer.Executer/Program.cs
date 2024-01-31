using System;
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
        private static bool argCleanup = false;

        [MTAThread]
        static void Main(string[] args)
        {
            // SyncMLViewer.Executer -Command GET -OmaUri "./DevDetail/Ext/DeviceHardwareData"
            // SyncMLViewer.Executer -SyncMLInputFile "C:\Code\GitHubRepos\SyncMLViewer\SyncMLViewer\bin\x64\Debug\SyncMlViewer.SyncMl.txt"
            // SyncMLViewer.Executer -SyncML "<SyncBody><GET><CmdID>1</CmdID><Item><Target><LocURI>./DevDetail/Ext/DeviceHardwareData</LocURI></Target><Meta><Format xmlns=`"syncml:metinf`">chr</Format><Type xmlns=`"syncml:metinf`">text/plain</Type></Meta><Data></Data></Item></GET></SyncBody>"

            ParseCommandlineArgs(args);

            bool inputFile = false;
            string syncMl = string.Empty;
            string syncMLResult = string.Empty;
            string dataDefault = string.Empty;
            string commandDefault = "GET";
            string formatDefault = "int";
            string typeDefault = "text/plain";

            if (args.Length == 1)
            {
                // sometimes [-SyncMLInputFile "C:\Code\GitHubRepos\SyncMLViewer\SyncMLViewer\bin\x64\Debug\SyncMlViewer.SyncMl.txt"] is passed in as a single argument
                // rare special case , but we handle it

                if (argSyncMlFile)
                {
                    Debug.WriteLine($"Received: {args[0]}");
                    try
                    {
                        int startIndex = "-syncmlfile".Length + 1;
                        var filePath = args[0].ToLower().Substring(startIndex, args[0].Length - startIndex);
                        syncMl = File.ReadAllText(filePath);
                        inputFile = true;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to read file, ex = {ex}");
                    }
                }
            }
            else if (args.Length >= 2)
            {
                if (argSyncMlFile)
                {
                    try
                    {
                        Debug.WriteLine($"Received {nameof(argSyncMlFile)}: {argSyncMlFileValue}");
                        syncMl = File.ReadAllText(argSyncMlFileValue);
                        inputFile = true;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to read file {argSyncMlFileValue}, ex = {ex}");
                    }

                    // Call the Local MDM API
                    syncMLResult = MdmLocalManagement.SendRequestProcedure(syncMl);
                }
                if (argSyncMl)
                {
                    Debug.WriteLine($"Received {nameof(argSyncMl)}: {argSyncMlValue}");

                    // Call the Local MDM API
                    syncMLResult = MdmLocalManagement.SendRequestProcedure(argSyncMlValue);
                }
                if (argOmaUri)
                {
                    Debug.WriteLine($"Received {nameof(argOmaUri)}: {argOmaUriValue}");

                    if (argCommand)
                    {
                        Debug.WriteLine($"Received {nameof(argCommand)}: {argCommandValue}");
                        commandDefault = argCommandValue;
                    }
                    if (argData)
                    {
                        Debug.WriteLine($"Received {nameof(argData)}: {argDataValue}");
                        dataDefault = argDataValue;
                    }
                    if (argFormat)
                    {
                        Debug.WriteLine($"Received {nameof(argFormat)}: {argFormatValue}");
                        formatDefault = argFormatValue;
                    }
                    if (argType)
                    {
                        Debug.WriteLine($"Received {nameof(argType)}: {argTypeValue}");
                        typeDefault = argTypeValue;
                    }

                    // Call the Local MDM API
                    syncMLResult = MdmLocalManagement.SendRequestProcedure(argOmaUriValue, commandDefault, dataDefault, formatDefault, typeDefault);
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
                    Debug.WriteLine($"Failed to write {syncMlOutputFilePath} to disk, ex = {ex}");
                }
            }

            // write output to console
            Console.WriteLine(MdmLocalManagement.TryPrettyXml(syncMLResult));

            if (argCleanup)
            {
                MdmLocalManagement.UnregisterLocalMDM();
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
                            case "cleanup":
                                argCleanup = true;
                                break;
                            case "?":
                            case "h":
                                Console.WriteLine($"2024 by Oliver Kieselbach (oliverkieselbach.com)");
                                Console.WriteLine($"The tools uses the Local MDM management API to register, execute, and unregister.");
                                Console.WriteLine("");
                                Console.WriteLine($"USAGE: {Assembly.GetExecutingAssembly().GetName().Name} [options...]");
                                Console.WriteLine("");
                                Console.WriteLine($"-SyncMLFile <filepath>  path to input file with SyncML data: e.g. <SyncBody><GET>...");
                                Console.WriteLine($"                        parameter -syncMLFile <filepath> can not be combined with other parameters");
                                Console.WriteLine($"-SyncML <syncml>        one liner SyncML data: e.g. <SyncBody><GET>... escape quotes \" accordingly!");
                                Console.WriteLine($"                        parameter -syncML <syncml> can not be combined with other parameters");
                                Console.WriteLine($"-OMAURI <omauri>        specify OMA-URI: e.g. ./DevDetail/Ext/Microsoft/DeviceName");
                                Console.WriteLine($"-Command <command>      specify Command: GET, ADD, ATOMIC, DELETE, EXEC, REPLACE, RESULT");
                                Console.WriteLine($"-Data <data>            specify Data: e.g. 123, ABC, base64");
                                Console.WriteLine($"-Format <format>        specify Format: int, char, bool, b64, null, xml");
                                Console.WriteLine($"-Type <type>            specify Type: e.g. text/plain, b64, registered MIME content-type");
                                Console.WriteLine($"-Cleanup                specify Cleanup, calls Unregister API to revert most changes");
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
