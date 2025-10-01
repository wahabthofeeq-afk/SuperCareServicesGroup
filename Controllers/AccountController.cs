using SuperCareServicesGroup.Models;
using System;
using System.Linq;
using System.Web.Mvc;

namespace SuperCareServicesGroup.Controllers
{
    public class AccountController : Controller
    {
        // Database-First context
        private readonly SuperCareServicesNewEntities1 db = new SuperCareServicesNewEntities1();

        // -------------------- CUSTOMER LOGIN --------------------
        public ActionResult CustomerLogin()
        {
            return View(new CustomerLoginView());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CustomerLogin(CustomerLoginView model)
        {
            if (!ModelState.IsValid)
                return View(model);

            // Check if customer exists - PLAIN TEXT COMPARISON
            var customer = db.Customers
                .FirstOrDefault(c => c.Email == model.Email && c.PasswordHash == model.Password);

            if (customer != null)
            {
                Session["CustomerID"] = customer.CustomerId;
                Session["CustomerName"] = customer.FirstName;

                return RedirectToAction("CustomerHome", "Home");
            }

            ViewBag.Error = "Invalid email or password. Please register an account.";
            return View(model);
        }

        // -------------------- CUSTOMER REGISTER --------------------
        public ActionResult CustomerRegister()
        {
            return View(new RegisterView());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CustomerRegister(RegisterView model)
        {
            if (!ModelState.IsValid)
                return View(model);

            if (db.Customers.Any(c => c.Email == model.Email))
            {
                ModelState.AddModelError("Email", "This email is already registered.");
                return View(model);
            }

            var customer = new Customer
            {
                FirstName = model.FirstName,
                LastName = model.LastName,
                Email = model.Email,
                PhoneNumber = model.PhoneNumber,
                PasswordHash = model.Password, // ✅ Store as plain text
            };

            db.Customers.Add(customer);
            db.SaveChanges();

            return RedirectToAction("CustomerLogin");
        }
        // -------------------- Manager REGISTER --------------------
        //public ActionResult ManagerRegister()
       // {
        //    return View(new ManagerRegisterView());
       // }

       // [HttpPost]
       // [ValidateAntiForgeryToken]
       // public ActionResult ManagerRegister(ManagerRegisterView model)
       // {
         //   if (!ModelState.IsValid)
          //      return View(model);

          //  if (db.Managers.Any(e => e.Email == model.Email))
          //  {
           //     ModelState.AddModelError("Email", "This email is already registered.");
          //      return View(model);
          //  }

          //  var manager= new Manager
           // {
            //    FirstName = model.FirstName,
              //  LastName = model.LastName,
                //Email = model.Email,
               // PhoneNumber = model.PhoneNumber,
                //PasswordHash = model.Password // ✅ Store as plain text
            //};

          //  db.Managers.Add(manager);
          //  db.SaveChanges();

         //   return RedirectToAction("ManagerLogin");
      //  }

        // -------------------- EMPLOYEE REGISTER --------------------
        public ActionResult EmployeeRegister()
        {
            return View(new EmployeeRegisterView());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EmployeeRegister(EmployeeRegisterView model)
        {
            if (!ModelState.IsValid)
                return View(model);

            if (db.Employees.Any(e => e.Email == model.Email))
            {
                ModelState.AddModelError("Email", "This email is already registered.");
                return View(model);
            }

            var employee = new Employee
            {
                FirstName = model.FirstName,
                LastName = model.LastName,
                Email = model.Email,
                PhoneNumber = model.PhoneNumber,
                PasswordHash = model.Password // ✅ Store as plain text
            };

            db.Employees.Add(employee);
            db.SaveChanges();

            return RedirectToAction("EmployeeLogin");
        }

        // -------------------- EMPLOYEE LOGIN --------------------
        public ActionResult EmployeeLogin()
        {
            return View(new EmployeeLoginView());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EmployeeLogin(EmployeeLoginView model)
        {
            if (!ModelState.IsValid)
                return View(model);

            // 1️⃣ Check pending Employee table first
            var pending = db.Employees.FirstOrDefault(e => e.Email == model.Email);
            if (pending != null)
            {
                ViewBag.Error = "Your account is pending approval. Please try again later.";
                return View(model);
            }

            // 2️⃣ Check registered employees - PLAIN TEXT COMPARISON
            var employee = db.RegisteredEmployees
                .FirstOrDefault(e => e.Email == model.Email && e.PasswordHash == model.Password);

            if (employee != null)
            {
                Session["RegisteredEmployeeID"] = employee.RegisteredEmployeeID;
                Session["EmployeeName"] = employee.FirstName;
                return RedirectToAction("EmployeeClock", "Employee");
            }

            // 3️⃣ Default invalid login
            ViewBag.Error = "Invalid email or password. Please register an account.";
            return View(model);
        }

        // -------------------- MANAGER LOGIN --------------------
        public ActionResult ManagerLogin()
        {
            return View(new ManagerLoginView());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ManagerLogin(ManagerLoginView model)
        {
            if (!ModelState.IsValid)
                return View(model);

            // PLAIN TEXT COMPARISON for managers too
            var manager = db.Managers
                .FirstOrDefault(m => m.Email == model.Email && m.PasswordHash == model.Password);

            if (manager != null)
            {
                Session["ManagerID"] = manager.ManagerID;
                Session["ManagerName"] = manager.FirstName;
                return RedirectToAction("ManagerEmployeesInfo", "Manager");
            }

            ViewBag.Error = "Invalid email or password. Please register an account.";
            return View(model);
        }

        // -------------------- ROLE SELECTION --------------------
        public ActionResult SelectRole()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SelectRole(string role)
        {
            if (role == "Employee")
                return RedirectToAction("EmployeeLogin");

            if (role == "Manager")
                return RedirectToAction("ManagerLogin");
            if (role == "Customer")
                return RedirectToAction("CustomerLogin");

            ViewBag.Error = "Please select a valid role.";
            return View();
        }

        // -------------------- LOGOUT --------------------
        public ActionResult Logout()
        {
            Session.Clear();
            return RedirectToAction("Index", "Home");
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ChangePassword(string Email, string CurrentPassword, string NewPassword, string ConfirmPassword)
        {
            // Validation
            if (string.IsNullOrWhiteSpace(Email))
                ModelState.AddModelError("Email", "Email is required.");

            if (string.IsNullOrWhiteSpace(CurrentPassword))
                ModelState.AddModelError("CurrentPassword", "Current password is required.");

            if (string.IsNullOrWhiteSpace(NewPassword))
                ModelState.AddModelError("NewPassword", "New password is required.");

            if (string.IsNullOrWhiteSpace(ConfirmPassword))
                ModelState.AddModelError("ConfirmPassword", "Confirm password is required.");

            if (NewPassword != ConfirmPassword)
                ModelState.AddModelError("ConfirmPassword", "New password and confirm password do not match.");

            if (!ModelState.IsValid)
            {
                TempData["ChangePasswordError"] = "Please correct the errors below.";
                return RedirectToAction("CustomerLogin");
            }

            // Find user
            var user = db.Customers.FirstOrDefault(u => u.Email == Email);
            if (user == null)
            {
                TempData["ChangePasswordError"] = "User not found.";
                return RedirectToAction("CustomerLogin");
            }

            // Check current password (⚠️ use hashing in production)
            if (user.PasswordHash != CurrentPassword)
            {
                TempData["ChangePasswordError"] = "Current password is incorrect.";
                return RedirectToAction("CustomerLogin");
            }

            // Save new password
            user.PasswordHash = NewPassword;
            db.SaveChanges();

            TempData["ChangePasswordSuccess"] = "Password updated successfully!";
            return RedirectToAction("CustomerLogin");
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EmployeeChangePassword(string Email, string CurrentPassword, string NewPassword, string ConfirmPassword)
        {
            // Validation
            if (string.IsNullOrWhiteSpace(Email))
                ModelState.AddModelError("Email", "Email is required.");

            if (string.IsNullOrWhiteSpace(CurrentPassword))
                ModelState.AddModelError("CurrentPassword", "Current password is required.");

            if (string.IsNullOrWhiteSpace(NewPassword))
                ModelState.AddModelError("NewPassword", "New password is required.");

            if (string.IsNullOrWhiteSpace(ConfirmPassword))
                ModelState.AddModelError("ConfirmPassword", "Confirm password is required.");

            if (NewPassword != ConfirmPassword)
                ModelState.AddModelError("ConfirmPassword", "New password and confirm password do not match.");

            if (!ModelState.IsValid)
            {
                TempData["ChangePasswordError"] = "Please correct the errors below.";
                return RedirectToAction("EmployeeLogin");
            }

            // Find user
            var user = db.RegisteredEmployees.FirstOrDefault(u => u.Email == Email);
            if (user == null)
            {
                TempData["ChangePasswordError"] = "User not found.";
                return RedirectToAction("EmployeeRegister");
            }

            // Check current password (⚠️ use hashing in production)
            if (user.PasswordHash != CurrentPassword)
            {
                TempData["ChangePasswordError"] = "Current password is incorrect.";
                return RedirectToAction("EmployeeLogin");
            }

            // Save new password
            user.PasswordHash = NewPassword;
            db.SaveChanges();

            TempData["ChangePasswordSuccess"] = "Password updated successfully!";
            return RedirectToAction("EmployeeLogin");
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ManagerChangePassword(string Email, string CurrentPassword, string NewPassword, string ConfirmPassword)
        {
            // Validation
            if (string.IsNullOrWhiteSpace(Email))
                ModelState.AddModelError("Email", "Email is required.");

            if (string.IsNullOrWhiteSpace(CurrentPassword))
                ModelState.AddModelError("CurrentPassword", "Current password is required.");

            if (string.IsNullOrWhiteSpace(NewPassword))
                ModelState.AddModelError("NewPassword", "New password is required.");

            if (string.IsNullOrWhiteSpace(ConfirmPassword))
                ModelState.AddModelError("ConfirmPassword", "Confirm password is required.");

            if (NewPassword != ConfirmPassword)
                ModelState.AddModelError("ConfirmPassword", "New password and confirm password do not match.");

            if (!ModelState.IsValid)
            {
                TempData["ChangePasswordError"] = "Please correct the errors below.";
                return RedirectToAction("ManagerLogin");
            }

            // Find user
            var user = db.Managers.FirstOrDefault(u => u.Email == Email);
            if (user == null)
            {
                TempData["ChangePasswordError"] = "User not found.";
                return RedirectToAction("ManagerLogin");
            }

            // Check current password (⚠️ use hashing in production)
            if (user.PasswordHash != CurrentPassword)
            {
                TempData["ChangePasswordError"] = "Current password is incorrect.";
                return RedirectToAction("ManagerLogin");
            }

            // Save new password
            user.PasswordHash = NewPassword;
            db.SaveChanges();

            TempData["ChangePasswordSuccess"] = "Password updated successfully!";
            return RedirectToAction("ManagerLogin");
        }
    }
}