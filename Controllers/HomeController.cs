using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WebUIMVC.Models;
using WebUIMVC.MyClasses;
using WebUIMVC.Services;

namespace WebUIMVC.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IProductService _productService;

        public HomeController(ILogger<HomeController> logger, IProductService productService)
        {
            _logger = logger;
            _productService = productService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
        {
            var deals = await _productService.GetDealsFeedAsync(cancellationToken);

            // Safely mark newly inserted products as viewed in database without blocking the page if write is busy
            try
            {
                await _productService.MarkDealsAsReadAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Non-critical: Unable to update read status during index load");
            }

            var model = new Products { PL = deals };
            return View(model);
        }

        [HttpPost]
        public async Task<int> UpdateRead(CancellationToken cancellationToken = default)
        {
            await _productService.ArchiveReadDealsAsync(cancellationToken);
            return 0;
        }

        // Wishlist Actions
        [HttpPost]
        public async Task<JsonResult> AddToWishlist(string asin, CancellationToken cancellationToken = default)
        {
            var count = await _productService.AddToWishlistAsync(asin, cancellationToken);
            return Json(new { success = true, count = count });
        }

        [HttpPost]
        public async Task<JsonResult> RemoveFromWishlist(string asin, CancellationToken cancellationToken = default)
        {
            var count = await _productService.RemoveFromWishlistAsync(asin, cancellationToken);
            return Json(new { success = true, count = count });
        }

        [HttpGet]
        public async Task<IActionResult> WishList(CancellationToken cancellationToken = default)
        {
            var wishlist = await _productService.GetWishlistAsync(cancellationToken);
            var model = new Products { PL = wishlist };
            return View(model);
        }

        [HttpGet]
        public async Task<JsonResult> GetWishlistCount(CancellationToken cancellationToken = default)
        {
            var count = await _productService.GetWishlistCountAsync(cancellationToken);
            return Json(new { count = count });
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> ProductAdmin(CancellationToken cancellationToken = default)
        {
            var model = await _productService.GetProductAdminDataAsync(cancellationToken);
            return View(model);
        }

        [HttpGet]
        public async Task<JsonResult> GetDeleteCount(DateTime start, DateTime end, CancellationToken cancellationToken = default)
        {
            var count = await _productService.GetDeletePreviewCountAsync(start, end, cancellationToken);
            return Json(new { count = count });
        }

        [HttpPost]
        public async Task<IActionResult> ConfirmDeleteProductsByDateRange(DateTime start, DateTime end, CancellationToken cancellationToken = default)
        {
            var count = await _productService.GetDeletePreviewCountAsync(start, end, cancellationToken);
            var productsToDelete = await _productService.GetDeletePreviewProductsAsync(start, end, 100, cancellationToken);

            ViewBag.Start = start;
            ViewBag.End = end;
            ViewBag.Count = count;
            return View("ConfirmDeleteProductsByDateRange", productsToDelete);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteProductsByDateRangeConfirmed(DateTime start, DateTime end, CancellationToken cancellationToken = default)
        {
            int count = await _productService.DeleteProductsByDateRangeAsync(start, end, excludeWishlist: true, cancellationToken);

            if (count > 0)
            {
                TempData["DeleteResult"] = $"Deleted {count} non-wishlisted products in the selected date range.";
            }
            else
            {
                TempData["DeleteResult"] = "No non-wishlisted products found in the specified date range. (0 would be deleted)";
            }

            return RedirectToAction(nameof(ProductAdmin));
        }

        [HttpPost]
        public async Task<IActionResult> DeleteProductsByDateRange(DateTime start, DateTime end, CancellationToken cancellationToken = default)
        {
            int count = await _productService.DeleteProductsByDateRangeAsync(start, end, excludeWishlist: false, cancellationToken);

            if (count > 0)
            {
                TempData["DeleteResult"] = $"Deleted {count} products in the selected date range.";
            }
            else
            {
                TempData["DeleteResult"] = "No products found in the specified date range. (0 would be deleted)";
            }

            return RedirectToAction(nameof(ProductAdmin));
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            var exceptionFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
            if (exceptionFeature != null)
            {
                _logger.LogError(exceptionFeature.Error, "Unhandled exception occurred at path {Path}: {Message}", exceptionFeature.Path, exceptionFeature.Error.Message);
            }

            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
