using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SuperCareServicesGroup.Models
{
    public class EquipmentPackage
    {
        public string PackageName { get; set; }
        public List<string> Equipment { get; set; }
    }
}