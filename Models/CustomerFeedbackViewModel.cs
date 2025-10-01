using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SuperCareServicesGroup.Models
{
    public class CustomerFeedbackViewModel
    {
        public BookCleaning Booking { get; set; }
        public CustomerFeedback Feedback { get; set; }

        public List<string> FeedbackOptions { get; set; }
    }
}