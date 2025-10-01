using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SuperCareServicesGroup.Models
{
    public class EmployeeTask
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Client { get; set; }
        public string Service { get; set; }
        public DateTime Date { get; set; }
        public string Status { get; set; }
        public string Address { get; set; }
        public string Description { get; set; }
        public decimal QuoteAmount { get; set; }
        public string Feedback { get; set; }
    }
}