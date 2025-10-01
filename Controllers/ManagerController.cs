using Antlr.Runtime.Misc;
using SuperCareServicesGroup.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace SuperCareServicesGroup.Controllers
{
    public class ManagerController : Controller
    {
        private SuperCareServicesNewEntities1 db = new SuperCareServicesNewEntities1();


        // Helper method for priority calculation
        private string DeterminePriority(DateTime preferredDate)
        {
            var today = DateTime.Today;
            var daysDifference = (preferredDate - today).TotalDays;

            if (daysDifference <= 2) return "High";
            if (daysDifference <= 5) return "Medium";
            return "Low";
        }
        // ----------------- MANAGER PROFILE -----------------
        [HttpGet]
        public ActionResult ManagerProfile()
        {
            if (Session["ManagerID"] == null)
            {
                return RedirectToAction("ManagerLogin", "Account");
            }

            int managerId = (int)Session["ManagerID"];
            var manager = db.Managers.Find(managerId);

            if (manager == null)
                return HttpNotFound();

            return View(manager);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ManagerProfile(Manager model, HttpPostedFileBase ProfileImage)
        {
            if (!ModelState.IsValid)
                return View(model);

            var manager = db.Managers.Find(model.ManagerID);
            if (manager == null)
                return HttpNotFound();

            manager.FirstName = model.FirstName;
            manager.LastName = model.LastName;
            manager.PhoneNumber = model.PhoneNumber;

            // Store password as plain text (NO HASHING)
            if (!string.IsNullOrWhiteSpace(model.PasswordHash))
                manager.PasswordHash = model.PasswordHash;

            // Handle profile image upload
            if (ProfileImage != null && ProfileImage.ContentLength > 0)
            {
                var validationError = ValidateAndSaveProfileImage(ProfileImage, out string relativeUrl);
                if (validationError != null)
                {
                    ModelState.AddModelError("", validationError);
                    return View(model);
                }
                manager.ProfilePic = relativeUrl;
            }

            try
            {
                db.SaveChanges();
                TempData["Message"] = "Profile updated successfully!";
                return RedirectToAction("ManagerProfile");
            }
            catch (System.Data.Entity.Validation.DbEntityValidationException ex)
            {
                foreach (var validationErrors in ex.EntityValidationErrors)
                {
                    foreach (var validationError in validationErrors.ValidationErrors)
                        System.Diagnostics.Debug.WriteLine(
                            $"Property: {validationError.PropertyName} Error: {validationError.ErrorMessage}");
                }
                ViewBag.ValidationError = "There was a problem saving your profile. Please check all fields and try again.";
                return View(model);
            }
        }

        // ----------------- AJAX UPDATE PROFILE -----------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateProfileAjax(string Email, string FirstName, string LastName, string PhoneNumber, string NewPassword, HttpPostedFileBase ProfilePic)
        {
            if (string.IsNullOrWhiteSpace(Email))
                return Json(new { success = false, error = "Email is required." });

            try
            {
                var manager = db.Managers.SingleOrDefault(m => m.Email == Email);
                if (manager == null)
                    return Json(new { success = false, error = "Manager not found." });

                // Update fields
                manager.FirstName = FirstName?.Trim();
                manager.LastName = LastName?.Trim();
                manager.PhoneNumber = PhoneNumber?.Trim();

                // Store password as plain text (NO HASHING)
                if (!string.IsNullOrWhiteSpace(NewPassword))
                    manager.PasswordHash = NewPassword;

                if (ProfilePic != null && ProfilePic.ContentLength > 0)
                {
                    var validationError = ValidateAndSaveProfileImage(ProfilePic, out string relativeUrl);
                    if (validationError != null)
                        return Json(new { success = false, error = validationError });

                    manager.ProfilePic = relativeUrl;
                }

                db.SaveChanges();

                return Json(new
                {
                    success = true,
                    firstName = manager.FirstName,
                    lastName = manager.LastName,
                    phoneNumber = manager.PhoneNumber,
                    email = manager.Email,
                    profilePic = manager.ProfilePic
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ----------------- PASSWORD VERIFICATION -----------------
        [HttpPost]
        public JsonResult VerifyPassword(string password, string email)
        {
            var manager = db.Managers.FirstOrDefault(m => m.Email == email);
            if (manager == null)
            {
                return Json(new { success = false, error = "Manager not found" });
            }

            // PLAIN TEXT comparison (NO HASHING)
            if (manager.PasswordHash == password)
            {
                return Json(new { success = true });
            }

            return Json(new { success = false, error = "Invalid password" });
        }

        // ----------------- HELPERS -----------------
        private string ValidateAndSaveProfileImage(HttpPostedFileBase file, out string relativeUrl)
        {
            relativeUrl = null;

            const int maxFileSize = 2 * 1024 * 1024; // 2MB
            var allowed = new[] { "image/jpeg", "image/png", "image/gif" };

            if (file.ContentLength > maxFileSize)
                return "Profile picture must be under 2MB.";

            if (!allowed.Contains(file.ContentType))
                return "Only JPG, PNG, or GIF formats are allowed.";

            var ext = Path.GetExtension(file.FileName);
            var unique = Guid.NewGuid().ToString("N") + ext;

            var folder = Server.MapPath("~/Uploads/ProfilePics");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            var full = Path.Combine(folder, unique);
            file.SaveAs(full);

            relativeUrl = Url.Content("~/Uploads/ProfilePics/" + unique);
            return null;
        }

        // ----------------- OTHER MANAGER ACTIONS -----------------
        public ActionResult ManagerEmployeesInfo()
        {
            var registered = db.RegisteredEmployees.ToList();
            var pending = db.Employees.ToList();
            ViewBag.PendingEmployees = pending;
            return View(registered);
        }

        public ActionResult AllowEmployee(int id)
        {
            var employee = db.Employees.FirstOrDefault(e => e.Id == id);
            if (employee != null)
            {
                var registered = new RegisteredEmployee
                {
                    FirstName = employee.FirstName,
                    LastName = employee.LastName,
                    Email = employee.Email,
                    PhoneNumber = employee.PhoneNumber,
                    PasswordHash = employee.PasswordHash
                };

                db.RegisteredEmployees.Add(registered);
                db.Employees.Remove(employee);
                db.SaveChanges();
            }

            return RedirectToAction("ManagerEmployeesInfo");
        }

        public ActionResult DeclineEmployee(int id)
        {
            var employee = db.Employees.FirstOrDefault(e => e.Id == id);
            if (employee != null)
            {
                db.Employees.Remove(employee);
                db.SaveChanges();
            }

            return RedirectToAction("ManagerEmployeesInfo");
        }

        public ActionResult RemoveRegistered(int id)
        {
            try
            {
                var registered = db.RegisteredEmployees.Find(id);
                if (registered != null)
                {
                    var clockings = db.EmployeeClockings
                        .Where(c => c.RegisteredEmployeeId == id)
                        .ToList();

                    db.EmployeeClockings.RemoveRange(clockings);
                    db.RegisteredEmployees.Remove(registered);
                    db.SaveChanges();
                    TempData["SuccessMessage"] = "Employee removed successfully!";
                }
                return RedirectToAction("ManagerEmployeesInfo");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting employee: {ex.Message}");
                TempData["ErrorMessage"] = "Cannot delete employee because they have clock-in records. Please contact administrator.";
                return RedirectToAction("ManagerEmployeesInfo");
            }
        }



        // GET: Manager/ManagerAssignCleaningJob
        public ActionResult ManagerAssignCleaningJob()
        {
            if (Session["ManagerName"] == null)
                return RedirectToAction("ManagerLogin", "Account");

            try
            {
                // 1. Auto clock-out employees active over 24 hours
                var activeClockings = db.EmployeeClockings
                                        .Where(c => c.IsClockedIn)
                                        .ToList();

                foreach (var clock in activeClockings)
                {
                    var hours = (DateTime.Now - clock.ClockInTime).TotalHours;
                    if (hours >= 24)
                    {
                        clock.ClockOutTime = clock.ClockInTime.AddHours(24);
                        clock.IsClockedIn = false;

                        var employee = db.RegisteredEmployees
                                         .FirstOrDefault(e => e.RegisteredEmployeeID == clock.RegisteredEmployeeId);
                        if (employee != null)
                        {
                            employee.IsActive = false;
                        }
                    }
                }

                db.SaveChanges();

                // 2. Reset HasBeenAssigned if ALL are true
                var allEmployeesList = db.RegisteredEmployees.ToList();
                if (allEmployeesList.Count > 0 && allEmployeesList.All(e => e.HasBeenAssigned))
                {
                    foreach (var emp in allEmployeesList)
                    {
                        emp.HasBeenAssigned = false;
                    }
                    db.SaveChanges();
                }

                // 3. Employees grouped for tables
                var activeNotAssigned = db.RegisteredEmployees
                                          .Where(e => e.IsActive && !e.HasBeenAssigned)
                                          .ToList();

                var assignedEmployees = db.RegisteredEmployees
                                          .Where(e => e.HasBeenAssigned)
                                          .ToList();

                var allEmployees = db.RegisteredEmployees.ToList();

                // 4. Paid bookings that are NOT assigned
                var paidBookings = db.BookCleanings
                                     .Where(b => b.DepositPaid && !b.Assigned && b.Status != "Completed")
                                     .OrderByDescending(b => b.CreatedAt)
                                     .ToList();

                var bookingAssignments = db.BookCleaningEmployees.ToList();

                // 5. Pass data to view
                ViewBag.ActiveNotAssigned = activeNotAssigned;
                ViewBag.AssignedEmployees = assignedEmployees;
                ViewBag.AllEmployees = allEmployees;
                ViewBag.PaidBookCleanings = paidBookings;
                ViewBag.BookingAssignments = bookingAssignments;

                return View();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading ManagerAssignCleaningJob: {ex.Message}");
                TempData["ErrorMessage"] = "Error loading data. Please try again.";
                return View();
            }
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AssignEmployees(int bookCleaningID, int[] employeeIds)
        {
            if (employeeIds == null || employeeIds.Length == 0)
                return RedirectToAction("ManagerAssignCleaningJob");

            foreach (var empId in employeeIds)
            {
                // Prevent duplicate assignments
                var exists = db.BookCleaningEmployees
                               .Any(b => b.BookCleaningID == bookCleaningID && b.RegisteredEmployeeID == empId);

                if (!exists)
                {
                    db.BookCleaningEmployees.Add(new BookCleaningEmployee
                    {
                        BookCleaningID = bookCleaningID,
                        RegisteredEmployeeID = empId,
                        AssignedAt = DateTime.Now
                    });

                    // ✅ Mark employee as assigned
                    var assignedEmp = db.RegisteredEmployees.Find(empId);
                    if (assignedEmp != null)
                    {
                        assignedEmp.HasBeenAssigned = true;
                    }
                }
            }


            // Mark the BookCleaning as assigned
            var booking = db.BookCleanings.Find(bookCleaningID);
            if (booking != null)
            {
                booking.Assigned = true;
                booking.Status = "Pending";
            }

            db.SaveChanges();

            return RedirectToAction("ManagerAssignCleaningJob");
        }


        // GET: Manager/ManagerAssignJobs
        public ActionResult ManagerAssignJobs()
        {
            if (Session["ManagerName"] == null)
            {
                TempData["ErrorMessage"] = "Please login as manager to access this page.";
                return RedirectToAction("ManagerLogin", "Account");
            }

            try
            {
                // 1. Auto clock-out employees active over 24 hours
                var activeClockings = db.EmployeeClockings.Where(c => c.IsClockedIn).ToList();

                foreach (var clock in activeClockings)
                {
                    var hours = (DateTime.Now - clock.ClockInTime).TotalHours;
                    if (hours >= 24)
                    {
                        clock.ClockOutTime = clock.ClockInTime.AddHours(24);
                        clock.IsClockedIn = false;

                        var employee = db.RegisteredEmployees
                            .FirstOrDefault(e => e.RegisteredEmployeeID == clock.RegisteredEmployeeId);

                        if (employee != null)
                        {
                            employee.IsActive = false;
                        }
                    }
                }

                db.SaveChanges();

                // 2. Active employees not assigned
                var activeNotAssigned = db.RegisteredEmployees
                    .Where(e => e.IsActive && !e.HasBeenAssigned)
                    .ToList();

                // 3. Employees already assigned
                var assignedEmployees = db.RegisteredEmployees
                    .Where(e => e.HasBeenAssigned)
                    .ToList();

                // 4. All registered employees
                var allEmployees = db.RegisteredEmployees.ToList();

                // ✅ Reset HasBeenAssigned if ALL employees are true
                if (allEmployees.Any() && allEmployees.All(e => e.HasBeenAssigned))
                {
                    foreach (var emp in allEmployees)
                    {
                        emp.HasBeenAssigned = false;
                    }
                    db.SaveChanges();

                    // Rebuild lists since assignments changed
                    activeNotAssigned = db.RegisteredEmployees
                        .Where(e => e.IsActive && !e.HasBeenAssigned)
                        .ToList();

                    assignedEmployees = new List<RegisteredEmployee>(); // now empty
                }

                // 5. Quotes
                var quotesAwaitingConfirmation = db.QuoteRequests
                    .Where(q => !q.Confirmation)
                    .OrderByDescending(q => q.SubmittedAt)
                    .ToList();

                var paidQuotes = db.QuoteRequests
                    .Where(q => q.IsPaid && q.Confirmation)
                    .OrderByDescending(q => q.SubmittedAt)
                    .ToList();

                // 6. Pass data to view
                ViewBag.ActiveNotAssigned = activeNotAssigned;
                ViewBag.AssignedEmployees = assignedEmployees;
                ViewBag.AllEmployees = allEmployees;
                ViewBag.QuotesAwaitingConfirmation = quotesAwaitingConfirmation;
                ViewBag.PaidQuotes = paidQuotes;

                return View();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading ManagerAssignJobs: {ex.Message}");
                TempData["ErrorMessage"] = "Error loading data. Please try again.";
                return View();
            }
        }


        // POST: Manager/AssignEmployee
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AssignEmployee(int QuoteRequestID, int employeeId)
        {
            if (Session["ManagerName"] == null)
            {
                TempData["ErrorMessage"] = "Please login as manager to perform this action.";
                return RedirectToAction("ManagerLogin", "Account");
            }

            try
            {
                var quoteRequest = db.QuoteRequests.Find(QuoteRequestID);
                if (quoteRequest == null)
                {
                    TempData["ErrorMessage"] = "Quote request not found.";
                    return RedirectToAction("ManagerAssignJobs");
                }

                var employee = db.RegisteredEmployees.Find(employeeId);
                if (employee == null)
                {
                    TempData["ErrorMessage"] = "Employee not found.";
                    return RedirectToAction("ManagerAssignJobs");
                }

                // Only assign to confirmed quotes
                if (!quoteRequest.Confirmation)
                {
                    TempData["ErrorMessage"] = "Cannot assign employees to unconfirmed quotes.";
                    return RedirectToAction("ManagerAssignJobs");
                }

                quoteRequest.AssignedEmployeeID = employeeId;
                quoteRequest.Status = "In Progress";  // Or your status logic

                // ✅ Mark employee as assigned
                employee.HasBeenAssigned = true;
                db.SaveChanges();

                TempData["SuccessMessage"] = $"Successfully assigned {employee.FirstName} {employee.LastName} to quote #{QuoteRequestID}.";
                return RedirectToAction("AssignedCallouts");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error assigning employee: {ex.Message}");
                TempData["ErrorMessage"] = "Error assigning employee. Please try again.";
                return RedirectToAction("ManagerAssignJobs");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RejectQuote(int QuoteRequestID)
        {
            if (Session["ManagerName"] == null)
            {
                TempData["ErrorMessage"] = "Please login as manager to perform this action.";
                return RedirectToAction("ManagerLogin", "Account");
            }

            var quote = db.QuoteRequests.Find(QuoteRequestID);
            if (quote == null)
            {
                TempData["ErrorMessage"] = "Quote request not found.";
                return RedirectToAction("ManagerAssignJobs");
            }

            // Delete any linked BookCleaning records first
            var linkedBookings = db.BookCleanings.Where(b => b.QuoteRequestID == QuoteRequestID).ToList();
            foreach (var booking in linkedBookings)
            {
                db.BookCleanings.Remove(booking);
            }

            // Now delete the quote
            db.QuoteRequests.Remove(quote);
            db.SaveChanges();

            TempData["SuccessMessage"] = $"Quote #{QuoteRequestID} has been rejected and deleted.";
            return RedirectToAction("AssignedCallouts");
        }

      
        // GET: ManagerAllowBooking
        public ActionResult ManagerAllowBooking()
        {
            // Get quotes that are completed and not yet allowed for booking
            var pendingBookingQuotes = db.QuoteRequests
                .Where(q => q.Status.ToLower() == "completed" && q.AllowBooking == false)
                .OrderByDescending(q => q.PreferredDate)
                .ToList();

            ViewBag.CompletedQuotes = pendingBookingQuotes;

            // Get all employees for potential assignment
            ViewBag.Employees = db.RegisteredEmployees.ToList();

            return View();
        }
        [HttpPost]
        public JsonResult CreateBookCleaning(int QuoteRequestID, string CustomerName, string CleaningType,
     string Details, string CustomerLocation, decimal CleaningAmount, decimal Deposit, string ServiceLevel)
        {
            var quote = db.QuoteRequests.Find(QuoteRequestID);
            if (quote == null)
            {
                return Json(new { success = false });
            }

            BookCleaning book = new BookCleaning
            {
                CustomerId = quote.CustomerID,
                QuoteRequestID = QuoteRequestID,
                CustomerName = CustomerName,
                CleaningType = CleaningType,
                ServiceLevel = ServiceLevel,
                Details = Details,
                CustomerLocation = CustomerLocation,
                CleaningAmount = CleaningAmount,
                Deposit = Deposit,
                CreatedAt = DateTime.Now
            };

            db.BookCleanings.Add(book);
            quote.AllowBooking = true;
            db.SaveChanges();

            return Json(new { success = true });
        }


        // Assigned Callouts Controller
        public ActionResult AssignedCallouts()
        {
            if (Session["ManagerName"] == null)
            {
                TempData["ErrorMessage"] = "Please login as manager to access this page.";
                return RedirectToAction("ManagerLogin", "Account");
            }

            try
            {
                // Get employees
                var employees = db.RegisteredEmployees.ToList();

                // Get paid quotes that are assigned to employees
                var assignedQuotes = db.QuoteRequests
                    .Where(q => q.IsPaid && q.Confirmation && q.AssignedEmployeeID != null)
                    .OrderByDescending(q => q.SubmittedAt)
                    .ToList();

                ViewBag.RegisteredEmployees = employees;
                ViewBag.PaidQuotes = assignedQuotes;

                return View();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading AssignedCallouts: {ex.Message}");
                TempData["ErrorMessage"] = "Error loading data. Please try again.";
                return View();
            }
        }


        public ActionResult ConfirmCallout()
        {
            if (Session["ManagerName"] == null)
                return RedirectToAction("ManagerLogin", "Account");

            var quotesAwaitingConfirmation = db.QuoteRequests
                .Where(q => !q.IsPaid && !q.Confirmation && q.AssignedEmployeeID == null)
                .OrderByDescending(q => q.SubmittedAt)
                .ToList();

            ViewBag.QuotesAwaitingConfirmation = quotesAwaitingConfirmation;

            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ConfirmCallout(int QuoteRequestID, string action)
        {
            if (Session["ManagerName"] == null)
                return RedirectToAction("ManagerLogin", "Account");

            var quote = db.QuoteRequests.Find(QuoteRequestID);
            if (quote == null)
            {
                TempData["ErrorMessage"] = "Quote not found.";
                return RedirectToAction("ConfirmCallout");
            }

            if (action == "Accept")
            {
                quote.Confirmation = true;
                quote.Status = "Pending";
                TempData["SuccessMessage"] = $"Callout #{QuoteRequestID} confirmed successfully!";
            }
            else if (action == "Reject")
            {
                var linkedBookings = db.BookCleanings.Where(b => b.QuoteRequestID == QuoteRequestID).ToList();
                foreach (var booking in linkedBookings)
                {
                    db.BookCleanings.Remove(booking);
                }

                db.QuoteRequests.Remove(quote);
                TempData["SuccessMessage"] = $"Callout #{QuoteRequestID} has been rejected and removed.";
            }

            db.SaveChanges();
            return RedirectToAction("ConfirmCallout");
        }


        public ActionResult ManagerCompletedJobs()
        {
            if (Session["ManagerID"] == null)
                return RedirectToAction("ManagerLogin", "Account");

            // Get all completed jobs
            var completedJobs = db.BookCleanings
                .Where(q => q.Status == "Completed")
                .OrderByDescending(q => q.StartTime)
                .ToList();

            ViewBag.CompletedJobs = completedJobs;

            return View();
        }


        public ActionResult ManagerFeedback()
        {
            var feedbacks = db.CustomerFeedbacks.ToList();
            return View(feedbacks);
        }

        // POST: Manager/DeleteFeedback/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteFeedback(int BookCleaningID)
        {
            var feedback = db.CustomerFeedbacks.FirstOrDefault(f => f.BookCleaningID == BookCleaningID);
            if (feedback == null)
            {
                return HttpNotFound("Feedback not found");
            }

            db.CustomerFeedbacks.Remove(feedback);
            db.SaveChanges();

            TempData["SuccessMessage"] = "Feedback deleted successfully!";
            return RedirectToAction("ManagerFeedback");
        }

        // Hardcoded master equipment list
        private static readonly List<string> EquipmentOptions = new List<string>
{
    "Vacuum Cleaner", "Mop", "Bucket", "Glass Cleaner", "Furniture Polish",
    "Industrial Vacuum", "Disinfectant Sprays", "Dusting Cloths", "Carpet Cleaner",
    "Trash Bags", "Protective Gloves", "Medical-Grade Disinfectant", "Face Masks",
    "Biohazard Bags", "Sanitizer Fogger", "Floor Scrubber", "Degreaser",
    "Trash Bins", "Broom & Dustpan", "Sanitizing Spray", "Mop & Bucket", "Floor Cleaner"
};

        public ActionResult ManagerServicePackages()
        {
            // Ensure there is always a row with ID == 1
            var row = db.ServicePackages.FirstOrDefault(p => p.Id == 1);
            if (row == null)
            {
                row = new ServicePackage
                {
                    Standard = "",
                    Deep = "",
                    Disinfection = "",
                    PostEvent = "",
                    Move = ""
                };
                db.ServicePackages.Add(row);
                db.SaveChanges();
            }

            // Prepare model
            var packages = new List<EquipmentPackage>
    {
        new EquipmentPackage { PackageName = "Standard", Equipment = row.Standard?.Split(',').Where(s => !string.IsNullOrEmpty(s)).ToList() ?? new List<string>() },
        new EquipmentPackage { PackageName = "Deep", Equipment = row.Deep?.Split(',').Where(s => !string.IsNullOrEmpty(s)).ToList() ?? new List<string>() },
        new EquipmentPackage { PackageName = "Disinfection", Equipment = row.Disinfection?.Split(',').Where(s => !string.IsNullOrEmpty(s)).ToList() ?? new List<string>() },
        new EquipmentPackage { PackageName = "PostEvent", Equipment = row.PostEvent?.Split(',').Where(s => !string.IsNullOrEmpty(s)).ToList() ?? new List<string>() },
        new EquipmentPackage { PackageName = "Move", Equipment = row.Move?.Split(',').Where(s => !string.IsNullOrEmpty(s)).ToList() ?? new List<string>() }
    };

            ViewBag.EquipmentOptions = EquipmentOptions; // full master list
            return View(packages);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdatePackage(string packageName, List<string> selectedEquipment)
        {
            // Get the package row with ID == 1
            var packageRow = db.ServicePackages.FirstOrDefault(p => p.Id == 1);
            if (packageRow == null)
            {
                // Create if it doesn't exist
                packageRow = new ServicePackage
                {
                    Standard = "",
                    Deep = "",
                    Disinfection = "",
                    PostEvent = "",
                    Move = ""
                };
                db.ServicePackages.Add(packageRow);
            }

            // Convert selected equipment to CSV
            string equipmentCsv = selectedEquipment != null && selectedEquipment.Any()
                                  ? string.Join(",", selectedEquipment)
                                  : "";

            // Update the correct column based on packageName
            switch (packageName)
            {
                case "Standard":
                    packageRow.Standard = equipmentCsv;
                    break;
                case "Deep":
                    packageRow.Deep = equipmentCsv;
                    break;
                case "Disinfection":
                    packageRow.Disinfection = equipmentCsv;
                    break;
                case "PostEvent":
                    packageRow.PostEvent = equipmentCsv;
                    break;
                case "Move":
                    packageRow.Move = equipmentCsv;
                    break;
            }

            db.SaveChanges(); // save changes to DB

            TempData["SuccessMessage"] = $"{packageName} package updated successfully.";
            return RedirectToAction("ManagerServicePackages");
        }

        public JsonResult GetPendingCount()
        {
            var count = db.QuoteRequests
                .Where(q => !q.IsPaid && !q.Confirmation && q.AssignedEmployeeID == null)
                .Count();

            return Json(count, JsonRequestBehavior.AllowGet);
        }

        public JsonResult GetUnassignedPaidConfirmedCount()
        {
            var count = db.QuoteRequests
                .Where(q => q.Confirmation
                            && q.IsPaid
                            && (q.AssignedEmployeeID == null || q.AssignedEmployeeID == 0))
                .Count();

            return Json(count, JsonRequestBehavior.AllowGet);
        }
        public JsonResult GetAllowBookingCount()
        {
            var count = db.QuoteRequests
                .Count(q => q.Status.ToLower() == "completed" && q.AllowBooking == false);
            return Json(count, JsonRequestBehavior.AllowGet);
        }

        public JsonResult GetUnassignedPaidCleaningCount()
        {
            // Count all BookCleaning records where DepositPaid is true and Assigned is false
            var count = db.BookCleanings
                          .Where(b => b.DepositPaid && !b.Assigned)
                          .Count();

            return Json(count, JsonRequestBehavior.AllowGet);
        }

    }




}



