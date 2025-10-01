using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SuperCareServicesGroup.Models
{
    public class EmployeeAssignmentViewModel
    {
        public int RegisteredEmployeeID { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string SkillQualification { get; set; }   // <--- included
        public bool IsChecked { get; set; }
    }
}