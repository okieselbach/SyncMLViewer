using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Linq;

namespace SyncMLViewer.Executer
{
    public static partial class MdmLocalManagement
    {
        // CREDIT: PowerShell LocalMDM Module from Michael Niehaus (https://www.powershellgallery.com/packages/LocalMDM)

        // https://github.com/ms-iot/iot-core-azure-dm-client/blob/master/src/SystemConfigurator/CSPs/MdmProvision.cpp
        // https://github.com/daveRendon/azure-client-tools/blob/675c5dc3606eca6bcf1da92cfd1ff123f8549281/code/AzureDeviceManagementClient/Mdm/MdmServer.cpp#L79
        // https://github.com/tpn/winsdk-10/blob/9b69fd26ac0c7d0b83d378dba01080e93349c2ed/Include/10.0.16299.0/um/mdmlocalmanagement.h
        
        // Most important info is in the Windows SDK e.g.:
        // C:\Program Files (x86)\Windows Kits\10\Include\10.0.22621.0\um\mdmlocalmanagement.h

        private static int CmdIdCounter = 1;

        //	Routine Description:
        //
        //	This function is used to execute a SyncML.
        //	The device must invoke RegisterDeviceWithLocalManagement prior to calling this function.
        //
        //	Arguments:
        //
        //		syncMLRequest - Null terminated string containing SyncML request.
        //
        //		syncMLResult - Null terminated string containing SyncML result. 
        //					   Caller is responsible releasing memory allocated with LocalFree.
        //
        //	Return Value:
        //
        //		HRESULT indicating success or failure.

        //STDAPI
        //ApplyLocalManagementSyncML(
        //	_In_ 						  PCWSTR syncMLRequest,
        //	_Outptr_opt_result_maybenull_ PWSTR* syncMLResult
        //  );

        [DllImport("mdmlocalmanagement.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint ApplyLocalManagementSyncML(string syncMLRequest, out IntPtr syncMLResult);

        //	Routine Description:
        //
        //	This API is used to register a device with Local MDM Management synchronously.
        //	If a device is already registered, out parameter alreadyRegister is set to
        //	TRUE and function returns S_OK.In all other cases, out parameter alreadyRegistered
        //  is set to FALSE.
        //
        //	Return Value:
        //
        //		HRESULT indicating success or failure.

        //STDAPI
        //RegisterDeviceWithLocalManagement(
        //	_Out_opt_ BOOL* alreadyRegistered);

        [DllImport("mdmlocalmanagement.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint RegisterDeviceWithLocalManagement([MarshalAs(UnmanagedType.Bool)] out bool alreadyRegistered);

        //	Routine Description:
        //
        //	This function is used to unregister a device with Local MDM Management synchronously.
        //
        //	Return Value:
        //
        //		HRESULT indicating success or failure.

        //STDAPI
        //UnregisterDeviceWithLocalManagement();

        [DllImport("mdmlocalmanagement.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint UnregisterDeviceWithLocalManagement();

        [DllImport("kernel32.dll")]
        private static extern uint LocalFree(IntPtr hMem);


        public static uint Apply(string syncML, out string syncMLResult)
        {
            // rc = 2147549446 - MDM local management requires running in MTA
            // rc = 2147746132 - MDM local management requires a 64-bit process
            // rc = 2147947423 - embedded mode not set
            // rc = 2147943860 - timeout
            // rc = 2147549183 - catastrophic failure

            Debug.WriteLine($"[MDMLocalManagement] Is64BitProcess, rc = {Environment.Is64BitProcess}");
            Debug.WriteLine($"[MDMLocalManagement] GetApartmentState(), rc = {Thread.CurrentThread.GetApartmentState()}");

            IntPtr resultPtr = IntPtr.Zero;

            var rc = ApplyLocalManagementSyncML(syncML, out resultPtr);
            Debug.WriteLine($"[MDMLocalManagement] ApplyLocalManagementSyncML(), GetLastWin32Error() = {Marshal.GetLastWin32Error()}");
            Debug.WriteLine($"[MDMLocalManagement] ApplyLocalManagementSyncML(), rc = {rc}");

            syncMLResult = string.Empty;
            if (resultPtr != IntPtr.Zero)
            {
                syncMLResult = Marshal.PtrToStringUni(resultPtr);
                _ = LocalFree(resultPtr);
            }
            return 0;
        }

        public static string RegisterLocalMDM()
        {
            var keyPathEnrollments = @"SOFTWARE\Microsoft\Enrollments";
            string[] enrollmentIdsBefore = new string[0];
            string[] enrollmentIdsAfter = new string[0];
            string newEnrollmentId;

            try
            {
                using (var registryKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default).OpenSubKey(keyPathEnrollments))
                {
                    enrollmentIdsBefore = registryKey.GetSubKeyNames();
                }
            }
            catch (Exception)
            {
                // ignore
            }

            var rc = RegisterDeviceWithLocalManagement(out bool alreadyRegistered);

            Debug.WriteLine($"[MDMLocalManagement] RegisterDeviceWithLocalManagement(), GetLastWin32Error() = {Marshal.GetLastWin32Error()}");
            Debug.WriteLine($"[MDMLocalManagement] RegisterDeviceWithLocalManagement(), rc = {rc}");
            Debug.WriteLine($"[MDMLocalManagement] RegisterDeviceWithLocalManagement(), alreadyRegistered = {alreadyRegistered}");

            try
            {
                using (var registryKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default).OpenSubKey(keyPathEnrollments))
                {
                    enrollmentIdsAfter = registryKey.GetSubKeyNames();
                }
            }
            catch (Exception)
            {
                // ignore
            }
            try
            {
                newEnrollmentId = enrollmentIdsAfter.Except(enrollmentIdsBefore).ToList().First();
                Debug.WriteLine($"[MDMLocalManagement] RegisterDeviceWithLocalManagement(), found Enrollment ID = {newEnrollmentId}");
            }
            catch (Exception)
            {
                newEnrollmentId = string.Empty;
                Debug.WriteLine($"[MDMLocalManagement] RegisterDeviceWithLocalManagement(), no new Enrollment ID found");
            }

            return newEnrollmentId;
        }

        public static void UnregisterLocalMDM()
        {
            var rc = UnregisterDeviceWithLocalManagement();
            Debug.WriteLine($"[MDMLocalManagement] UnregisterDeviceWithLocalManagement(), GetLastWin32Error() = {Marshal.GetLastWin32Error()}");
            Debug.WriteLine($"[MDMLocalManagement] UnregisterDeviceWithLocalManagement(), rc = {rc}");
        }

        public static string SendRequestProcedure(string dataText, string command = "GET", string data = "", string format = "int", string type = "text/plain", bool keepLocalMDMEnrollment = true, bool redirectLocalMDMEnrollment = false)
        {
            string syncMLResult = string.Empty;
            object originalFlagsValue = 0;

            Helper.GetEnrollmentGuids(out string OmaDmAccountIdMDM, out _);

            try
            {
                GetRegistryKeyEmbeddedModeFlag(out originalFlagsValue);

                if (!SetEmbeddedMode())
                {
                    return string.Empty;
                }

                var newEnrollmentId = RegisterLocalMDM();

                if (redirectLocalMDMEnrollment && !string.IsNullOrEmpty(newEnrollmentId))
                {
                    Debug.WriteLine($"[MDMLocalManagement] Normal MDM Enrollment ID = {OmaDmAccountIdMDM}");
                    Debug.WriteLine($"[MDMLocalManagement] Temporarily changing EnrollmentType of normal MDM enrollment to local MDM EnrollmentType ID");

                    var enrollmentid = newEnrollmentId;
                    var value = 6;
                    var result = Helper.SetRegistryLocalMachineDWordValue($"SOFTWARE\\Microsoft\\Enrollments\\{enrollmentid}", "EnrollmentType", value);
                    Debug.WriteLine($"[MDMLocalManagement] Enrollment ID = {enrollmentid}, set EnrollmentType = {value}, rc = {result}");

                    enrollmentid = OmaDmAccountIdMDM;
                    value = 20;
                    result = Helper.SetRegistryLocalMachineDWordValue($"SOFTWARE\\Microsoft\\Enrollments\\{enrollmentid}", "EnrollmentType", value);
                    Debug.WriteLine($"[MDMLocalManagement] Enrollment ID = {enrollmentid}, set EnrollmentType = {value}, rc = {result}");
                }
                else
                {
                    Debug.WriteLine($"[MDMLocalManagement] RedirectLocalMDMEnrollment not possible, new enrollment not detected!");
                }

                syncMLResult = SendRequest(dataText, command, data, format, type);

                if (redirectLocalMDMEnrollment && !string.IsNullOrEmpty(newEnrollmentId))
                {
                    Debug.WriteLine($"[MDMLocalManagement] Reverting change of EnrollmentType");

                    var enrollmentid = newEnrollmentId;
                    var value = 20;
                    var result = Helper.SetRegistryLocalMachineDWordValue($"SOFTWARE\\Microsoft\\Enrollments\\{enrollmentid}", "EnrollmentType", value);
                    Debug.WriteLine($"[MDMLocalManagement] Enrollment ID = {enrollmentid}, set EnrollmentType = {value}, rc = {result}");

                    enrollmentid = OmaDmAccountIdMDM;
                    value = 6;
                    result = Helper.SetRegistryLocalMachineDWordValue($"SOFTWARE\\Microsoft\\Enrollments\\{enrollmentid}", "EnrollmentType", value);
                    Debug.WriteLine($"[MDMLocalManagement] Enrollment ID = {enrollmentid}, set EnrollmentType = {value}, rc = {result}");
                }

                if (!keepLocalMDMEnrollment)
                {
                    UnregisterLocalMDM();
                }
            }
            finally
            {
                // restore original flags value, so device is untouched in the end
                if (originalFlagsValue != null)
                {
                    if (SetRegistryKeyEmbeddedModeFlag(originalFlagsValue))
                    {
                        if (originalFlagsValue is byte[] valueByteArray)
                        {
                            Debug.WriteLine($"[MDMLocalManagement] Registry EmbeddedMode Flags restored to '{ToHexString(valueByteArray)}'");
                        }
                        else if (originalFlagsValue is int intValue)
                        {
                            Debug.WriteLine($"[MDMLocalManagement] Registry EmbeddedMode Flags restored to '{intValue}'");
                        }
                    }
                    else
                    {
                        Debug.WriteLine("[MDMLocalManagement] Registry EmbeddedMode Flags could not be restored");
                        ClearEmbeddedMode();
                    }
                }
                else
                {
                    ClearEmbeddedMode();
                }
            }

            return syncMLResult;
        }

        //dataText is either OMA URI or SyncML XML
        public static string SendRequest(string dataText, string command = "GET", string data = "", string format = "int", string type = "text/plain")
        {
            StringBuilder syncMLBuilder = new StringBuilder();
            syncMLBuilder.AppendLine("<SyncBody>");
            syncMLBuilder.AppendLine("    <CMD-ITEM>");
            syncMLBuilder.AppendLine("        <CmdID>CMDID-ITEM</CmdID>");
            syncMLBuilder.AppendLine("        <Item>");
            syncMLBuilder.AppendLine("            <Target>");
            syncMLBuilder.AppendLine("                <LocURI>OMAURI-ITEM</LocURI>");
            syncMLBuilder.AppendLine("            </Target>");
            syncMLBuilder.AppendLine("            <Meta>");
            syncMLBuilder.AppendLine("                <Format xmlns=\"syncml:metinf\">FORMAT-ITEM</Format>");
            syncMLBuilder.AppendLine("                <Type xmlns=\"syncml:metinf\">TYPE-ITEM</Type>");
            syncMLBuilder.AppendLine("            </Meta>");
            syncMLBuilder.AppendLine("            <Data>DATA-ITEM</Data>");
            syncMLBuilder.AppendLine("        </Item>");
            syncMLBuilder.AppendLine("    </CMD-ITEM>");
            syncMLBuilder.AppendLine("</SyncBody>");

            string syncMLprepared = syncMLBuilder.ToString();

            if (dataText.ToLower().StartsWith("<syncbody>"))
            {
                syncMLprepared = dataText;
            }
            else // dataText is OMA URI
            {
                syncMLprepared = syncMLprepared.Replace("CMD-ITEM", command.ToString());
                syncMLprepared = syncMLprepared.Replace("CMDID-ITEM", CmdIdCounter++.ToString());
                syncMLprepared = syncMLprepared.Replace("OMAURI-ITEM", dataText.ToString());
                syncMLprepared = syncMLprepared.Replace("FORMAT-ITEM", format);
                syncMLprepared = syncMLprepared.Replace("TYPE-ITEM", type);
                syncMLprepared = syncMLprepared.Replace("DATA-ITEM", data);
            }

            Debug.WriteLine("[MDMLocalManagement] Raw XML SyncML request:");
            Debug.WriteLine(TryPrettyXml(syncMLprepared));

            _ = Apply(syncMLprepared, out string syncMLResult);

            Debug.WriteLine("[MDMLocalManagement] Raw XML SyncML result:");
            Debug.WriteLine(TryPrettyXml(syncMLResult));

            return syncMLResult;
        }

        public static bool SetEmbeddedMode()
        {
            object originalFlagsValue;

            string uuidString = GetSmBiosGuid();
            Debug.WriteLine($"[MDMLocalManagement] GetSmBiosGuid() = {uuidString}");

            // write hashed UUID value to registry embeddedmode\Parameters\flags value otherwise mdmlocalmanagement.dll functions are not working
            // verified with IDA, embeddedmodesvcapi.dll!GetFlags() function
            byte[] hash = GetSHA265(uuidString);
            Debug.WriteLine($"[MDMLocalManagement] GetSHA265(uuidString) = {PrettifyArray(hash)}");

            Debug.WriteLine(@"[MDMLocalManagement] Accessing Registry 'HKLM\SYSTEM\CurrentControlSet\Services\EmbeddedMode\Parameters\Flags'");
            if (GetRegistryKeyEmbeddedModeFlag(out originalFlagsValue))
            {
                if (originalFlagsValue != null)
                {
                    if (originalFlagsValue is byte[] valueByteArray)
                    {
                        Debug.WriteLine($"[MDMLocalManagement] Registry Original EmbeddedMode Flags read '{ToHexString(valueByteArray)}'");
                        Debug.WriteLine($"[MDMLocalManagement] Registry Original EmbeddedMode Flags could be a leftover, crashed instance for example...");
                    }
                    else if (originalFlagsValue is int intValue)
                    {
                        Debug.WriteLine($"[MDMLocalManagement] Registry Original EmbeddedMode Flags read '{intValue}'");
                    }
                }
                else
                {
                    Debug.WriteLine("[MDMLocalManagement] Registry Original EmbeddedMode Flags could not read");
                    return false;
                }

                if (SetRegistryKeyEmbeddedModeFlag(hash))
                {
                    Debug.WriteLine($"[MDMLocalManagement] Registry EmbeddedMode Flags set successful to '{ToHexString(hash)}'");
                }
                else
                {
                    Debug.WriteLine("[MDMLocalManagement] Registry EmbeddedMode Flags could not be set");
                }
            }
            else
            {
                Debug.WriteLine("[MDMLocalManagement] Registry Original EmbeddedMode Flags could not read");
                return false;
            }

            return true;
        }

        public static bool ClearEmbeddedMode()
        {
            Debug.WriteLine("[MDMLocalManagement] Registry EmbeddedMode Flags set to '0'");
            return SetRegistryKeyEmbeddedModeFlag(0);
        }

        private static bool SetRegistryKeyEmbeddedModeFlag(object originalFlagsValue)
        {
            try
            {
                RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\embeddedmode\Parameters", true);
                if (key != null)
                {
                    key.SetValue("Flags", originalFlagsValue);
                    key.Close();
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MDMLocalManagement] SetRegistryKeyEmbeddedModeFlag() was unable to set flag, {ex}");
                return false;
            }
        }

        private static bool GetRegistryKeyEmbeddedModeFlag(out object originalFlagsValue)
        {
            try
            {
                RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\embeddedmode\Parameters", false);
                if (key != null)
                {
                    originalFlagsValue = key.GetValue("Flags");
                    key.Close();
                }
                else
                {
                    originalFlagsValue = 0;
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MDMLocalManagement] GetRegistryKeyEmbeddedModeFlag() was unable to get flag, {ex}");
                originalFlagsValue = 0;
                return false;
            }
        }

        private static byte[] GetSHA265(string text)
        {
            byte[] hash;

            try
            {
                var uuid = Guid.Parse(text);
                byte[] uuidBytes = uuid.ToByteArray();

                using (SHA256 hasher = SHA256.Create())
                {
                    hash = hasher.ComputeHash(uuidBytes);
                }

                return hash;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MDMLocalManagement] GetSHA265() was unable to get hash, {ex}");
                return new byte[0];
            }
        }

        private static string GetSmBiosGuid()
        {
            string uuidString = string.Empty;

            try
            {
                ObjectQuery query = new ObjectQuery("SELECT UUID FROM Win32_ComputerSystemProduct");

                ManagementObjectSearcher searcher = new ManagementObjectSearcher(query);
                ManagementObjectCollection queryCollection = searcher.Get();

                foreach (ManagementObject mo in queryCollection)
                {
                    if (mo["UUID"] != null)
                    {
                        uuidString = mo["UUID"].ToString();
                        mo.Dispose();

                        if (uuidString == null) 
                        { 
                            uuidString = string.Empty; 
                        }
                    }
                }

                return uuidString;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MDMLocalManagement] GetSmBiosGuid() was unable to get UUID, {ex}");
                return string.Empty;
            }
        }

        public static string TryPrettyXml(string syncMLResult)
        {
            try
            {
                XDocument xmlDoc = XDocument.Parse(syncMLResult);
                string prettyXml = xmlDoc.ToString();
                return prettyXml;
            }
            catch
            {
                return syncMLResult;
            }
        }

        public static string PrettifyArray(byte[] bytes)
        {
            // based on https://stackoverflow.com/questions/10940883/converting-byte-array-to-string-and-printing-out-to-console

            var sb = new StringBuilder("new byte[] { ");
            foreach (var b in bytes)
            {
                sb.Append(b + ", ");
            }
            sb.Remove(sb.Length - 2, 2);
            sb.Append("}");

            return sb.ToString();
        }

        public static string ToHexString(byte[] data)
        {
            // based on https://stackoverflow.com/questions/311165/how-do-you-convert-a-byte-array-to-a-hexadecimal-string-and-vice-versa

            byte b;
            int i, j, k;
            int l = data.Length;
            char[] r = new char[l * 2];
            for (i = 0, j = 0; i < l; ++i)
            {
                b = data[i];
                k = b >> 4;
                r[j++] = (char)(k > 9 ? k + 0x37 : k + 0x30);
                k = b & 15;
                r[j++] = (char)(k > 9 ? k + 0x37 : k + 0x30);
            }
            var tmp = new string(r);
            tmp = Regex.Replace(tmp, @"(.{2})", "$1 ");

            return tmp.Substring(0, tmp.Length - 1); ;
        }
    }
}
