using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VPM.Integration.Lauramac.AzureFunction.Models.Encompass.Response;

namespace VPM.Integration.Lauramac.AzureFunction.Models.Encompass
{
    public class Loan
    {
        [JsonProperty("loanId")]
        public string LoanId { get; set; }
        [JsonProperty("fields")]
        public LoanFields Fields { get; set; }
    }
}
