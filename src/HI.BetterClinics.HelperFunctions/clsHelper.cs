using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Nehta.VendorLibrary.HI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text.Json;
using System.Xml;
using System.Xml.Serialization;

namespace HI.BetterClinics.HelperFunctions
{
    /// <summary>
    /// Represents a generic configuration for a client object.
    /// This class is used for logging purposes to serialize client configuration details into XML.
    /// </summary>
    /// <typeparam name="TProduct">The type of the product information.</typeparam>
    /// <typeparam name="TQualifiedId">The type of the qualified identifiers for User and Hpio.</typeparam>
    public class ClientConfig<TProduct, TQualifiedId>
    {
        public required string Endpoint { get; set; }
        public required TProduct Product { get; set; }
        public required TQualifiedId User { get; set; }
        public required TQualifiedId Hpio { get; set; }
        public required string CertificateThumbprint { get; set; }
        public required string CertificateSerial { get; set; }
    }

    /// <summary>
    /// Wraps the result of a digital health client creation operation.
    /// It contains the client object on success, or an error message on failure.
    /// </summary>
    public class DigitalHealthClientResult
    {
        public object? DigitalHealthClient { get; set; }
        public string? Error { get; set; }
        public bool IsSuccess => string.IsNullOrEmpty(Error);
    }

    /// <summary>
    /// Provides static helper methods for various functionalities including
    /// exception handling, output formatting, and data manipulation.
    /// </summary>
    public static class clsHelper
    {
        /// <summary>
        /// Handles a <see cref="FaultException"/> by extracting detailed error information
        /// from the SOAP fault message and formatting it into a standardized JSON output.
        /// </summary>
        /// <param name="functionName">The name of the AWS Lambda function where the exception occurred.</param>
        /// <param name="fex">The fault exception instance.</param>
        /// <param name="client">The client instance that made the call, used to access SOAP messages and context.</param>
        /// <param name="context">The Lambda context for logging.</param>
        /// <returns>A formatted JSON string representing the error.</returns>
        public static dynamic HandleFaultException(string functionName, FaultException fex, dynamic client, ILambdaContext context)
        {
            context.Logger.LogInformation("HandleFaultException function started");

            MessageFault fault = fex.CreateMessageFault();
            if (fault.HasDetail)
            {
                Type serviceMessagesType = GetServiceMessagesType(client, context);
                if (serviceMessagesType != null)
                {
                    MethodInfo getDetailMethod = typeof(MessageFault).GetMethod("GetDetail", new Type[] { }).MakeGenericMethod(serviceMessagesType);
                    dynamic error = getDetailMethod.Invoke(fault, null);

                    if (error != null && error.serviceMessage != null && error.serviceMessage.Length > 0)
                    {
                        return FormatOutput(functionName, "FAILURE", error.serviceMessage[0].severity.ToString(), error.serviceMessage[0].code, error.serviceMessage[0].reason, client, context);
                    }
                }
            }

            context.Logger.LogInformation("HandleFaultException function completed");

            // Fallback to returning the raw SOAP response if detailed error parsing fails.
            return FormatOutput(functionName, "FAILURE", "ERROR", "FaultException", client.SoapMessages.SoapResponse, client, context);
        }

        /// <summary>
        /// Determines the specific <c>ServiceMessagesType</c> based on the provided client's type name.
        /// This is used to correctly deserialize the SOAP fault detail.
        /// </summary>
        /// <param name="client">The web service client instance.</param>
        /// <param name="context">The Lambda context for logging.</param>
        /// <returns>The <see cref="Type"/> of the service message, or null if the client type is unknown.</returns>
        private static Type GetServiceMessagesType(dynamic client, ILambdaContext context)
        {
            context.Logger.LogInformation("GetServiceMessagesType function started");

            string clientTypeName = client.GetType().Name;
            context.Logger.LogInformation($"Client type name: {clientTypeName}");

            return clientTypeName switch
            {
                // IHI Inquiry Search via B2B (TECH.SIS.HI.06)
                nameof(ConsumerSearchIHIClient) => typeof(nehta.mcaR3.ConsumerSearchIHI.ServiceMessagesType),
                // Search for Provider Individual Details (TECH.SIS.HI.31)
                nameof(ProviderSearchForProviderIndividualClient) => typeof(nehta.mcaR50.ProviderSearchForProviderIndividual.ServiceMessagesType),
                // Search for Provider Organisation Details (TECH.SIS.HI.32)
                nameof(ProviderSearchForProviderOrganisationClient) => typeof(nehta.mcaR50.ProviderSearchForProviderOrganisation.ServiceMessagesType),
                // Read Provider Organisation Details (TECH.SIS.HI.16)
                nameof(ProviderReadProviderOrganisationClient) => typeof(nehta.mcaR32.ProviderReadProviderOrganisation.ServiceMessagesType),
                // Healthcare Provider Directory – Search for Individual Provider Directory Entry (TECH.SIS.HI.17)
                nameof(ProviderSearchHIProviderDirectoryForIndividualClient) => typeof(nehta.mcaR32.ProviderSearchHIProviderDirectoryForIndividual.ServiceMessagesType),
                // Healthcare Provider Directory – Search for Organisation Provider Directory Entry (TECH.SIS.HI.18)
                nameof(ProviderSearchHIProviderDirectoryForOrganisationClient) => typeof(nehta.mcaR32.ProviderSearchHIProviderDirectoryForOrganisation.ServiceMessagesType),
                // Create Provisional IHI via B2B (TECH.SIS.HI.10)
                nameof(ConsumerCreateProvisionalIHIClient) => typeof(nehta.mcaR3.ConsumerCreateProvisionalIHI.ServiceMessagesType),
                // Create Unverified IHI via B2B (TECH.SIS.HI.11)
                nameof(ConsumerCreateUnverifiedIHIClient) => typeof(nehta.mcaR302.ConsumerCreateUnverifiedIHI.ServiceMessagesType),
                // Update Provisional IHI via B2B (TECH.SIS.HI.03)
                nameof(ConsumerUpdateProvisionalIHIClient) => typeof(nehta.mcaR3.ConsumerUpdateProvisionalIHI.ServiceMessagesType),
                // Update IHI via B2B (TECH.SIS.HI.05)
                nameof(ConsumerUpdateIHIClient) => typeof(nehta.mcaR32.ConsumerUpdateIHI.ServiceMessagesType),
                // Resolve Provisional IHI-Merge record via B2B (TECH.SIS.HI.08)
                nameof(ConsumerMergeProvisionalIHIClient) => typeof(nehta.mcaR3.ConsumerMergeProvisionalIHI.ServiceMessagesType),
                // Resolve Provisional IHI – Create Unverified IHI via B2B (TECH.SIS.HI.09)
                nameof(ConsumerResolveProvisionalIHIClient) => typeof(nehta.mcaR302.ConsumerResolveProvisionalIHI.ServiceMessagesType),
                // Notify of Duplicate IHI via B2B (TECH.SIS.HI.24)
                nameof(ConsumerNotifyDuplicateIHIClient) => typeof(nehta.mcaR32.ConsumerNotifyDuplicateIHI.ServiceMessagesType),
                // Notify of Replica IHI via B2B (TECH.SIS.HI.25)
                nameof(ConsumerNotifyReplicaIHIClient) => typeof(nehta.mcaR32.ConsumerNotifyReplicaIHI.ServiceMessagesType),
                // Create verified IHI for Newborns (TECH.SIS.HI.26)
                nameof(ConsumerCreateVerifiedIHIClient) => typeof(nehta.mcaR40.CreateVerifiedIHI.ServiceMessagesType),
                _ => null,
            };
        }

        /// <summary>
        /// Formats the output of a function into a standardized JSON structure.
        /// </summary>
        /// <param name="functionName">The name of the AWS Lambda function.</param>
        /// <param name="status">The status of the operation (e.g., "SUCCESS", "FAILURE").</param>
        /// <param name="severity">The severity of the result (e.g., "ERROR", "WARNING", "INFORMATION").</param>
        /// <param name="code">An error or status code.</param>
        /// <param name="reason">A descriptive message or a JSON string with detailed information.</param>
        /// <param name="client">The client instance, used to access SOAP request and response messages.</param>
        /// <param name="context">The Lambda context for logging.</param>
        /// <returns>A JSON string representing the formatted output.</returns>
        public static string FormatOutput(string functionName, string status, string severity, string code, string reason, dynamic client, ILambdaContext context)
        {
            context.Logger.LogInformation("FormatOutput function started");

            var soapRequest = client?.SoapMessages?.SoapRequest;
            var soapResponse = client?.SoapMessages?.SoapResponse;

            // If 'reason' is already a JSON string, use JRaw to preserve it as-is (so it is not double serialized)
            var reasonObject = IsValidJson(reason) ? new JRaw(reason) : (object)reason;

            var retOutput = new
            {
                status,
                output = new
                {
                    severity,
                    code,
                    reason = reasonObject
                },
                awsFunction = functionName,
                apiXmlRequest = soapRequest,
                apiXmlResponse = soapResponse
            };

            var settings = new JsonSerializerSettings
            {
                StringEscapeHandling = StringEscapeHandling.Default,
                Formatting = Newtonsoft.Json.Formatting.None
            };

            string jsonOutput = JsonConvert.SerializeObject(retOutput, settings);

            context.Logger.LogInformation(jsonOutput);

            context.Logger.LogInformation("FormatOutput function completed");

            context.Logger.LogInformation($"{functionName} function completed...");

            return jsonOutput;
        }

        /// <summary>
        /// Checks if the input string is a valid JSON object or array.
        /// </summary>
        /// <param name="strInput">The string to validate.</param>
        /// <returns>True if the string is valid JSON, otherwise false.</returns>
        private static bool IsValidJson(string strInput)
        {
            if (string.IsNullOrWhiteSpace(strInput)) { return false; }
            strInput = strInput.Trim();
            if ((strInput.StartsWith("{") && strInput.EndsWith("}")) ||
                (strInput.StartsWith("[") && strInput.EndsWith("]")))
            {
                try
                {
                    JToken.Parse(strInput);
                    return true;
                }
                catch (JsonReaderException)
                {
                    return false;
                }
            }
            return false;
        }

        /// <summary>
        /// Converts SOAP request and response XML strings from a client into JSON documents.
        /// Note: This method is currently not used in the codebase.
        /// </summary>
        /// <param name="client">The client containing SoapMessages.</param>
        /// <returns>A tuple containing the JSON document for the request and response.</returns>
        private static (JsonDocument JsonRequest, JsonDocument JsonResponse) GetSoapMessages(dynamic client)
        {
            JsonDocument? jsonRequestObject = null;
            JsonDocument? jsonresponseObject = null;
            XmlDocument doc = new XmlDocument();

            if (!string.IsNullOrEmpty(client.SoapMessages.SoapRequest))
            {
                doc.LoadXml(client.SoapMessages.SoapRequest);
                string jsonRequestString = JsonConvert.SerializeXmlNode(doc);
                jsonRequestObject = JsonDocument.Parse(jsonRequestString);
            }

            if (!string.IsNullOrEmpty(client.SoapMessages.SoapResponse))
            {
                doc.LoadXml(client.SoapMessages.SoapResponse);
                string jsonResponseString = JsonConvert.SerializeXmlNode(doc);
                jsonresponseObject = JsonDocument.Parse(jsonResponseString);
            }

            return (jsonRequestObject, jsonresponseObject);
        }

        /// <summary>
        /// Creates an ElectronicCommunicationType object from email or mobile phone details in the input.
        /// Email is given priority over mobile.
        /// </summary>
        /// <typeparam name="T">The specific ElectronicCommunicationType to create.</typeparam>
        /// <typeparam name="TMedium">The specific MediumType enum.</typeparam>
        /// <typeparam name="TUsage">The specific UsageType enum.</typeparam>
        /// <typeparam name="TTrueFalse">The specific TrueFalseType enum.</typeparam>
        /// <param name="root">The root <see cref="JsonElement"/> of the input payload.</param>
        /// <returns>An ElectronicCommunicationType object, or null if no details are provided.</returns>
        public static T GetElectronicCommunication<T, TMedium, TUsage, TTrueFalse>(JsonElement root)
            where T : class, new()
            where TMedium : struct, Enum
            where TUsage : struct, Enum
            where TTrueFalse : struct, Enum
        {
            string email = GetStringProperty(root, "emailField");
            string mobile = GetStringProperty(root, "mobileField");
            string preferredComms = GetStringProperty(root, "preferredElectronicCommunicationField");

            if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(mobile))
            {
                return null;
            }

            var comms = new T();
            var typeT = typeof(T);

            if (!string.IsNullOrWhiteSpace(email))
            {
                typeT.GetProperty("medium")?.SetValue(comms, Enum.Parse(typeof(TMedium), "E"));
                typeT.GetProperty("usage")?.SetValue(comms, Enum.Parse(typeof(TUsage), "P"));
                typeT.GetProperty("details")?.SetValue(comms, email);
                if (string.Equals(preferredComms, "Y", StringComparison.OrdinalIgnoreCase))
                {
                    typeT.GetProperty("preferred")?.SetValue(comms, Enum.Parse(typeof(TTrueFalse), "T"));
                    typeT.GetProperty("preferredSpecified")?.SetValue(comms, true);
                }
            }
            else if (!string.IsNullOrWhiteSpace(mobile))
            {
                typeT.GetProperty("medium")?.SetValue(comms, Enum.Parse(typeof(TMedium), "M"));
                typeT.GetProperty("usage")?.SetValue(comms, Enum.Parse(typeof(TUsage), "P"));
                typeT.GetProperty("details")?.SetValue(comms, mobile);
                if (string.Equals(preferredComms, "Y", StringComparison.OrdinalIgnoreCase))
                {
                    typeT.GetProperty("preferred")?.SetValue(comms, Enum.Parse(typeof(TTrueFalse), "T"));
                    typeT.GetProperty("preferredSpecified")?.SetValue(comms, true);
                }
            }

            return comms;
        }

        /// <summary>
        /// Creates an AustralianUnstructuredStreetAddressType object from address details in the input.
        /// The address is only processed if the country is specified as "AUSTRALIA".
        /// </summary>
        /// <typeparam name="T">The specific AustralianUnstructuredStreetAddressType to create.</typeparam>
        /// <typeparam name="TState">The specific StateType enum.</typeparam>
        /// <param name="root">The root <see cref="JsonElement"/> of the input payload.</param>
        /// <returns>An AustralianUnstructuredStreetAddressType object, or null if not applicable.</returns>
        public static T GetAustralianAddressUnstructured<T, TState>(JsonElement root)
            where T : class, new()
            where TState : struct, Enum
        {
            if (root.TryGetProperty("australianAddressField", out var addrRoot) &&
                addrRoot.ValueKind == JsonValueKind.Object &&
                addrRoot.TryGetProperty("countryField", out var countryElem) &&
                countryElem.ValueKind == JsonValueKind.String &&
                string.Equals(countryElem.GetString()?.Trim(), "AUSTRALIA", StringComparison.OrdinalIgnoreCase))
            {
                var ausAddr = new T();
                typeof(T).GetProperty("addressLineOne")?.SetValue(ausAddr, clsHelper.GetStringProperty(addrRoot, "addressLineOneField"));
                typeof(T).GetProperty("addressLineTwo")?.SetValue(ausAddr, clsHelper.GetStringProperty(addrRoot, "addressLineTwoField"));
                typeof(T).GetProperty("suburb")?.SetValue(ausAddr, clsHelper.GetStringProperty(addrRoot, "suburbField"));
                typeof(T).GetProperty("postcode")?.SetValue(ausAddr, clsHelper.GetStringProperty(addrRoot, "postcodeField"));

                if (addrRoot.TryGetProperty("stateField", out var stateElem) &&
                    stateElem.ValueKind == JsonValueKind.String)
                {
                    var stateAbbr = clsHelper.GetStateAbbreviation(stateElem.GetString());
                    if (Enum.TryParse<TState>(stateAbbr, true, out var stateEnum))
                    {
                        typeof(T).GetProperty("state")?.SetValue(ausAddr, stateEnum);
                    }
                }
                return ausAddr;
            }
            return null;
        }

        /// <summary>
        /// Converts an Australian state name or abbreviation to its standard three-letter abbreviation.
        /// </summary>
        /// <param name="stateValue">The full state name or abbreviation.</param>
        /// <returns>The standardized state abbreviation in uppercase.</returns>
        public static string GetStateAbbreviation(string stateValue) => stateValue.Trim().ToUpperInvariant() switch
        {
            "NSW" or "NEW SOUTH WALES" => "NSW",
            "QLD" or "QUEENSLAND" => "QLD",
            "VIC" or "VICTORIA" => "VIC",
            "SA" or "SOUTH AUSTRALIA" => "SA",
            "WA" or "WESTERN AUSTRALIA" => "WA",
            "TAS" or "TASMANIA" => "TAS",
            "ACT" or "AUSTRALIAN CAPITAL TERRITORY" => "ACT",
            "NT" or "NORTHERN TERRITORY" => "NT",
            _ => stateValue.Trim().ToUpperInvariant()
        };

        /// <summary>
        /// Constructs an array of address objects from the provided JSON payload.
        /// This method specifically handles unstructured Australian addresses.
        /// </summary>
        /// <typeparam name="TAddress">The type of the address object to be created. This type should contain a property to hold the unstructured address.</typeparam>
        /// <typeparam name="TUnstructuredAddress">The specific type for an Australian unstructured street address.</typeparam>
        /// <typeparam name="TState">The enum type representing Australian states (e.g., NSW, VIC).</typeparam>
        /// <param name="root">The root <see cref="JsonElement"/> of the input payload containing address information.</param>
        /// <returns>An array of <typeparamref name="TAddress"/> objects if an address is found; otherwise, null.</returns>
        public static TAddress[] BuildAddressArray<TAddress, TUnstructuredAddress, TState>(JsonElement root)
            where TAddress : new()
            where TUnstructuredAddress : class, new()
            where TState : struct, Enum
        {
            var addressList = new List<TAddress>();

            var ausUnstructured = clsHelper.GetAustralianAddressUnstructured<TUnstructuredAddress, TState>(root);
            if (ausUnstructured != null)
            {
                var address = new TAddress();
                address.GetType().GetProperty("australianUnstructuredStreetAddress")?.SetValue(address, ausUnstructured);
                addressList.Add(address);
            }

            return addressList.Count > 0 ? addressList.ToArray() : null;
        }

        /// <summary>
        /// Truncates a string to a specified maximum length.
        /// </summary>
        /// <param name="value">The string to truncate.</param>
        /// <param name="maxLength">The maximum length of the string.</param>
        /// <returns>The truncated string, or the original string if it's shorter than the max length.</returns>
        public static string TruncateString(string value, int maxLength) =>
            !string.IsNullOrEmpty(value) && value.Length > maxLength ? value.Substring(0, maxLength) : value;

        /// <summary>
        /// Attempts to get a string property from a JsonElement and prefixes it with a qualifier.
        /// </summary>
        /// <param name="root">The root JsonElement to search within.</param>
        /// <param name="propertyName">The name of the property to retrieve.</param>
        /// <param name="qualifier">The qualifier string to prepend to the value.</param>
        /// <returns>The qualified string if the property exists and is not empty; otherwise, null.</returns>
        public static string? TryGetQualifiedString(JsonElement root, string propertyName, string qualifier)
        {
            var value = GetStringProperty(root, propertyName);
            return !string.IsNullOrWhiteSpace(value) ? qualifier + value : null;
        }

        /// <summary>
        /// Safely retrieves a string property from a <see cref="JsonElement"/>.
        /// </summary>
        /// <param name="input">The <see cref="JsonElement"/> to extract the property from.</param>
        /// <param name="propertyName">The name of the property.</param>
        /// <returns>The string value if found and not whitespace; otherwise, null.</returns>
        public static string? GetStringProperty(JsonElement input, string propertyName)
        {
            return input.TryGetProperty(propertyName, out var element) &&
                   element.ValueKind == JsonValueKind.String &&
                   !string.IsNullOrWhiteSpace(element.GetString())
                ? element.GetString()
                : null;
        }
    }

    /// <summary>
    /// A factory class for creating various pre-configured Digital Health service clients.
    /// It handles fetching configuration from AWS SSM, retrieving certificates from S3,
    /// and instantiating the appropriate client based on input.
    /// </summary>
    public class clsDigitalHealthClient
    {
        // Singleton HttpClient for API client requests to improve performance and resource management.
        private static readonly HttpClient HttpClient = new HttpClient();

        // Defines the set of required AWS Systems Manager (SSM) parameter store names.
        private static readonly string[] SsmParameterNames = new[]
        {
            "/AustralianDigitalHealth/Uri",
            "/AustralianDigitalHealth/Product/Platform",
            "/AustralianDigitalHealth/Product/ProductName",
            "/AustralianDigitalHealth/Product/ProductVersion",
            "/AustralianDigitalHealth/Product/Vendor/Id",
            "/AustralianDigitalHealth/Product/Vendor/Qualifier",
            "/AustralianDigitalHealth/User/Qualifier",
            "/AustralianDigitalHealth/Hpio/Qualifier",
            "/AustralianDigitalHealth/Certificate/S3Bucket",
            "/AustralianDigitalHealth/Certificate/S3ObjectKey",
            "/AustralianDigitalHealth/Certificate/Password"
        };

        /// <summary>
        /// Asynchronously creates and configures a Digital Health service client.
        /// </summary>
        /// <param name="input">A JsonElement containing input parameters like clientType, internalUserId, and internalHPIO.</param>
        /// <param name="context">The AWS Lambda context for logging.</param>
        /// <returns>A <see cref="DigitalHealthClientResult"/> containing the created client or an error message.</returns>
        public async Task<object> GetClientAsync(JsonElement input, ILambdaContext context)
        {
            // Input Structure:
            // {
            //    "clientType": "",
            //    "internalUserId": "",
            //    "internalHPIO": ""
            //}

            context.Logger.LogInformation("GetClientAsync function started");

            context.Logger.LogInformation($"Input Data: {input.GetRawText()}");

            // Retrieve all required configuration parameters from AWS SSM Parameter Store.
            Dictionary<string, string> ssmParameters;
            try
            {
                ssmParameters = await GetSsmParametersAsync(SsmParameterNames, context);
            }
            catch (Exception ex)
            {
                return new DigitalHealthClientResult { Error = ex.Message };
            }

            // Extract SSM parameters into local variables for easier access.
            string digitalHealthUri = ssmParameters["/AustralianDigitalHealth/Uri"];
            string productPlatform = ssmParameters["/AustralianDigitalHealth/Product/Platform"];
            string productProductName = ssmParameters["/AustralianDigitalHealth/Product/ProductName"];
            string productProductVersion = ssmParameters["/AustralianDigitalHealth/Product/ProductVersion"];
            string productVendorId = ssmParameters["/AustralianDigitalHealth/Product/Vendor/Id"];
            string productVendorQualifier = ssmParameters["/AustralianDigitalHealth/Product/Vendor/Qualifier"];
            string userQualifier = ssmParameters["/AustralianDigitalHealth/User/Qualifier"];
            string hpioQualifier = ssmParameters["/AustralianDigitalHealth/Hpio/Qualifier"];
            string s3BucketName = ssmParameters["/AustralianDigitalHealth/Certificate/S3Bucket"];
            string s3ObjectKey = ssmParameters["/AustralianDigitalHealth/Certificate/S3ObjectKey"];
            string s3CertPassword = ssmParameters["/AustralianDigitalHealth/Certificate/Password"];

            // Parse and validate input parameters from the JSON payload.
            var clientType = input.TryGetProperty("clientType", out var clientTypeElement) &&
                             clientTypeElement.ValueKind == JsonValueKind.String &&
                             !string.IsNullOrWhiteSpace(clientTypeElement.GetString())
                             ? clientTypeElement.GetString()
                             : "0";

            var internalUserId = input.TryGetProperty("internalUserId", out var internalUserIdElement) &&
                                 internalUserIdElement.ValueKind == JsonValueKind.String &&
                                 !string.IsNullOrWhiteSpace(internalUserIdElement.GetString())
                                 ? internalUserIdElement.GetString()
                                 : null;

            var internalHPIO = input.TryGetProperty("internalHPIO", out var internalHPIOElement) &&
                                 internalHPIOElement.ValueKind == JsonValueKind.String &&
                                 !string.IsNullOrWhiteSpace(internalHPIOElement.GetString())
                                 ? internalHPIOElement.GetString()
                                 : null;

            // Validate that all required input parameters are present.
            if (string.IsNullOrWhiteSpace(clientType))
            {
                return new DigitalHealthClientResult { Error = "GetClientAsync Input Parameters - Missing or empty clientType." };
            }

            if (string.IsNullOrWhiteSpace(internalUserId))
            {
                return new DigitalHealthClientResult { Error = "GetClientAsync Input Parameters - Missing or empty internalUserId." };
            }

            if (string.IsNullOrWhiteSpace(internalHPIO))
            {
                return new DigitalHealthClientResult { Error = "GetClientAsync Input Parameters - Missing or empty internalHPIO." };
            }

            // Check if the Digital Health API endpoint is available before proceeding.
            try
            {
                var response = await HttpClient.GetAsync(digitalHealthUri);
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    return new DigitalHealthClientResult { Error = "The Australian Digital Health System is unavailable at this time. Please try again later." };
                }
            }
            catch (Exception ex)
            {
                return new DigitalHealthClientResult { Error = ex.Message };
            }

            // Retrieve and load the client certificate from AWS S3.
            X509Certificate2 cert;
            try
            {
                cert = await GetCertificateAsync(s3BucketName, s3ObjectKey, s3CertPassword, context);
            }
            catch (Exception ex)
            {
                return new DigitalHealthClientResult { Error = ex.Message };
            }

            // Validate the certificate's existence and expiration date.
            if (cert == null || cert.GetExpirationDateString() == null)
            {
                return new DigitalHealthClientResult { Error = "The certificate is missing. Please contact the system administrator." };
            }

            context.Logger.LogInformation($"Certificate - Subject: {cert.Subject}\nThumbprint: {cert.Thumbprint}\nExpiry: {cert.GetExpirationDateString()}");


            if (cert.NotAfter < DateTime.UtcNow)
            {
                return new DigitalHealthClientResult { Error = "The certificate has expired. Please contact the system administrator." };
            }

            // Instantiate the appropriate client based on the clientType parameter.
            object client = clientType switch
            {
                // IHI Inquiry Search via B2B (TECH.SIS.HI.06)
                "1" => new ConsumerSearchIHIClient(
                    new Uri(digitalHealthUri),
                    CreateProduct<nehta.mcaR3.ConsumerSearchIHI.ProductType, nehta.mcaR3.ConsumerSearchIHI.QualifiedId>(
                        productPlatform, productProductName, productProductVersion, productVendorId, productVendorQualifier),
                    CreateUser<nehta.mcaR3.ConsumerSearchIHI.QualifiedId>(internalUserId, userQualifier, productProductName),
                    CreateHpio<nehta.mcaR3.ConsumerSearchIHI.QualifiedId>(internalHPIO, hpioQualifier),
                    cert,
                    cert),

                // Search for Provider Individual Details (TECH.SIS.HI.31)
                "2" => new ProviderSearchForProviderIndividualClient(
                    new Uri(digitalHealthUri),
                    CreateProduct<nehta.mcaR50.ProviderSearchForProviderIndividual.ProductType, nehta.mcaR50.ProviderSearchForProviderIndividual.QualifiedId>(
                        productPlatform, productProductName, productProductVersion, productVendorId, productVendorQualifier),
                    CreateUser<nehta.mcaR50.ProviderSearchForProviderIndividual.QualifiedId>(internalUserId, userQualifier, productProductName),
                    CreateHpio<nehta.mcaR50.ProviderSearchForProviderIndividual.QualifiedId>(internalHPIO, hpioQualifier),
                    cert,
                    cert),

                // Search for Provider Organisation Details (TECH.SIS.HI.32)
                "3" => new ProviderSearchForProviderOrganisationClient(
                    new Uri(digitalHealthUri),
                    CreateProduct<nehta.mcaR50.ProviderSearchForProviderOrganisation.ProductType, nehta.mcaR50.ProviderSearchForProviderOrganisation.QualifiedId>(
                        productPlatform, productProductName, productProductVersion, productVendorId, productVendorQualifier),
                    CreateUser<nehta.mcaR50.ProviderSearchForProviderOrganisation.QualifiedId>(internalUserId, userQualifier, productProductName),
                    CreateHpio<nehta.mcaR50.ProviderSearchForProviderOrganisation.QualifiedId>(internalHPIO, hpioQualifier),
                    cert,
                    cert),

                // Read Provider Organisation Details (TECH.SIS.HI.16)
                "4" => new ProviderReadProviderOrganisationClient(
                    new Uri(digitalHealthUri),
                    CreateProduct<nehta.mcaR32.ProviderReadProviderOrganisation.ProductType, nehta.mcaR32.ProviderReadProviderOrganisation.QualifiedId>(
                        productPlatform, productProductName, productProductVersion, productVendorId, productVendorQualifier),
                    CreateUser<nehta.mcaR32.ProviderReadProviderOrganisation.QualifiedId>(internalUserId, userQualifier, productProductName),
                    CreateHpio<nehta.mcaR32.ProviderReadProviderOrganisation.QualifiedId>(internalHPIO, hpioQualifier),
                    cert,
                    cert),

                // Healthcare Provider Directory – Search for Individual Provider Directory Entry (TECH.SIS.HI.17)
                "5" => new ProviderSearchHIProviderDirectoryForIndividualClient(
                    new Uri(digitalHealthUri),
                    CreateProduct<nehta.mcaR32.ProviderSearchHIProviderDirectoryForIndividual.ProductType, nehta.mcaR32.ProviderSearchHIProviderDirectoryForIndividual.QualifiedId>(
                        productPlatform, productProductName, productProductVersion, productVendorId, productVendorQualifier),
                    CreateUser<nehta.mcaR32.ProviderSearchHIProviderDirectoryForIndividual.QualifiedId>(internalUserId, userQualifier, productProductName),
                    CreateHpio<nehta.mcaR32.ProviderSearchHIProviderDirectoryForIndividual.QualifiedId>(internalHPIO, hpioQualifier),
                    cert,
                    cert),

                // Healthcare Provider Directory – Search for Organisation Provider Directory Entry (TECH.SIS.HI.18)
                "6" => new ProviderSearchHIProviderDirectoryForOrganisationClient(
                    new Uri(digitalHealthUri),
                    CreateProduct<nehta.mcaR32.ProviderSearchHIProviderDirectoryForOrganisation.ProductType, nehta.mcaR32.ProviderSearchHIProviderDirectoryForOrganisation.QualifiedId>(
                        productPlatform, productProductName, productProductVersion, productVendorId, productVendorQualifier),
                    CreateUser<nehta.mcaR32.ProviderSearchHIProviderDirectoryForOrganisation.QualifiedId>(internalUserId, userQualifier, productProductName),
                    CreateHpio<nehta.mcaR32.ProviderSearchHIProviderDirectoryForOrganisation.QualifiedId>(internalHPIO, hpioQualifier),
                    cert,
                    cert),

                // Create Provisional IHI via B2B (TECH.SIS.HI.10)
                "7" => new ConsumerCreateProvisionalIHIClient(
                    new Uri(digitalHealthUri),
                    CreateProduct<nehta.mcaR3.ConsumerCreateProvisionalIHI.ProductType, nehta.mcaR3.ConsumerCreateProvisionalIHI.QualifiedId>(
                        productPlatform, productProductName, productProductVersion, productVendorId, productVendorQualifier),
                    CreateUser<nehta.mcaR3.ConsumerCreateProvisionalIHI.QualifiedId>(internalUserId, userQualifier, productProductName),
                    CreateHpio<nehta.mcaR3.ConsumerCreateProvisionalIHI.QualifiedId>(internalHPIO, hpioQualifier),
                    cert,
                    cert),

                // Create Unverified IHI via B2B (TECH.SIS.HI.11)
                "8" => new ConsumerCreateUnverifiedIHIClient(
                    new Uri(digitalHealthUri),
                    CreateProduct<nehta.mcaR302.ConsumerCreateUnverifiedIHI.ProductType, nehta.mcaR302.ConsumerCreateUnverifiedIHI.QualifiedId>(
                        productPlatform, productProductName, productProductVersion, productVendorId, productVendorQualifier),
                    CreateUser<nehta.mcaR302.ConsumerCreateUnverifiedIHI.QualifiedId>(internalUserId, userQualifier, productProductName),
                    CreateHpio<nehta.mcaR302.ConsumerCreateUnverifiedIHI.QualifiedId>(internalHPIO, hpioQualifier),
                    cert,
                    cert),

                // Update Provisional IHI via B2B (TECH.SIS.HI.03)
                "9" => new ConsumerUpdateProvisionalIHIClient(
                    new Uri(digitalHealthUri),
                    CreateProduct<nehta.mcaR3.ConsumerUpdateProvisionalIHI.ProductType, nehta.mcaR3.ConsumerUpdateProvisionalIHI.QualifiedId>(
                        productPlatform, productProductName, productProductVersion, productVendorId, productVendorQualifier),
                    CreateUser<nehta.mcaR3.ConsumerUpdateProvisionalIHI.QualifiedId>(internalUserId, userQualifier, productProductName),
                    CreateHpio<nehta.mcaR3.ConsumerUpdateProvisionalIHI.QualifiedId>(internalHPIO, hpioQualifier),
                    cert,
                    cert),

                // Update IHI via B2B (TECH.SIS.HI.05)
                "10" => new ConsumerUpdateIHIClient(
                    new Uri(digitalHealthUri),
                    CreateProduct<nehta.mcaR32.ConsumerUpdateIHI.ProductType, nehta.mcaR32.ConsumerUpdateIHI.QualifiedId>(
                        productPlatform, productProductName, productProductVersion, productVendorId, productVendorQualifier),
                    CreateUser<nehta.mcaR32.ConsumerUpdateIHI.QualifiedId>(internalUserId, userQualifier, productProductName),
                    CreateHpio<nehta.mcaR32.ConsumerUpdateIHI.QualifiedId>(internalHPIO, hpioQualifier),
                    cert,
                    cert),

                // Resolve Provisional IHI-Merge record via B2B (TECH.SIS.HI.08)
                "11" => new ConsumerMergeProvisionalIHIClient(
                    new Uri(digitalHealthUri),
                    CreateProduct<nehta.mcaR3.ConsumerMergeProvisionalIHI.ProductType, nehta.mcaR3.ConsumerMergeProvisionalIHI.QualifiedId>(
                        productPlatform, productProductName, productProductVersion, productVendorId, productVendorQualifier),
                    CreateUser<nehta.mcaR3.ConsumerMergeProvisionalIHI.QualifiedId>(internalUserId, userQualifier, productProductName),
                    CreateHpio<nehta.mcaR3.ConsumerMergeProvisionalIHI.QualifiedId>(internalHPIO, hpioQualifier),
                    cert,
                    cert),

                // Resolve Provisional IHI – Create Unverified IHI via B2B (TECH.SIS.HI.09)
                "12" => new ConsumerResolveProvisionalIHIClient(
                    new Uri(digitalHealthUri),
                    CreateProduct<nehta.mcaR302.ConsumerResolveProvisionalIHI.ProductType, nehta.mcaR302.ConsumerResolveProvisionalIHI.QualifiedId>(
                        productPlatform, productProductName, productProductVersion, productVendorId, productVendorQualifier),
                    CreateUser<nehta.mcaR302.ConsumerResolveProvisionalIHI.QualifiedId>(internalUserId, userQualifier, productProductName),
                    CreateHpio<nehta.mcaR302.ConsumerResolveProvisionalIHI.QualifiedId>(internalHPIO, hpioQualifier),
                    cert,
                    cert),

                // Notify of Duplicate IHI via B2B (TECH.SIS.HI.24)
                "13" => new ConsumerNotifyDuplicateIHIClient(
                    new Uri(digitalHealthUri),
                    CreateProduct<nehta.mcaR32.ConsumerNotifyDuplicateIHI.ProductType, nehta.mcaR32.ConsumerNotifyDuplicateIHI.QualifiedId>(
                        productPlatform, productProductName, productProductVersion, productVendorId, productVendorQualifier),
                    CreateUser<nehta.mcaR32.ConsumerNotifyDuplicateIHI.QualifiedId>(internalUserId, userQualifier, productProductName),
                    CreateHpio<nehta.mcaR32.ConsumerNotifyDuplicateIHI.QualifiedId>(internalHPIO, hpioQualifier),
                    cert,
                    cert),

                // Notify of Replica IHI via B2B (TECH.SIS.HI.25)
                "14" => new ConsumerNotifyReplicaIHIClient(
                    new Uri(digitalHealthUri),
                    CreateProduct<nehta.mcaR32.ConsumerNotifyReplicaIHI.ProductType, nehta.mcaR32.ConsumerNotifyReplicaIHI.QualifiedId>(
                        productPlatform, productProductName, productProductVersion, productVendorId, productVendorQualifier),
                    CreateUser<nehta.mcaR32.ConsumerNotifyReplicaIHI.QualifiedId>(internalUserId, userQualifier, productProductName),
                    CreateHpio<nehta.mcaR32.ConsumerNotifyReplicaIHI.QualifiedId>(internalHPIO, hpioQualifier),
                    cert,
                    cert),

                // Create verified IHI for Newborns (TECH.SIS.HI.26)
                "15" => new ConsumerCreateVerifiedIHIClient(
                    new Uri(digitalHealthUri),
                    CreateProduct<nehta.mcaR40.CreateVerifiedIHI.ProductType, nehta.mcaR40.CreateVerifiedIHI.QualifiedId>(
                        productPlatform, productProductName, productProductVersion, productVendorId, productVendorQualifier),
                    CreateUser<nehta.mcaR40.CreateVerifiedIHI.QualifiedId>(internalUserId, userQualifier, productProductName),
                    CreateHpio<nehta.mcaR40.CreateVerifiedIHI.QualifiedId>(internalHPIO, hpioQualifier),
                    cert,
                    cert),

                // Return null for an unknown clientType.
                _ => null
            };

            // Handle the case where the clientType is unknown or instantiation fails.
            if (client == null)
            {
                return new DigitalHealthClientResult { Error = "Unknown clientType or missing parameters. Please contact the system administrator." };
            }

            // Log the configuration of the created client for debugging and auditing.
            switch (clientType)
            {
                // IHI Inquiry Search via B2B (TECH.SIS.HI.06)
                case "1":
                    LogClientConfig<nehta.mcaR3.ConsumerSearchIHI.ProductType, nehta.mcaR3.ConsumerSearchIHI.QualifiedId>(
                        digitalHealthUri, productPlatform, productProductName, productProductVersion, productVendorId, productVendorQualifier,
                        internalUserId, userQualifier, internalHPIO, hpioQualifier, cert, context);
                    break;
                // Search for Provider Individual Details (TECH.SIS.HI.31)
                case "2":
                    LogClientConfig<nehta.mcaR50.ProviderSearchForProviderIndividual.ProductType, nehta.mcaR50.ProviderSearchForProviderIndividual.QualifiedId>(
                        digitalHealthUri, productPlatform, productProductName, productProductVersion, productVendorId, productVendorQualifier,
                        internalUserId, userQualifier, internalHPIO, hpioQualifier, cert, context);
                    break;
                // Search for Provider Organisation Details (TECH.SIS.HI.32)
                case "3":
                    LogClientConfig<nehta.mcaR50.ProviderSearchForProviderOrganisation.ProductType, nehta.mcaR50.ProviderSearchForProviderOrganisation.QualifiedId>(
                        digitalHealthUri, productPlatform, productProductName, productProductVersion, productVendorId, productVendorQualifier,
                        internalUserId, userQualifier, internalHPIO, hpioQualifier, cert, context);
                    break;
                // Read Provider Organisation Details (TECH.SIS.HI.16)
                case "4":
                    LogClientConfig<nehta.mcaR32.ProviderReadProviderOrganisation.ProductType, nehta.mcaR32.ProviderReadProviderOrganisation.QualifiedId>(
                        digitalHealthUri, productPlatform, productProductName, productProductVersion, productVendorId, productVendorQualifier,
                        internalUserId, userQualifier, internalHPIO, hpioQualifier, cert, context);
                    break;
                // Healthcare Provider Directory – Search for Individual Provider Directory Entry (TECH.SIS.HI.17)
                case "5":
                    LogClientConfig<nehta.mcaR32.ProviderSearchHIProviderDirectoryForIndividual.ProductType, nehta.mcaR32.ProviderSearchHIProviderDirectoryForIndividual.QualifiedId>(
                        digitalHealthUri, productPlatform, productProductName, productProductVersion, productVendorId, productVendorQualifier,
                        internalUserId, userQualifier, internalHPIO, hpioQualifier, cert, context);
                    break;
                // Healthcare Provider Directory – Search for Organisation Provider Directory Entry (TECH.SIS.HI.18)
                case "6":
                    LogClientConfig<nehta.mcaR32.ProviderSearchHIProviderDirectoryForOrganisation.ProductType, nehta.mcaR32.ProviderSearchHIProviderDirectoryForOrganisation.QualifiedId>(
                        digitalHealthUri, productPlatform, productProductName, productProductVersion, productVendorId, productVendorQualifier,
                        internalUserId, userQualifier, internalHPIO, hpioQualifier, cert, context);
                    break;
                // Create Provisional IHI via B2B (TECH.SIS.HI.10)
                case "7":
                    LogClientConfig<nehta.mcaR3.ConsumerCreateProvisionalIHI.ProductType, nehta.mcaR3.ConsumerCreateProvisionalIHI.QualifiedId>(
                        digitalHealthUri, productPlatform, productProductName, productProductVersion, productVendorId, productVendorQualifier,
                        internalUserId, userQualifier, internalHPIO, hpioQualifier, cert, context);
                    break;
                // Create Unverified IHI via B2B (TECH.SIS.HI.11)
                case "8":
                    LogClientConfig<nehta.mcaR302.ConsumerCreateUnverifiedIHI.ProductType, nehta.mcaR302.ConsumerCreateUnverifiedIHI.QualifiedId>(
                        digitalHealthUri, productPlatform, productProductName, productProductVersion, productVendorId, productVendorQualifier,
                        internalUserId, userQualifier, internalHPIO, hpioQualifier, cert, context);
                    break;
                // Update Provisional IHI via B2B (TECH.SIS.HI.03)
                case "9":
                    LogClientConfig<nehta.mcaR3.ConsumerUpdateProvisionalIHI.ProductType, nehta.mcaR3.ConsumerUpdateProvisionalIHI.QualifiedId>(
                        digitalHealthUri, productPlatform, productProductName, productProductVersion, productVendorId, productVendorQualifier,
                        internalUserId, userQualifier, internalHPIO, hpioQualifier, cert, context);
                    break;
                // Update IHI via B2B (TECH.SIS.HI.05)
                case "10":
                    LogClientConfig<nehta.mcaR32.ConsumerUpdateIHI.ProductType, nehta.mcaR32.ConsumerUpdateIHI.QualifiedId>(
                        digitalHealthUri, productPlatform, productProductName, productProductVersion, productVendorId, productVendorQualifier,
                        internalUserId, userQualifier, internalHPIO, hpioQualifier, cert, context);
                    break;
                // Resolve Provisional IHI-Merge record via B2B (TECH.SIS.HI.08)
                case "11":
                    LogClientConfig<nehta.mcaR3.ConsumerMergeProvisionalIHI.ProductType, nehta.mcaR3.ConsumerMergeProvisionalIHI.QualifiedId>(
                        digitalHealthUri, productPlatform, productProductName, productProductVersion, productVendorId, productVendorQualifier,
                        internalUserId, userQualifier, internalHPIO, hpioQualifier, cert, context);
                    break;
                // Resolve Provisional IHI – Create Unverified IHI via B2B (TECH.SIS.HI.09)
                case "12":
                    LogClientConfig<nehta.mcaR302.ConsumerResolveProvisionalIHI.ProductType, nehta.mcaR302.ConsumerResolveProvisionalIHI.QualifiedId>(
                        digitalHealthUri, productPlatform, productProductName, productProductVersion, productVendorId, productVendorQualifier,
                        internalUserId, userQualifier, internalHPIO, hpioQualifier, cert, context);
                    break;
                // Notify of Duplicate IHI via B2B (TECH.SIS.HI.24)
                case "13":
                    LogClientConfig<nehta.mcaR32.ConsumerNotifyDuplicateIHI.ProductType, nehta.mcaR32.ConsumerNotifyDuplicateIHI.QualifiedId>(
                        digitalHealthUri, productPlatform, productProductName, productProductVersion, productVendorId, productVendorQualifier,
                        internalUserId, userQualifier, internalHPIO, hpioQualifier, cert, context);
                    break;
                // Notify of Replica IHI via B2B (TECH.SIS.HI.25)
                case "14":
                    LogClientConfig<nehta.mcaR32.ConsumerNotifyReplicaIHI.ProductType, nehta.mcaR32.ConsumerNotifyReplicaIHI.QualifiedId>(
                        digitalHealthUri, productPlatform, productProductName, productProductVersion, productVendorId, productVendorQualifier,
                        internalUserId, userQualifier, internalHPIO, hpioQualifier, cert, context);
                    break;
                // Create verified IHI for Newborns (TECH.SIS.HI.26)
                case "15":
                    LogClientConfig<nehta.mcaR40.CreateVerifiedIHI.ProductType, nehta.mcaR40.CreateVerifiedIHI.QualifiedId>(
                        digitalHealthUri, productPlatform, productProductName, productProductVersion, productVendorId, productVendorQualifier,
                        internalUserId, userQualifier, internalHPIO, hpioQualifier, cert, context);
                    break;
            }

            context.Logger.LogInformation("GetClientAsync function completed");

            // Return the successfully created client.
            return new DigitalHealthClientResult { DigitalHealthClient = client, Error = null };
        }

        /// <summary>
        /// Retrieves a batch of parameters from AWS Systems Manager (SSM) Parameter Store.
        /// </summary>
        /// <param name="names">An array of parameter names to retrieve.</param>
        /// <param name="context">The Lambda context for logging.</param>
        /// <returns>A dictionary of parameter names and their values.</returns>
        /// <exception cref="Exception">Thrown if any parameters cannot be retrieved or are missing.</exception>
        private static async Task<Dictionary<string, string>> GetSsmParametersAsync(string[] names, ILambdaContext context)
        {
            context.Logger.LogInformation("GetSsmParametersAsync function started");

            var result = new Dictionary<string, string>();
            using var ssmClient = new AmazonSimpleSystemsManagementClient();
            const int batchSize = 10; // SSM GetParameters API has a limit of 10 parameters per request.
            for (int i = 0; i < names.Length; i += batchSize)
            {
                var batch = names.Skip(i).Take(batchSize).ToList();
                var request = new GetParametersRequest
                {
                    Names = batch,
                    WithDecryption = true // Decrypt SecureString parameters.
                };

                try
                {
                    var response = await ssmClient.GetParametersAsync(request);
                    foreach (var param in response.Parameters)
                    {
                        result[param.Name] = param.Value;
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"SSM parameters error: {ex.Message}");
                }
            }

            // Ensure all requested parameters were successfully retrieved.
            var missing = names.Except(result.Keys).ToList();
            if (missing.Any())
            {
                throw new Exception($"Missing SSM parameters: {string.Join(", ", missing)}");
            }

            context.Logger.LogInformation("GetSsmParametersAsync function completed");

            return result;
        }

        /// <summary>
        /// Retrieves a certificate from an S3 bucket and loads it into an X509Certificate2 object.
        /// </summary>
        /// <param name="bucket">The S3 bucket name.</param>
        /// <param name="key">The S3 object key (file path).</param>
        /// <param name="password">The password for the certificate file.</param>
        /// <param name="context">The Lambda context for logging.</param>
        /// <returns>An <see cref="X509Certificate2"/> instance.</returns>
        /// <exception cref="Exception">Thrown if the certificate cannot be retrieved from S3.</exception>
        private static async Task<X509Certificate2> GetCertificateAsync(string bucket, string key, string password, ILambdaContext context)
        {
            context.Logger.LogInformation("GetCertificateAsync function started");

            var certBytes = Array.Empty<byte>();
            try
            {
                using var s3Client = new AmazonS3Client(Amazon.RegionEndpoint.APSoutheast2);
                var getRequest = new GetObjectRequest { BucketName = bucket, Key = key };
                using var getResponse = await s3Client.GetObjectAsync(getRequest);
                using var memoryStream = new MemoryStream();
                await getResponse.ResponseStream.CopyToAsync(memoryStream);
                certBytes = memoryStream.ToArray();
            }
            catch (Exception ex)
            {
                throw new Exception($"Get Certificate from S3 error: {ex.Message}");
            }

            context.Logger.LogInformation("GetCertificateAsync function completed");

            // Load the certificate bytes with the provided password.
            return new X509Certificate2(certBytes, password, X509KeyStorageFlags.Exportable);
        }

        /// <summary>
        /// Creates a generic Product object using reflection to set its properties.
        /// </summary>
        /// <typeparam name="TProduct">The type of the product object to create.</typeparam>
        /// <typeparam name="TVendor">The type of the vendor object, which is part of the product.</typeparam>
        /// <returns>A configured product object of type TProduct.</returns>
        private static TProduct CreateProduct<TProduct, TVendor>(string platform, string productName, string productVersion, string vendorId, string vendorQualifier)
            where TProduct : new()
            where TVendor : new()
        {
            var product = new TProduct();
            var vendor = new TVendor();

            // Use reflection to set properties for the vendor object.
            var vendorType = typeof(TVendor);
            var idProp = vendorType.GetProperty("id");
            var qualifierProp = vendorType.GetProperty("qualifier");
            idProp?.SetValue(vendor, vendorId);
            qualifierProp?.SetValue(vendor, vendorQualifier);

            // Use reflection to set properties for the product object.
            var productType = typeof(TProduct);
            var platformProp = productType.GetProperty("platform");
            var productNameProp = productType.GetProperty("productName");
            var productVersionProp = productType.GetProperty("productVersion");
            var vendorProp = productType.GetProperty("vendor");
            platformProp?.SetValue(product, platform);
            productNameProp?.SetValue(product, productName);
            productVersionProp?.SetValue(product, productVersion);
            vendorProp?.SetValue(product, vendor);

            return product;
        }

        /// <summary>
        /// Creates a generic User object using reflection to set its properties.
        /// </summary>
        /// <typeparam name="TUser">The type of the user object to create.</typeparam>
        /// <returns>A configured user object of type TUser.</returns>
        private static TUser CreateUser<TUser>(string internalUserId, string userQualifier, string productProductName)
            where TUser : new()
        {
            var user = new TUser();
            var idProp = typeof(TUser).GetProperty("id");
            var qualifierProp = typeof(TUser).GetProperty("qualifier");
            idProp?.SetValue(user, internalUserId);
            // The user qualifier often contains a placeholder for the application name.
            qualifierProp?.SetValue(user, userQualifier.Replace("{appname}", productProductName.Replace(" ", "").ToLower()));
            return user;
        }

        /// <summary>
        /// Creates a generic HPIO (Healthcare Provider Identifier - Organisation) object using reflection.
        /// </summary>
        /// <typeparam name="THpio">The type of the HPIO object to create.</typeparam>
        /// <returns>A configured HPIO object of type THpio, or null if the input HPIO is empty.</returns>
        private static THpio? CreateHpio<THpio>(string internalHPIO, string hpioQualifier)
            where THpio : class, new()
        {
            if (string.IsNullOrWhiteSpace(internalHPIO))
                return null;

            var hpio = new THpio();
            var idProp = typeof(THpio).GetProperty("id");
            var qualifierProp = typeof(THpio).GetProperty("qualifier");
            idProp?.SetValue(hpio, internalHPIO);
            qualifierProp?.SetValue(hpio, hpioQualifier.ToLower());
            return hpio;
        }

        /// <summary>
        /// Logs the configuration of a created client as an XML string for auditing and debugging.
        /// </summary>
        /// <typeparam name="TProduct">The product type of the client.</typeparam>
        /// <typeparam name="TQualifiedId">The qualified ID type for User and HPIO.</typeparam>
        private void LogClientConfig<TProduct, TQualifiedId>(
                                                            string endpoint,
                                                            string productPlatform,
                                                            string productProductName,
                                                            string productProductVersion,
                                                            string productVendorId,
                                                            string productVendorQualifier,
                                                            string internalUserId,
                                                            string userQualifier,
                                                            string internalHPIO,
                                                            string hpioQualifier,
                                                            X509Certificate2 tlsCert,
                                                            ILambdaContext context)
                                                    where TProduct : class, new()
                                                    where TQualifiedId : class, new()
        {
            context.Logger.LogInformation("LogClientConfig function started");

            var product = CreateProduct<TProduct, TQualifiedId>(productPlatform, productProductName, productProductVersion, productVendorId, productVendorQualifier);

            var user = CreateUser<TQualifiedId>(internalUserId, userQualifier, productProductName);

            var hpio = CreateHpio<TQualifiedId>(internalHPIO, hpioQualifier);

            // Populate the configuration object for serialization.
            var config = new ClientConfig<TProduct, TQualifiedId>
            {
                Endpoint = endpoint,
                Product = product,
                User = user,
                Hpio = hpio,
                CertificateThumbprint = tlsCert.Thumbprint,
                CertificateSerial = tlsCert.SerialNumber
            };

            // Serialize the configuration to XML and log it.
            var configSerializer = new XmlSerializer(typeof(ClientConfig<TProduct, TQualifiedId>));
            using (var stringWriter = new StringWriter())
            {
                configSerializer.Serialize(stringWriter, config);
                context.Logger.LogInformation($"Client Object XML: {stringWriter.ToString()}");
            }

            context.Logger.LogInformation("LogClientConfig function completed");
        }
    }
}