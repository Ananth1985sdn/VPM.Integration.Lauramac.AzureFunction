using System;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using VPM.Integration.Lauramac.AzureFunction.Interface.ILoanDataService;
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

            var clientId = Environment.GetEnvironmentVariable("EncompassClientId");
            var clientSecret = Environment.GetEnvironmentVariable("EncompassClientSecret");
            var username = Environment.GetEnvironmentVariable("EncompassUsername");
            var password = Environment.GetEnvironmentVariable("EncompassPassword");
            var fullUrl = $"{encompassBaseURL.TrimEnd('/')}{tokenUrl}";

            string token = await _loanDataService.GetToken(username, password, clientId, clientSecret, fullUrl);
            return token;     
        }
    }
}
