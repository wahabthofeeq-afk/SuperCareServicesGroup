using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SuperCareServicesGroup.Models
{
    public class BookingUpdateModel
    {
        public int BookCleaningID { get; set; }
        public string PreferredDate { get; set; }
        public string TimeSlot { get; set; }
    }
}