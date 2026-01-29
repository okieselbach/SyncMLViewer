using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SyncMLViewer
{
    /// <summary>
    /// Provides CSP (Configuration Service Provider) documentation lookup functionality.
    /// Maps CSP names to their Microsoft Learn documentation URLs.
    /// </summary>
    public class CspDocumentationModel
    {
        private readonly Dictionary<string, string> _cspDocumentationMap;
        private const string BaseUrl = "https://learn.microsoft.com/en-us/windows/client-management/mdm/";

        public CspDocumentationModel()
        {
            _cspDocumentationMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // Core CSPs
                { "Policy", "policy-configuration-service-provider" },
                { "DMClient", "dmclient-csp" },
                { "DeviceManageability", "devicemanageability-csp" },
                { "DevDetail", "devdetail-csp" },
                { "DevInfo", "devinfo-csp" },
                { "NodeCache", "nodecache-csp" },
                
                // Security & Compliance
                { "BitLocker", "bitlocker-csp" },
                { "LAPS", "laps-csp" },
                { "PassportForWork", "passportforwork-csp" },
                { "DeviceLock", "devicelock-csp" },
                { "HealthAttestation", "healthattestation-csp" },
                { "Defender", "defender-csp" },
                { "WindowsDefenderApplicationGuard", "windowsdefenderapplicationguard-csp" },
                { "ApplicationControl", "applicationcontrol-csp" },
                { "Firewall", "firewall-csp" },
                { "PersonalDataEncryption", "personaldataencryption-csp" },
                
                // Certificates & Authentication
                { "CertificateStore", "certificatestore-csp" },
                { "ClientCertificateInstall", "clientcertificateinstall-csp" },
                { "RootCACertificates", "rootcacertificates-csp" },
                { "RootCATrustedCertificates", "rootcacertificates-csp" },
                
                // Network
                { "WiFi", "wifi-csp" },
                { "VPN", "vpn-csp" },
                { "VPNv2", "vpnv2-csp" },
                { "WiredNetwork", "wirednetwork-csp" },
                { "Proxy", "networkproxy-csp" },
                { "NetworkProxy", "networkproxy-csp" },
                { "CMPolicy", "cmpolicy-csp" },
                { "CMPolicyEnterprise", "cmpolicyenterprise-csp" },
                
                // Apps & Enterprise
                { "EnterpriseModernAppManagement", "enterprisemodernappmanagement-csp" },
                { "EnterpriseDesktopAppManagement", "enterprisedesktopappmanagement-csp" },
                { "AppLocker", "applocker-csp" },
                { "AssignedAccess", "assignedaccess-csp" },
                { "EnterpriseAppVManagement", "enterpriseappvmanagement-csp" },
                { "Office", "office-csp" },
                
                // Device Management
                { "Update", "update-csp" },
                { "WindowsLicensing", "windowslicensing-csp" },
                { "WindowsAdvancedThreatProtection", "windowsadvancedthreatprotection-csp" },
                { "Reboot", "reboot-csp" },
                { "RemoteWipe", "remotewipe-csp" },
                { "CleanPC", "cleanpc-csp" },
                { "DeviceStatus", "devicestatus-csp" },
                { "DeveloperSetup", "developersetup-csp" },
                
                // Provisioning & Enrollment
                { "Provisioning", "provisioning-csp" },
                { "Enrollment", "enrollment-csp" },
                { "w7", "w7-application-csp" },
                { "DMAcc", "dmacc-csp" },
                { "DMSessionActions", "dmsessionactions-csp" },
                
                // User & Experience
                { "PersonalizationCSP", "personalization-csp" },
                { "Personalization", "personalization-csp" },
                { "EnterpriseDataProtection", "enterprisedataprotection-csp" },
                { "DiagnosticLog", "diagnosticlog-csp" },
                { "Storage", "storage-csp" },
                { "eUICCs", "euiccs-csp" },
                { "SurfaceHub", "surfacehub-csp" },
                { "Language", "language-pack-management-csp" },
                { "LanguagePackManagement", "language-pack-management-csp" },
                
                // Email & Accounts
                { "Email2", "email2-csp" },
                { "Accounts", "accounts-csp" },
                
                // Enterprise specific
                { "EnterpriseAPN", "enterpriseapn-csp" },
                { "EnterpriseExtFileSystem", "enterpriseextfilesystem-csp" },
                { "Maps", "maps-csp" },
                { "Messaging", "messaging-csp" },
                { "MultiSIM", "multisim-csp" },
                
                // Tenant & Cloud
                { "TenantLockdown", "tenantlockdown-csp" },
                { "CloudDesktop", "clouddesktop-csp" },
                { "DeclaredConfiguration", "declaredconfiguration-csp" },
                
                // Print
                { "Printer", "universalprint-csp" },
                { "UniversalPrint", "universalprint-csp" },
                
                // Remote
                { "RemoteDesktop", "remotedesktop-csp" },
                { "RemoteFind", "remotefind-csp" },
                { "RemoteRing", "remotering-csp" },
                
                // ADMX-backed policies
                { "ADMX", "understanding-admx-backed-policies" },
                
                // Autopilot
                { "Autopilot", "autopilot-csp" },
                
                // SecureAssessment
                { "SecureAssessment", "secureassessment-csp" },
                
                // Win32 compatible
                { "Win32CompatibilityAppraiser", "win32compatibilityappraiser-csp" },
                { "Win32AppInventory", "win32appinventory-csp" },
                
                // TPM
                { "TPMPolicy", "tpmpolicy-csp" },
            };
        }

        /// <summary>
        /// Extracts the CSP name from an OMA-URI.
        /// </summary>
        /// <param name="omaUri">The OMA-URI to parse (e.g., "./Device/Vendor/MSFT/Policy/Config/...")</param>
        /// <returns>The CSP name or null if not found</returns>
        public string ExtractCspName(string omaUri)
        {
            if (string.IsNullOrWhiteSpace(omaUri))
                return null;

            // Decode URL-encoded characters
            omaUri = Uri.UnescapeDataString(omaUri);

            // Pattern 1: ./Device/Vendor/MSFT/{CSP}/... or ./User/Vendor/MSFT/{CSP}/...
            var msftPattern = new Regex(@"\./(?:Device|User)/Vendor/MSFT/([^/]+)", RegexOptions.IgnoreCase);
            var msftMatch = msftPattern.Match(omaUri);
            if (msftMatch.Success)
            {
                return msftMatch.Groups[1].Value;
            }

            // Pattern 2: ./Vendor/MSFT/{CSP}/...
            var vendorPattern = new Regex(@"\./Vendor/MSFT/([^/]+)", RegexOptions.IgnoreCase);
            var vendorMatch = vendorPattern.Match(omaUri);
            if (vendorMatch.Success)
            {
                return vendorMatch.Groups[1].Value;
            }

            // Pattern 3: ./DevDetail/..., ./DevInfo/..., etc. (root CSPs)
            var rootPattern = new Regex(@"^\./([^/]+)", RegexOptions.IgnoreCase);
            var rootMatch = rootPattern.Match(omaUri);
            if (rootMatch.Success)
            {
                var rootCsp = rootMatch.Groups[1].Value;
                // Only return if it's a known root CSP
                if (_cspDocumentationMap.ContainsKey(rootCsp))
                {
                    return rootCsp;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the documentation URL for a CSP name.
        /// </summary>
        /// <param name="cspName">The CSP name</param>
        /// <returns>The full documentation URL or the CSP list URL if not found</returns>
        public string GetDocumentationUrl(string cspName)
        {
            if (string.IsNullOrWhiteSpace(cspName))
                return GetCspListUrl();

            if (_cspDocumentationMap.TryGetValue(cspName, out string docPath))
            {
                return BaseUrl + docPath;
            }

            // Try lowercase version
            var lowerCspName = cspName.ToLower();
            return BaseUrl + lowerCspName + "-csp";
        }

        /// <summary>
        /// Gets the documentation URL for an OMA-URI.
        /// </summary>
        /// <param name="omaUri">The OMA-URI</param>
        /// <returns>The documentation URL for the CSP, or the CSP list if not determinable</returns>
        public string GetDocumentationUrlFromOmaUri(string omaUri)
        {
            var cspName = ExtractCspName(omaUri);
            return GetDocumentationUrl(cspName);
        }

        /// <summary>
        /// Gets the CSP list URL.
        /// </summary>
        /// <returns>URL to the CSP reference documentation</returns>
        public string GetCspListUrl()
        {
            return "https://aka.ms/CSPList";
        }

        /// <summary>
        /// Checks if a CSP name is known/documented.
        /// </summary>
        /// <param name="cspName">The CSP name to check</param>
        /// <returns>True if the CSP is known</returns>
        public bool IsCspKnown(string cspName)
        {
            return !string.IsNullOrWhiteSpace(cspName) && _cspDocumentationMap.ContainsKey(cspName);
        }

        /// <summary>
        /// Gets all known CSP names.
        /// </summary>
        /// <returns>Collection of known CSP names</returns>
        public IEnumerable<string> GetAllCspNames()
        {
            return _cspDocumentationMap.Keys;
        }

        /// <summary>
        /// Extracts a LocURI from XML content if present.
        /// </summary>
        /// <param name="xmlContent">XML content that may contain a LocURI element</param>
        /// <returns>The LocURI value or null</returns>
        public string ExtractLocUriFromXml(string xmlContent)
        {
            if (string.IsNullOrWhiteSpace(xmlContent))
                return null;

            // Try to extract LocURI from XML
            var locUriPattern = new Regex(@"<LocURI>([^<]+)</LocURI>", RegexOptions.IgnoreCase);
            var match = locUriPattern.Match(xmlContent);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            // Try to extract Target/LocURI
            var targetPattern = new Regex(@"<Target>\s*<LocURI>([^<]+)</LocURI>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var targetMatch = targetPattern.Match(xmlContent);
            if (targetMatch.Success)
            {
                return targetMatch.Groups[1].Value;
            }

            return null;
        }
    }
}
