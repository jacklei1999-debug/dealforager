using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using WebUIMVC.Controllers;
using WebUIMVC.Data;
using WebUIMVC.Models;
using WebUIMVC.MyClasses;
using WebUIMVC.Services;
using Xunit;

namespace WebUIMVC.Tests
{
    public class HomeControllerTests
    {
        private class FakeProductService : IProductService
        {
            public List<Product> Deals { get; set; } = new();
            public List<Product> Wishlist { get; set; } = new();
            public int WishlistCount { get; set; } = 3;
            public bool MarkDealsCalled { get; set; }
            public bool ArchiveReadCalled { get; set; }

            public Task<List<Product>> GetDealsFeedAsync(CancellationToken cancellationToken = default) => Task.FromResult(Deals);
            public Task MarkDealsAsReadAsync(CancellationToken cancellationToken = default)
            {
                MarkDealsCalled = true;
                return Task.CompletedTask;
            }
            public Task<int> ArchiveReadDealsAsync(CancellationToken cancellationToken = default)
            {
                ArchiveReadCalled = true;
                return Task.FromResult(1);
            }
            public Task<List<Product>> GetWishlistAsync(CancellationToken cancellationToken = default) => Task.FromResult(Wishlist);
            public Task<int> GetWishlistCountAsync(CancellationToken cancellationToken = default) => Task.FromResult(WishlistCount);
            public Task<int> AddToWishlistAsync(string asin, CancellationToken cancellationToken = default) => Task.FromResult(++WishlistCount);
            public Task<int> RemoveFromWishlistAsync(string asin, CancellationToken cancellationToken = default) => Task.FromResult(--WishlistCount);
            public Task<ProductAdminViewModel> GetProductAdminDataAsync(CancellationToken cancellationToken = default) =>
                Task.FromResult(new ProductAdminViewModel { TotalCount = 10 });
            public Task<int> GetDeletePreviewCountAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default) =>
                Task.FromResult(5);
            public Task<List<Product>> GetDeletePreviewProductsAsync(DateTime start, DateTime end, int maxPreview = 100, CancellationToken cancellationToken = default) =>
                Task.FromResult(new List<Product>());
            public Task<int> DeleteProductsByDateRangeAsync(DateTime start, DateTime end, bool excludeWishlist = true, CancellationToken cancellationToken = default) =>
                Task.FromResult(5);
            public void InvalidateFeedCache() { }
            public void InvalidateWishlistCache() { }
        }

        [Fact]
        public async Task Index_ReturnsViewResult_WithProductsModel()
        {
            var fakeService = new FakeProductService();
            fakeService.Deals.Add(new Product { asin = "TEST1" });

            var controller = new HomeController(NullLogger<HomeController>.Instance, fakeService);

            var result = await controller.Index();

            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<Products>(viewResult.Model);
            Assert.Single(model.PL);
            Assert.True(fakeService.MarkDealsCalled);
        }

        [Fact]
        public async Task Wishlist_AddAndCount_ReturnsExpectedJson()
        {
            var fakeService = new FakeProductService { WishlistCount = 5 };
            var controller = new HomeController(NullLogger<HomeController>.Instance, fakeService);

            var addResult = await controller.AddToWishlist("TESTASIN");
            var jsonAdd = Assert.IsType<JsonResult>(addResult);
            Assert.NotNull(jsonAdd.Value);

            var countResult = await controller.GetWishlistCount();
            var jsonCount = Assert.IsType<JsonResult>(countResult);
            Assert.NotNull(jsonCount.Value);
        }

        [Fact]
        public async Task ProductAdmin_ReturnsAdminViewModel()
        {
            var fakeService = new FakeProductService();
            var controller = new HomeController(NullLogger<HomeController>.Instance, fakeService);

            var result = await controller.ProductAdmin();

            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<ProductAdminViewModel>(viewResult.Model);
            Assert.Equal(10, model.TotalCount);
        }
    }
}

