﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VPM.Integration.Lauramac.AzureFunction.Interface.ILoanDataService
{
    public interface ILoanDataService
    {
       public Task<string> GetToken(string username, string password, string clientId, string clientSecret, string fullUrl);
    }
}
