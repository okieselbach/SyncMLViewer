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
        private static bool argOmaUri = false;
        private static string argOmaUriValue = string.Empty;
        private static bool argCommand = false;
        private static string argCommandValue = string.Empty;
        private static bool argCleanup = false;

        [MTAThread]
        static void Main(string[] args)
        {
            // SyncMLViewer.Executer -Command GET -OmaUri "./DevDetail/Ext/DeviceHardwareData"
            // SyncMLViewer.Executer -Command GET -SyncML "<SyncBody><GET><CmdID>1</CmdID><Item><Target><LocURI>./DevDetail/Ext/DeviceHardwareData</LocURI></Target><Meta><Format xmlns=\"syncml:metinf\">int</Format><Type xmlns=\"syncml:metinf\">text/plain</Type></Meta><Data></Data></Item></GET></SyncBody>"
            // SyncMLViewer.Executer -Command GET -SyncMLInputFile "C:\Code\GitHubRepos\SyncMLViewer\SyncMLViewer\bin\x64\Debug\SyncMlViewer.SyncMl.txt"

            ParseCommandlineArgs(args);

            bool inputFile = false;
            string syncMLResult = string.Empty;
            string data = string.Empty;
            string command = "GET";

            if (args.Length == 1)
            {
                // sometimes [-SyncMLInputFile "C:\Code\GitHubRepos\SyncMLViewer\SyncMLViewer\bin\x64\Debug\SyncMlViewer.SyncMl.txt"] is passed in as a single argument
                Debug.WriteLine($"Argument1: {args[0]}");

                if (argSyncMlFile)
                {
                    int startIndex = "-syncmlfile".Length + 1;
                    var filePath = args[0].ToLower().Substring(startIndex, args[0].Length - startIndex);
                    try
                    {

                        data = File.ReadAllText(filePath);
                        inputFile = true;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to read file {filePath}, ex = {ex}");
                    }
                }
            }
            else if (args.Length >= 2)
            {
                Debug.WriteLine($"Argument1: {args[0]}");
                Debug.WriteLine($"Argument2: {args[1]}");

                if (argSyncMlFile)
                {
                    try
                    {

                        data = File.ReadAllText(argSyncMlFileValue);
                        inputFile = true;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to read file {argSyncMlFileValue}, ex = {ex}");
                    }

                    // Call the Local MDM API
                    syncMLResult = MdmLocalManagement.SendRequestProcedure(data);
                }
                if (argOmaUri)
                {
                    data = argOmaUriValue;

                    if (argCommand)
                    {
                        command = argCommandValue;
                    }

                    // Call the Local MDM API
                    syncMLResult = MdmLocalManagement.SendRequestProcedure(data, command);
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
                            case "omauri":
                                argOmaUri = true;
                                argOmaUriValue = args[i + 1];
                                break;
                            case "command":
                                argCommand = true;
                                argCommandValue = args[i + 1];
                                break;
                            case "cleanup":
                                argCleanup = true;
                                break;
                            case "?":
                            case "h":
                                Console.WriteLine($"2024 by Oliver Kieselbach (oliverkieselbach.com)");
                                Console.WriteLine("");
                                Console.WriteLine($"USAGE: {Assembly.GetExecutingAssembly().GetName().Name} [options...]");
                                Console.WriteLine("");
                                Console.WriteLine($"-SyncMLFile <filepath>  path to input file with SyncML data: e.g. <SyncBody><GET>...");
                                Console.WriteLine($"                        parameter -syncMLFile <filepath> can not be combined with other parameters");
                                Console.WriteLine($"-OMAURI <omauri>        specify OMA-URI: e.g. ./DevDetail/Ext/Microsoft/DeviceName");
                                Console.WriteLine($"-Command <command>      specify Command: GET, ADD, ATOMIC, DELETE, EXEC, REPLACE, RESULT");
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
