using Amazon.Lambda.Core;
using Nehta.VendorLibrary.Common;
using Nehta.VendorLibrary.HI;
using HI.BetterClinics.HelperFunctions;
using nehta.mcaR32.ProviderSearchHIProviderDirectoryForIndividual;
using nehta.mcaR50.ProviderSearchForProviderIndividual;
using Newtonsoft.Json;
using System.Net;
using System.ServiceModel;
using System.Text.Json;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace HI.BetterClinics.Provider
{
    /// <summary>
    /// Represents the AWS Lambda function for performing Healthcare Identifiers (HI) services related to Providers.
    /// </summary>
    public class Function
    {
        private const string FunctionName = "HI_BetterClinics_Provider";

        /// <summary>
        /// The main entry point for the AWS Lambda function.
        /// It handles the entire process of :
        ///     1) receiving a request
        ///     2) creating a client
        ///     3) calling a service
        ///     4) returning a formatted response
        /// 
        /// Input Structure:
        /// {
        ///    "internalMode": "1",                                 // Determines which HI service to call (mandatory)
        ///    "internalUserId": "userId",                          // User identifier for authentication (mandatory)
        ///    "internalHPIO": "hpioNumber",                        // Healthcare Provider Organisation Identifier (mandatory)
        ///    ...
        ///    ...other mandatory/optional fields as per the service being called
        /// }
        ///
        /// Output Structure:
        /// {
        ///     "status": "string",                             // Overall status of the operation (ie. SUCCESS/FAILURE)
        ///     "output": {
        ///                 "severity": "string",               // Severity of the message (ie. INFO/WARN/ERROR)
        ///                 "code": "string",                   // Specific code for the message (if applicable)
        ///                 "reason": {},                       // Formatted output results of service
        ///               },
        ///     "awsFunction": "string",                        // Name of the AWS Lambda function (eg. HI.BetterClinics.Consumer)
        ///     "apiXmlRequest": {},                            // The raw SOAP request XML (if applicable)
        ///     "apiXmlResponse": {}                            // The raw SOAP response XML (if applicable)
        /// }        
        /// </summary>
        public async Task<string> FunctionHandler(JsonElement input, ILambdaContext context)
        {
            try
            {
                context.Logger.LogInformation($"{FunctionName} function started...");

                context.Logger.LogInformation($"Input Data : {input.GetRawText()}");

                // Ensure that all outbound connections use TLS 1.2 or 1.3 for security.
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;

                // Extract core parameters required for all operations.
                var internalMode = clsHelper.GetStringProperty(input, "internalMode");
                var internalUserId = clsHelper.GetStringProperty(input, "internalUserId");
                var internalHPIO = clsHelper.GetStringProperty(input, "internalHPIO");

                // Validate that essential parameters are provided.
                if (string.IsNullOrWhiteSpace(internalMode))
                {
                    return clsHelper.FormatOutput(FunctionName, "FAILURE", "ERROR", "PARAM", "Input Parameters - Missing or empty internalMode.", null, context);
                }

                if (string.IsNullOrWhiteSpace(internalUserId))
                {
                    return clsHelper.FormatOutput(FunctionName, "FAILURE", "ERROR", "PARAM", "Input Parameters - Missing or empty internalUserId.", null, context);
                }

                if (string.IsNullOrWhiteSpace(internalHPIO))
                {
                    return clsHelper.FormatOutput(FunctionName, "FAILURE", "ERROR", "PARAM", "Input Parameters - Missing or empty internalHPIO.", null, context);
                }

                // Determine the client type based on the internalMode. This maps to different HI service specifications.
                string clientType = "2";
                if (internalMode == "1")        // Search for Provider Individual Details (TECH.SIS.HI.31)
                {
                    clientType = "2";      
                }
                else if (internalMode == "2")   // Healthcare Provider Directory – Search for Individual Provider Directory Entry (TECH.SIS.HI.17)
                {
                    clientType = "5";
                }

                // Configure the Digital Health Client loader, which handles certificate authentication.
                var digitalHealthClientInput = JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(new
                {
                    clientType,
                    internalUserId,
                    internalHPIO
                })).RootElement;

                // Instantiate the client factory and create the service client.
                var digitalHealthClientLoader = new clsDigitalHealthClient();
                var digitalHealthClientResult = (DigitalHealthClientResult)await digitalHealthClientLoader.GetClientAsync(digitalHealthClientInput, context);
                if (!digitalHealthClientResult.IsSuccess)
                {
                    return clsHelper.FormatOutput(FunctionName, "FAILURE", "ERROR", "CERTIFICATE", digitalHealthClientResult.Error, null, context);
                }

                // Route to the correct HI Service based on the internalMode parameter
                if (internalMode == "1")        // Search for Provider Individual Details (TECH.SIS.HI.31)
                {
                    // Cast the generic client object to the specific ProviderSearchForProviderIndividualClient.
                    var objClient = (ProviderSearchForProviderIndividualClient)digitalHealthClientResult.DigitalHealthClient;
                    if (objClient == null)
                    {
                        return clsHelper.FormatOutput(FunctionName, "FAILURE", "ERROR", "CERTIFICATE", "The client certificate was not loaded successfully.", null, context);
                    }

                    // Construct the SOAP request object from the input JSON.
                    var request = BuildSearchRequest1(input);

                    // Determine which search type to use and clear irrelevant fields (if applicable)
                    var searchType = GetSearchTypeAndClearFields1(request);
                    if (searchType is null)
                    {
                        return clsHelper.FormatOutput(FunctionName, "FAILURE", "ERROR", "PARAM", "The HPII search request does not contain the minimum search criteria. Please check all input fields and resubmit.", null, context);
                    }

                    // Call the appropriate HI service operation.
                    try
                    {
                        context.Logger.LogInformation($"{searchType} Request Data : {System.Text.Json.JsonSerializer.Serialize(request)}");

                        var hpiiResponse = await objClient.ProviderIndividualSearchAsync(request);

                        // Configure JSON serialization settings for the output
                        var settings = new JsonSerializerSettings
                        {
                            StringEscapeHandling = StringEscapeHandling.Default,
                            Formatting = Newtonsoft.Json.Formatting.None,
                            Converters = { new Newtonsoft.Json.Converters.StringEnumConverter() }
                        };

                        // Format and return the successful response.
                        return clsHelper.FormatOutput(FunctionName, "SUCCESS", "INFO", "", JsonConvert.SerializeObject(hpiiResponse.searchForProviderIndividualResult, settings), objClient, context);
                    }
                    catch (FaultException fex)
                    {
                        // Handle specific SOAP fault exceptions.
                        return clsHelper.HandleFaultException(FunctionName, fex, objClient, context);
                    }
                    catch (Exception ex)
                    {
                        // Handle other general exceptions.
                        return clsHelper.FormatOutput(FunctionName, "FAILURE", "ERROR", "", ex.Message, objClient, context);
                    }
                }
                else if (internalMode == "2")       // Healthcare Provider Directory – Search for Individual Provider Directory Entry (TECH.SIS.HI.17)
                {
                    // Cast the generic client object to the specific ProviderSearchHIProviderDirectoryForIndividualClient.
                    var objClient = (ProviderSearchHIProviderDirectoryForIndividualClient)digitalHealthClientResult.DigitalHealthClient;

                    if (objClient == null)
                    {
                        return clsHelper.FormatOutput(FunctionName, "FAILURE", "ERROR", "CERTIFICATE", "The client certificate was not loaded successfully.", null, context);
                    }

                    // Construct the SOAP request object from the input JSON.
                    var request = BuildSearchRequest2(input);

                    // Determine which search type to use and clear irrelevant fields (if applicable)
                    var searchType = GetSearchTypeAndClearFields2(request);
                    if (searchType is null)
                    {
                        return clsHelper.FormatOutput(FunctionName, "FAILURE", "ERROR", "PARAM", "The HPII search request does not contain the minimum search criteria. Please check all input fields and resubmit.", null, context);
                    }

                    // Call the appropriate HI service operation.
                    try
                    {
                        context.Logger.LogInformation($"{searchType} Request Data : {System.Text.Json.JsonSerializer.Serialize(request)}");

                        var hpiiResponse = await objClient.IdentifierSearchAsync(request);

                        // Configure JSON serialization settings for the output
                        var settings = new JsonSerializerSettings
                        {
                            StringEscapeHandling = StringEscapeHandling.Default,
                            Formatting = Newtonsoft.Json.Formatting.None,
                            Converters = { new Newtonsoft.Json.Converters.StringEnumConverter() }
                        };

                        // Format and return the successful response.
                        return clsHelper.FormatOutput(FunctionName, "SUCCESS", "INFO", "", JsonConvert.SerializeObject(hpiiResponse.searchHIProviderDirectoryForIndividualResult, settings), objClient, context);
                    }
                    catch (FaultException fex)
                    {
                        // Handle specific SOAP fault exceptions.
                        return clsHelper.HandleFaultException(FunctionName, fex, objClient, context);
                    }
                    catch (Exception ex)
                    {
                        // Handle any other general exceptions during the service call.
                        return clsHelper.FormatOutput(FunctionName, "FAILURE", "ERROR", "", ex.Message, objClient, context);
                    }
                }

                // If the mode is not recognized, return null.
                return null;
            }
            catch (Exception ex)
            {
                // Catch-all for any unhandled exceptions during function execution.
                return clsHelper.FormatOutput(FunctionName, "FAILURE", "ERROR", "", ex.Message, null, context);
            }
        }

        /// <summary>
        /// Builds the request object for the 'searchForProviderIndividual' (Mode 1) operation.
        /// </summary>
        /// <param name="root">The root JsonElement of the input payload.</param>
        /// <returns>A populated <see cref="searchForProviderIndividual"/> object.</returns>
        private static searchForProviderIndividual BuildSearchRequest1(JsonElement root)
        {
            var request = new searchForProviderIndividual
            {
                hpiiNumber = clsHelper.TryGetQualifiedString(root, "hpiiNumberField", HIQualifiers.HPIIQualifier),
                registrationId = clsHelper.GetStringProperty(root, "registrationIdField"),
                familyName = clsHelper.GetStringProperty(root, "familyNameField"),
                givenName = GetGivenNames(root),
                searchAustralianAddress = clsHelper.GetAustralianAddressUnstructured<SearchAustralianAddressType, nehta.mcaR50.ProviderSearchForProviderIndividual.StateType>(root),
                searchInternationalAddress = null // International address search is not implemented.
            };

            // Safely parse and set the date of birth if it exists and is valid.
            if (root.TryGetProperty("dateOfBirthField", out var dobElem) &&
                dobElem.ValueKind == JsonValueKind.String &&
                DateTime.TryParse(dobElem.GetString(), out var dob))
            {
                request.dateOfBirth = dob;
                request.dateOfBirthSpecified = true;
            }

            // Safely parse and set the sex if it exists and is a valid enum value.
            if (root.TryGetProperty("sexField", out var sexElem) &&
                sexElem.ValueKind == JsonValueKind.String &&
                Enum.TryParse<nehta.mcaR50.ProviderSearchForProviderIndividual.SexType>(sexElem.GetString(), true, out var sexEnum))
            {
                request.sex = sexEnum;
                request.sexSpecified = true;
            }

            return request;
        }

        /// <summary>
        /// Builds the request object for the 'searchHIProviderDirectoryForIndividual' (Mode 2) operation.
        /// </summary>
        /// <param name="root">The root JsonElement of the input payload.</param>
        /// <returns>A populated <see cref="searchHIProviderDirectoryForIndividual"/> object.</returns>
        private static searchHIProviderDirectoryForIndividual BuildSearchRequest2(JsonElement root)
        {
            var request = new searchHIProviderDirectoryForIndividual
            {
                hpiiNumber = clsHelper.TryGetQualifiedString(root, "hpiiNumberField", HIQualifiers.HPIIQualifier)
            };

            return request;
        }

        /// <summary>
        /// Extracts the given name from the input and returns it as a string array, as required by the SOAP service.
        /// </summary>
        /// <param name="root">The root JsonElement of the input payload.</param>
        /// <returns>A string array containing the given name, or null if not provided.</returns>
        private static string[] GetGivenNames(JsonElement root)
        {
            var givenName = clsHelper.GetStringProperty(root, "givenNameField");
            return !string.IsNullOrWhiteSpace(givenName) ? new[] { givenName } : null;
        }

        /// <summary>
        /// Determines the search type for Mode 1 and clears other search fields to comply with HI service rules,
        /// which only allow one type of search per request (e.g., by HPI-I, by registration ID, or by demographics).
        /// </summary>
        /// <param name="request">The request object to be modified.</param>
        /// <returns>A string indicating the search type, or null if minimum criteria are not met.</returns>
        private static string GetSearchTypeAndClearFields1(searchForProviderIndividual request)
        {
            // The HI service requires that only one set of search criteria is provided.
            // This logic enforces that rule by checking criteria in a specific order of precedence.

            // 1. Search by HPI-I (highest precedence).
            if (request.hpiiNumber != null)
            {
                request.registrationId = null;
                request.searchAustralianAddress = null;
                request.searchInternationalAddress = null;
                return "ProviderIndividualSearch (by HPI-I)";
            }
            // 2. Search by Registration ID.
            if (request.registrationId != null)
            {
                request.hpiiNumber = null;
                request.searchAustralianAddress = null;
                request.searchInternationalAddress = null;
                return "ProviderIndividualSearch (by Registration ID)";
            }
            // 3. Search by Demographics (requires at least family name).
            if (request.familyName != null)
            {
                request.hpiiNumber = null;
                request.registrationId = null;
                request.searchInternationalAddress = null;
                return "ProviderIndividualSearch (by Demographics)";
            }
            // If none of the above criteria are met, the search is invalid.
            return null;
        }

        /// <summary>
        /// Determines the search type for Mode 2. This service only supports searching by HPI-I.
        /// </summary>
        /// <param name="request">The request object.</param>
        /// <returns>A string indicating the search type, or null if criteria are not met.</returns>
        private static string GetSearchTypeAndClearFields2(searchHIProviderDirectoryForIndividual request)
        {
            // This service only allows searching by HPI-I.
            if (request.hpiiNumber != null)
            {
                return "IdentifierSearch";
            }
            return null;
        }
    }
}
