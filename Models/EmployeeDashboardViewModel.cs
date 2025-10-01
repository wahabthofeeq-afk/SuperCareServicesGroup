using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SuperCareServicesGroup.Models
{
    public class EmployeeDashboardViewModel
    {
        public EmployeeClocking LastClock { get; set; }
        public List<EmployeeClocking> Clockings { get; set; }
    }
}