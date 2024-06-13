using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyncMLViewer
{
    using System;
    using System.Collections.Generic;

    public class StatusCodeLookupModel
    {
        private readonly Dictionary<int, string> statusCodeMap = new Dictionary<int, string>();

        public StatusCodeLookupModel()
        {
            InitializeStatusCodeMap();
        }

        private void InitializeStatusCodeMap()
        {
            // Informational (1xx)
            AddStatusCode(101, "In progress. The specified SyncML command is being carried out, but has not yet completed.");

            // Successful (2xx)
            AddStatusCode(200, "OK. The SyncML command completed successfully.");
            AddStatusCode(201, "Item added. The requested item was added.");
            AddStatusCode(202, "Accepted for processing. The request to either run a remote execution of an application or to alert a user or application was successfully performed.");
            AddStatusCode(203, "Non-authoritative response. The request is being responded to by an entity other than the one targeted. The response is only to be returned when the request would have been resulted in a 200 response code from the authoritative target.");
            AddStatusCode(204, "No content. The request was successfully completed but no data is being returned. The response code is also returned in response to a Get when the target has no content.");
            AddStatusCode(205, "Reset content. The source SHOULD update their content. The originator of the request is being told that their content SHOULD be synchronized to get an up to date version.");
            AddStatusCode(206, "Partial content. The response indicates that only part of the command was completed. If the remainder of the command can be completed later, then when completed another appropriate completion request status code SHOULD be created.");
            AddStatusCode(207, "Conflict resolved with merge. The response indicates that the request created a conflict; which was resolved with a merge of the client and server instances of the data. The response includes both the Target and Source URLs in the Item of the Status. In addition, a Replace command is returned with the merged data.");
            AddStatusCode(208, "Conflict resolved with client’s command 'winning'. The response indicates that there was an update conflict; which was resolved by the client command winning.");
            AddStatusCode(209, "Conflict resolved with duplicate. The response indicates that the request created an update conflict; which was resolved with a duplication of the client’s data being created in the server database. The response includes both the target URI of the duplicate in the Item of the Status. In addition, in the case of a two-way synchronization, an Add command is returned with the duplicate data definition.");
            AddStatusCode(210, "Delete without archive. The response indicates that the requested data was successfully deleted, but that it was not archived prior to deletion because this OPTIONAL feature was not supported by the implementation.");
            AddStatusCode(211, "Item not deleted. The requested item was not found. It could have been previously deleted.");
            AddStatusCode(212, "Authentication accepted. No further authentication is needed for the remainder of the synchronization session. This response code can only be used in response to a request in which the credentials were provided.");
            AddStatusCode(213, "Chunked item accepted and buffered.");
            AddStatusCode(214, "Operation cancelled. The SyncML command completed successfully, but no more commands will be processed within the session.");
            AddStatusCode(215, "Not executed. A command was not executed, as a result of user interaction and user chose not to accept the choice.");
            AddStatusCode(216, "Atomic roll back OK. A command was inside Atomic element and Atomic failed. This command was rolled back successfully.");

            // Redirection (3xx)
            AddStatusCode(300, "Multiple choices. The requested target is one of a number of multiple alternatives requested target. The alternative SHOULD also be returned in the Item element type in the Status.");
            AddStatusCode(301, "Moved permanently. The requested target has a new URI. The new URI SHOULD also be returned in the Item element type in the Status.");
            AddStatusCode(302, "Found. The requested target has temporarily moved to a different URI. The original URI SHOULD continue to be used. The URI of the temporary location SHOULD also be returned in the Item element type in the Status. The requestor SHOULD confirm the identity and authority of the temporary URI to act on behalf of the original target URI.");
            AddStatusCode(303, "See other. The requested target can be found at another URI. The other URI SHOULD be returned in the Item element type in the Status.");
            AddStatusCode(304, "Not modified. The requested SyncML command was not executed on the target. This is an additional response that can be added to any of the other Redirection response codes.");
            AddStatusCode(305, "Use proxy. The requested target MUST be accessed through the specified proxy URI. The proxy URI SHOULD also be returned in the Item element type in the Status.");

            // Originator Exceptions (4xx)
            AddStatusCode(400, "Bad request. The requested command could not be performed because of malformed syntax in the command. The malformed command MAY also be returned in the Item element type in the Status.");
            AddStatusCode(401, "Invalid credentials. The requested command failed because the requestor MUST provide proper authentication. If the property type of authentication was presented in the original request, then the response code indicates that the requested command has been refused for those credentials.");
            AddStatusCode(402, "Payment needed. The requested command failed because proper payment is needed. This version of SyncML does not standardize the payment mechanism.");
            AddStatusCode(403, "Forbidden. The requested command failed, but the recipient understood the requested command. Authentication will not help and the request SHOULD NOT be repeated. If the recipient wishes to make public why the request was denied, then a description MAY be specified in the Item element type in the Status. If the recipient does not wish to make public why the request was denied then the response code 404 MAY be used instead.");
            AddStatusCode(404, "Not found. The requested target was not found. No indication is given as to whether this is a temporary or permanent condition. The response code 410 SHOULD be used when the condition is permanent and the recipient wishes to make this fact public. This response code is also used when the recipient does not want to make public the reason for why a requested command is not allowed or when no other response code is appropriate.");
            AddStatusCode(405, "Command not allowed. The requested command is not allowed on the target. The recipient SHOULD return the allowed command for the target in the Item element type in the Status.");
            AddStatusCode(406, "Optional feature not supported. The requested command failed because an OPTIONAL feature in the request was not supported. The unsupported feature SHOULD be specified by the Item element type in the Status.");
            AddStatusCode(407, "Missing credentials. This response code is similar to 401 except that the response code indicates that the originator MUST first authenticate with the recipient. The recipient SHOULD also return the suitable challenge in the Chal element type in the Status.");
            AddStatusCode(408, "Request timeout. An expected message was not received within the REQUIRED period of time. The request can be repeated at another time. The RespURI can be used to specify the URI and optionally the date/time after which the originator can repeat the request.");
            AddStatusCode(409, "Conflict. The requested failed because of an update conflict between the client and server versions of the data. Setting of the conflict resolution policy is outside the scope of this version of SyncML. However, identification of conflict resolution performed, if any, is within the scope.");
            AddStatusCode(410, "Gone. The requested target is no longer on the recipient and no forwarding URI is known.");
            AddStatusCode(411, "Size REQUIRED. The requested command MUST be accompanied by byte size or length information in the Meta element type.");
            AddStatusCode(412, "Incomplete command. The requested command failed on the recipient because it was incomplete or incorrectly formed. The recipient SHOULD specify the portion of the command that was incomplete or incorrect in the Item element type in the Status.");
            AddStatusCode(413, "Request entity too large. The recipient is refusing to perform the requested command because the requested item is larger than the recipient is able or willing to process. If the condition is temporary, the recipient SHOULD also include a Status with the response code 418 and specify a RespURI with the response URI and optionally the date/time that the command SHOULD be repeated.");
            AddStatusCode(414, "URI too long. The requested command failed because the target URI is too long for what the recipient is able or willing to process. This response code is seldom encountered, but is used when a recipient perceives that an intruder might be attempting to exploit security holes or other defects in order to threaten the recipient.");
            AddStatusCode(415, "Unsupported media type or format. The unsupported content type or format SHOULD also be identified in the Item element type in the Status.");
            AddStatusCode(416, "Requested size too big. The request failed because the specified byte size in the request was too big.");
            AddStatusCode(417, "Retry later. The request failed at this time and the originator SHOULD retry the request later. The recipient SHOULD specify a RespURI with the response URI and the date/time that the command SHOULD be repeated.");
            AddStatusCode(418, "Already exists. The requested Put or Add command failed because the target already exists.");
            AddStatusCode(419, "Conflict resolved with server data. The response indicates that the client request created a conflict; which was resolved by the server command winning. The normal information in the Status SHOULD be sufficient for the client to 'undo' the resolution, if it is desired.");
            AddStatusCode(420, "Device full. The response indicates that the recipient has no more storage space for the remaining synchronization data. The response includes the remaining number of data that could not be returned to the originator in the Item of the Status.");
            AddStatusCode(421, "Unknown search grammar. The requested command failed on the server because the specified search grammar was not known. The client SHOULD re-specify the search using a known search grammar.");
            AddStatusCode(422, "Bad CGI Script. The requested command failed on the server because the CGI scripting in the LocURI was incorrectly formed. The client SHOULD re-specify the portion of the command that was incorrect in the Item element type in the Status.");
            AddStatusCode(423, "Soft-delete conflict. The requested command failed because the 'Soft Deleted' item was previously 'Hard Deleted' on the server.");
            AddStatusCode(424, "Size mismatch. The chunked object was received, but the size of the received object did not match the size declared within the first chunk.");
            AddStatusCode(425, "Permission Denied. The requested command failed because the sender does not have adequate access control permissions (ACL) on the recipient.");
            AddStatusCode(426, "Partial item not accepted. Receiver of status code MAY resend the whole item in next package.");
            AddStatusCode(427, "Item Not empty. Parent cannot be deleted since it contains children.");
            AddStatusCode(428, "Move Failed");

            // Recipient Exception (5xx)
            AddStatusCode(500, "Command failed. The recipient encountered an unexpected condition which prevented it from fulfilling the request");
            AddStatusCode(501, "Command not implemented. The recipient does not support the command REQUIRED to fulfill the request. This is the appropriate response when the recipient does not recognize the requested command and is not capable of supporting it for any resource.");
            AddStatusCode(502, "Bad gateway. The recipient, while acting as a gateway or proxy, received an invalid response from the upstream recipient it accessed in attempting to fulfill the request.");
            AddStatusCode(503, "Service unavailable. The recipient is currently unable to handle the request due to a temporary overloading or maintenance of the recipient. The implication is that this is a temporary condition; which will be alleviated after some delay. The recipient SHOULD specify the URI and date/time after which the originator SHOULD retry in the RespURI in the response.");
            AddStatusCode(504, "Gateway timeout. The recipient, while acting as a gateway or proxy, did not receive a timely response from the upstream recipient specified by the URI (e.g. HTTP, FTP, LDAP) or some other auxiliary recipient (e.g. DNS) it needed to access in attempting to complete the request.");
            AddStatusCode(505, "DTD Version not supported. The recipient does not support or refuses to support the specified version of SyncML DTD used in the request SyncML Message. The recipient MUST include the versions it does support in the Item element type in the Status.");
            AddStatusCode(506, "Processing error. An application error occurred while processing the request. The originator SHOULD retry the request. The RespURI can contain the URI and date/time after which the originator can retry the request.");
            AddStatusCode(507, "Atomic failed. The error caused all SyncML commands within an Atomic element type to fail.");
            AddStatusCode(508, "Refresh REQUIRED. An error occurred that necessitates a refresh of the current synchronization state of the client with the server. Client is requested to initiate the session type specified in the server’s ALERT (which is included in the same package as the Status 508). The only valid values for this ALERT are either a slow sync (201) or a refresh with the server.");
            AddStatusCode(509, "Reserved for future use.");
            AddStatusCode(510, "Data store failure. An error occurred while processing the request. The error is related to a failure in the recipient data store.");
            AddStatusCode(511, "Server failure. A severe error occurred in the server while processing the request. The originator SHOULD NOT retry the request.");
            AddStatusCode(512, "Synchronization failed. An application error occurred during the synchronization session. The originator SHOULD restart the synchronization session from the beginning.");
            AddStatusCode(513, "Protocol Version not supported. The recipient does not support or refuses to support the specified version of the SyncML Synchronization Protocol used in the request SyncML Message. The recipient MUST include the versions it does support in the Item element type in the Status.");
            AddStatusCode(514, "Operation cancelled. The SyncML command was not completed successfully, since the operation was already cancelled before processing the command. The originator SHOULD repeat the command in the next session.");
            AddStatusCode(516, "Atomic roll back failed. Command was inside Atomic element and Atomic failed. This command was not rolled back successfully. Server SHOULD take action to try to recover client back into original state.");
            AddStatusCode(517, "Atomic response too large to fit. The response to an atomic command was too large to fit in a single message.");

            // Health Attestation Exception (6xx)
            AddStatusCode(600, "HEALTHATTESTATION_CERT_RETRIEVAL_UNINITIALIZED This is the initial state for devices that have never participated in a DHA-Session.");
            AddStatusCode(601, "HEALTHATTESTATION_CERT_RETRIEVAL_REQUESTED This state signifies that MDM client’s Exec call on the node VerifyHealth has been triggered and now the OS is trying to retrieve DHA-EncBlob from DHA-Server.");
            AddStatusCode(602, "HEALTHATTESTATION_CERT_RETRIEVAL_FAILED This state signifies that the device failed to retrieve DHA-EncBlob from DHA-Server.");
            AddStatusCode(603, "HEALTHATTESTATION_CERT_RETRIEVAL_COMPLETE This state signifies that the device failed to retrieve DHA-EncBlob from DHA-Server.");
            AddStatusCode(604, "HEALTHATTESTATION_CERT_RETRIEVAL_PCR_FAIL Deprecated in Windows 10, version 1607.");
            AddStatusCode(605, "HEALTHATTESTATION_CERT_RETRIEVAL_GETQUOTE_FAIL DHA-CSP failed to get a claim quote.");
            AddStatusCode(606, "HEALTHATTESTATION_CERT_RETRIEVAL_DEVICE_NOT_READY DHA-CSP failed in opening a handle to Microsoft Platform Crypto Provider.");
            AddStatusCode(607, "HEALTHATTESTATION_CERT_RETRIEVAL_WINDOWS_AIK_FAIL DHA-CSP failed in retrieving Windows AIK");
            AddStatusCode(608, "HEALTHATTESTATION_CERT_RETRIEVAL_FROM_WEB_FAIL Deprecated in Windows 10, version 1607.");
            AddStatusCode(609, "HEALTHATTESTATION_CERT_RETRIEVAL_INVALID_TPM_VERSION Invalid TPM version (TPM version is not 1.2 or 2.0)");
            AddStatusCode(610, "HEALTHATTESTATION_CERT_RETRIEVAL_GETNONCE_FAIL Nonce was not found in the registry.");
            AddStatusCode(611, "HEALTHATTESTATION_CERT_RETRIEVAL_GETCORRELATIONID_FAIL Correlation ID was not found in the registry.");
            AddStatusCode(612, "HEALTHATTESTATION_CERT_RETRIEVAL_GETCERT_FAIL Deprecated in Windows 10, version 1607.");
            AddStatusCode(613, "HEALTHATTESTATION_CERT_RETRIEVAL_GETCLAIM_FAIL Deprecated in Windows 10, version 1607.");
            AddStatusCode(614, "HEALTHATTESTATION_CERT_RETRIEVAL_ENCODING_FAIL Failure in Encoding functions.");
            AddStatusCode(615, "HEALTHATTESTATION_CERT_RETRIEVAL_ENDPOINTOVERRIDE_FAIL Deprecated in Windows 10, version 1607.");
            AddStatusCode(616, "HEALTHATTESTATION_CERT_RETRIEVAL_FAILED_LOAD_XML DHA-CSP failed to load the payload it received from DHA-Service");
            AddStatusCode(617, "HEALTHATTESTATION_CERT_RETRIEVAL_FAILED_CORRUPT_XML DHA-CSP received a corrupted response from DHA-Service.");
            AddStatusCode(618, "HEALTHATTESTATION_CERT_RETRIEVAL_FAILED_EMPTY_XML DHA-CSP received an empty response from DHA-Service.");
            AddStatusCode(619, "HEALTHATTESTATION_CERT_RETRIEVAL_FAILED_DECRYPT_AES_EK DHA-CSP failed in decrypting the AES key from the EK challenge.");
            AddStatusCode(620, "HEALTHATTESTATION_CERT_RETRIEVAL_FAILED_DECRYPT_CERT_AES_EK DHA-CSP failed in decrypting the health cert with the AES key.");
            AddStatusCode(621, "HEALTHATTESTATION_CERT_RETRIEVAL_FAILED_EXPORT_AIKPUB DHA-CSP failed in exporting the AIK Public Key.");
            AddStatusCode(622, "HEALTHATTESTATION_CERT_RETRIEVAL_FAILED_CREATE_CLAIMAUTHORITYONLY DHA-CSP failed in trying to create a claim with AIK attestation data.");
            AddStatusCode(623, "HEALTHATTESTATION_CERT_RETRIEVAL_FAILED_APPEND_AIKPUB DHA-CSP failed in appending the AIK Pub to the request blob.");
            AddStatusCode(624, "HEALTHATTESTATION_CERT_RETRIEVAL_FAILED_APPEND_AIKCERT DHA-CSP failed in appending the AIK Cert to the request blob.");
            AddStatusCode(625, "HEALTHATTESTATION_CERT_RETRIEVAL_FAILED_INIT_HTTPHANDLE DHA-CSP failed to obtain a Session handle.");
            AddStatusCode(626, "HEALTHATTESTATION_CERT_RETRIEVAL_FAILED_GETTARGET_HTTPHANDLE DHA-CSP failed to connect to the DHA-Service.");
            AddStatusCode(627, "HEALTHATTESTATION_CERT_RETRIEVAL_FAILED_CREATE_HTTPHANDLE DHA-CSP failed to create a HTTP request handle.");
            AddStatusCode(628, "HEALTHATTESTATION_CERT_RETRIEVAL_FAILED_SET_INTERNETOPTION DHA-CSP failed to set options.");
            AddStatusCode(629, "HEALTHATTESTATION_CERT_RETRIEVAL_FAILED_ADD_REQUESTHEADERS DHA-CSP failed to add request headers.");
            AddStatusCode(630, "HEALTHATTESTATION_CERT_RETRIEVAL_FAILED_SEND_REQUEST DHA-CSP failed to send the HTTP request.");
            AddStatusCode(631, "HEALTHATTESTATION_CERT_RETRIEVAL_FAILED_RECEIVE_RESPONSE DHA-CSP failed to receive a response from the DHA-Service.");
            AddStatusCode(632, "HEALTHATTESTATION_CERT_RETRIEVAL_FAILED_QUERY_HEADERS DHA-CSP failed to query headers when trying to get HTTP status code.");
            AddStatusCode(633, "HEALTHATTESTATION_CERT_RETRIEVAL_FAILED_EMPTY_RESPONSE DHA-CSP received an empty response from DHA-Service even though HTTP status was OK.");
            AddStatusCode(634, "HEALTHATTESTATION_CERT_RETRIEVAL_FAILED_MISSING_RESPONSE DHA-CSP received an empty response along with a HTTP error code from DHA-Service.");
            AddStatusCode(635, "HEALTHATTESTATION_CERT_RETRIEVAL_FAILED_IMPERSONATE_USER DHA-CSP failed to impersonate user.");
            AddStatusCode(636, "HEALTHATTESTATION_CERT_RETRIEVAL_FAILED_ACQUIRE_PDCNETWORKACTIVATOR DHA-CSP failed to acquire the PDC activators that are needed for network communication when the device is in Connected standby mode.");
            AddStatusCode(637, "HEALTHATTESTATION_CERT_RETRIEVAL_FAILED_UNKNOWN DHA-CSP failed due to an unknown reason");
            AddStatusCode(638, "Bad_Request_From_Client DHA-CSP has received a bad (malformed) attestation request.");
            AddStatusCode(639, "Endpoint_Not_Reachable DHA-Service is not reachable by DHA-CSP");

            // Additional application-specific codes
            AddStatusCodeRange(1100, 1199, "Application specific: Should be used for Informational (11xx)");
            AddStatusCodeRange(1200, 1299, "Application specific: Should be used for Successful (12xx)");
            AddStatusCodeRange(1300, 1399, "Application specific: Should be used for Redirection (13xx)");
            AddStatusCodeRange(1400, 1499, "Application specific: Should be used for Originator Exceptions (14xx)");
            AddStatusCodeRange(1500, 1599, "Application specific: Should be used for Recipient Exception (15xx)");
            AddStatusCodeRange(1000, 1099, "Application specific: Status codes and the meanings of these are not defined in the SyncML Representation Protocol specification.");
            AddStatusCodeRange(1600, 1999, "Application specific: Status codes and the meanings of these are not defined in the SyncML Representation Protocol specification.");
        }

        private void AddStatusCode(int code, string description)
        {
            statusCodeMap[code] = description;
        }

        private void AddStatusCodeRange(int start, int end, string description)
        {
            for (int i = start; i <= end; i++)
            {
                statusCodeMap[i] = description;
            }
        }

        public string GetDescription(int code)
        {
            if (statusCodeMap.ContainsKey(code))
            {
                return statusCodeMap[code];
            }
            else
            {
                return "Unknown status code.";
            }
        }

        public string GetDescription(string code)
        {
            if (int.TryParse(code, out int intCode))
            {
                return GetDescription(intCode);
            }
            else
            {
                return "Unknown status code.";
            }   
        }
    }

}
