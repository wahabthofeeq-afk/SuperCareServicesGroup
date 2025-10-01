using System;
using System.ComponentModel.DataAnnotations;

namespace SuperCareServicesGroup.Models
{
    public class QuotePayment
    {
        public int Id { get; set; }
        public int QuoteRequestID { get; set; }

        // 🔹 Enum for Cleaning Type
        public enum CleaningType
        {
            House,
            Office,
            School,
            Hall,
            Restaurant,
            Retail,
            Medical
        }

        // 🔹 Enum for Service Level
        public enum ServiceLevel
        {
            Standard,
            Deep,
            Move,
            PostEvent,
            Disinfection
        }

        // 🔹 Enum for Cleaning Details (Call Outs)
        public enum CleaningDetails
        {
            // House
            Windows,
            Carpet,
            Kitchen,
            Bathroom,
            Bedroom,
            LivingRoom,
            DiningRoom,
            Garage,
            Garden,
            Patio,
            FurniturePolish,
            Walls,
            Ceilings,
            Doors,

            // Office
            DeskCleaning,
            ComputerEquipment,
            ConferenceRooms,
            OfficeCarpet,
            WindowsOffice,
            BreakRoom,
            RestroomsOffice,
            TrashDisposal,

            // School
            ClassroomCleaning,
            LaboratoryCleaning,
            GymCleaning,
            Hallways,
            Cafeteria,
            RestroomsSchool,
            Playground,

            // Hall / Event Venue
            StageCleaning,
            SeatingAreas,
            RestroomsHall,
            KitchenHall,
            FloorPolish,
            DecorationCleanup,

            // Restaurant
            KitchenDeepClean,
            DiningArea,
            BarArea,
            RestroomsRestaurant,
            FloorsRestaurant,
            WindowsRestaurant,
            TrashDisposalRestaurant,

            // Retail
            SalesFloor,
            Shelves,
            WindowsRetail,
            StorageRooms,
            RestroomsRetail,
            CheckoutAreas,

            // Medical
            PatientRooms,
            WaitingArea,
            OperatingTheatre,
            EquipmentSanitization,
            RestroomsMedical,
            ReceptionArea,
            LaboratoryMedical
        }

        // 🔹 Properties
        [Required]
        public CleaningType Cleaning { get; set; }

        [Required]
        public ServiceLevel Level { get; set; }

        [Required]
        public CleaningDetails Details { get; set; }  // ✅ Call Out selection

        public decimal? SavedQuoteAmount { get; set; }
        public string Location { get; set; }

        [Required(ErrorMessage = "Preferred date is required.")]
        public DateTime? PreferredDate { get; set; }

        // Calculate base price based on cleaning type
        public decimal CalcCleaningType()
        {
            switch (Cleaning)
            {
                case CleaningType.House: return 500;
                case CleaningType.Office: return 800;
                case CleaningType.School: return 2000;
                case CleaningType.Hall: return 1000;
                case CleaningType.Restaurant: return 1500;
                case CleaningType.Retail: return 1200;
                case CleaningType.Medical: return 2000;
                default: return 0;
            }
        }

        // Calculate multiplier based on service level
        public decimal CalcServiceLevel()
        {
            switch (Level)
            {
                case ServiceLevel.Standard: return 1.0m;
                case ServiceLevel.Deep: return 1.5m;
                case ServiceLevel.Move: return 2.0m;
                case ServiceLevel.PostEvent: return 1.8m;
                case ServiceLevel.Disinfection: return 2.2m;
                default: return 1.0m;
            }
        }

        // Calculate total
        public decimal calcTotal()
        {
            return CalcCleaningType() * CalcServiceLevel();
        }
    }
}
