using Amazon.Lambda.Core;
using HI.BetterClinics.HelperFunctions;
using Nehta.VendorLibrary.Common;
using Nehta.VendorLibrary.HI;
using nehta.mcaR3.ConsumerCreateProvisionalIHI;
using nehta.mcaR3.ConsumerSearchIHI;
using nehta.mcaR302.ConsumerCreateUnverifiedIHI;
using nehta.mcaR3.ConsumerMergeProvisionalIHI;
using nehta.mcaR40.CreateVerifiedIHI;
using Newtonsoft.Json;
using System.Net;
using System.ServiceModel;
using System.Text.Json;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace HI.BetterClinics.Consumer
{
    /// <summary>
    /// Represents the AWS Lambda function for performing Healthcare Identifiers (HI) services related to Consumers.
    /// </summary>
    public class Function
    {
        private const string FunctionName = "HI.BetterClinics.Consumer";

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
                    return clsHelper.FormatOutput(FunctionName, "FAILURE", "ERROR", "PARAM", "Input Parameters - Missing or empty internalModeA.", null, context);
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
                string clientType = "1";
                if (internalMode == "1")        // IHI Inquiry Search via B2B (TECH.SIS.HI.06)
                {
                    clientType = "1";
                }
                else if (internalMode == "2")    // Create Provisional IHI via B2B (TECH.SIS.HI.10)
                {
                    clientType = "7";
                }
                else if (internalMode == "3")    // Create Unverified IHI via B2B (TECH.SIS.HI.11)
                {
                    clientType = "8";
                }
                else if (internalMode == "4")    // Update Provisional IHI via B2B (TECH.SIS.HI.03)
                {
                    clientType = "9";
                }
                else if (internalMode == "5")    // Update IHI via B2B (TECH.SIS.HI.05)
                {
                    clientType = "10";
                }
                else if (internalMode == "6")    // Resolve Provisional IHI-Merge record via B2B (TECH.SIS.HI.08)
                {
                    clientType = "11";
                }
                else if (internalMode == "7")    // Resolve Provisional IHI – Create Unverified IHI via B2B (TECH.SIS.HI.09)
                {
                    clientType = "12";
                }
                else if (internalMode == "8")    // Notify of Duplicate IHI via B2B (TECH.SIS.HI.24)
                {
                    clientType = "13";
                }
                else if (internalMode == "9")    // Notify of Replica IHI via B2B (TECH.SIS.HI.25)
                {
                    clientType = "14";
                }
                else if (internalMode == "10")    // Create verified IHI for Newborns (TECH.SIS.HI.26)
                {
                    clientType = "15";
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
                if (internalMode == "1")        // IHI Inquiry Search via B2B (TECH.SIS.HI.06)
                {
                    // Cast the generic client object to the specific ConsumerSearchIHIClient.
                    var objClient = (ConsumerSearchIHIClient)digitalHealthClientResult.DigitalHealthClient;
                    if (objClient == null)
                    {
                        return clsHelper.FormatOutput(FunctionName, "FAILURE", "ERROR", "CERTIFICATE", "The client certificate was not loaded successfully.", null, context);
                    }

                    // Construct the SOAP request object from the input JSON.
                    var request = BuildSearchRequest(input);

                    // Determine which search type to use and clear irrelevant fields (if applicable)
                    var searchType = GetSearchTypeAndClearFields(request);
                    if (searchType is null)
                    {
                        return clsHelper.FormatOutput(FunctionName, "FAILURE", "ERROR", "PARAM", "The IHI search request does not contain the minimum search criteria. Please check all input fields and resubmit.", null, context);
                    }

                    // Call the appropriate HI service operation.
                    try
                    {
                        context.Logger.LogInformation($"{searchType} Request Data : {System.Text.Json.JsonSerializer.Serialize(request)}");

                        var ihiResponse = searchType switch
                        {
                            "BasicSearchAsync" => await objClient.BasicSearchAsync(request),
                            "BasicMedicareSearchAsync" => await objClient.BasicMedicareSearchAsync(request),
                            "BasicDvaSearchAsync" => await objClient.BasicDvaSearchAsync(request),
                            "AustralianUnstructuredAddressSearchAsync" => await objClient.AustralianUnstructuredAddressSearchAsync(request),
                            "DetailedSearchAsync" => await objClient.DetailedSearchAsync(request),

                            _ => throw new InvalidOperationException("Unknown search type")
                        };

                        // Configure JSON serialization settings for the output
                        var settings = new JsonSerializerSettings
                        {
                            StringEscapeHandling = StringEscapeHandling.Default,
                            Formatting = Newtonsoft.Json.Formatting.None,
                            Converters = { new Newtonsoft.Json.Converters.StringEnumConverter() }
                        };

                        // Format and return the successful response.
                        return clsHelper.FormatOutput(FunctionName, "SUCCESS", "INFO", "", JsonConvert.SerializeObject(ihiResponse.searchIHIResult, settings), objClient, context);
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
                else if (internalMode == "2")    // Create Provisional IHI via B2B (TECH.SIS.HI.10)
                {
                    // Cast the generic client object to the specific ConsumerCreateProvisionalIHIClient.
                    var objClient = (ConsumerCreateProvisionalIHIClient)digitalHealthClientResult.DigitalHealthClient;
                    if (objClient == null)
                    {
                        return clsHelper.FormatOutput(FunctionName, "FAILURE", "ERROR", "CERTIFICATE", "The client certificate was not loaded successfully.", null, context);
                    }

                    // Construct the SOAP request object from the input JSON.
                    var request = BuildCreateProvisionalIHIRequest(input);

                    // Call the appropriate HI service operation.
                    try
                    {
                        context.Logger.LogInformation($"{FunctionName} Request Data : {System.Text.Json.JsonSerializer.Serialize(request)}");

                        var ihiResponse = await objClient.CreateProvisionalIhiAsync(request);

                        // Configure JSON serialization settings for the output
                        var settings = new JsonSerializerSettings
                        {
                            StringEscapeHandling = StringEscapeHandling.Default,
                            Formatting = Newtonsoft.Json.Formatting.None,
                            Converters = { new Newtonsoft.Json.Converters.StringEnumConverter() }
                        };

                        // Format and return the successful response.
                        return clsHelper.FormatOutput(FunctionName, "SUCCESS", "INFO", "", JsonConvert.SerializeObject(ihiResponse.createProvisionalIHIResult, settings), objClient, context);
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
                else if (internalMode == "3")    // Create Unverified IHI via B2B (TECH.SIS.HI.11)
                {
                    // Cast the generic client object to the specific ConsumerCreateUnverifiedIHIClient.
                    var objClient = (ConsumerCreateUnverifiedIHIClient)digitalHealthClientResult.DigitalHealthClient;
                    if (objClient == null)
                    {
                        return clsHelper.FormatOutput(FunctionName, "FAILURE", "ERROR", "CERTIFICATE", "The client certificate was not loaded successfully.", null, context);
                    }

                    // Construct the SOAP request object from the input JSON.
                    var request = BuildCreateUnverifiedIHIRequest(input);

                    // Call the appropriate HI service operation.
                    try
                    {
                        context.Logger.LogInformation($"{FunctionName} Request Data : {System.Text.Json.JsonSerializer.Serialize(request)}");

                        var ihiResponse = await objClient.CreateUnverifiedIhiAsync(request);

                        // Configure JSON serialization settings for the output
                        var settings = new JsonSerializerSettings
                        {
                            StringEscapeHandling = StringEscapeHandling.Default,
                            Formatting = Newtonsoft.Json.Formatting.None,
                            Converters = { new Newtonsoft.Json.Converters.StringEnumConverter() }
                        };

                        // Format and return the successful response.
                        return clsHelper.FormatOutput(FunctionName, "SUCCESS", "INFO", "", JsonConvert.SerializeObject(ihiResponse.createUnverifiedIHIResult, settings), objClient, context);
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
                else if (internalMode == "4")    // Update Provisional IHI via B2B (TECH.SIS.HI.03)
                {
                    // Cast the generic client object to the specific ConsumerUpdateProvisionalIHIClient.
                    var objClient = (ConsumerUpdateProvisionalIHIClient)digitalHealthClientResult.DigitalHealthClient;
                    if (objClient == null)
                    {
                        return clsHelper.FormatOutput(FunctionName, "FAILURE", "ERROR", "CERTIFICATE", "The client certificate was not loaded successfully.", null, context);
                    }

                    // Construct the SOAP request object from the input JSON.
                    var request = BuildConsumerUpdateProvisionalIHIRequest(input);

                    // Call the appropriate HI service operation.
                    try
                    {
                        context.Logger.LogInformation($"{FunctionName} Request Data : {System.Text.Json.JsonSerializer.Serialize(request)}");

                        var ihiResponse = await objClient.UpdateProvisionalIhiAsync(request);

                        // Configure JSON serialization settings for the output
                        var settings = new JsonSerializerSettings
                        {
                            StringEscapeHandling = StringEscapeHandling.Default,
                            Formatting = Newtonsoft.Json.Formatting.None,
                            Converters = { new Newtonsoft.Json.Converters.StringEnumConverter() }
                        };

                        // Format and return the successful response.
                        return clsHelper.FormatOutput(FunctionName, "SUCCESS", "INFO", "", JsonConvert.SerializeObject(ihiResponse.updateProvisionalIHIResult, settings), objClient, context);
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
                else if (internalMode == "5")    // Update IHI via B2B (TECH.SIS.HI.05)
                {
                    // Cast the generic client object to the specific ConsumerUpdateIHIClient.
                    var objClient = (ConsumerUpdateIHIClient)digitalHealthClientResult.DigitalHealthClient;
                    if (objClient == null)
                    {
                        return clsHelper.FormatOutput(FunctionName, "FAILURE", "ERROR", "CERTIFICATE", "The client certificate was not loaded successfully.", null, context);
                    }

                    // Construct the SOAP request object from the input JSON.
                    var request = BuildConsumerUpdateIHIRequest(input);

                    // Call the appropriate HI service operation.
                    try
                    {
                        context.Logger.LogInformation($"{FunctionName} Request Data : {System.Text.Json.JsonSerializer.Serialize(request)}");

                        var ihiResponse = await objClient.UpdateIhiAsync(request);

                        // Configure JSON serialization settings for the output
                        var settings = new JsonSerializerSettings
                        {
                            StringEscapeHandling = StringEscapeHandling.Default,
                            Formatting = Newtonsoft.Json.Formatting.None,
                            Converters = { new Newtonsoft.Json.Converters.StringEnumConverter() }
                        };

                        // Format and return the successful response.
                        return clsHelper.FormatOutput(FunctionName, "SUCCESS", "INFO", "", JsonConvert.SerializeObject(ihiResponse.updateIHIResult, settings), objClient, context);
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
                else if (internalMode == "6")    // Resolve Provisional IHI-Merge record via B2B (TECH.SIS.HI.08)
                {
                    // Cast the generic client object to the specific ConsumerMergeProvisionalIHIClient.
                    var objClient = (ConsumerMergeProvisionalIHIClient)digitalHealthClientResult.DigitalHealthClient;
                    if (objClient == null)
                    {
                        return clsHelper.FormatOutput(FunctionName, "FAILURE", "ERROR", "CERTIFICATE", "The client certificate was not loaded successfully.", null, context);
                    }

                    // Construct the SOAP request object from the input JSON.
                    var request = BuildConsumerMergeProvisionalIHIRequest(input);

                    // Call the appropriate HI service operation.
                    try
                    {
                        context.Logger.LogInformation($"{FunctionName} Request Data : {System.Text.Json.JsonSerializer.Serialize(request.mergeProvisionalIHI)}");

                        var ihiResponse = await objClient.MergeProvisionalIhiAsync(request);

                        // Configure JSON serialization settings for the output
                        var settings = new JsonSerializerSettings
                        {
                            StringEscapeHandling = StringEscapeHandling.Default,
                            Formatting = Newtonsoft.Json.Formatting.None,
                            Converters = { new Newtonsoft.Json.Converters.StringEnumConverter() }
                        };

                        // Format and return the successful response.
                        return clsHelper.FormatOutput(FunctionName, "SUCCESS", "INFO", "", JsonConvert.SerializeObject(ihiResponse.mergeProvisionalIHIResult, settings), objClient, context);
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
                else if (internalMode == "7")    // Resolve Provisional IHI – Create Unverified IHI via B2B (TECH.SIS.HI.09)
                {
                    // Cast the generic client object to the specific ConsumerResolveProvisionalIHIClient.
                    var objClient = (ConsumerResolveProvisionalIHIClient)digitalHealthClientResult.DigitalHealthClient;
                    if (objClient == null)
                    {
                        return clsHelper.FormatOutput(FunctionName, "FAILURE", "ERROR", "CERTIFICATE", "The client certificate was not loaded successfully.", null, context);
                    }

                    // Construct the SOAP request object from the input JSON.
                    var request = BuildConsumerResolveProvisionalIHIRequest(input);

                    // Call the appropriate HI service operation.
                    try
                    {
                        context.Logger.LogInformation($"{FunctionName} Request Data : {System.Text.Json.JsonSerializer.Serialize(request)}");

                        var ihiResponse = await objClient.ResolveProvisionalIhiAsync(request);

                        // Configure JSON serialization settings for the output
                        var settings = new JsonSerializerSettings
                        {
                            StringEscapeHandling = StringEscapeHandling.Default,
                            Formatting = Newtonsoft.Json.Formatting.None,
                            Converters = { new Newtonsoft.Json.Converters.StringEnumConverter() }
                        };

                        // Format and return the successful response.
                        return clsHelper.FormatOutput(FunctionName, "SUCCESS", "INFO", "", JsonConvert.SerializeObject(ihiResponse.resolveProvisionalIHIResult, settings), objClient, context);
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
                else if (internalMode == "8")    // Notify of Duplicate IHI via B2B (TECH.SIS.HI.24)
                {
                    // Cast the generic client object to the specific ConsumerNotifyDuplicateIHIClient.
                    var objClient = (ConsumerNotifyDuplicateIHIClient)digitalHealthClientResult.DigitalHealthClient;
                    if (objClient == null)
                    {
                        return clsHelper.FormatOutput(FunctionName, "FAILURE", "ERROR", "CERTIFICATE", "The client certificate was not loaded successfully.", null, context);
                    }

                    // Construct the SOAP request object from the input JSON.
                    var request = BuildConsumerNotifyDuplicateIHIRequest(input);

                    // Call the appropriate HI service operation.
                    try
                    {
                        context.Logger.LogInformation($"{FunctionName} Request Data : {System.Text.Json.JsonSerializer.Serialize(request)}");

                        var ihiResponse = await objClient.NotifyDuplicateIhiAsync(request);

                        // Configure JSON serialization settings for the output
                        var settings = new JsonSerializerSettings
                        {
                            StringEscapeHandling = StringEscapeHandling.Default,
                            Formatting = Newtonsoft.Json.Formatting.None,
                            Converters = { new Newtonsoft.Json.Converters.StringEnumConverter() }
                        };

                        // Format and return the successful response.
                        return clsHelper.FormatOutput(FunctionName, "SUCCESS", "INFO", "", JsonConvert.SerializeObject(ihiResponse.notifyDuplicateIHIResult, settings), objClient, context);
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
                else if (internalMode == "9")    // Notify of Replica IHI via B2B (TECH.SIS.HI.25)
                {
                    // Cast the generic client object to the specific ConsumerNotifyReplicaIHIClient.
                    var objClient = (ConsumerNotifyReplicaIHIClient)digitalHealthClientResult.DigitalHealthClient;
                    if (objClient == null)
                    {
                        return clsHelper.FormatOutput(FunctionName, "FAILURE", "ERROR", "CERTIFICATE", "The client certificate was not loaded successfully.", null, context);
                    }

                    // Construct the SOAP request object from the input JSON.
                    var request = BuildConsumerNotifyReplicaIHIRequest(input);

                    // Call the appropriate HI service operation.
                    try
                    {
                        context.Logger.LogInformation($"{FunctionName} Request Data : {System.Text.Json.JsonSerializer.Serialize(request)}");

                        var ihiResponse = await objClient.NotifyReplicaIhiAsync(request);

                        // Configure JSON serialization settings for the output
                        var settings = new JsonSerializerSettings
                        {
                            StringEscapeHandling = StringEscapeHandling.Default,
                            Formatting = Newtonsoft.Json.Formatting.None,
                            Converters = { new Newtonsoft.Json.Converters.StringEnumConverter() }
                        };

                        // Format and return the successful response.
                        return clsHelper.FormatOutput(FunctionName, "SUCCESS", "INFO", "", JsonConvert.SerializeObject(ihiResponse.notifyReplicaIHIResult, settings), objClient, context);
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
                else if (internalMode == "10")    // Create verified IHI for Newborns (TECH.SIS.HI.26)
                {
                    // Cast the generic client object to the specific ConsumerCreateVerifiedIHIClient.
                    var objClient = (ConsumerCreateVerifiedIHIClient)digitalHealthClientResult.DigitalHealthClient;
                    if (objClient == null)
                    {
                        return clsHelper.FormatOutput(FunctionName, "FAILURE", "ERROR", "CERTIFICATE", "The client certificate was not loaded successfully.", null, context);
                    }

                    // Construct the SOAP request object from the input JSON.
                    var request = BuildConsumerCreateVerifiedIHIRequest(input);

                    // Call the appropriate HI service operation.
                    try
                    {
                        context.Logger.LogInformation($"{FunctionName} Request Data : {System.Text.Json.JsonSerializer.Serialize(request)}");

                        var ihiResponse = await objClient.CreateVerifiedIhiAsync(request);

                        // Configure JSON serialization settings for the output
                        var settings = new JsonSerializerSettings
                        {
                            StringEscapeHandling = StringEscapeHandling.Default,
                            Formatting = Newtonsoft.Json.Formatting.None,
                            Converters = { new Newtonsoft.Json.Converters.StringEnumConverter() }
                        };

                        // Format and return the successful response.
                        return clsHelper.FormatOutput(FunctionName, "SUCCESS", "INFO", "", JsonConvert.SerializeObject(ihiResponse.createVerifiedIHIResult, settings), objClient, context);
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
        /// Constructs a <see cref="searchIHI"/> request object from the input JSON payload.
        /// </summary>
        /// <param name="root">The root <see cref="JsonElement"/> of the input payload.</param>
        /// <returns>A populated <see cref="searchIHI"/> object.</returns>
        private static searchIHI BuildSearchRequest(JsonElement root)
        {
            var request = new searchIHI
            {
                // Populate fields from the JSON input, applying qualifiers and truncation where necessary.
                ihiNumber = clsHelper.TryGetQualifiedString(root, "ihiNumberField", HIQualifiers.IHIQualifier),
                medicareCardNumber = clsHelper.GetStringProperty(root, "medicareCardNumberField"),
                medicareIRN = clsHelper.GetStringProperty(root, "medicareIRNField"),
                dvaFileNumber = clsHelper.GetStringProperty(root, "dvaFileNumberField"),
                familyName = clsHelper.TruncateString(clsHelper.GetStringProperty(root, "familyNameField"), 40),
                givenName = clsHelper.TruncateString(clsHelper.GetStringProperty(root, "givenNameField"), 40),
                electronicCommunication = clsHelper.GetElectronicCommunication<
                                                                            nehta.mcaR3.ConsumerSearchIHI.ElectronicCommunicationType,
                                                                            nehta.mcaR3.ConsumerSearchIHI.MediumType,
                                                                            nehta.mcaR3.ConsumerSearchIHI.UsageType,
                                                                            nehta.mcaR3.ConsumerSearchIHI.TrueFalseType>(root),
                australianPostalAddress = null,         // Not used in this implementation
                australianStreetAddress = null,         // Not used in this implementation
                australianUnstructuredStreetAddress = clsHelper.GetAustralianAddressUnstructured<nehta.mcaR3.ConsumerSearchIHI.AustralianUnstructuredStreetAddressType, nehta.mcaR3.ConsumerSearchIHI.StateType>(root),
                internationalAddress = null             // Not used in this implementation
            };

            // Safely parse and set the date of birth if provided.
            if (root.TryGetProperty("dateOfBirthField", out var dobElem) &&
                dobElem.ValueKind == JsonValueKind.String &&
                DateTime.TryParse(dobElem.GetString(), out var dob))
            {
                request.dateOfBirth = dob;
            }

            // Safely parse and set the sex if provided.
            if (root.TryGetProperty("sexField", out var sexElem) &&
                sexElem.ValueKind == JsonValueKind.String &&
                Enum.TryParse<nehta.mcaR3.ConsumerSearchIHI.SexType>(sexElem.GetString(), true, out var sexEnum))
            {
                request.sex = sexEnum;
            }

            return request;
        }

        /// <summary>
        /// Determines the appropriate search type based on the fields populated in the request object.
        /// It follows a specific priority order and clears irrelevant fields to ensure the correct
        /// HI service operation is invoked by the underlying SOAP client library.
        /// </summary>
        /// <param name="request">The <see cref="searchIHI"/> request object.</param>
        /// <returns>A string representing the name of the async method to call, or null if no valid search criteria are met.</returns>
        private static string GetSearchTypeAndClearFields(searchIHI request)
        {
            // The order of these checks defines the priority of the search type.
            // For each valid search type, unrelated fields are set to null to avoid ambiguity.

            // 1. Medicare Card Search: Highest priority if Medicare number is present.
            if (request.medicareCardNumber != null)
            {
                request.ihiNumber = null;
                request.dvaFileNumber = null;
                request.electronicCommunication = null;
                request.australianPostalAddress = null;
                request.australianStreetAddress = null;
                request.australianUnstructuredStreetAddress = null;
                request.internationalAddress = null;
                return "BasicMedicareSearchAsync";
            }
            // 2. DVA File Number Search: Second priority.
            if (request.dvaFileNumber != null)
            {
                request.ihiNumber = null;
                request.medicareCardNumber = null;
                request.medicareIRN = null;
                request.electronicCommunication = null;
                request.australianPostalAddress = null;
                request.australianStreetAddress = null;
                request.australianUnstructuredStreetAddress = null;
                request.internationalAddress = null;
                return "BasicDvaSearchAsync";
            }
            // 3. IHI Number Search: Third priority.
            if (request.ihiNumber != null)
            {
                request.medicareCardNumber = null;
                request.medicareIRN = null;
                request.dvaFileNumber = null;
                request.electronicCommunication = null;
                request.australianPostalAddress = null;
                request.australianStreetAddress = null;
                request.australianUnstructuredStreetAddress = null;
                request.internationalAddress = null;
                return "BasicSearchAsync";
            }
            // 4. Unstructured Address Search: Requires address, family name, and given name.
            if ((request.australianUnstructuredStreetAddress != null) &&
                    (request.familyName != null) &&
                    (request.givenName != null))
            {
                request.ihiNumber = null;
                request.medicareCardNumber = null;
                request.medicareIRN = null;
                request.dvaFileNumber = null;
                request.electronicCommunication = null;
                request.australianPostalAddress = null;
                request.australianStreetAddress = null;
                request.internationalAddress = null;
                return "AustralianUnstructuredAddressSearchAsync";
            }
            // 5. Detailed Search: Requires electronic communication and family name.
            // Given name is optional if the communication medium is mobile.
            if (request.electronicCommunication != null && request.familyName != null &&
                (
                    (request.givenName != null)
                    ||
                    (request.givenName == null && request.electronicCommunication.medium == nehta.mcaR3.ConsumerSearchIHI.MediumType.M)
                )
            )
            {
                request.ihiNumber = null;
                request.medicareCardNumber = null;
                request.medicareIRN = null;
                request.dvaFileNumber = null;
                request.australianPostalAddress = null;
                request.australianStreetAddress = null;
                request.australianUnstructuredStreetAddress = null;
                request.internationalAddress = null;
                return "DetailedSearchAsync";
            }

            // If no criteria are met, return null.
            return null;
        }

        /// <summary>
        /// Constructs a <see cref="createProvisionalIHI"/> request object from the input JSON payload.
        /// </summary>
        /// <param name="root">The root <see cref="JsonElement"/> of the input payload.</param>
        /// <returns>A populated <see cref="createProvisionalIHI"/> object.</returns>
        private static createProvisionalIHI BuildCreateProvisionalIHIRequest(JsonElement root)
        {

            // Populate fields from the JSON input, applying qualifiers and truncation where necessary.
            createProvisionalIHI request = new createProvisionalIHI
            {
                familyName = clsHelper.TruncateString(clsHelper.GetStringProperty(root, "familyNameField"), 40),
                givenName = clsHelper.TruncateString(clsHelper.GetStringProperty(root, "givenNameField"), 40),
                sex = nehta.mcaR3.ConsumerCreateProvisionalIHI.SexType.N,                                               // Default (N - Not Specified)
                dateOfBirth = DateTime.Parse("01 Jan 2000"),                                                            // Default
                dateOfBirthAccuracyIndicator = nehta.mcaR3.ConsumerCreateProvisionalIHI.DateAccuracyIndicatorType.EEE,  // Default (Day, Month & Year are estimated)
                //dateOfDeath = null,                                                                                   // Not used in this implementation
                dateOfDeathSpecified = false,
                //dateOfDeathAccuracyIndicator = null,                                                                  // Not used in this implementation
                dateOfDeathAccuracyIndicatorSpecified = false,
                //sourceOfDeathNotification = null,                                                                     // Not used in this implementation
                sourceOfDeathNotificationSpecified = false
            };

            // Safely parse and set the sex if provided.
            if (root.TryGetProperty("sexField", out var sexElem) &&
                sexElem.ValueKind == JsonValueKind.String &&
                Enum.TryParse<nehta.mcaR3.ConsumerCreateProvisionalIHI.SexType>(sexElem.GetString(), true, out var sexEnum))
            {
                request.sex = sexEnum;
            }

            // Safely parse and set the date of birth if provided.
            if (root.TryGetProperty("dateOfBirthField", out var dobElem) &&
                dobElem.ValueKind == JsonValueKind.String &&
                DateTime.TryParse(dobElem.GetString(), out var dob))
            {
                request.dateOfBirth = dob;
                request.dateOfBirthAccuracyIndicator = nehta.mcaR3.ConsumerCreateProvisionalIHI.DateAccuracyIndicatorType.AAA;
            }

            return request;
        }

        /// <summary>
        /// Constructs a <see cref="createUnverifiedIHI"/> request object from the input JSON payload.
        /// </summary>
        /// <param name="root">The root <see cref="JsonElement"/> of the input payload.</param>
        /// <returns>A populated <see cref="createUnverifiedIHI"/> object.</returns>
        private static createUnverifiedIHI BuildCreateUnverifiedIHIRequest(JsonElement root)
        {
            // Populate fields from the JSON input, applying qualifiers and truncation where necessary.
            createUnverifiedIHI request = new createUnverifiedIHI
            {
                individualHealthcareIdentity = new IndividualHealthcareIdentityType
                {
                    dateOfBirth = DateTime.Parse("01 Jan 2000"),                                                                // Default
                    dateOfBirthAccuracyIndicator = nehta.mcaR302.ConsumerCreateUnverifiedIHI.DateAccuracyIndicatorType.EEE,     // Default (Day, Month & Year are estimated)
                    sex = nehta.mcaR302.ConsumerCreateUnverifiedIHI.SexType.N,                                                  // Default (N - Not Specified)
                    //birthPlurality = nehta.mcaR302.ConsumerCreateUnverifiedIHI.BirthPluralityType.Item1,                      // Not used in this implementation
                    birthPluralitySpecified = false,
                    //birthOrder = nehta.mcaR302.ConsumerCreateUnverifiedIHI.BirthOrderType.Item1,                              // Not used in this implementation
                    birthOrderSpecified = false,
                    //dateOfDeath = null,                                                                                       // Not used in this implementation
                    dateOfDeathSpecified = false,
                    //dateOfDeathAccuracyIndicator = null,                                                                      // Not used in this implementation
                    dateOfDeathAccuracyIndicatorSpecified = false,
                    //sourceOfDeathNotification = null,                                                                         // Not used in this implementation
                    sourceOfDeathNotificationSpecified = false
                },
                electronicCommunication = clsHelper.GetElectronicCommunication<nehta.mcaR302.ConsumerCreateUnverifiedIHI.ElectronicCommunicationType, nehta.mcaR302.ConsumerCreateUnverifiedIHI.MediumType, nehta.mcaR302.ConsumerCreateUnverifiedIHI.UsageType, nehta.mcaR302.ConsumerCreateUnverifiedIHI.TrueFalseType>(root) != null
                                        ? new[] { clsHelper.GetElectronicCommunication<nehta.mcaR302.ConsumerCreateUnverifiedIHI.ElectronicCommunicationType, nehta.mcaR302.ConsumerCreateUnverifiedIHI.MediumType, nehta.mcaR302.ConsumerCreateUnverifiedIHI.UsageType, nehta.mcaR302.ConsumerCreateUnverifiedIHI.TrueFalseType>(root) }
                                        : null,
                //nameTitle = null,                                                                                             // Not used in this implementation
                nameTitleSpecified = false,
                familyName = clsHelper.TruncateString(clsHelper.GetStringProperty(root, "familyNameField"), 40),
                givenName = new[] { clsHelper.TruncateString(clsHelper.GetStringProperty(root, "givenNameField"), 40) },
                //nameSuffix = null,                                                                                            // Not used in this implementation
                nameSuffixSpecified = false,
                //usage = nehta.mcaR302.ConsumerCreateUnverifiedIHI.IndividualNameUsageType.L,                                  // Not used in this implementation
                //conditionalUse = nehta.mcaR302.ConsumerCreateUnverifiedIHI.ConditionalUseType.Item1,                          // Not used in this implementation
                conditionalUseSpecified = false,
                address = clsHelper.BuildAddressArray<
                    nehta.mcaR302.ConsumerCreateUnverifiedIHI.AddressType,
                    nehta.mcaR302.ConsumerCreateUnverifiedIHI.AustralianUnstructuredStreetAddressType,
                    nehta.mcaR302.ConsumerCreateUnverifiedIHI.StateType
                >(root)
            };

            // Safely parse and set the date of birth if provided.
            if (root.TryGetProperty("dateOfBirthField", out var dobElem) &&
                dobElem.ValueKind == JsonValueKind.String &&
                DateTime.TryParse(dobElem.GetString(), out var dob))
            {
                request.individualHealthcareIdentity.dateOfBirth = dob;
                request.individualHealthcareIdentity.dateOfBirthAccuracyIndicator = nehta.mcaR302.ConsumerCreateUnverifiedIHI.DateAccuracyIndicatorType.AAA;
            }

            // Safely parse and set the sex if provided.
            if (root.TryGetProperty("sexField", out var sexElem) &&
                sexElem.ValueKind == JsonValueKind.String &&
                Enum.TryParse<nehta.mcaR302.ConsumerCreateUnverifiedIHI.SexType>(sexElem.GetString(), true, out var sexEnum))
            {
                request.individualHealthcareIdentity.sex = sexEnum;
            }

            return request;
        }

        /// <summary>
        /// Constructs a <see cref="updateProvisionalIHI"/> request object from the input JSON payload.
        /// </summary>
        /// <param name="root">The root <see cref="JsonElement"/> of the input payload.</param>
        /// <returns>A populated <see cref="updateProvisionalIHI"/> object.</returns>
        private static nehta.mcaR3.ConsumerUpdateProvisionalIHI.updateProvisionalIHI BuildConsumerUpdateProvisionalIHIRequest(JsonElement root)
        {
            // Populate fields from the JSON input, applying qualifiers and truncation where necessary.
            var request = new nehta.mcaR3.ConsumerUpdateProvisionalIHI.updateProvisionalIHI
            {
                ihiNumber = clsHelper.TryGetQualifiedString(root, "ihiNumberField", HIQualifiers.IHIQualifier),
                familyName = clsHelper.TruncateString(clsHelper.GetStringProperty(root, "familyNameField"), 40),
                givenName = clsHelper.TruncateString(clsHelper.GetStringProperty(root, "givenNameField"), 40),
                sex = nehta.mcaR3.ConsumerUpdateProvisionalIHI.SexType.N,                                               // Default (N - Not Specified)
                dateOfBirth = DateTime.Parse("01 Jan 2000"),                                                            // Default
                dateOfBirthAccuracyIndicator = nehta.mcaR3.ConsumerUpdateProvisionalIHI.DateAccuracyIndicatorType.EEE,  // Default (Day, Month & Year are estimated)                
                //dateOfDeath = null,                                                                                   // Not used in this implementation    
                dateOfDeathSpecified = false,
                //dateOfDeathAccuracyIndicator = null,                                                                  // Not used in this implementation
                dateOfDeathAccuracyIndicatorSpecified = false,
                //sourceOfDeathNotification = null,                                                                     // Not used in this implementation
                sourceOfDeathNotificationSpecified = false
            };

            // Safely parse and set the sex if provided.
            if (root.TryGetProperty("sexField", out var sexElem) &&
                sexElem.ValueKind == JsonValueKind.String &&
                Enum.TryParse<nehta.mcaR3.ConsumerUpdateProvisionalIHI.SexType>(sexElem.GetString(), true, out var sexEnum))
            {
                request.sex = sexEnum;
            }

            // Safely parse and set the date of birth if provided.
            if (root.TryGetProperty("dateOfBirthField", out var dobElem) &&
                dobElem.ValueKind == JsonValueKind.String &&
                DateTime.TryParse(dobElem.GetString(), out var dob))
            {
                request.dateOfBirth = dob;
                request.dateOfBirthAccuracyIndicator = nehta.mcaR3.ConsumerUpdateProvisionalIHI.DateAccuracyIndicatorType.AAA;
            }
            
            return request;
        }
        /// <summary>
        /// Constructs a <see cref="nehta.mcaR3.ConsumerUpdateIHI.updateIHI"/> request object from the input JSON payload.
        /// </summary>
        /// <param name="root">The root <see cref="JsonElement"/> of the input payload.</param>
        /// <returns>A populated <see cref="nehta.mcaR3.ConsumerUpdateIHI.updateIHI"/> object.</returns>
        private static nehta.mcaR32.ConsumerUpdateIHI.updateIHI BuildConsumerUpdateIHIRequest(JsonElement root)
        {
            // Populate fields from the JSON input, applying qualifiers and truncation where necessary.
            var request = new nehta.mcaR32.ConsumerUpdateIHI.updateIHI
            {
                ihiNumber = clsHelper.TryGetQualifiedString(root, "ihiNumberField", HIQualifiers.IHIQualifier),
                dateOfBirth = DateTime.Parse("01 Jan 2000"),    // Default
                dateOfBirthSpecified = false,
                dateOfBirthAccuracyIndicator = nehta.mcaR32.ConsumerUpdateIHI.DateAccuracyIndicatorType.EEE,        // Default (Day, Month & Year are estimated)
                dateOfBirthAccuracyIndicatorSpecified = false,
                sex = nehta.mcaR32.ConsumerUpdateIHI.SexType.N,                                                     // Default (N - Not Specified)
                //birthPlurality = nehta.mcaR32.ConsumerUpdateIHI.BirthPluralityType.Item9,                         // Not used in this implementation
                birthPluralitySpecified = false,
                //birthOrder = nehta.mcaR32.ConsumerUpdateIHI.BirthOrderType.Item9,                                 // Not used in this implementation
                birthOrderSpecified = false,
                //dateOfDeath = null,                                                                               // Not used in this implementation    
                dateOfDeathSpecified = false,
                //dateOfDeathAccuracyIndicator = null,                                                              // Not used in this implementation
                dateOfDeathAccuracyIndicatorSpecified = false,
                //sourceOfDeathNotification = null,                                                                 // Not used in this implementation
                sourceOfDeathNotificationSpecified = false,
                electronicCommunication = clsHelper.GetElectronicCommunication<
                            nehta.mcaR32.ConsumerUpdateIHI.ElectronicCommunicationType,
                            nehta.mcaR32.ConsumerUpdateIHI.MediumType,
                            nehta.mcaR32.ConsumerUpdateIHI.UsageType,
                            nehta.mcaR32.ConsumerUpdateIHI.TrueFalseType>(root) != null
                            ? new[] { clsHelper.GetElectronicCommunication<
                                nehta.mcaR32.ConsumerUpdateIHI.ElectronicCommunicationType,
                                nehta.mcaR32.ConsumerUpdateIHI.MediumType,
                                nehta.mcaR32.ConsumerUpdateIHI.UsageType,
                                nehta.mcaR32.ConsumerUpdateIHI.TrueFalseType>(root) }
                            : null,
                name = new[]
                {
                    new nehta.mcaR32.ConsumerUpdateIHI.NameType
                    {
                        //nameTitle = null,                                                                         // Not used in this implementation
                        nameTitleSpecified = false,
                        familyName = clsHelper.TruncateString(clsHelper.GetStringProperty(root, "familyNameField"), 40),
                        givenName = new[] { clsHelper.TruncateString(clsHelper.GetStringProperty(root, "givenNameField"), 40), },
                        //nameSuffix = null,                                                                        // Not used in this implementation
                        nameSuffixSpecified = false,
                        usage = nehta.mcaR32.ConsumerUpdateIHI.IndividualNameUsageType.L,
                        //preferred = nehta.mcaR32.ConsumerUpdateIHI.TrueFalseType.F,                               // Not used in this implementation
                        preferredSpecified = false,
                        //conditionalUse = null,                                                                    // Not used in this implementation
                        conditionalUseSpecified = false
                    }
                },
                nameUpdateGroup = null,                                                                             // Not used in this implementation
                address = clsHelper.BuildAddressArray<
                    nehta.mcaR32.ConsumerUpdateIHI.AddressType,
                    nehta.mcaR32.ConsumerUpdateIHI.AustralianUnstructuredStreetAddressType,
                    nehta.mcaR32.ConsumerUpdateIHI.StateType
                >(root)
            };

            // Safely parse and set the sex if provided.
            if (root.TryGetProperty("sexField", out var sexElem) &&
                sexElem.ValueKind == JsonValueKind.String &&
                Enum.TryParse<nehta.mcaR32.ConsumerUpdateIHI.SexType>(sexElem.GetString(), true, out var sexEnum))
            {
                request.sex = sexEnum;
            }

            // Safely parse and set the date of birth if provided.
            if (root.TryGetProperty("dateOfBirthField", out var dobElem) &&
                dobElem.ValueKind == JsonValueKind.String &&
                DateTime.TryParse(dobElem.GetString(), out var dob))
            {
                request.dateOfBirth = dob;
                request.dateOfBirthSpecified = true;
                request.dateOfBirthAccuracyIndicator = nehta.mcaR32.ConsumerUpdateIHI.DateAccuracyIndicatorType.AAA;
                request.dateOfBirthAccuracyIndicatorSpecified = true;
            }

            return request;
        }
        /// <summary>
        /// Constructs a <see cref="nehta.mcaR3.ConsumerMergeProvisionalIHI.mergeProvisionalIHI"/> request object from the input JSON payload.
        /// </summary>
        /// <param name="root">The root <see cref="JsonElement"/> of the input payload.</param>
        /// <returns>A populated <see cref="nehta.mcaR3.ConsumerMergeProvisionalIHI.mergeProvisionalIHI"/> object.</returns>
        private static mergeProvisionalIHIRequest BuildConsumerMergeProvisionalIHIRequest(JsonElement root)
        {
            // Populate fields from the JSON input, applying qualifiers and truncation where necessary.
            mergeProvisionalIHIRequest request = new mergeProvisionalIHIRequest
            {
                mergeProvisionalIHI = new[]
                {
                    clsHelper.TryGetQualifiedString(root, "ihiNumberField1", HIQualifiers.IHIQualifier),
                    clsHelper.TryGetQualifiedString(root, "ihiNumberField2", HIQualifiers.IHIQualifier)
                }
            };

            return request;
        }
        /// <summary>
        /// Constructs a <see cref="nehta.mcaR302.ConsumerResolveProvisionalIHI.resolveProvisionalIHI"/> request object from the input JSON payload.
        /// </summary>
        /// <param name="root">The root <see cref="JsonElement"/> of the input payload.</param>
        /// <returns>A populated <see cref="nehta.mcaR302.ConsumerResolveProvisionalIHI.resolveProvisionalIHI"/> object.</returns>
        private static nehta.mcaR302.ConsumerResolveProvisionalIHI.resolveProvisionalIHI BuildConsumerResolveProvisionalIHIRequest(JsonElement root)
        {
            // Populate fields from the JSON input, applying qualifiers and truncation where necessary.
            var request = new nehta.mcaR302.ConsumerResolveProvisionalIHI.resolveProvisionalIHI
            {
                ihiNumber = clsHelper.TryGetQualifiedString(root, "ihiNumberField", HIQualifiers.IHIQualifier),
                dateOfBirth = DateTime.Parse("01 Jan 2000"),                                                                    // Default
                dateOfBirthAccuracyIndicator = nehta.mcaR302.ConsumerResolveProvisionalIHI.DateAccuracyIndicatorType.EEE,       // Default (Day, Month & Year are estimated)
                sex = nehta.mcaR302.ConsumerResolveProvisionalIHI.SexType.N,                                                    // Default (N - Not Specified)
                //birthPlurality = nehta.mcaR302.ConsumerResolveProvisionalIHI.BirthPluralityType.Item9,                        // Not used in this implementation
                birthPluralitySpecified = false,
                //birthOrder = nehta.mcaR302.ConsumerResolveProvisionalIHI.BirthOrderType.Item9,                                // Not used in this implementation
                birthOrderSpecified = false,
                //dateOfDeath = null,                                                                                           // Not used in this implementation    
                dateOfDeathSpecified = false,
                //dateOfDeathAccuracyIndicator = null,                                                                          // Not used in this implementation
                dateOfDeathAccuracyIndicatorSpecified = false,            
                //sourceOfDeathNotification = null,                                                                             // Not used in this implementation
                sourceOfDeathNotificationSpecified = false,
                electronicCommunication = clsHelper.GetElectronicCommunication<
                                nehta.mcaR302.ConsumerResolveProvisionalIHI.ElectronicCommunicationType,
                                nehta.mcaR302.ConsumerResolveProvisionalIHI.MediumType,
                                nehta.mcaR302.ConsumerResolveProvisionalIHI.UsageType,
                                nehta.mcaR302.ConsumerResolveProvisionalIHI.TrueFalseType>(root) != null
                                ? new[] { clsHelper.GetElectronicCommunication<
                                    nehta.mcaR302.ConsumerResolveProvisionalIHI.ElectronicCommunicationType,
                                    nehta.mcaR302.ConsumerResolveProvisionalIHI.MediumType,
                                    nehta.mcaR302.ConsumerResolveProvisionalIHI.UsageType,
                                    nehta.mcaR302.ConsumerResolveProvisionalIHI.TrueFalseType>(root) }
                                : null,
                //nameTitle = null,                                                                                             // Not used in this implementation
                nameTitleSpecified = false,
                familyName = clsHelper.TruncateString(clsHelper.GetStringProperty(root, "familyNameField"), 40),
                givenName = new[] { clsHelper.TruncateString(clsHelper.GetStringProperty(root, "givenNameField"), 40) },

                //nameSuffix = null,                                                                                            // Not used in this implementation
                //usage = nehta.mcaR302.ConsumerResolveProvisionalIHI.IndividualNameUsageType.L,                                // Not used in this implementation
                //conditionalUse = nehta.mcaR302.ConsumerResolveProvisionalIHI.ConditionalUseType.Item1,                        // Not used in this implementation
                conditionalUseSpecified = false,
                address = clsHelper.BuildAddressArray<
                    nehta.mcaR302.ConsumerResolveProvisionalIHI.AddressType,
                    nehta.mcaR302.ConsumerResolveProvisionalIHI.AustralianUnstructuredStreetAddressType,
                    nehta.mcaR302.ConsumerResolveProvisionalIHI.StateType
                >(root).First()

            };

            // Safely parse and set the date of birth if provided.
            if (root.TryGetProperty("dateOfBirthField", out var dobElem) &&
                dobElem.ValueKind == JsonValueKind.String &&
                DateTime.TryParse(dobElem.GetString(), out var dob))
            {
                request.dateOfBirth = dob;
                request.dateOfBirthAccuracyIndicator = nehta.mcaR302.ConsumerResolveProvisionalIHI.DateAccuracyIndicatorType.AAA;
            }

            // Safely parse and set the sex if provided.
            if (root.TryGetProperty("sexField", out var sexElem) &&
                sexElem.ValueKind == JsonValueKind.String &&
                Enum.TryParse<nehta.mcaR302.ConsumerResolveProvisionalIHI.SexType>(sexElem.GetString(), true, out var sexEnum))
            {
                request.sex = sexEnum;
            }

            return request;
        }
        /// <summary>
        /// Constructs a <see cref="nehta.mcaR32.ConsumerNotifyDuplicateIHI.notifyDuplicateIHI"/> request object from the input JSON payload.
        /// </summary>
        /// <param name="root">The root <see cref="JsonElement"/> of the input payload.</param>
        /// <returns>A populated <see cref="nehta.mcaR32.ConsumerNotifyDuplicateIHI.notifyDuplicateIHI"/> object.</returns>
        private static nehta.mcaR32.ConsumerNotifyDuplicateIHI.notifyDuplicateIHI BuildConsumerNotifyDuplicateIHIRequest(JsonElement root)
        {
            // Populate fields from the JSON input, applying qualifiers and truncation where necessary.
            var ihiNumbers = new List<string>();
            if (root.TryGetProperty("ihiNumberField", out var ihiNumberElement) && ihiNumberElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var ihi in ihiNumberElement.EnumerateArray())
                {
                    if (ihi.ValueKind == JsonValueKind.String)
                    {
                        string rawIhi = ihi.GetString();
                        if (!string.IsNullOrWhiteSpace(rawIhi))
                        {
                            ihiNumbers.Add($"{HIQualifiers.IHIQualifier}{rawIhi}");
                        }
                    }
                }
            }

            var request = new nehta.mcaR32.ConsumerNotifyDuplicateIHI.notifyDuplicateIHI
            {
                ihiNumber = ihiNumbers.ToArray(),
                comment = clsHelper.TruncateString(clsHelper.GetStringProperty(root, "comment"), 240),
            };

            return request;
        }

        /// <summary>
        /// Constructs a <see cref="nehta.mcaR32.ConsumerNotifyReplicaIHI.notifyReplicaIHI"/> request object from the input JSON payload.
        /// </summary>
        /// <param name="root">The root <see cref="JsonElement"/> of the input payload.</param>
        /// <returns>A populated <see cref="nehta.mcaR32.ConsumerNotifyReplicaIHI.notifyReplicaIHI"/> object.</returns>
        private static nehta.mcaR32.ConsumerNotifyReplicaIHI.notifyReplicaIHI BuildConsumerNotifyReplicaIHIRequest(JsonElement root)
        {
            // Populate fields from the JSON input, applying qualifiers and truncation where necessary.
            var request = new nehta.mcaR32.ConsumerNotifyReplicaIHI.notifyReplicaIHI
            {
                ihiNumber = clsHelper.TryGetQualifiedString(root, "ihiNumberField", HIQualifiers.IHIQualifier),
                comment = clsHelper.TruncateString(clsHelper.GetStringProperty(root, "comment"), 240)
            };
            
            return request;
        }

        /// <summary>
        /// Constructs a <see cref="nehta.mcaR40.CreateVerifiedIHI.CreateVerifiedIHI"/> request object from the input JSON payload.
        /// </summary>
        /// <param name="root">The root <see cref="JsonElement"/> of the input payload.</param>
        /// <returns>A populated <see cref="nehta.mcaR40.CreateVerifiedIHI.CreateVerifiedIHI"/> object.</returns>
        /// <summary>
        /// Constructs a <see cref="nnehta.mcaR40.CreateVerifiedIHI.CreateVerifiedIHI"/> request object from the input JSON payload.
        /// </summary>
        /// <param name="root">The root <see cref="JsonElement"/> of the input payload.</param>
        /// <returns>A populated <see cref="nehta.mcaR40.CreateVerifiedIHI.CreateVerifiedIHI"/> object.</returns>
        private static createVerifiedIHI BuildConsumerCreateVerifiedIHIRequest(JsonElement root)
        {
            // Populate fields from the JSON input, applying qualifiers and truncation where necessary.
            var request = new createVerifiedIHI
            {
                dateOfBirth = DateTime.Parse("01 Jan 2000"),                                                        // Default
                dateOfBirthAccuracyIndicator = nehta.mcaR40.CreateVerifiedIHI.DateAccuracyIndicatorType.EEE,        // Default (Day, Month & Year are estimated)
                sex = nehta.mcaR40.CreateVerifiedIHI.SexType.N,                                                     // Default (N - Not Specified)
                //birthPlurality = nehta.mcaR40.CreateVerifiedIHI.BirthPluralityType.Item9,                         // Not used in this implementation    
                birthPluralitySpecified = false,
                //birthOrder = nehta.mcaR40.CreateVerifiedIHI.BirthOrderType.Item9,                                 // Not used in this implementation    
                birthOrderSpecified = false,
                //dateOfDeath = null,                                                                               // Not used in this implementation
                dateOfDeathSpecified = false,
                //dateOfDeathAccuracyIndicator = null,                                                              // Not used in this implementation
                dateOfDeathAccuracyIndicatorSpecified = false,
                //sourceOfDeathNotification = null,                                                                 // Not used in this implementation
                sourceOfDeathNotificationSpecified = false,
                electronicCommunication = clsHelper.GetElectronicCommunication<
                                    nehta.mcaR40.CreateVerifiedIHI.ElectronicCommunicationType,
                                    nehta.mcaR40.CreateVerifiedIHI.MediumType,
                                    nehta.mcaR40.CreateVerifiedIHI.UsageType,
                                    nehta.mcaR40.CreateVerifiedIHI.TrueFalseType>(root) != null
                                    ? new[] { clsHelper.GetElectronicCommunication<
                                        nehta.mcaR40.CreateVerifiedIHI.ElectronicCommunicationType,
                                        nehta.mcaR40.CreateVerifiedIHI.MediumType,
                                        nehta.mcaR40.CreateVerifiedIHI.UsageType,
                                        nehta.mcaR40.CreateVerifiedIHI.TrueFalseType>(root) }
                                    : null,
                //nameTitle = null,                                                                                 // Not used in this implementation
                nameTitleSpecified = false,
                familyName = clsHelper.TruncateString(clsHelper.GetStringProperty(root, "familyNameField"), 40),
                givenName = new[] { clsHelper.TruncateString(clsHelper.GetStringProperty(root, "givenNameField"), 40) },
                //onlyNameIndicator = false,                                                                        // Not used in this implementation
                onlyNameIndicatorSpecified = false,
                //nameSuffix = null,                                                                                // Not used in this implementation
                nameSuffixSpecified = false,
                //usage = nehta.mcaR40.CreateVerifiedIHI.IndividualNameUsageType.L,                                 // Not used in this implementation
                //conditionalUse = nehta.mcaR40.CreateVerifiedIHI.ConditionalUseType.Item1,                         // Not used in this implementation
                conditionalUseSpecified = false,
                address = clsHelper.BuildAddressArray<
                        nehta.mcaR40.CreateVerifiedIHI.AddressType,
                        nehta.mcaR40.CreateVerifiedIHI.AustralianUnstructuredStreetAddressType,
                        nehta.mcaR40.CreateVerifiedIHI.StateType
                    >(root).First(),
                privacyNotification = true,                                                                         // Default to true as per specification
            };

            // Safely parse and set the date of birth if provided.
            if (root.TryGetProperty("dateOfBirthField", out var dobElem) &&
                dobElem.ValueKind == JsonValueKind.String &&
                DateTime.TryParse(dobElem.GetString(), out var dob))
            {
                request.dateOfBirth = dob;
                request.dateOfBirthAccuracyIndicator = nehta.mcaR40.CreateVerifiedIHI.DateAccuracyIndicatorType.AAA;
            }

            // Safely parse and set the sex if provided.
            if (root.TryGetProperty("sexField", out var sexElem) &&
                sexElem.ValueKind == JsonValueKind.String &&
                Enum.TryParse<nehta.mcaR40.CreateVerifiedIHI.SexType>(sexElem.GetString(), true, out var sexEnum))
            {
                request.sex = sexEnum;
            }

            return request;
        }
    }
}
