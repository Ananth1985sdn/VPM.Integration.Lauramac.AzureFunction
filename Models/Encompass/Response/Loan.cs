using Newtonsoft.Json;

namespace VPM.Integration.Lauramac.AzureFunction.Models.Encompass.Response
{
    public class Loan
    {
        [JsonProperty("loanId")]
        public string LoanId { get; set; }
        [JsonProperty("fields")]
        public LoanFields Fields { get; set; }
    }
}
