using Amazon.Lambda.Core;
using HI.BetterClinics.HelperFunctions;
using Nehta.VendorLibrary.Common;
using Nehta.VendorLibrary.HI;
using nehta.mcaR32.ProviderReadProviderOrganisation;
using nehta.mcaR50.ProviderSearchForProviderOrganisation;
using nehta.mcaR32.ProviderSearchHIProviderDirectoryForOrganisation;
using Newtonsoft.Json;
using System.Net;
using System.ServiceModel;
using System.Text.Json;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace HI.BetterClinics.Organisation
{
    /// <summary>
    /// Represents the AWS Lambda function for performing Healthcare Identifiers (HI) services related to Organisations.
    /// </summary>
    public class Function
    {
        private const string FunctionName = "HI.BetterClinics.Organisation";

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
                string clientType = "3";
                if (internalMode == "1")        // Search for Provider Organisation Details (TECH.SIS.HI.32)
                {
                    clientType = "3";
                }
                else if (internalMode == "2")   // Read Provider Organisation Details (TECH.SIS.HI.16)
                {
                    clientType = "4";
                }
                else if (internalMode == "3")   // Healthcare Provider Directory – Search for Organisation Provider Directory Entry (TECH.SIS.HI.18)
                {
                    clientType = "6";
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
                if (internalMode == "1")        // Search for Provider Organisation Details (TECH.SIS.HI.32)
                {
                    // Cast the generic client object to the specific ProviderSearchForProviderOrganisationClient.
                    var objClient = (ProviderSearchForProviderOrganisationClient)digitalHealthClientResult.DigitalHealthClient;
                    if (objClient == null)
                    {
                        return clsHelper.FormatOutput(FunctionName, "FAILURE", "ERROR", "CERTIFICATE", "The client certificate was not loaded successfully.", null, context);
                    }

                    // Construct the SOAP request object from the input JSON.
                    var request = BuildSearchRequest1(input);
                    var searchType = "ProviderOrganisationSearch";

                    // Call the appropriate HI service operation.
                    try
                    {
                        context.Logger.LogInformation($"{searchType} Request Data : {System.Text.Json.JsonSerializer.Serialize(request)}");

                        var hpioResponse = await objClient.ProviderOrganisationSearchAsync(request);

                        // Configure JSON serialization settings for the output
                        var settings = new JsonSerializerSettings
                        {
                            StringEscapeHandling = StringEscapeHandling.Default,
                            Formatting = Newtonsoft.Json.Formatting.None,
                            Converters = { new Newtonsoft.Json.Converters.StringEnumConverter() }
                        };

                        // Format and return the successful response.
                        return clsHelper.FormatOutput(FunctionName, "SUCCESS", "INFO", "", JsonConvert.SerializeObject(hpioResponse.searchForProviderOrganisationResult, settings), objClient, context);
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
                else if (internalMode == "2")       // Read Provider Organisation Details (TECH.SIS.HI.16)
                {
                    // Cast the generic client object to the specific ProviderReadProviderOrganisationClient.
                    var objClient = (ProviderReadProviderOrganisationClient)digitalHealthClientResult.DigitalHealthClient;
                    if (objClient == null)
                    {
                        return clsHelper.FormatOutput(FunctionName, "FAILURE", "ERROR", "CERTIFICATE", "The client certificate was not loaded successfully.", null, context);
                    }

                    // Construct the SOAP request object from the input JSON.
                    var request = BuildSearchRequest2(input);
                    var searchType = "ProviderOrganisationRead";

                    // Call the appropriate HI service operation.
                    try
                    {
                        context.Logger.LogInformation($"{searchType} Request Data : {System.Text.Json.JsonSerializer.Serialize(request)}");

                        var hpioResponse = await objClient.ReadProviderOrganisationAsync(request);

                        // Configure JSON serialization settings for the output
                        var settings = new JsonSerializerSettings
                        {
                            StringEscapeHandling = StringEscapeHandling.Default,
                            Formatting = Newtonsoft.Json.Formatting.None,
                            Converters = { new Newtonsoft.Json.Converters.StringEnumConverter() }
                        };

                        // Format and return the successful response.
                        return clsHelper.FormatOutput(FunctionName, "SUCCESS", "INFO", "", JsonConvert.SerializeObject(hpioResponse.readProviderOrganisationResult, settings), objClient, context);
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
                else if (internalMode == "3")       // Healthcare Provider Directory – Search for Organisation Provider Directory Entry (TECH.SIS.HI.18)
                {
                    // Cast the generic client object to the specific ProviderSearchHIProviderDirectoryForOrganisationClient.
                    var objClient = (ProviderSearchHIProviderDirectoryForOrganisationClient)digitalHealthClientResult.DigitalHealthClient;
                    if (objClient == null)
                    {
                        return clsHelper.FormatOutput(FunctionName, "FAILURE", "ERROR", "CERTIFICATE", "The client certificate was not loaded successfully.", null, context);
                    }

                    // Construct the SOAP request object from the input JSON.
                    var request = BuildSearchRequest3(input);

                    // Determine which search type to use and clear irrelevant fields (if applicable)
                    var searchType = GetSearchTypeAndClearFields3(request);
                    if (searchType is null)
                    {
                        return clsHelper.FormatOutput(FunctionName, "FAILURE", "PARAM", "CERTIFICATE", "The HPIO search request does not contain the minimum search criteria. Please check all input fields and resubmit.", null, context);
                    }

                    // Call the appropriate HI service operation.
                    try
                    {
                        context.Logger.LogInformation($"{searchType} Request Data : {System.Text.Json.JsonSerializer.Serialize(request)}");

                        var hpioResponse = await objClient.IdentifierSearchAsync(request);

                        // Configure JSON serialization settings for the output
                        var settings = new JsonSerializerSettings
                        {
                            StringEscapeHandling = StringEscapeHandling.Default,
                            Formatting = Newtonsoft.Json.Formatting.None,
                            Converters = { new Newtonsoft.Json.Converters.StringEnumConverter() }
                        };

                        // Format and return the successful response.
                        return clsHelper.FormatOutput(FunctionName, "SUCCESS", "INFO", "", JsonConvert.SerializeObject(hpioResponse.searchHIProviderDirectoryForOrganisationResult, settings), objClient, context);
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
        /// Builds a search request for the 'ProviderOrganisationSearch' operation (Mode 1).
        /// </summary>
        /// <param name="root">The root JsonElement of the input payload.</param>
        /// <returns>A <see cref="searchForProviderOrganisation"/> request object.</returns>
        private static searchForProviderOrganisation BuildSearchRequest1(JsonElement root)
        {
            var request = new searchForProviderOrganisation
            {
                hpioNumber = clsHelper.TryGetQualifiedString(root, "hpioNumberField", HIQualifiers.HPIOQualifier)
            };
            return request;
        }

        /// <summary>
        /// Builds a search request for the 'ReadProviderOrganisation' operation (Mode 2).
        /// </summary>
        /// <param name="root">The root JsonElement of the input payload.</param>
        /// <returns>A <see cref="readProviderOrganisation"/> request object.</returns>
        private static readProviderOrganisation BuildSearchRequest2(JsonElement root)
        {
            var request = new readProviderOrganisation
            {
                hpioNumber = clsHelper.TryGetQualifiedString(root, "hpioNumberField", HIQualifiers.HPIOQualifier),
                linkSearchType = "All"
            };
            return request;
        }

        /// <summary>
        /// Builds a search request for the 'SearchHIProviderDirectoryForOrganisation' operation (Mode 3).
        /// </summary>
        /// <param name="root">The root JsonElement of the input payload.</param>
        /// <returns>A <see cref="searchHIProviderDirectoryForOrganisation"/> request object.</returns>
        private static searchHIProviderDirectoryForOrganisation BuildSearchRequest3(JsonElement root)
        {
            var request = new searchHIProviderDirectoryForOrganisation
            {
                hpioNumber = clsHelper.TryGetQualifiedString(root, "hpioNumberField", HIQualifiers.HPIOQualifier),
                name = clsHelper.TruncateString(clsHelper.GetStringProperty(root, "nameField"), 200),
                organisationType = clsHelper.GetStringProperty(root, "organisationTypeField"),
                serviceType = clsHelper.GetStringProperty(root, "serviceTypeField"),
                unitType = clsHelper.GetStringProperty(root, "unitTypeField"),
                organisationDetails = GetOrganisationDetails(root),
                australianAddressCriteria = clsHelper.GetAustralianAddressUnstructured<AustralianAddressCriteriaType, nehta.mcaR32.ProviderSearchHIProviderDirectoryForOrganisation.StateType>(root),
                internationalAddressCriteria = null,                                // Not implemented in this version.
                linkSearchType = "All"
            };
            return request;
        }

        /// <summary>
        /// Determines the search type for a Mode 3 request and clears irrelevant fields to ensure
        /// that only one search criterion is used, as required by the HI service.
        /// </summary>
        /// <param name="request">The search request to process.</param>
        /// <returns>A string indicating the search type, or null if no valid criteria are provided.</returns>
        private static string GetSearchTypeAndClearFields3(searchHIProviderDirectoryForOrganisation request)
        {
            // If HPIO number is provided, it takes precedence. Clear all other fields.
            if (request.hpioNumber != null)
            {
                request.name = null;
                request.organisationType = null;
                request.serviceType = null;
                request.unitType = null;
                request.organisationDetails = null;
                request.australianAddressCriteria = null;
                return "ProviderOrganisationSearch";
            }

            // ***Note***
            // Searching by 'name' is not fully supported in the current implementation.
            if (request.name != null)
            {
                request.hpioNumber = null;
                return "ProviderOrganisationSearch";
            }

            // If no valid search criteria are found, return null.
            return null;
        }

        /// <summary>
        /// Extracts organisation details (ABN, ACN) from the JSON input payload.
        /// </summary>
        /// <param name="root">The root JsonElement of the input payload.</param>
        /// <returns>An <see cref="OrganisationDetails"/> object, or null if not present.</returns>
        private static nehta.mcaR32.ProviderSearchHIProviderDirectoryForOrganisation.OrganisationDetails GetOrganisationDetails(JsonElement root)
        {
            if (root.TryGetProperty("organisationDetailsField", out var addrRoot) &&
                addrRoot.ValueKind == JsonValueKind.Object)
            {
                var orgDetails = new nehta.mcaR32.ProviderSearchHIProviderDirectoryForOrganisation.OrganisationDetails
                {
                    australianBusinessNumber = clsHelper.GetStringProperty(addrRoot, "australianBusinessNumberField"),
                    australianCompanyNumber = clsHelper.GetStringProperty(addrRoot, "australianCompanyNumberField")
                };

                return orgDetails;
            }
            return null;
        }
    }
}
