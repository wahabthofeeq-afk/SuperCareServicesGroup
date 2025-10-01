using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SuperCareServicesGroup.Models
{
    public class InvoiceHistoryViewModel
    {
        public IEnumerable<QuoteRequest> CompletedQuotes { get; set; }
        public IEnumerable<BookCleaning> ConfirmedBookings { get; set; }
    }
}