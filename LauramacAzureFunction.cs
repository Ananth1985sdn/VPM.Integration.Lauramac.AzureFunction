using System;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using VPM.Integration.Lauramac.AzureFunction.Interface.ILoanDataService;
using VPM.Integration.Lauramac.AzureFunction.Models.Encompass;
using VPM.Integration.Lauramac.AzureFunction.Models.Encompass.Request;
using VPM.Integration.Lauramac.AzureFunction.Services.LoanDataService;

namespace VPM.Integration.Lauramac.AzureFunction
{
    public class LauramacAzureFunction
    {
        private readonly ILogger _logger;
        private readonly ILoanDataService _loanDataService;

        public LauramacAzureFunction(ILoggerFactory loggerFactory, ILoanDataService loanDataService)
        {
            _logger = loggerFactory.CreateLogger<LauramacAzureFunction>();
            _loanDataService = loanDataService;
        }

        [Function("LauramacAzureFunction")]
        public async Task Run([TimerTrigger("0 */1 * * * *")] TimerInfo myTimer)
        {
            try
            {   
                // Your function logic here
                _logger.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
                string token = await GetEncompassAccessTokenAsync(_logger);

                if (myTimer.ScheduleStatus is not null)
                {
                    if (!string.IsNullOrEmpty(token))
                    {
                        await CallLoanPipelineApiAsync(token);
                    }
                    else
                    {
                        _logger.LogError("Failed to retrieve Encompass access token.");
                    }
                    _logger.LogInformation($"Next timer schedule at: {myTimer.ScheduleStatus.Next}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred: {ex.Message}");
            }            
        }

        public async Task<string> GetEncompassAccessTokenAsync(ILogger log)
        {
            var encompassBaseURL = Environment.GetEnvironmentVariable("EncompassApiBaseURL");
            var tokenUrl = Environment.GetEnvironmentVariable("EncompassTokenUrl");

            var credentials = new EncompassCredentials();
            var clientId = credentials.ClientId;
            var clientSecret = credentials.ClientSecret;
            var username = credentials.Username;
            var password = credentials.Password;

            var fullUrl = $"{encompassBaseURL.TrimEnd('/')}{tokenUrl}";

            string token = await _loanDataService.GetToken(username, password, clientId, clientSecret, fullUrl);
            return token;     
        }

        private async Task CallLoanPipelineApiAsync(string token)
        {
            var encompassBaseURL = Environment.GetEnvironmentVariable("EncompassApiBaseURL");
            var pipeLineUrl = Environment.GetEnvironmentVariable("EncompassLoanPipelineURL");
            var requestUrl = $"{encompassBaseURL.TrimEnd('/')}{pipeLineUrl}";

            var documentPackage = Environment.GetEnvironmentVariable("DocumentPackageName");

            var filterTerms = new List<FilterTerm>
            {
                new FilterTerm {
                    canonicalName = "Loan.CurrentMilestoneName",
                    value = new[] { "Started" },
                    matchType = "MultiValue",
                    include = true
                },
                new FilterTerm {
                    canonicalName = "Loan.LoanNumber",
                    value = "5",
                    matchType = "startsWith",
                    include = false
                },
                new FilterTerm {
                    canonicalName = "Fields.CX.DUEDILIGENCE_START_DT",
                    value = "04/11/2025",
                    matchType = "Equals",
                    precision = "Day"
                },
                new FilterTerm {
                    canonicalName = "Fields.CX.NAME_DDPROVIDER",
                    value = "Canopy",
                    matchType = "Exact",
                    include = true
                }
            };

            var requestBody = new
            {
                fields = new[]
                {
                    "Loan.LoanNumber", "Fields.19", "Fields.608", "Loan.LoanAmount", "Loan.LTV", "Fields.976",
                    "Loan.Address1", "Loan.City", "Loan.State", "Fields.15", "Fields.1041", "Loan.OccupancyStatus",
                    "Fields.1401", "Fields.CX.VP.DOC.TYPE", "Fields.4000", "Fields.4002", "Fields.CX.CREDITSCORE",
                    "Fields.325", "Fields.3", "Fields.742", "Fields.CX.VP.BUSINESS.PURPOSE", "Fields.1550",
                    "Fields.675", "Fields.QM.X23", "Fields.QM.X25", "Fields.2278"
                },
                filter = new
                {
                    @operator = "and",
                    terms = filterTerms
                },
                orgType = "internal",
                loanOwnership = "AllLoans",
                sortOrder = new[]
                {
                    new {
                        canonicalName = "Loan.LastModified",
                        order = "Descending"
                    }
                }
            };

            var json = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await httpClient.PostAsync(requestUrl, content);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Loan Pipeline Response: " + result);

                try
                {
                    var loans = JsonConvert.DeserializeObject<List<Loan>>(result);
                    _logger.LogInformation($"Number of Loans: {loans.Count}");

                    var documentsHttpClient = new HttpClient();
                    documentsHttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    documentsHttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    var baseUrl = Environment.GetEnvironmentVariable("EncompassApiBaseURL");
                    var endpointTemplate = Environment.GetEnvironmentVariable("EncompassGetDocumentsURL");

                    foreach (var loan in loans)
                    {
                        _logger.LogInformation($"Loan ID: {loan.LoanId}, Loan Number: {loan.Fields.LoanNumber}, Amount: {loan.Fields.LoanAmount}");

                        var endpoint = endpointTemplate.Replace("{loanId}", loan.LoanId);
                        var fullUrl = $"{baseUrl.TrimEnd('/')}/{endpoint.TrimStart('/')}";

                        var documentsResponse = await httpClient.GetAsync(fullUrl);

                        if (response.IsSuccessStatusCode)
                        {
                            var documentsResult = await documentsResponse.Content.ReadAsStringAsync();
                            _logger.LogInformation($"Attachments for Loan {loan.Fields.LoanNumber}: {documentsResult}");

                            var attachments = JsonConvert.DeserializeObject<List<Attachment>>(documentsResult);

                            foreach (var attachment in attachments)
                            {
                                if (attachment.AssignedTo?.EntityName != documentPackage || (attachment.FileSize <= 0 || attachment.Type != "Image"))
                                    continue;
                                else
                                {
                                    _logger.LogInformation($"Attachment Title: {attachment.Title}, CreatedBy: {attachment.AssignedTo?.EntityName}, File Size: {attachment.FileSize}");
                                    //loan.LoanId = "66b6fc88-f675-4cdd-b78a-214453cde1e9";
                                    //attachment.Id = "eb00e165-4ce6-4580-a39a-555067afdaca";
                                    var url = await GetDocumentURL(loan.LoanId, attachment.Id, token);
                                    if (url != null)
                                        await DownloadDocument(loan.LoanId, loan.Fields.Field4002, url);
                                    break;
                                }
                            }
                        }
                        else
                        {
                            var error = await response.Content.ReadAsStringAsync();
                            _logger.LogError($"Failed to fetch attachments for Loan {loan.Fields.LoanNumber}: {error}");
                        }
                    }

                }
                catch (JsonException ex)
                {
                    _logger.LogError($"Error deserializing response: {ex.Message}");
                }
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Loan Pipeline API failed with {response.StatusCode}: {error}");
            }
        }

        private async Task DownloadDocument(string loanId, string lastName, string documentURL)
        {

            if (string.IsNullOrWhiteSpace(documentURL))
            {
                throw new ArgumentException("Document URL cannot be null or empty.", nameof(documentURL));
            }

            using (var httpClient = new HttpClient())
            {

                var request = new HttpRequestMessage(HttpMethod.Get, documentURL);
                var response = await httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    throw new Exception($"Failed to download document. Status: {response.StatusCode}, Response: {errorContent}");
                }
                var contentType = response.Content.Headers.ContentType?.MediaType;
                _logger.LogInformation($"Content-Type: {contentType}");
                var pdfBytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

                var fileName = loanId + "_" + lastName + "_shippingfiles.pdf";

#if DEBUG
                var downloadsPath = Path.Combine(Directory.GetCurrentDirectory(), "Downloads");
#else
                var downloadsPath = Path.Combine(Path.GetTempPath(), "Downloads");
#endif

                if (!Directory.Exists(downloadsPath))
                {
                    Directory.CreateDirectory(downloadsPath);
                }

                var filePath = Path.Combine(downloadsPath, fileName);

                await File.WriteAllBytesAsync(filePath, pdfBytes).ConfigureAwait(false);

                Console.WriteLine($"PDF downloaded successfully to: {filePath}");
            }
        }

        private async Task<string> GetDocumentURL(string loanId, string attachmentId, string accessToken)
        {
            var encompassBaseURL = Environment.GetEnvironmentVariable("EncompassApiBaseURL");
            var documentURL = Environment.GetEnvironmentVariable("EncompassGetDocumentURL");

            if (string.IsNullOrWhiteSpace(encompassBaseURL) || string.IsNullOrWhiteSpace(documentURL))
            {
                throw new InvalidOperationException("Missing environment variables for Encompass API base URL or document URL endpoint.");
            }

            var documentURLEndpoint = documentURL.Replace("{loanId}", loanId);
            var requestUrl = $"{encompassBaseURL.TrimEnd('/')}{documentURLEndpoint}";

            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var payload = new
                {
                    attachments = new[] { attachmentId }
                };

                var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(requestUrl, content).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    throw new Exception($"Failed to get document URL. Status: {response.StatusCode}, Response: {error}");
                }

                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var responseObject = JsonConvert.DeserializeObject<DownloadUrlResponse>(json);

                if (responseObject?.Attachments == null || responseObject.Attachments.Count == 0)
                {
                    throw new Exception("No attachments found in the response.");
                }

                var attachment = responseObject.Attachments[0];
                var pages = attachment?.Pages;

                if (pages != null && pages.Count > 0)
                {
                    if (pages.Count == 1)
                    {
                        return pages[0].Url;
                    }
                    else
                    {
                        if (attachment.originalUrls != null && attachment.originalUrls.Count > 0)
                        {
                            return attachment.originalUrls[0];
                        }
                        else if (attachment.Pages != null && attachment.Pages.Count > 0)
                        {
                            return attachment.Pages[0].Url;
                        }
                        else
                        {
                            throw new Exception("No valid document URL found.");
                        }
                    }
                }

                throw new Exception("No pages found for the attachment.");
            }
        }
    }
}
