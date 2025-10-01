using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SuperCareServicesGroup.Models
{
    public class QuoteCalculator
    {
        public string CleaningType { get; set; }
        public string ServiceLevel { get; set; } 
        public decimal EstimatedPrice { get; set; }

        // Price calculation logic
        public decimal CalculatePrice()
        {
            decimal basePrice = 0;

            switch (CleaningType)
            {
                case "House": basePrice = 800; break;         
                case "Office": basePrice = 3000; break;
                case "School": basePrice = 7000; break;
                case "Hall": basePrice = 2500; break;
                case "Restaurant": basePrice = 4000; break;
                case "Retail": basePrice = 2000; break;
                case "Medical": basePrice = 5000; break;
                default: basePrice = 1000; break;
            }

            switch (ServiceLevel)
            {
                case "Standard": EstimatedPrice = basePrice; break;
                case "Deep": EstimatedPrice = basePrice * 1.5m; break;
                case "Move": EstimatedPrice = basePrice * 1.8m; break;
                case "PostEvent": EstimatedPrice = basePrice * 1.3m; break;
                case "Disinfection": EstimatedPrice = basePrice * 2m; break;
                default: EstimatedPrice = basePrice; break;
            }

            return EstimatedPrice;
        }
    }
}