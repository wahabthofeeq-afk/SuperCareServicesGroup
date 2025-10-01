using iTextSharp.text;
using iTextSharp.text.pdf;
using SuperCareServicesGroup.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Validation;
using System.IO;
using System.Linq;
//using static SuperCareServicesGroup.Models.QuotePayment;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Web;
using System.Web.Mvc;
using System.Web.UI;


namespace SuperCareServicesGroup.Controllers
{
    public class CustomerController : Controller
    {
        private readonly SuperCareServicesNewEntities1 db = new SuperCareServicesNewEntities1();

        // ----------------- STATIC PAGES -----------------
        public ActionResult Notifications() => View();
        public ActionResult BookCleaning() => View();

        public ActionResult ContactInfo() => View();
        public ActionResult Location() => View();
        public ActionResult Specialties() => View();
       


       
       

        // ----------------- CUSTOMER PROFILE -----------------
        [HttpGet]
        public ActionResult CustomerProfile()
        {
            if (Session["CustomerID"] == null)
                return RedirectToAction("CustomerLogin", "Account");

            int customerId = (int)Session["CustomerID"];
            var customer = db.Customers.Find(customerId);

            if (customer == null)
                return HttpNotFound();

            return View(customer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CustomerProfile(Customer model, HttpPostedFileBase ProfileImage)
        {
            if (!ModelState.IsValid)
                return View(model);

            var customer = db.Customers.Find(model.CustomerId);
            if (customer == null)
                return HttpNotFound();

            customer.FirstName = model.FirstName;
            customer.LastName = model.LastName;
            customer.PhoneNumber = model.PhoneNumber;

            // Password update if provided
            if (!string.IsNullOrWhiteSpace(model.PasswordHash))
                customer.PasswordHash = model.PasswordHash; // Store plain text

            // Handle profile image upload
            if (ProfileImage != null && ProfileImage.ContentLength > 0)
            {
                var validationError = ValidateAndSaveProfileImage(ProfileImage, out string relativeUrl);
                if (validationError != null)
                {
                    ModelState.AddModelError("", validationError);
                    return View(model);
                }
                customer.ProfilePic = relativeUrl;
            }

            try
            {
                db.SaveChanges();
                TempData["Message"] = "Profile updated successfully!";
                return RedirectToAction("CustomerProfile");
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
                var customer = db.Customers.SingleOrDefault(c => c.Email == Email);
                if (customer == null)
                    return Json(new { success = false, error = "Customer not found." });

                // Update fields
                customer.FirstName = FirstName?.Trim();
                customer.LastName = LastName?.Trim();
                customer.PhoneNumber = PhoneNumber?.Trim();

                if (!string.IsNullOrWhiteSpace(NewPassword))
                    customer.PasswordHash = NewPassword;

                if (ProfilePic != null && ProfilePic.ContentLength > 0) // Changed from ProfileImage to ProfilePic
                {
                    var validationError = ValidateAndSaveProfileImage(ProfilePic, out string relativeUrl); // Changed here too
                    if (validationError != null)
                        return Json(new { success = false, error = validationError });

                    customer.ProfilePic = relativeUrl;
                }

                db.SaveChanges();

                return Json(new
                {
                    success = true,
                    firstName = customer.FirstName,
                    lastName = customer.LastName,
                    phoneNumber = customer.PhoneNumber,
                    email = customer.Email,
                    profilePic = customer.ProfilePic
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
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

        [HttpPost]
        public JsonResult VerifyPassword(string password, string email)
        {
            var customer = db.Customers.FirstOrDefault(c => c.Email == email);
            if (customer == null)
            {
                return Json(new { success = false, error = "User not found" });
            }

            // ✅ PLAIN TEXT comparison (NO HASHING)
            if (customer.PasswordHash == password)
            {
                return Json(new { success = true });
            }

            return Json(new { success = false, error = "Invalid password" });
        }




        // ---------------- REQUEST QUOTE ----------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RequestQuote(FormCollection form)
        {
            // Check if customer is logged in
            if (Session["CustomerID"] == null)
                return RedirectToAction("CustomerLogin", "Account");

            int customerId = (int)Session["CustomerID"];
            string customerName = "Unknown";

            var customer = db.Customers.FirstOrDefault(c => c.CustomerId == customerId);
            if (customer != null)
                customerName = $"{customer.FirstName} {customer.LastName}";

            try
            {
                // Read form values directly
                string cleaningType = form["Cleaning"];
                string serviceLevel = form["Level"];
                string details = form["Details"];
                string location = form["Location"];
                DateTime preferredDate = DateTime.TryParse(form["PreferredDate"], out var dt) ? dt : DateTime.Today;
                decimal quoteAmount = decimal.TryParse(form["SavedQuoteAmount"], out var amt) ? amt : 0;

                // Save to DB
                var quote = new QuoteRequest
                {
                    CustomerID = customerId,
                    CustomerName = customerName,
                    CleaningType = cleaningType,
                    ServiceLevel = serviceLevel,
                    Details = details,
                    PreferredDate = preferredDate,
                    CustomerLocation = location,
                    QuoteAmount = quoteAmount,
                    SubmittedAt = DateTime.Now,
                    Status = "Pending",
                    IsPaid = false,
                    Confirmation = false
                };

                db.QuoteRequests.Add(quote);
                db.SaveChanges();

                TempData["SuccessMessage"] = "Your Callout request has been submitted successfully!";
                return RedirectToAction("QuotesAwaitingConfirmation");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "There was an error saving your quote request: " + ex.Message);
                return View("QuotePayment");
            }
        }

        // ---------------- QUOTE PAYMENT PAGE ----------------
        [HttpGet]
        public ActionResult QuotePayment(int? id)
        {
            if (Session["CustomerID"] == null)
                return RedirectToAction("CustomerLogin", "Account");

            if (!id.HasValue)
                return RedirectToAction("BookCleaning");

            int customerId = (int)Session["CustomerID"];
            var quote = db.QuoteRequests.FirstOrDefault(q => q.QuoteRequestID == id.Value && q.CustomerID == customerId);

            if (quote == null)
                return RedirectToAction("BookCleaning");

            // Prepare model for the view
            var pending = new QuotePayment
            {
                QuoteRequestID = quote.QuoteRequestID,
                Cleaning = (QuotePayment.CleaningType)Enum.Parse(typeof(QuotePayment.CleaningType), quote.CleaningType),
                Level = (QuotePayment.ServiceLevel)Enum.Parse(typeof(QuotePayment.ServiceLevel), quote.ServiceLevel),
                Details = (QuotePayment.CleaningDetails)Enum.Parse(typeof(QuotePayment.CleaningDetails), quote.Details),
                PreferredDate = quote.PreferredDate,
                Location = quote.CustomerLocation,
                // Add this line to display the exact saved amount
                SavedQuoteAmount = quote.QuoteAmount
            };


            Session["PendingQuote"] = pending; // keep for POST
            return View(pending);
        }

        // ---------------- PROCESS PAYMENT ----------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ProcessPayment(QuotePayment model)
        {
            if (Session["CustomerID"] == null)
                return RedirectToAction("CustomerLogin", "Account");

            if (!ModelState.IsValid)
                return View("QuotePayment", model);

            // Find the quote
            var savedQuote = db.QuoteRequests.FirstOrDefault(q => q.QuoteRequestID == model.QuoteRequestID);
            if (savedQuote == null)
            {
                if (Request.IsAjaxRequest())
                    return Json(new { success = false, message = "Quote not found." });

                ModelState.AddModelError("", "Quote not found.");
                return View("QuotePayment", model);
            }

            // Mark as paid
            savedQuote.IsPaid = true;
            savedQuote.Status = "Pending";
            db.SaveChanges();

            // Send email
            try
            {
                int customerId = (int)Session["CustomerID"];
                var customer = db.Customers.FirstOrDefault(c => c.CustomerId == customerId);
                if (customer != null && !string.IsNullOrWhiteSpace(customer.Email))
                {
                    SendPaymentEmail(customer.Email, savedQuote);
                }
            }
            catch { /* ignore email errors */ }

            TempData["SuccessMessage"] = "Payment successful!";
            Session.Remove("PendingQuote");

            // 🔹 If AJAX, return JSON for fetch
            if (Request.IsAjaxRequest())
            {
                return Json(new
                {
                    success = true,
                    message = "Payment successful!",
                    redirectUrl = Url.Action("MyPaidQuotes", "Customer") // ✅ redirect to BookCleaning
                });
            }

            // 🔹 Normal POST fallback (non-AJAX)
            return RedirectToAction("MyPaidQuotes");
        }





        // ---------------- EMAIL SENDING ----------------
        private void SendPaymentEmail(string toEmail, QuoteRequest quote)
        {
            if (string.IsNullOrWhiteSpace(toEmail) || quote == null) return;

            var fromAddress = new MailAddress("supercareservicesgroup@gmail.com", "SuperCare Services");
            const string fromPassword = "cnnn uqkr rpwu wfoi"; // replace with secure password

            using (var smtp = new SmtpClient
            {
                Host = "smtp.gmail.com",
                Port = 587,
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new System.Net.NetworkCredential(fromAddress.Address, fromPassword)
            })
            using (var message = new MailMessage(fromAddress, new MailAddress(toEmail))
            {
                Subject = $"Payment Confirmation -  Callout #{quote.QuoteRequestID}",
                Body = $@"
                    <div style='font-family:Arial,sans-serif;color:#333'>
                      <h2>Payment Confirmation</h2>
                      <p>Dear Customer,</p>
                      <p>Thank you for your payment. Below are your payment details:</p>
                      <table cellpadding='6' cellspacing='0' style='border-collapse:collapse'>
                        <tr><td><strong>Callout ID</strong></td><td>#{quote.QuoteRequestID}</td></tr>
                        <tr><td><strong>Cleaning Type</strong></td><td>{quote.CleaningType}</td></tr>
                        <tr><td><strong>Service Level</strong></td><td>{quote.ServiceLevel}</td></tr>
                        <tr><td><strong>Amount</strong></td><td>R {quote.QuoteAmount:N2}</td></tr>
                        <tr><td><strong>Preferred Date</strong></td><td>{quote.PreferredDate:yyyy-MM-dd}</td></tr>
                        <tr><td><strong>Submitted</strong></td><td>{quote.SubmittedAt:g}</td></tr>
                        <tr><td><strong>Status</strong></td><td>{quote.Status}</td></tr>
                      </table>
                      <p style='margin-top:20px'>Kind regards,<br/>SuperCare Services</p>
                    </div>",
                IsBodyHtml = true
            })
            {
                smtp.Send(message);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
        public ActionResult ScheduleAppointment(int? bookingId)
        {
            if (Session["CustomerID"] == null)
                return RedirectToAction("CustomerLogin", "Account");

            int customerId = Convert.ToInt32(Session["CustomerID"]);

            // Get all bookings for this customer
            var bookings = db.BookCleanings
                .Where(b => b.CustomerId == customerId)
                .OrderByDescending(b => b.PreferredDate)
                .ToList();

            if (!bookings.Any())
            {
                // No bookings exist, show a flag for the view to trigger the popup
                ViewBag.ShowNoBookingsPopup = true;
                return View(); // return the same view, no model
            }

            // Determine selected booking
            BookCleaning selectedBooking;
            if (bookingId.HasValue)
            {
                selectedBooking = bookings.FirstOrDefault(b => b.BookCleaningID == bookingId.Value) ?? bookings.First();
            }
            else
            {
                selectedBooking = bookings.First();
            }

            // Ensure amounts are non-null
            if (!selectedBooking.CleaningAmount.HasValue) selectedBooking.CleaningAmount = 0;
            if (!selectedBooking.Deposit.HasValue) selectedBooking.Deposit = 0;

            ViewBag.Bookings = bookings; // For the selection dropdown
            return View(selectedBooking);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RejectQuote(int QuoteRequestID)
        {
            // Optionally, check if user is logged in
            if (Session["CustomerID"] == null)
            {
                TempData["ErrorMessage"] = "Please login to perform this action.";
                return RedirectToAction("CustomerLogin", "Account");
            }

            var quote = db.QuoteRequests.Find(QuoteRequestID);
            if (quote == null)
            {
                TempData["ErrorMessage"] = "Quote not found.";
                return RedirectToAction("BookCleaning"); // or wherever your customer sees quotes
            }

            // Delete any linked BookCleaning records first
            var linkedBookings = db.BookCleanings.Where(b => b.QuoteRequestID == QuoteRequestID).ToList();
            foreach (var booking in linkedBookings)
            {
                db.BookCleanings.Remove(booking);
            }

            // Now remove the quote
            db.QuoteRequests.Remove(quote);
            db.SaveChanges();

           
            return RedirectToAction("QuotesAwaitingConfirmation"); // replace with your view
        }
        [HttpGet]
        public ActionResult DepositPayment(int id, string selectedDate, string selectedTime)
        {
            var booking = db.BookCleanings.FirstOrDefault(b => b.BookCleaningID == id);
            if (booking == null) return HttpNotFound();

            if (!string.IsNullOrEmpty(selectedDate) && DateTime.TryParse(selectedDate, out DateTime parsedDate))
            {
                booking.PreferredDate = parsedDate;
            }

            if (!string.IsNullOrEmpty(selectedTime))
            {
                booking.TimeSlot = selectedTime;  // <-- save timeslot here
            }

            db.SaveChanges();

            ViewBag.SelectedDate = booking.PreferredDate?.ToString("dd MMM yyyy HH:mm");
            return View(booking);
        }






        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ProcessDepositPayment(int BookCleaningID, string selectedTime)
        {
            var booking = db.BookCleanings.FirstOrDefault(b => b.BookCleaningID == BookCleaningID);
            if (booking == null)
                return Json(new { success = false, message = "Booking not found." });

            booking.DepositPaid = true;
            booking.Status = "Confirmed";

            // Save the selected timeslot
            if (!string.IsNullOrEmpty(selectedTime))
            {
                booking.TimeSlot = selectedTime;
            }

            db.SaveChanges();

            // Get customer email
            var customer = db.Customers.FirstOrDefault(c => c.CustomerId == booking.CustomerId);
            if (customer != null)
            {
                SendDepositEmail(customer.Email, booking);
            }

            return Json(new
            {
                success = true,
                redirectUrl = Url.Action("ScheduleAppointment", "Customer", new { id = booking.BookCleaningID })
            });
        }




        // ---------------- EMAIL SENDING ----------------
        private void SendDepositEmail(string toEmail, BookCleaning booking)
        {
            if (string.IsNullOrWhiteSpace(toEmail) || booking == null) return;

            var fromAddress = new MailAddress("supercareservicesgroup@gmail.com", "SuperCare Services");
            const string fromPassword = "cnnn uqkr rpwu wfoi"; // secure app password

            using (var smtp = new SmtpClient
            {
                Host = "smtp.gmail.com",
                Port = 587,
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new System.Net.NetworkCredential(fromAddress.Address, fromPassword)
            })
            using (var message = new MailMessage(fromAddress, new MailAddress(toEmail))
            {
                Subject = $"Deposit Payment Confirmation - Booking #{booking.BookCleaningID}",
                Body = $@"
            <div style='font-family:Arial,sans-serif;color:#333'>
              <h2>Deposit Payment Confirmation</h2>
              <p>Dear Customer,</p>
              <p>Thank you for your deposit payment. Below are your booking details:</p>
              <table cellpadding='6' cellspacing='0' style='border-collapse:collapse'>
                <tr><td><strong>Booking ID</strong></td><td>#{booking.BookCleaningID}</td></tr>
                <tr><td><strong>Cleaning Type</strong></td><td>{booking.CleaningType}</td></tr>
                <tr><td><strong>Location</strong></td><td>{booking.CustomerLocation}</td></tr>
                <tr><td><strong>Preferred Date</strong></td><td>{booking.PreferredDate:yyyy-MM-dd}</td></tr>
                <tr><td><strong>Total Amount</strong></td><td>R {booking.CleaningAmount:N2}</td></tr>
                <tr><td><strong>Deposit Paid</strong></td><td>R {booking.Deposit:N2}</td></tr>
               <tr><td><strong>Amount Outstanding</strong></td><td>R {booking.Deposit:N2}</td></tr>
                <tr><td><strong>Status</strong></td><td>{booking.Status}</td></tr>
              </table>
              <p style='margin-top:20px'>Kind regards,<br/>SuperCare Services</p>
            </div>",
                IsBodyHtml = true
            })
            {
                smtp.Send(message);
            }
        }
        public ActionResult UpcomingBookings()
        {
            if (Session["CustomerID"] == null)
                return RedirectToAction("CustomerLogin", "Account");

            int customerId = (int)Session["CustomerID"];

            var bookings = db.BookCleanings
                .Where(b => b.CustomerId == customerId)
                .OrderBy(b => b.PreferredDate)
                .ToList();

            var viewBookings = bookings.Select(b => new
            {
                b.BookCleaningID,
                b.CleaningType,
                PreferredDate = b.PreferredDate?.ToString("yyyy-MM-ddTHH:mm:ss"), // ISO-like
                b.TimeSlot, // <-- add this
                b.CustomerLocation,
                b.CleaningAmount,
                b.Deposit
            }).ToList();

            ViewBag.Bookings = viewBookings;
            return View();
        }

        // GET: RescheduleCancel
        public ActionResult RescheduleCancel()
        {
            if (Session["CustomerID"] == null)
                return RedirectToAction("CustomerLogin", "Account");

            int customerId = (int)Session["CustomerID"];

            var bookings = db.BookCleanings
                .Where(b => b.CustomerId == customerId)
                .OrderBy(b => b.PreferredDate)
                .ToList();

            var viewBookings = bookings.Select(b => new
            {
                b.BookCleaningID,
                b.CleaningType,
                PreferredDate = b.PreferredDate?.ToString("yyyy-MM-ddTHH:mm:ss"), // ISO-like for JS
                b.TimeSlot,
                b.CustomerLocation,
                b.CleaningAmount,
                b.Deposit
            }).ToList();

            ViewBag.Bookings = viewBookings;
            return View();
        }

        // POST: UpdateBooking
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult UpdateBooking(BookingUpdateModel model)
        {
            if (Session["CustomerID"] == null)
                return Json(new { success = false });

            int customerId = (int)Session["CustomerID"];

            var booking = db.BookCleanings
                .FirstOrDefault(b => b.BookCleaningID == model.BookCleaningID && b.CustomerId == customerId);

            if (booking != null)
            {
                try
                {
                    booking.PreferredDate = DateTime.Parse(model.PreferredDate);
                    booking.TimeSlot = model.TimeSlot;
                    db.SaveChanges();

                    return Json(new { success = true });
                }
                catch (Exception)
                {
                    return Json(new { success = false });
                }
            }

            return Json(new { success = false });
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult CancelBooking(int BookCleaningID)
        {
            if (Session["CustomerID"] == null)
                return Json(new { success = false });

            int customerId = (int)Session["CustomerID"];

            var booking = db.BookCleanings
                .FirstOrDefault(b => b.BookCleaningID == BookCleaningID && b.CustomerId == customerId);

            if (booking != null)
            {
                try
                {
                    db.BookCleanings.Remove(booking);
                    db.SaveChanges();
                    return Json(new { success = true });
                }
                catch (Exception)
                {
                    return Json(new { success = false });
                }
            }

            return Json(new { success = false });
        }
      
        public ActionResult MyPaidQuotes()
        {
            if (Session["CustomerID"] == null)
                return RedirectToAction("CustomerLogin", "Account");

            int customerId = (int)Session["CustomerID"];

            // Paid and confirmed quotes (already paid)
            ViewBag.PaidQuotes = db.QuoteRequests
                .Where(q => q.CustomerID == customerId && q.IsPaid && q.Confirmation)
                .OrderByDescending(q => q.SubmittedAt)
                .ToList();
            return View();
        }

      
        public ActionResult ConfirmedQuotes()
        {
            if (Session["CustomerID"] == null)
                return RedirectToAction("CustomerLogin", "Account");

            int customerId = (int)Session["CustomerID"];

            // Confirmed but not yet paid
            ViewBag.ConfirmedQuotes = db.QuoteRequests
                .Where(q => q.CustomerID == customerId && q.Confirmation && !q.IsPaid)
                .OrderByDescending(q => q.SubmittedAt)
                .ToList();
            return View();
        }

       
        public ActionResult QuotesAwaitingConfirmation()
        {
            if (Session["CustomerID"] == null)
                return RedirectToAction("CustomerLogin", "Account");

            int customerId = (int)Session["CustomerID"];

            // Awaiting confirmation (not paid and not confirmed)
            ViewBag.AwaitingConfirmationQuotes = db.QuoteRequests
                .Where(q => q.CustomerID == customerId && !q.IsPaid && !q.Confirmation)
                .OrderByDescending(q => q.SubmittedAt)
                .ToList();
            return View();
        }


        [HttpGet]
        public ActionResult FinalPayment(int id, string selectedDate, string selectedTime)
        {
            var booking = db.BookCleanings.FirstOrDefault(b => b.BookCleaningID == id);
            if (booking == null) return HttpNotFound();

            if (!string.IsNullOrEmpty(selectedDate) && DateTime.TryParse(selectedDate, out DateTime parsedDate))
            {
                booking.PreferredDate = parsedDate;
            }

            if (!string.IsNullOrEmpty(selectedTime))
            {
                booking.TimeSlot = selectedTime;  // <-- save timeslot here
            }

            db.SaveChanges();

            ViewBag.SelectedDate = booking.PreferredDate?.ToString("dd MMM yyyy HH:mm");
            return View(booking);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ProcessFinalPayment(int BookCleaningID, string selectedTime)
        {
            // Find the booking
            var booking = db.BookCleanings.FirstOrDefault(b => b.BookCleaningID == BookCleaningID);
            if (booking == null)
            {
                return Json(new { success = false, message = "Booking not found." });
            }

            // Mark as completed
            booking.Status = "Completed";

            // Save the selected timeslot if provided
            if (!string.IsNullOrEmpty(selectedTime))
            {
                booking.TimeSlot = selectedTime;
            }

            db.SaveChanges();


            // Create a new CustomerFeedback record with booking details
            var feedback = new CustomerFeedback
            {
                BookCleaningID = (int)booking.BookCleaningID, // cast from int? to int
                CustomerId = (int)booking.CustomerId,
                CustomerName = booking.CustomerName,
                CleaningType = booking.CleaningType,
                ServiceLevel = booking.ServiceLevel,
                Rating = null,
                Feedback = null
            };

            db.CustomerFeedbacks.Add(feedback);
            db.SaveChanges(); // Save both the booking update and the feedback record

            // Send final email
            var customer = db.Customers.FirstOrDefault(c => c.CustomerId == booking.CustomerId);
            if (customer != null)
            {
                SendFinalEmail(customer.Email, booking);
            }

            // Build redirect URL to feedback page, only BookCleaningID is needed
            var redirectUrl = Url.Action("CustomerFeedback", "Customer", new
            {
                BookCleaningId = booking.BookCleaningID
            });

            return Json(new
            {
                success = true,
                redirectUrl = redirectUrl
            });
        }
        // GET: Customer/CustomerFeedback
        public ActionResult CustomerFeedback(int BookCleaningID)
        {
            // Fetch the record from the CustomerFeedback table
            var feedback = db.CustomerFeedbacks.FirstOrDefault(f => f.BookCleaningID == BookCleaningID);

            if (feedback == null)
            {
                return HttpNotFound("Feedback record not found for this booking.");
            }

            // Pass data to view via ViewBag (or ViewModel)
            ViewBag.BookCleaningID = feedback.BookCleaningID;
            ViewBag.CustomerId = feedback.CustomerId;
            ViewBag.CustomerName = feedback.CustomerName;
            ViewBag.CleaningType = feedback.CleaningType;
            ViewBag.ServiceLevel = feedback.ServiceLevel;

            // Populate FeedbackOptions (dropdown list)
            ViewBag.FeedbackOptions = new List<string>
    {
        "Excellent Service",
        "Good",
        "Average",
        "Poor",
        "Very Poor"
    };

            return View(feedback);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CustomerFeedbackSubmit(int BookCleaningID, string Rating, string Feedback)
        {
            // Fetch the feedback record by BookCleaningID
            var record = db.CustomerFeedbacks.FirstOrDefault(f => f.BookCleaningID == BookCleaningID);

            if (record == null)
            {
                return HttpNotFound("Feedback record not found for this booking.");
            }

            // Convert Rating from string to int
            if (int.TryParse(Rating, out int parsedRating))
            {
                record.Rating = parsedRating;  // matches your int column
            }

            record.Feedback = Feedback;       // string field

            db.SaveChanges();

            TempData["SuccessMessage"] = "Thank you for your feedback!";

            // ✅ Redirect to CustomerHome page
            return RedirectToAction("CustomerHome", "Home");
        }





        // ---------------- EMAIL SENDING ----------------
        private void SendFinalEmail(string toEmail, BookCleaning booking)
        {
            if (string.IsNullOrWhiteSpace(toEmail) || booking == null) return;

            var fromAddress = new MailAddress("supercareservicesgroup@gmail.com", "SuperCare Services");
            const string fromPassword = "cnnn uqkr rpwu wfoi"; // secure app password

            using (var smtp = new SmtpClient
            {
                Host = "smtp.gmail.com",
                Port = 587,
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new System.Net.NetworkCredential(fromAddress.Address, fromPassword)
            })
            using (var message = new MailMessage(fromAddress, new MailAddress(toEmail))
            {
                Subject = $"Final Payment Confirmation - Booking #{booking.BookCleaningID}",
                Body = $@"
            <div style='font-family:Arial,sans-serif;color:#333'>
              <h2>Final Payment Confirmation</h2>
              <p>Dear Customer,</p>
              <p>Thank you for your final payment. Below are your booking details:</p>
              <table cellpadding='6' cellspacing='0' style='border-collapse:collapse'>
                <tr><td><strong>Booking ID</strong></td><td>#{booking.BookCleaningID}</td></tr>
                <tr><td><strong>Cleaning Type</strong></td><td>{booking.CleaningType}</td></tr>
                <tr><td><strong>Location</strong></td><td>{booking.CustomerLocation}</td></tr>
                <tr><td><strong>Preferred Date</strong></td><td>{booking.PreferredDate:yyyy-MM-dd}</td></tr>
                <tr><td><strong>Total Amount</strong></td><td>R {booking.CleaningAmount:N2}</td></tr>
                <tr><td><strong>Deposit Paid</strong></td><td>R {booking.Deposit:N2}</td></tr>
                <tr><td><strong>Final Amount Paid</strong></td><td>R {booking.Deposit:N2}</td></tr>
                <tr><td><strong>Status</strong></td><td>{booking.Status}</td></tr>
              </table>
              <p style='margin-top:20px'>Kind regards,<br/>SuperCare Services</p>
            </div>",
                IsBodyHtml = true
            })
            {
                smtp.Send(message);
            }
        }


        public ActionResult CompletedJobs()
        {
            if (Session["CustomerID"] == null)
                return RedirectToAction("CustomerLogin", "Account");

            int customerId = (int)Session["CustomerID"];

            // Confirmed but not yet paid
            ViewBag.ConfirmedQuotes = db.BookCleanings
                .Where(q => q.CustomerId == customerId && q.Status =="Awaiting Payment")
                .OrderByDescending(q => q.StartTime)
                .ToList();

            

            // Get paid quotes that are assigned to employees
            var assignedQuotes = db.BookCleanings
                .Where(q => q.Status == "Completed")
                .OrderByDescending(q => q.StartTime)
                .ToList();

            ViewBag.PaidQuotes = assignedQuotes;

            return View();
        }
        public ActionResult InvoiceHistory()
        {
            if (Session["CustomerID"] == null)
                return RedirectToAction("CustomerLogin", "Account");

            int customerId = (int)Session["CustomerID"];

            var model = new InvoiceHistoryViewModel
            {
                CompletedQuotes = db.QuoteRequests
                                   .Where(q => q.Status == "completed" && q.CustomerID == customerId)
                                   .ToList(),
                ConfirmedBookings = db.BookCleanings
                                      .Where(b => b.Status == "Completed" && b.CustomerId == customerId)
                                      .ToList()
            };

            return View(model);
        }

        [AllowAnonymous] // or [Authorize] if you want only logged-in users
        [HttpGet]
        public ActionResult DownloadInvoice(int id, string type)
        {
            MemoryStream workStream = new MemoryStream();
            Document document = new Document(PageSize.A4, 25, 25, 30, 30);
            PdfWriter.GetInstance(document, workStream).CloseStream = false;

            document.Open();

            // Business Header
            var titleFont = FontFactory.GetFont("Arial", 16, Font.BOLD);
            var normalFont = FontFactory.GetFont("Arial", 12, Font.NORMAL);

            document.Add(new Paragraph("SuperCare Services Invoice", titleFont));
            document.Add(new Paragraph("Generated on: " + DateTime.Now.ToString("dd MMM yyyy HH:mm")));
            document.Add(new Paragraph("-------------------------------------------------------"));

            // Load data
            if (type == "callout")
            {
                var quote = db.QuoteRequests.FirstOrDefault(q => q.QuoteRequestID == id);
                if (quote != null)
                {
                    document.Add(new Paragraph("Invoice for Callout ID: " + quote.QuoteRequestID, normalFont));
                    document.Add(new Paragraph("Customer: " + quote.CustomerName));
                    document.Add(new Paragraph("Cleaning Type: " + quote.CleaningType));
                    document.Add(new Paragraph("Service Level: " + quote.ServiceLevel));
                    document.Add(new Paragraph("Amount Paid: " + (quote.QuoteAmount.HasValue ? quote.QuoteAmount.Value.ToString("C") : "-")));
                }
            }
            else if (type == "booking")
            {
                var booking = db.BookCleanings.FirstOrDefault(b => b.BookCleaningID == id);
                if (booking != null)
                {
                    document.Add(new Paragraph("Invoice for Booking ID: " + booking.BookCleaningID, normalFont));
                    document.Add(new Paragraph("Customer: " + booking.CustomerName));
                    document.Add(new Paragraph("Cleaning Type: " + booking.CleaningType));
                    document.Add(new Paragraph("Service Level: " + booking.ServiceLevel));
                    document.Add(new Paragraph("Amount Paid: " + (booking.CleaningAmount.HasValue ? booking.CleaningAmount.Value.ToString("C") : "-")));
                }
            }

            document.Close();

            byte[] byteInfo = workStream.ToArray();
            workStream.Write(byteInfo, 0, byteInfo.Length);
            workStream.Position = 0;

            return File(workStream, "application/pdf", $"Invoice_{id}.pdf");
        }
        public JsonResult GetCustomerPendingPayments()
        {
            if (Session["CustomerID"] == null)
                return Json(0, JsonRequestBehavior.AllowGet);

            // Convert session to int
            int customerID;
            if (!int.TryParse(Session["CustomerID"].ToString(), out customerID))
                return Json(0, JsonRequestBehavior.AllowGet);

            // Count confirmed quotes awaiting payment
            var count = db.QuoteRequests
                .Where(q => q.CustomerID == customerID
                            && q.Confirmation == true
                            && q.IsPaid == false)
                .Count();

            return Json(count, JsonRequestBehavior.AllowGet);
        }


        public JsonResult GetCustomerPendingDeposits()
        {
            if (Session["CustomerID"] == null)
                return Json(0, JsonRequestBehavior.AllowGet);

            int customerID;
            if (!int.TryParse(Session["CustomerID"].ToString(), out customerID))
                return Json(0, JsonRequestBehavior.AllowGet);

            // Count bookings where DepositPaid is false
            var count = db.BookCleanings
                          .Where(b => b.CustomerId == customerID && b.DepositPaid == false)
                          .Count();

            return Json(count, JsonRequestBehavior.AllowGet);
        }
        public JsonResult GetCustomerAwaitingPaymentsCount()
        {
            if (Session["CustomerID"] == null)
            {
                return Json(0, JsonRequestBehavior.AllowGet);
            }

            int customerId = (int)Session["CustomerID"];

            var count = db.BookCleanings
                          .Where(b => b.CustomerId == customerId
                                   && b.Status == "Awaiting Payment")
                          .Count();

            return Json(count, JsonRequestBehavior.AllowGet);
        }

    }
}





