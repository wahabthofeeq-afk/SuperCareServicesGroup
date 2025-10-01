using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace SuperCareServicesGroup.Controllers
{
    public class HomeController : Controller
    {
        private readonly SuperCareServicesNewEntities1 db = new SuperCareServicesNewEntities1();
        public ActionResult Index()
        {
            var feedbacks = db.CustomerFeedbacks.ToList();

            ViewBag.Feedbacks = feedbacks;
            return View();
        }
        public ActionResult CustomerHome()
        {
            var feedbacks = db.CustomerFeedbacks.ToList();

            ViewBag.Feedbacks = feedbacks;
            return View();
        }

    }
}