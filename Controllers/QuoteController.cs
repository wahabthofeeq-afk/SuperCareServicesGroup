using SuperCareServicesGroup.Models;
using System;
using System.Linq;
using System.Web.Mvc;
using SuperCareServicesGroup;

namespace SuperCareServicesGroup.Controllers
{
    public class QuoteController : Controller
    {
        private SuperCareServicesNewEntities db = new SuperCareServicesNewEntities();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SubmitQuote(string CleaningType, string ServiceLevel, string Details,
                           DateTime PreferredDate, string CustomerLocation,
                           string paymentOption)
        {
            try
            {
                if (Session["CustomerID"] == null)
                {
                    TempData["ErrorMessage"] = "Please login to submit a quote request.";
                    return RedirectToAction("CustomerLogin", "Account");
                }

                int customerId = (int)Session["CustomerID"];

                // Use QuoteCalculator to determine the price
                var calculator = new QuoteCalculator
                {
                    CleaningType = CleaningType,
                    ServiceLevel = ServiceLevel
                };

                decimal calculatedPrice = calculator.CalculatePrice();

                // Create new quote request
                var quoteRequest = new QuoteRequest
                {
                    CleaningType = CleaningType,
                    ServiceLevel = ServiceLevel,
                    Details = Details,
                    PreferredDate = PreferredDate,
                    CustomerLocation = CustomerLocation,
                    QuoteAmount = calculatedPrice,
                    CustomerID = customerId,
                    SubmittedAt = DateTime.Now,
                    Status = "Pending",
                    IsPaid = (paymentOption == "PayNow")
                };

                db.QuoteRequests.Add(quoteRequest);
                db.SaveChanges();

                if (paymentOption == "PayNow")
                {
                    return RedirectToAction("QuotePayment", "Customer", new { id = quoteRequest.QuoteRequestID });
                }
                else
                {
                    TempData["SuccessMessage"] = "✅ Quote saved successfully! You chose to pay later.";
                    return RedirectToAction("BookCleaning", "Customer");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error submitting quote: {ex.Message}");
                TempData["ErrorMessage"] = "❌ There was an error submitting your quote request.";
                return RedirectToAction("BookCleaning", "Customer");
            }
        }

    }
}
