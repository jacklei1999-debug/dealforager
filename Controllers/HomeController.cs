        // ...existing code...
// ...existing code...
        // ...existing code...
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using DealForager.Shared;
using WebUIMVC.Models;
using WebUIMVC.MyClasses;

namespace WebUIMVC.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly Context _context;

        public HomeController(ILogger<HomeController> logger, Context context)
        {
            _logger = logger;
            _context = context;
        }

        public IActionResult Index()
        {
            Products ProList = new Products();
		    // Exclude wishlisted items (readit == 4) from main feed
		    ProList.PL = (from i in _context.Products where i.readit <= 2 select i).ToList();

            ProList.PL = ProList.PL.OrderByDescending(x => x.savingspercent).ToList();

            (from i in _context.Products where i.readit <= 1 select i).ToList().ForEach(i => { i.readit = 2; });
            _context.SaveChanges();
             //ViewBag.PL = PL;
            return View(ProList);
        }


        [HttpPost]
        public async Task<int> UpdateRead()
        {
            // Only archive non-wishlisted items (readit == 2), keep wishlisted items (readit == 4)
            (from i in _context.Products where i.readit == 2 select i).ToList().ForEach(i => { i.readit = 3; });
            await _context.SaveChangesAsync();

            return 0;
        }

        // Wishlist Actions
        [HttpPost]
        public async Task<JsonResult> AddToWishlist(string asin)
        {
            var product = _context.Products.FirstOrDefault(p => p.asin == asin);
            if (product != null)
            {
                product.readit = 4; // 4 = wishlisted
                await _context.SaveChangesAsync();
            }
            var count = _context.Products.Count(p => p.readit == 4);
            return Json(new { success = true, count = count });
        }

        [HttpPost]
        public async Task<JsonResult> RemoveFromWishlist(string asin)
        {
            var product = _context.Products.FirstOrDefault(p => p.asin == asin);
            if (product != null)
            {
                product.readit = 3; // 3 = archived
                await _context.SaveChangesAsync();
            }
            var count = _context.Products.Count(p => p.readit == 4);
            return Json(new { success = true, count = count });
        }

        [HttpGet]
        public IActionResult WishList()
        {
            Products ProList = new Products();
            ProList.PL = (from i in _context.Products where i.readit == 4 select i).ToList();
            ProList.PL = ProList.PL.OrderByDescending(x => x.savingspercent).ToList();
            return View(ProList);
        }

        [HttpGet]
        public JsonResult GetWishlistCount()
        {
            var count = _context.Products.Count(p => p.readit == 4);
            return Json(new { count = count });
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [HttpGet]
        public IActionResult ProductAdmin()
        {
            Products ProList = new Products();
            ProList.PL = _context.Products.ToList();
            return View(ProList);
        }

        [HttpPost]
        public IActionResult ConfirmDeleteProductsByDateRange(DateTime start, DateTime end)
        {
            // Only count and list non-wishlisted items (readit != 4) in the date range
            var productsToDelete = _context.Products.Where(p => p.readit != 4 && p.intertTime >= start && p.intertTime <= end).ToList();
            ViewBag.Start = start;
            ViewBag.End = end;
            ViewBag.Count = productsToDelete.Count;
            return View("ConfirmDeleteProductsByDateRange", productsToDelete);
        }

        [HttpPost]
        public IActionResult DeleteProductsByDateRangeConfirmed(DateTime start, DateTime end)
        {
            // Only delete non-wishlisted items (readit != 4) in the date range
            var productsToDelete = _context.Products.Where(p => p.readit != 4 && p.intertTime >= start && p.intertTime <= end).ToList();
            int count = productsToDelete.Count;
            if (count > 0)
            {
                _context.Products.RemoveRange(productsToDelete);
                _context.SaveChanges();
                TempData["DeleteResult"] = $"Deleted {count} non-wishlisted products in the selected date range.";
            }
            else
            {
                TempData["DeleteResult"] = $"No non-wishlisted products found in the specified date range. (0 would be deleted)";
            }
            return RedirectToAction("ProductAdmin");
        }

        [HttpPost]
        public IActionResult DeleteProductsByDateRange(DateTime start, DateTime end)
        {
            var productsToDelete = _context.Products.Where(p => p.intertTime >= start && p.intertTime <= end).ToList();
            int count = productsToDelete.Count;
            if (count > 0)
            {
                _context.Products.RemoveRange(productsToDelete);
                _context.SaveChanges();
                TempData["DeleteResult"] = $"Deleted {count} products in the selected date range.";
            }
            else
            {
                TempData["DeleteResult"] = $"No products found in the specified date range. (0 would be deleted)";
            }
            return RedirectToAction("ProductAdmin");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

    }
}
