using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VPM.Integration.Lauramac.AzureFunction.Models.Encompass
{
    public class Page
    {
        public PageImage PageImage { get; set; }
        public PageImage ThumbnailImage { get; set; }
        public long FileSize { get; set; }
        public int Rotation { get; set; }
    }
}
