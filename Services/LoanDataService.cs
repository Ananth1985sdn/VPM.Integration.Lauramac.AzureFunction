using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using VPM.Integration.Lauramac.AzureFunction.Interface;
using VPM.Integration.Lauramac.AzureFunction.Models.Encompass.Response;

namespace VPM.Integration.Lauramac.AzureFunction.Services
{
    public class LoanDataService : ILoanDataService
    {
        public async Task<string> GetLoanData(string requestUrl, StringContent content,string accessToken)
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                var response = await client.PostAsync(requestUrl, content);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return json;
                }
                else
                {
                    // Handle error response
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Error: {response.StatusCode}, Content: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                throw new NotImplementedException();
            }
        }

        public async Task<string> GetToken(string username, string password, string clientId, string clientSecret, string fullUrl)
        {
            try
            {
                using var client = new HttpClient();

                var requestBody = new FormUrlEncodedContent(new[]
                 {
                new KeyValuePair<string, string>("grant_type", "password"),
                new KeyValuePair<string, string>("username", username),
                new KeyValuePair<string, string>("password", password),
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("client_secret", clientSecret),
                new KeyValuePair<string, string>("scope", "lp")
            });


                var response = await client.PostAsync(fullUrl, requestBody);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    dynamic obj = JsonConvert.DeserializeObject(json);
                    return obj.access_token;
                }
                else
                {
                    // Handle error response
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Error: {response.StatusCode}, Content: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                // Handle exception
                throw new NotImplementedException();
            }
        }

        public async Task<string> GetAllLoanDocuments(string accessToken, string loanId)
        {
            try
            {
                using var client = new HttpClient();

                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var baseUrl = Environment.GetEnvironmentVariable("EncompassApiBaseURL");
                var endpointTemplate = Environment.GetEnvironmentVariable("EncompassGetDocumentsURL");

                var endpoint = endpointTemplate.Replace("{loanId}", loanId);
                var fullUrl = $"{baseUrl.TrimEnd('/')}/{endpoint.TrimStart('/')}";

                var response = await client.GetAsync(fullUrl);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return json;
                }
                else
                {
                    // Handle error response
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Error: {response.StatusCode}, Content: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                throw new NotImplementedException();
            }
        }

        public async Task<string> GetDocumentUrl(string loanId, string attachmentId, string accessToken)
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
                var responseObject = JsonConvert.DeserializeObject<AttachmentDownloadUrlResponse>(json);

                if (responseObject?.AttachmentUrls == null || responseObject.AttachmentUrls.Count == 0)
                {
                    throw new Exception("No attachments found in the response.");
                }

                var attachment = responseObject.AttachmentUrls[0];
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
