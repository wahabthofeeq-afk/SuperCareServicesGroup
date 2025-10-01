using iTextSharp.text;
using iTextSharp.text.pdf;
using Newtonsoft.Json;
using SuperCareServicesGroup.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using ZXing; // BarcodeReader is here
using ZXing.Common;
namespace SuperCareServicesGroup.Controllers
{
    public class EmployeeController : Controller
    {
        private SuperCareServicesNewEntities1 db = new SuperCareServicesNewEntities1();
        public ActionResult EmployeeClock()
        {
            if (Session["RegisteredEmployeeID"] == null)
                return RedirectToAction("EmployeeLogin", "Account");

            int employeeId = (int)Session["RegisteredEmployeeID"];

            // Get all clocking records
            var clockings = db.EmployeeClockings
                              .Where(c => c.RegisteredEmployeeId == employeeId)
                              .OrderByDescending(c => c.ClockInTime)
                              .ToList();

            var model = new EmployeeDashboardViewModel
            {
                LastClock = clockings.FirstOrDefault(),
                Clockings = clockings
            };

            return View(model); // Pass model here
        }

        // Clock In
        public ActionResult ClockIn(int employeeId)
        {
            var lastClock = db.EmployeeClockings
                .Where(c => c.RegisteredEmployeeId == employeeId)
                .OrderByDescending(c => c.ClockInTime)
                .FirstOrDefault();

            if (lastClock != null && lastClock.IsClockedIn)
            {
                // Check if last clock exceeds 24 hours
                var hours = (DateTime.Now - lastClock.ClockInTime).TotalHours;
                if (hours >= 24)
                {
                    // Auto clock out at 24 hours
                    lastClock.ClockOutTime = lastClock.ClockInTime.AddHours(24);
                    lastClock.IsClockedIn = false;

                    // Update IsActive in BookCleaning
                    var activeBookings = db.RegisteredEmployees
                                           .Where(b => b.RegisteredEmployeeID == employeeId)
                                           .ToList();
                    foreach (var booking in activeBookings)
                    {
                        booking.IsActive = false;
                    }

                    db.SaveChanges();
                    TempData["Message"] = "You were automatically clocked out after 24 hours.";
                }
                else
                {
                    TempData["Message"] = "You are already clocked in!";
                    return RedirectToAction("EmployeeClock");
                }
            }

            // Create a new clock-in record
            var clock = new EmployeeClocking
            {
                RegisteredEmployeeId = employeeId,
                ClockInTime = DateTime.Now,
                IsClockedIn = true
            };

            db.EmployeeClockings.Add(clock);

            // Update IsActive in BookCleaning
            var activeBookingsNew = db.RegisteredEmployees
                                      .Where(b => b.RegisteredEmployeeID == employeeId)
                                      .ToList();
            foreach (var booking in activeBookingsNew)
            {
                booking.IsActive = true;
            }

            db.SaveChanges();

            TempData["Message"] = "You have successfully clocked in!";
            return RedirectToAction("EmployeeClock");
        }

        // Clock Out
        public ActionResult ClockOut(int employeeClockingId)
        {
            int employeeId = (int)Session["RegisteredEmployeeID"];

            var clock = db.EmployeeClockings
                .FirstOrDefault(c => c.ClockingID == employeeClockingId
                                  && c.RegisteredEmployeeId == employeeId);

            if (clock == null || !clock.IsClockedIn)
            {
                TempData["Message"] = "No active clock to clock out!";
                return RedirectToAction("EmployeeClock");
            }

            // Calculate total hours
            var totalHours = (DateTime.Now - clock.ClockInTime).TotalHours;

            if (totalHours > 24)
            {
                clock.ClockOutTime = clock.ClockInTime.AddHours(24);
            }
            else
            {
                clock.ClockOutTime = DateTime.Now;
            }

            clock.IsClockedIn = false;

            // Update IsActive in BookCleaning
            var activeBookings = db.RegisteredEmployees
                                   .Where(b => b.RegisteredEmployeeID == employeeId)
                                   .ToList();

            foreach (var booking in activeBookings)
            {
                booking.IsActive = false;
            }

            db.SaveChanges();

            TempData["Message"] = "You have successfully clocked out!";
            return RedirectToAction("EmployeeClock");
        }

        // GET: EmployeeProfile
        [HttpGet]
        public ActionResult EmployeeProfile()
        {
            if (Session["RegisteredEmployeeID"] == null)
                return RedirectToAction("EmployeeLogin", "Account");

            int employeeId = (int)Session["RegisteredEmployeeID"];
            var employee = db.RegisteredEmployees.Find(employeeId);

            if (employee == null)
                return HttpNotFound();

            return View(employee);
        }

        // POST: Verify Password for Editing
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult VerifyPassword(string Email, string Password)
        {
            var employee = db.RegisteredEmployees.SingleOrDefault(e => e.Email == Email);
            if (employee == null) return Json(new { success = false, error = "Employee not found." });

            bool verified = employee.PasswordHash == Password;
            if (verified) return Json(new { success = true });
            return Json(new { success = false, error = "Invalid password." });
        }


        // POST: AJAX Update Profile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult UpdateProfileAjax()
        {
            try
            {
                var form = Request.Form;
                string email = form["Email"];
                var employee = db.RegisteredEmployees.SingleOrDefault(e => e.Email == email);
                if (employee == null)
                    return Json(new { success = false, error = "Employee not found." });

                employee.FirstName = form["FirstName"];
                employee.LastName = form["LastName"];
                employee.PhoneNumber = form["PhoneNumber"];

                // Skills
                var skills = form.GetValues("SkillQualification");
                employee.SkillQualification = (skills != null && skills.Length > 0) ? string.Join(",", skills) : null;

                // Password
                string newPassword = form["NewPassword"];
                if (!string.IsNullOrWhiteSpace(newPassword))
                    employee.PasswordHash = newPassword;

                // Profile pic
                if (Request.Files["ProfilePic"] != null && Request.Files["ProfilePic"].ContentLength > 0)
                {
                    var file = Request.Files["ProfilePic"];
                    string validationError = ValidateAndSaveProfileImage(file, out string relativeUrl);
                    if (validationError != null)
                        return Json(new { success = false, error = validationError });
                    employee.ProfilePic = relativeUrl;
                }

                db.SaveChanges();

                return Json(new
                {
                    success = true,
                    firstName = employee.FirstName,
                    lastName = employee.LastName,
                    phoneNumber = employee.PhoneNumber,
                    email = employee.Email,
                    skill = employee.SkillQualification,
                    profilePic = employee.ProfilePic
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult GenerateBusinessCard(int id)
        {
            var employee = db.RegisteredEmployees.Find(id);
            if (employee == null) return HttpNotFound();

            // Generate unique alphanumeric value
            string alphaVal = "EMP" + employee.RegisteredEmployeeID.ToString("D4");
            employee.AlphaNumericVal = alphaVal; // Save to new column

            string folderPath = Server.MapPath("~/Content/BusinessCards/");
            if (!System.IO.Directory.Exists(folderPath))
                System.IO.Directory.CreateDirectory(folderPath);

            var barcodeWriter = new ZXing.BarcodeWriterPixelData
            {
                Format = ZXing.BarcodeFormat.CODE_128,
                Options = new ZXing.Common.EncodingOptions { Height = 100, Width = 300, Margin = 10 }
            };

            var pixelData = barcodeWriter.Write(alphaVal); // use alphanumeric value
            using (var bitmap = new System.Drawing.Bitmap(pixelData.Width, pixelData.Height, System.Drawing.Imaging.PixelFormat.Format32bppRgb))
            {
                var bitmapData = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, pixelData.Width, pixelData.Height),
                                                 System.Drawing.Imaging.ImageLockMode.WriteOnly,
                                                 System.Drawing.Imaging.PixelFormat.Format32bppRgb);
                try
                {
                    System.Runtime.InteropServices.Marshal.Copy(pixelData.Pixels, 0, bitmapData.Scan0, pixelData.Pixels.Length);
                }
                finally { bitmap.UnlockBits(bitmapData); }

                string barcodeFileName = $"{alphaVal}_barcode.png";
                string barcodePath = System.IO.Path.Combine(folderPath, barcodeFileName);
                bitmap.Save(barcodePath, System.Drawing.Imaging.ImageFormat.Png);

                // Save path to DB
                employee.BarcodePath = "/Content/BusinessCards/" + barcodeFileName;
            }

            db.SaveChanges();
            TempData["Message"] = "Business card generated successfully!";
            return RedirectToAction("EmployeeProfile");
        }

        public ActionResult DownloadBusinessCardPdf(int id)
        {
            var employee = db.RegisteredEmployees.Find(id);
            if (employee == null) return HttpNotFound();

            using (var ms = new System.IO.MemoryStream())
            {
                var pageSize = iTextSharp.text.PageSize.A4;
                var doc = new iTextSharp.text.Document(pageSize);
                var writer = iTextSharp.text.pdf.PdfWriter.GetInstance(doc, ms);
                doc.Open();

                float cardWidth = 340f;
                float cardHeight = 215f;
                float xCenter = (pageSize.Width - cardWidth) / 2;
                float yCenter = (pageSize.Height - cardHeight) / 2;

                var cb = writer.DirectContent;

                // Draw background
                cb.SetColorFill(new BaseColor(0, 86, 179));
                cb.Rectangle(xCenter, yCenter, cardWidth, cardHeight);
                cb.Fill();
                // Profile picture
                if (!string.IsNullOrEmpty(employee.ProfilePic))
                {
                    string profilePath = Server.MapPath("~" + employee.ProfilePic);
                    if (System.IO.File.Exists(profilePath))
                    {
                        var profileImg = iTextSharp.text.Image.GetInstance(profilePath);
                        profileImg.ScaleAbsolute(60, 60);
                        profileImg.SetAbsolutePosition(xCenter + 15, yCenter + cardHeight - 75);
                        doc.Add(profileImg);
                    }
                }

                // Barcode
                if (!string.IsNullOrEmpty(employee.BarcodePath))
                {
                    string barcodePath = Server.MapPath("~" + employee.BarcodePath);
                    if (System.IO.File.Exists(barcodePath))
                    {
                        var barcodeImg = iTextSharp.text.Image.GetInstance(barcodePath);
                        barcodeImg.ScaleAbsoluteWidth(cardWidth * 0.8f);
                        barcodeImg.ScaleAbsoluteHeight(50);
                        barcodeImg.SetAbsolutePosition(xCenter + (cardWidth - barcodeImg.ScaledWidth) / 2, yCenter + 20);
                        doc.Add(barcodeImg);
                    }
                }

                // Name & Email
                var nameFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14, BaseColor.WHITE);
                var emailFont = FontFactory.GetFont(FontFactory.HELVETICA, 10, BaseColor.WHITE);

                ColumnText.ShowTextAligned(cb, Element.ALIGN_LEFT,
                    new Phrase($"{employee.FirstName} {employee.LastName}", nameFont),
                    xCenter + 85, yCenter + cardHeight - 40, 0);

                ColumnText.ShowTextAligned(cb, Element.ALIGN_LEFT,
                    new Phrase(employee.Email, emailFont),
                    xCenter + 85, yCenter + cardHeight - 60, 0);

                var companyFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, BaseColor.WHITE);
                ColumnText.ShowTextAligned(cb, Element.ALIGN_RIGHT,
                    new Phrase("SuperCare", companyFont),
                    xCenter + cardWidth - 15, yCenter + cardHeight - 25, 0);

                doc.Close();
                return File(ms.ToArray(), "application/pdf", $"BusinessCard_{employee.RegisteredEmployeeID}.pdf");
            }
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

        public ActionResult EmployeeAssignedQuotes()
        {
            int? employeeId = Session["RegisteredEmployeeID"] as int?;

            if (employeeId == null)
            {
                return RedirectToAction("EmployeeLogin", "Account");
            }

            var tasks = db.QuoteRequests
                          .Where(q => q.AssignedEmployeeID == employeeId)
                          .Select(q => new EmployeeTask
                          {
                              Id = q.QuoteRequestID,
                              Title = q.CleaningType,

                              // Generate full customer name
                              Client = db.Customers
                                         .Where(c => c.CustomerId == q.CustomerID)
                                         .Select(c => c.FirstName + " " + c.LastName)
                                         .FirstOrDefault(),

                              Service = q.ServiceLevel,
                              Date = q.PreferredDate,

                              // Normalize status: if null or empty, default to "pending"
                              Status = string.IsNullOrEmpty(q.Status) ? "pending" : q.Status.Trim().ToLower(),

                              Address = q.CustomerLocation,
                              Description = q.Details,

                              QuoteAmount = q.QuoteAmount ?? 0m,
                              Feedback = q.Feedback ?? string.Empty
                          })
                          .ToList();

            return View(tasks);
        }

        [HttpPost]
        public JsonResult UpdateTaskStatus(int id, string action)
        {
            var task = db.QuoteRequests.Find(id);
            if (task == null)
                return Json(new { success = false, message = "Task not found" });

            if (action.ToLower() == "accept")
                task.Status = "completed"; // normalize to lowercase
            else if (action.ToLower() == "reject")
                task.Status = "rejected";

            db.SaveChanges();

            return Json(new { success = true, message = $"Task {action}ed successfully!" });
        }

        [HttpPost]
        public JsonResult RejectQuote(int id, string feedback)
        {
            var quote = db.QuoteRequests.Find(id);
            if (quote == null)
                return Json(new { success = false, message = "Callout not found" });

            if (string.IsNullOrWhiteSpace(feedback))
                return Json(new { success = false, message = "Feedback is required." });

            quote.Status = "rejected"; // normalize
            quote.Feedback = feedback;
            db.SaveChanges();

            return Json(new { success = true, message = "Callout rejected successfully." });
        }

        [HttpPost]
        public JsonResult AcceptQuote(int id, decimal cleaningAmount)
        {
            var quote = db.QuoteRequests.Find(id);
            if (quote == null)
                return Json(new { success = false, message = "Callout not found" });

            if (cleaningAmount <= 0)
                return Json(new { success = false, message = "Invalid cleaning amount." });

            quote.Status = "completed"; // normalize
            quote.CleaningAmount = cleaningAmount;
            db.SaveChanges();

            return Json(new { success = true, message = "Callout accepted successfully." });
        }

        public ActionResult EmployeeJobs()
        {

            // Check the correct session variable that EmployeeLogin sets
            if (Session["RegisteredEmployeeID"] == null)
            {
                return RedirectToAction("EmployeeLogin", "Account");
            }

            int employeeId = (int)Session["RegisteredEmployeeID"];
            var assignedBookings = db.BookCleaningEmployees
                                     .Where(be => be.RegisteredEmployeeID == employeeId)
                                     .Select(be => be.BookCleaning)
                                     .Where(b => b.DepositPaid && b.Status != "Completed")
                                     .OrderByDescending(b => b.CreatedAt)
                                     .ToList();

            ViewBag.AssignedBookings = assignedBookings;
            ViewBag.EmployeeName = Session["EmployeeName"]; // pass name to view
            return View("EmployeeJobs");
        }

        public ActionResult EmployeeScanEquipment(int? bookCleaningID)
        {
            // If no booking ID, allow view to show "please select job"
            if (bookCleaningID == null)
            {
                ViewBag.Job = null;
                ViewBag.SelectedPackage = null;
                ViewBag.AssignedEmployees = null; // Pass empty
                return View();
            }

            // Retrieve the job
            var job = db.BookCleanings.FirstOrDefault(b => b.BookCleaningID == bookCleaningID);

            if (job == null)
            {
                ViewBag.Job = null;
                ViewBag.SelectedPackage = null;
                ViewBag.AssignedEmployees = null; // Pass empty
                return View();
            }

            ViewBag.Job = job;

            // Map ServiceLevel to the package
            var packages = new List<EquipmentPackage>
    {
        new EquipmentPackage { PackageName = "Standard", Equipment = new List<string> { "Vacuum Cleaner", "Mop", "Bucket", "Glass Cleaner", "Furniture Polish" } },
        new EquipmentPackage { PackageName = "Deep", Equipment = new List<string> { "Industrial Vacuum", "Disinfectant Sprays", "Dusting Cloths", "Carpet Cleaner", "Trash Bags" } },
        new EquipmentPackage { PackageName = "Disinfection", Equipment = new List<string> { "Protective Gloves", "Medical-Grade Disinfectant", "Face Masks", "Biohazard Bags", "Sanitizer Fogger" } },
        new EquipmentPackage { PackageName = "PostEvent", Equipment = new List<string> { "Floor Scrubber", "Degreaser", "Trash Bins", "Broom & Dustpan", "Sanitizing Spray" } },
        new EquipmentPackage { PackageName = "Move", Equipment = new List<string> { "Vacuum Cleaner", "Mop & Bucket", "Glass Cleaner", "Dusting Cloths", "Trash Bags", "Floor Cleaner", "Furniture Polish" } }
    };

            var selectedPackage = packages.FirstOrDefault(p => p.PackageName == job.ServiceLevel);
            ViewBag.SelectedPackage = selectedPackage;

            // Fetch assigned employees for this job
            var assignedEmployeeIDs = db.BookCleaningEmployees
                                        .Where(bce => bce.BookCleaningID == bookCleaningID)
                                        .Select(bce => bce.RegisteredEmployeeID)
                                        .ToList();

            var assignedEmployees = db.RegisteredEmployees
                                      .Where(e => assignedEmployeeIDs.Contains(e.RegisteredEmployeeID))
                                      .ToList();

            ViewBag.AssignedEmployees = assignedEmployees;

            return View();
        }
        [HttpPost]
        public JsonResult VerifyEmployeeBarcode()
        {
            try
            {
                string scannedCode;
                int bookCleaningID;

                using (var reader = new StreamReader(Request.InputStream))
                {
                    var body = reader.ReadToEnd();
                    dynamic data = Newtonsoft.Json.JsonConvert.DeserializeObject(body);
                    scannedCode = data?.scannedCode;
                    bookCleaningID = data?.bookCleaningID;
                }

                if (string.IsNullOrEmpty(scannedCode))
                {
                    return Json(new { success = false, message = "No barcode received." });
                }

                // Find employee by barcode
                var employee = db.RegisteredEmployees
                    .FirstOrDefault(e => e.AlphaNumericVal == scannedCode);

                if (employee == null)
                {
                    return Json(new { success = false, message = "Barcode not found." });
                }

                // Find the employee assigned to this job
                var bce = db.BookCleaningEmployees
                    .FirstOrDefault(x => x.RegisteredEmployeeID == employee.RegisteredEmployeeID
                                      && x.BookCleaningID == bookCleaningID);

                if (bce == null)
                {
                    return Json(new { success = false, message = "Employee not assigned to this job." });
                }

                // Mark as checked
                bce.IsChecked = true;
                db.SaveChanges();

                // Return full name for JS
                string fullName = employee.FirstName + " " + employee.LastName;

                return Json(new { success = true, id = employee.RegisteredEmployeeID, name = fullName });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Server error: " + ex.Message });
            }
        }

        [HttpPost]
        public JsonResult StartJob()
        {
            try
            {
                string jsonData;
                using (var reader = new StreamReader(Request.InputStream))
                {
                    jsonData = reader.ReadToEnd();
                }

                dynamic data = Newtonsoft.Json.JsonConvert.DeserializeObject(jsonData);
                int bookCleaningID = data.bookCleaningID;

                var job = db.BookCleanings.FirstOrDefault(b => b.BookCleaningID == bookCleaningID);
                if (job == null)
                    return Json(new { success = false, message = "Job not found." });
                job.StartTime = DateTime.Now;
                job.JobStarted = true;
                job.Status = "In Progress";
                db.SaveChanges();

                return Json(new { success = true });
                
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Server error: " + ex.Message });
            }
            
        }

        public ActionResult CompleteJob(int bookCleaningID)
        {
            // Find the job
            var job = db.BookCleanings.FirstOrDefault(b => b.BookCleaningID == bookCleaningID);
            if (job == null)
            {
                TempData["ErrorMessage"] = "Job not found.";
                return RedirectToAction("EmployeeJobs");
            }

            // Set end time and status
            job.EndTime = DateTime.Now;
            job.Status = "Awaiting Payment";

            if (job.StartTime.HasValue)
            {
                job.CompletionTime = job.EndTime - job.StartTime;
            }

            db.SaveChanges();

            return RedirectToAction("EmployeeJobHistory");
        }

        public ActionResult EmployeeJobHistory()
        {
            if (Session["RegisteredEmployeeID"] == null)
            {
                return RedirectToAction("EmployeeLogin", "Account");
            }

            int employeeId = (int)Session["RegisteredEmployeeID"];

            // Step 1: Find all BookCleaningIDs assigned to this employee
            var assignedBookingIds = db.BookCleaningEmployees
                                       .Where(be => be.RegisteredEmployeeID == employeeId)
                                       .Select(be => be.BookCleaningID)
                                       .ToList();

            // Step 2: Get BookCleanings with those IDs and correct statuses
            var assignedBookings = db.BookCleanings
                                     .Where(b => assignedBookingIds.Contains(b.BookCleaningID)
                                              && (b.Status == "Completed" || b.Status == "Awaiting Payment"))
                                     .OrderByDescending(b => b.CreatedAt)
                                     .ToList();

            ViewBag.AssignedBookings = assignedBookings;
            ViewBag.EmployeeName = Session["EmployeeName"];

            // make sure the view name matches your .cshtml file
            return View("EmployeeJobHistory");
        }


        public ActionResult EmployeeChat()
        {
            return View();
        }
        public JsonResult GetEmployeePendingCount()
        {
            int? employeeId = Session["RegisteredEmployeeID"] as int?;

            if (employeeId == null)
                return Json(0, JsonRequestBehavior.AllowGet);

            var count = db.QuoteRequests
                          .Count(q => q.AssignedEmployeeID == employeeId &&
                                      q.Status != null &&
                                      q.Status.Trim().ToLower() == "in progress");

            return Json(count, JsonRequestBehavior.AllowGet);
        }

        public JsonResult GetEmployeeOngoingJobsCount()
        {
            if (Session["RegisteredEmployeeID"] == null)
            {
                return Json(0, JsonRequestBehavior.AllowGet);
            }

            int employeeId = (int)Session["RegisteredEmployeeID"];

            var count = db.BookCleaningEmployees
                          .Where(be => be.RegisteredEmployeeID == employeeId)
                          .Select(be => be.BookCleaning)
                          .Count(b => b.DepositPaid && b.Status == "Pending");

            return Json(count, JsonRequestBehavior.AllowGet);
        }

    }
}