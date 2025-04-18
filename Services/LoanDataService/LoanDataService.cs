﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VPM.Integration.Lauramac.AzureFunction.Interface.ILoanDataService;

namespace VPM.Integration.Lauramac.AzureFunction.Services.LoanDataService
{
    public class LoanDataService : ILoanDataService
    {
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
    }
}
