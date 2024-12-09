using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SyncMLViewer
{
    public static class AutopilotHashUtility
    {
        // This enum is referenced in the original code. Adjust as needed.
        public enum PowerPlatformRole
        {
            Unspecified = 0,
            Desktop,
            Mobile,
            Workstation,
            EnterpriseServer,
            SOHOServer,
            AppliancePC,
            PerformanceServer,
            Slate
        }

        [DllImport("crypt32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CryptStringToBinary(
            [MarshalAs(UnmanagedType.LPWStr)] string pszString,
            uint cchString,
            uint dwFlags,
            [Out] byte[] pbBinary,
            ref uint pcbBinary,
            uint pdwSkip,
            ref uint pdwFlags);

        public static IEnumerable<Dictionary<string, object>> ConvertFromAutopilotHash(string CSVFile = null, string Hash = null)
        {
            // Decide which parameter set is used
            List<string> hashes = new List<string>();
            if (!string.IsNullOrEmpty(CSVFile))
            {
                // Emulate Import-CSV and extract 'Hardware Hash' column
                var lines = File.ReadAllLines(CSVFile);
                // Assume first line is header
                var headers = lines[0].Split(',');
                int hashIndex = Array.IndexOf(headers, "Hardware Hash");
                if (hashIndex < 0)
                    throw new Exception("CSV does not contain 'Hardware Hash' column.");

                for (int i = 1; i < lines.Length; i++)
                {
                    var cols = lines[i].Split(',');
                    if (cols.Length > hashIndex)
                        hashes.Add(cols[hashIndex]);
                }
            }
            else if (!string.IsNullOrEmpty(Hash))
            {
                hashes.Add(Hash);
            }
            else
            {
                throw new ArgumentException("Either CSVFile or Hash must be provided.");
            }

            foreach (var h in hashes)
            {
                byte[] binary = Convert.FromBase64String(h);

                // Validate the header
                if (binary[0] != 79 || binary[1] != 65)
                {
                    throw new Exception("Invalid hash");
                }

                // TODO: Validate checksum if needed

                ushort totalLength = BitConverter.ToUInt16(binary, 2);
                Dictionary<string, object> data = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                int currentOffset = 4;
                int diskSerialCount = 0;
                int diskInfoCount = 0;
                int displayResolutionCount = 0;
                int gpuCount = 0;
                int gpuNameCount = 0;
                int networkCount = 0;

                while (currentOffset < totalLength)
                {
                    byte type = binary[currentOffset];
                    ushort length = BitConverter.ToUInt16(binary, currentOffset + 2);

                    switch (type)
                    {
                        case 1:
                            {
                                int offset = currentOffset + 4;
                                Version[] versions = new Version[2];
                                for (int i = 0; i <= 1; i++)
                                {
                                    ushort minor = BitConverter.ToUInt16(binary, offset);
                                    ushort major = BitConverter.ToUInt16(binary, offset + 2);
                                    ushort rev = BitConverter.ToUInt16(binary, offset + 4);
                                    ushort build = BitConverter.ToUInt16(binary, offset + 6);
                                    offset += 8;
                                    versions[i] = new Version(major, minor, build, rev);
                                }
                                data["ToolBuild"] = versions[0];
                                data["OSBuild"] = versions[1];

                                // OSType
                                byte osTypeVal = binary[currentOffset + length - 3];
                                data["OSType"] = osTypeVal == 2 ? "FullOS" : (object)osTypeVal;

                                // OSCpuArchitecture
                                byte archVal = binary[currentOffset + length - 2];
                                string archName;
                                switch (archVal)
                                {
                                    case 0:
                                        archName = "X86";
                                        break;
                                    case 9:
                                        archName = "X64";
                                        break;
                                    case 12:
                                        archName = "ARM64";
                                        break;
                                    default:
                                        archName = archVal.ToString();
                                        break;
                                }
                                data["OSCpuArchitecture"] = archName;

                                data["ToolVersion"] = binary[currentOffset + length - 1];
                            }
                            break;
                        case 2:
                            {
                                byte archVal = binary[currentOffset + 4];
                                string archName;
                                switch (archVal)
                                {
                                    case 0:
                                        archName = "X86";
                                        break;
                                    case 9:
                                        archName = "X64";
                                        break;
                                    case 12:
                                        archName = "ARM64";
                                        break;
                                    default:
                                        archName = archVal.ToString();
                                        break;
                                }
                                data["ProcessorArchitecture"] = archName;
                                data["ProcessorPackages"] = BitConverter.ToUInt16(binary, currentOffset + 6);
                                data["ProcessorCores"] = BitConverter.ToUInt16(binary, currentOffset + 8);
                                data["ProcessorThreads"] = BitConverter.ToUInt16(binary, currentOffset + 10);
                                data["ProcessorHyperthreading"] = (binary[currentOffset + 12] == 1);
                            }
                            break;
                        case 3:
                            data["ProcessorManufacturer"] = Encoding.UTF8.GetString(binary, currentOffset + 4, length - 4);
                            break;
                        case 4:
                            data["ProcessorModel"] = Encoding.UTF8.GetString(binary, currentOffset + 4, length - 4);
                            break;
                        case 5:
                            data["TotalPhysicalRam"] = BitConverter.ToUInt16(binary, currentOffset + 4);
                            data["SmbiosRamMaximumCapacity"] = BitConverter.ToUInt16(binary, currentOffset + 12);
                            data["SmbiosRamSlots"] = BitConverter.ToUInt16(binary, currentOffset + 20);
                            data["SmbiosRamArrayCount"] = BitConverter.ToUInt16(binary, currentOffset + 22);
                            byte errCorr = binary[currentOffset + 24];
                            string errCorrVal;
                            switch (errCorr)
                            {
                                case 3: errCorrVal = "None"; break;
                                case 4: errCorrVal = "Parity"; break;
                                case 5: errCorrVal = "Single-bit ECC"; break;
                                case 6: errCorrVal = "Multi-bit ECC"; break;
                                case 7: errCorrVal = "CRC"; break;
                                default: errCorrVal = errCorr.ToString(); break;
                            }
                            data["SmbiosRamErrorCorrection"] = errCorrVal;
                            break;
                        case 6:
                            {
                                diskInfoCount++;
                                uint diskCap = BitConverter.ToUInt32(binary, currentOffset + 4);
                                data[$"Disk{diskInfoCount}.DiskCapacity"] = diskCap;
                                byte busVal = binary[currentOffset + 12];
                                string busType;
                                switch (busVal)
                                {
                                    case 1:
                                        busType = "SCSI";
                                        break;
                                    case 2:
                                        busType = "ATAPI";
                                        break;
                                    case 3:
                                        busType = "ATA";
                                        break;
                                    case 8:
                                        busType = "RAID";
                                        break;
                                    case 10:
                                        busType = "SAS";
                                        break;
                                    case 11:
                                        busType = "SATA";
                                        break;
                                    case 17:
                                        busType = "NVMe";
                                        break;
                                    default:
                                        busType = busVal.ToString();
                                        break;
                                }
                                data[$"Disk{diskInfoCount}.StorageBusType"] = busType;

                                if (busType == "NVMe")
                                {
                                    data[$"Disk{diskInfoCount}.DiskType"] = "NVMe";
                                }
                                else
                                {
                                    byte diskTypeVal = binary[currentOffset + 14];
                                    if (diskTypeVal == 0) data[$"Disk{diskInfoCount}.DiskType"] = "SSD";
                                    else if (diskTypeVal == 255) data[$"Disk{diskInfoCount}.DiskType"] = "HDD";
                                }
                            }
                            break;
                        case 7:
                            diskSerialCount++;
                            data[$"Disk{diskSerialCount}.DiskSerial"] = Encoding.UTF8.GetString(binary, currentOffset + 4, length - 4);
                            break;
                        case 8:
                            {
                                networkCount++;
                                byte mediumVal = binary[currentOffset + 4];
                                string medium;
                                switch (mediumVal)
                                {
                                    case 1:
                                        medium = "Wireless Lan";
                                        break;
                                    case 9:
                                        medium = "Native 802.11";
                                        break;
                                    case 10:
                                        medium = "Bluetooth";
                                        break;
                                    case 14:
                                        medium = "Ethernet";
                                        break;
                                    default:
                                        medium = mediumVal.ToString();
                                        break;
                                }
                                data[$"Network{networkCount}.PhysicalMedium"] = medium;
                                byte[] mac = new byte[6];
                                Array.Copy(binary, currentOffset + 8, mac, 0, 6);
                                data[$"Network{networkCount}.MacAddress"] = BitConverter.ToString(mac);
                            }
                            break;
                        case 9:
                            {
                                displayResolutionCount++;
                                data[$"Display{displayResolutionCount}.SizePhysicalH"] = BitConverter.ToUInt16(binary, currentOffset + 4);
                                data[$"Display{displayResolutionCount}.SizePhysicalV"] = BitConverter.ToUInt16(binary, currentOffset + 6);
                                data[$"Display{displayResolutionCount}.ResolutionHorizontal"] = BitConverter.ToUInt16(binary, currentOffset + 8);
                                data[$"Display{displayResolutionCount}.ResolutionVertical"] = BitConverter.ToUInt16(binary, currentOffset + 10);
                            }
                            break;
                        case 10:
                            {
                                data["ChassisType"] = binary[currentOffset + 4];
                                // Attempting to interpret the next byte as PowerPlatformRole
                                byte roleVal = binary[currentOffset + 5];
                                data["PowerPlatformRole"] = (PowerPlatformRole)roleVal;
                            }
                            break;
                        case 11:
                            {
                                byte[] offlineBytes = new byte[length - 4 - 14 + 1];
                                Array.Copy(binary, currentOffset + 14, offlineBytes, 0, length - 4 - 14);
                                data["OfflineDeviceId"] = Convert.ToBase64String(offlineBytes);

                                byte idTypeVal = binary[currentOffset + 8];
                                string idType;
                                switch (idTypeVal)
                                {
                                    case 1:
                                        idType = "TPM_EK";
                                        break;
                                    case 2:
                                        idType = "UEFI_VARIABLE_TPM";
                                        break;
                                    default:
                                        idType = idTypeVal.ToString();
                                        break;
                                }
                                data["OfficeDeviceIdType"] = idType;
                            }
                            break;
                        case 12:
                            {
                                byte[] guidBytes = new byte[length - 4];
                                Array.Copy(binary, currentOffset + 4, guidBytes, 0, guidBytes.Length);
                                Guid g = new Guid(guidBytes);
                                data["SmbiosUuid"] = g;
                            }
                            break;
                        case 13:
                            data["TpmVersion"] = Encoding.UTF8.GetString(binary, currentOffset + 4, length - 4);
                            break;
                        case 14:
                            data["SmbiosSerial"] = Encoding.UTF8.GetString(binary, currentOffset + 4, length - 4);
                            break;
                        case 15:
                            data["SmbiosFirmwareVendor"] = Encoding.UTF8.GetString(binary, currentOffset + 4, length - 4);
                            break;
                        case 16:
                            data["SmbiosSystemManufacturer"] = Encoding.UTF8.GetString(binary, currentOffset + 4, length - 4);
                            break;
                        case 17:
                            data["SmbiosProductName"] = Encoding.UTF8.GetString(binary, currentOffset + 4, length - 4);
                            break;
                        case 18:
                            data["SmbiosSKUNumber"] = Encoding.UTF8.GetString(binary, currentOffset + 4, length - 4);
                            break;
                        case 19:
                            data["SmbiosSystemFamily"] = Encoding.UTF8.GetString(binary, currentOffset + 4, length - 4);
                            break;
                        case 20:
                            data["SmbiosFirmwareVendor2"] = Encoding.UTF8.GetString(binary, currentOffset + 4, length - 4);
                            break;
                        case 21:
                            data["SmbiosBoardProduct"] = Encoding.UTF8.GetString(binary, currentOffset + 4, length - 4);
                            break;
                        case 22:
                            data["SmbiosBoardVersion"] = Encoding.UTF8.GetString(binary, currentOffset + 4, length - 4);
                            break;
                        case 23:
                            data["SmbiosSystemVersion"] = Encoding.UTF8.GetString(binary, currentOffset + 4, length - 4);
                            break;
                        case 24:
                            data["ProductKeyID"] = Encoding.UTF8.GetString(binary, currentOffset + 4, length - 4);
                            break;
                        case 25:
                            {
                                byte[] valBytes = new byte[length - 4];
                                Array.Copy(binary, currentOffset + 4, valBytes, 0, valBytes.Length);
                                data["TpmEkPub"] = Convert.ToBase64String(valBytes);
                            }
                            break;
                        case 26:
                            data["ProductKeyPkPn"] = Encoding.UTF8.GetString(binary, currentOffset + 4, length - 4);
                            break;
                        case 28:
                            data["DiskSSNKernel"] = Encoding.UTF8.GetString(binary, currentOffset + 4, length - 4);
                            break;
                        case 29:
                            {
                                gpuCount++;
                                data[$"Gpu{gpuCount}.DedicatedVideoMemory"] = BitConverter.ToUInt32(binary, currentOffset + 4);
                                data[$"Gpu{gpuCount}.DedicatedSystemMemory"] = BitConverter.ToUInt32(binary, currentOffset + 8);
                            }
                            break;
                        case 30:
                            {
                                gpuNameCount++;
                                data[$"Gpu{gpuNameCount}.VideoAdapter"] = Encoding.Unicode.GetString(binary, currentOffset + 4, length - 4);
                            }
                            break;
                        default:
                            {
                                // Unknown type: In original code: Write-Host
                                // Here we just ignore or log it
                                // Could store hex in data if needed
                            }
                            break;
                    }

                    currentOffset += length;
                }

                yield return data;
            }
        }

        // Equivalent helper methods for New-AutopilotHash
        private static int AddString(byte[] binary, int offset, byte type, string value)
        {
            binary[offset] = type;
            ushort totalLen = (ushort)(value.Length + 5);
            byte[] lengthBytes = BitConverter.GetBytes(totalLen);
            binary[offset + 2] = lengthBytes[0];
            binary[offset + 3] = lengthBytes[1];
            byte[] stringBytes = Encoding.UTF8.GetBytes(value);
            Buffer.BlockCopy(stringBytes, 0, binary, offset + 4, stringBytes.Length);
            return offset + value.Length + 5;
        }

        private static int AddBinary(byte[] binary, int offset, byte type, byte[] value)
        {
            binary[offset] = type;
            ushort totalLen = (ushort)(value.Length + 4);
            byte[] lengthBytes = BitConverter.GetBytes(totalLen);
            binary[offset + 2] = lengthBytes[0];
            binary[offset + 3] = lengthBytes[1];
            Buffer.BlockCopy(value, 0, binary, offset + 4, value.Length);
            return offset + value.Length + 4;
        }

        private static int AddVersion(byte[] binary, int offset)
        {
            // type 1 and length 28 fixed
            binary[offset] = 1;
            binary[offset + 2] = 28;
            offset += 4;

            ushort major = 10;
            ushort minor = 0;
            ushort build = 22000;
            ushort rev = 800;

            for (int i = 0; i <= 1; i++)
            {
                // minor, major, rev, build in that order
                Buffer.BlockCopy(BitConverter.GetBytes(minor), 0, binary, offset, 2);
                Buffer.BlockCopy(BitConverter.GetBytes(major), 0, binary, offset + 2, 2);
                Buffer.BlockCopy(BitConverter.GetBytes(rev), 0, binary, offset + 4, 2);
                Buffer.BlockCopy(BitConverter.GetBytes(build), 0, binary, offset + 6, 2);
                offset += 8;
            }

            // skip 5 unknown bytes
            offset += 5;

            // architecture hard-coded to x64
            binary[offset] = 2;
            binary[offset + 1] = 9;
            binary[offset + 2] = 8;

            return offset + 3;
        }

        private static int AddProcessor(byte[] binary, int offset)
        {
            // type 2 and length 16 fixed
            binary[offset] = 2;
            binary[offset + 2] = 16;
            offset += 4;

            ushort processorArchitecture = 9;
            ushort processorPackages = 1;
            ushort processorCores = 1;
            ushort processorThreads = 2;
            ushort processorHyperthreading = 1;

            Buffer.BlockCopy(BitConverter.GetBytes(processorArchitecture), 0, binary, offset, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(processorPackages), 0, binary, offset + 2, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(processorCores), 0, binary, offset + 4, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(processorThreads), 0, binary, offset + 6, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(processorHyperthreading), 0, binary, offset + 8, 2);

            return offset + 12;
        }

        public static Dictionary<string, object> NewAutopilotHash(
            string Manufacturer,
            string Model,
            string Serial,
            string UUID = "",
            string SKUNumber = "",
            string SystemFamily = "",
            string TPMEkPub = "",
            string TPMVersion = "",
            string[] MacAddress = null,
            uint PhysicalMedium = 14,
            string OutputFile = ""
        )
        {
            if (MacAddress == null) MacAddress = new string[0];

            byte[] binary = new byte[3000];
            binary[0] = 79;
            binary[1] = 65;
            int currentOffset = 4;

            // Add version (type 1)
            currentOffset = AddVersion(binary, currentOffset);
            // Add CPU info (type 2)
            currentOffset = AddProcessor(binary, currentOffset);
            // Add manufacturer (type 16)
            currentOffset = AddString(binary, currentOffset, 16, Manufacturer);
            // Add model (type 17)
            currentOffset = AddString(binary, currentOffset, 17, Model);
            // Add serial (type 14)
            currentOffset = AddString(binary, currentOffset, 14, Serial);

            // UUID if specified (type 12)
            if (!string.IsNullOrEmpty(UUID))
            {
                byte[] guidBytes = new Guid(UUID).ToByteArray();
                currentOffset = AddBinary(binary, currentOffset, 12, guidBytes);
            }

            // TPMVersion if specified (type 13)
            if (!string.IsNullOrEmpty(TPMVersion))
            {
                currentOffset = AddString(binary, currentOffset, 13, TPMVersion);
            }

            // SKUNumber if specified (type 18)
            if (!string.IsNullOrEmpty(SKUNumber))
            {
                currentOffset = AddString(binary, currentOffset, 18, SKUNumber);
            }

            // SystemFamily if specified (type 19)
            if (!string.IsNullOrEmpty(SystemFamily))
            {
                currentOffset = AddString(binary, currentOffset, 19, SystemFamily);
            }

            // TPMEkPub if specified (type 25)
            if (!string.IsNullOrEmpty(TPMEkPub))
            {
                currentOffset = AddString(binary, currentOffset, 25, TPMEkPub);
            }

            // Add MAC addresses (type 8)
            if (MacAddress.Length > 0)
            {
                uint hexAny = 8;
                //uint pcbBinary = 0;
                uint pdwFlags = 0;
                foreach (var mac in MacAddress)
                {
                    string macStripped = mac.Replace(":", "").Replace("-", "").Replace(" ", "");
                    byte[] macBytes = new byte[6];
                    uint macLen = 0;
                    bool binaryResult = CryptStringToBinary(macStripped, (uint)macStripped.Length, hexAny, macBytes, ref macLen, 0, ref pdwFlags);
                    if (!binaryResult)
                    {
                        throw new Exception("Failed to convert MAC string to binary.");
                    }

                    byte[] fullBytes = new byte[10];
                    fullBytes[0] = (byte)PhysicalMedium;
                    // rest are zero except we place MAC at offset +4
                    Buffer.BlockCopy(macBytes, 0, fullBytes, 4, 6);
                    currentOffset = AddBinary(binary, currentOffset, 8, fullBytes);
                }
            }

            // Finalize
            byte[] lengthBytes = BitConverter.GetBytes((ushort)currentOffset);
            binary[2] = lengthBytes[0];
            binary[3] = lengthBytes[1];
            binary[currentOffset] = 67;
            binary[currentOffset + 1] = 83;
            binary[currentOffset + 2] = 36;
            binary[currentOffset + 3] = 0;
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(binary, 0, currentOffset);
                Buffer.BlockCopy(hash, 0, binary, currentOffset + 4, hash.Length);
            }

            string base64Hash = Convert.ToBase64String(binary);

            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            { "Device Serial Number", Serial },
            { "Windows Product ID", "" },
            { "Hardware Hash", base64Hash },
            { "Manufacturer name", Manufacturer },
            { "Device model", Model }
        };

            if (!string.IsNullOrEmpty(OutputFile))
            {
                // Write CSV: Device Serial Number, Windows Product ID, Hardware Hash
                var lines = new List<string>
                {
                    "Device Serial Number,Windows Product ID,Hardware Hash",
                    $"{Serial},,{base64Hash}"
                };
                File.WriteAllLines(OutputFile, lines);
            }

            return result;
        }
    }
}

// Example: ConvertFrom-AutopilotHash equivalent
//foreach (var item in AutopilotHashUtility.ConvertFromAutopilotHash(Hash: "T0EFBQEAHAA..."))
//{
//	foreach (var kvp in item)
//	{
//		Console.WriteLine($"{kvp.Key}: {kvp.Value}");
//	}
//}

//      Example: New-AutopilotHash equivalent
//		var newHash = AutopilotHashUtility.NewAutopilotHash(
//			Manufacturer: "Contoso",
//			Model: "ModelX",
//			Serial: "ABC123",
//			UUID: "00000000-0000-0000-0000-000000000000",
//			SystemFamily: "TestFamily"
//		);
//
//		Console.WriteLine("Generated Autopilot Hash:");
//		foreach (var kvp in newHash)
//		{
//			Console.WriteLine($"{kvp.Key}: {kvp.Value}");
//		}
