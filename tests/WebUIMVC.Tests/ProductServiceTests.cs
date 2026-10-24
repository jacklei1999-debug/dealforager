using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using WebUIMVC.Data;
using WebUIMVC.Services;
using Xunit;

namespace WebUIMVC.Tests
{
    public class ProductServiceTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly Context _context;
        private readonly IMemoryCache _memoryCache;
        private readonly ProductService _service;

        public ProductServiceTests()
        {
            _connection = new SqliteConnection("Filename=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<Context>()
                .UseSqlite(_connection)
                .Options;

            _context = new Context(options);
            _context.Database.EnsureCreated();

            _memoryCache = new MemoryCache(new MemoryCacheOptions());
            _service = new ProductService(_context, _memoryCache, NullLogger<ProductService>.Instance);
        }

        public void Dispose()
        {
            _context.Dispose();
            _connection.Dispose();
            _memoryCache.Dispose();
        }

        private async Task SeedSampleProductsAsync()
        {
            var products = new List<Product>
            {
                new Product { asin = "ITEM1", readit = 1, savingspercent = 5000, intertTime = DateTime.UtcNow.AddDays(-2), title = "Item 1" },
                new Product { asin = "ITEM2", readit = 2, savingspercent = 8000, intertTime = DateTime.UtcNow.AddDays(-1), title = "Item 2" },
                new Product { asin = "ITEM3", readit = 3, savingspercent = 7000, intertTime = DateTime.UtcNow.AddDays(-3), title = "Item 3 (Archived)" },
                new Product { asin = "ITEM4", readit = 4, savingspercent = 9000, intertTime = DateTime.UtcNow.AddDays(-4), title = "Item 4 (Wishlisted)" },
            };

            _context.Products.AddRange(products);
            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();
        }

        [Fact]
        public async Task GetDealsFeedAsync_ReturnsOnlyReaditLessOrEqualTo2_OrderedBySavingsPercentDesc()
        {
            await SeedSampleProductsAsync();

            var deals = await _service.GetDealsFeedAsync();

            Assert.Equal(2, deals.Count);
            Assert.Equal("ITEM2", deals[0].asin); // 8000% savings first
            Assert.Equal("ITEM1", deals[1].asin); // 5000% savings second
        }

        [Fact]
        public async Task MarkDealsAsReadAsync_UpdatesReadit1To2()
        {
            await SeedSampleProductsAsync();

            await _service.MarkDealsAsReadAsync();
            _context.ChangeTracker.Clear();

            var item1 = await _context.Products.AsNoTracking().FirstAsync(p => p.asin == "ITEM1");
            Assert.Equal(2, item1.readit);
        }

        [Fact]
        public async Task ArchiveReadDealsAsync_UpdatesReadit2To3()
        {
            await SeedSampleProductsAsync();

            var count = await _service.ArchiveReadDealsAsync();
            _context.ChangeTracker.Clear();

            Assert.Equal(1, count);
            var item2 = await _context.Products.AsNoTracking().FirstAsync(p => p.asin == "ITEM2");
            Assert.Equal(3, item2.readit);
        }

        [Fact]
        public async Task WishlistOperations_AddAndRemove_WorksAndUpdatesCounts()
        {
            await SeedSampleProductsAsync();

            // Initial wishlist count: ITEM4 is wishlisted
            var initialCount = await _service.GetWishlistCountAsync();
            Assert.Equal(1, initialCount);

            // Add ITEM1 to wishlist
            var afterAddCount = await _service.AddToWishlistAsync("ITEM1");
            Assert.Equal(2, afterAddCount);
            _context.ChangeTracker.Clear();

            var item1 = await _context.Products.AsNoTracking().FirstAsync(p => p.asin == "ITEM1");
            Assert.Equal(4, item1.readit);

            // Wishlist listing returns ITEM4 and ITEM1
            var wishlist = await _service.GetWishlistAsync();
            Assert.Equal(2, wishlist.Count);

            // Remove ITEM1 from wishlist (sets to 3 = archived)
            var afterRemoveCount = await _service.RemoveFromWishlistAsync("ITEM1");
            Assert.Equal(1, afterRemoveCount);
            _context.ChangeTracker.Clear();

            item1 = await _context.Products.AsNoTracking().FirstAsync(p => p.asin == "ITEM1");
            Assert.Equal(3, item1.readit);
        }

        [Fact]
        public async Task GetProductAdminDataAsync_ReturnsTotalAndOldest()
        {
            await SeedSampleProductsAsync();

            var data = await _service.GetProductAdminDataAsync();

            Assert.Equal(4, data.TotalCount);
            Assert.NotNull(data.OldestProduct);
            Assert.Equal("ITEM4", data.OldestProduct!.asin);
        }

        [Fact]
        public async Task DeleteProductsByDateRangeAsync_ExcludesWishlistWhenFlagTrue()
        {
            await SeedSampleProductsAsync();

            var start = DateTime.UtcNow.AddDays(-10);
            var end = DateTime.UtcNow;

            // Delete with excludeWishlist = true
            int deleted = await _service.DeleteProductsByDateRangeAsync(start, end, excludeWishlist: true);
            _context.ChangeTracker.Clear();

            // Should delete ITEM1, ITEM2, ITEM3 (3 items), leaving wishlisted ITEM4
            Assert.Equal(3, deleted);

            var remaining = await _context.Products.AsNoTracking().ToListAsync();
            Assert.Single(remaining);
            Assert.Equal("ITEM4", remaining[0].asin);
            Assert.Equal(4, remaining[0].readit);
        }

        [Fact]
        public async Task Caching_WishlistCount_ReturnsCachedValueUntilInvalidated()
        {
            await SeedSampleProductsAsync();

            var count1 = await _service.GetWishlistCountAsync();
            Assert.Equal(1, count1);

            // Directly modify database behind the scenes
            await _context.Products.Where(p => p.asin == "ITEM2").ExecuteUpdateAsync(s => s.SetProperty(p => p.readit, 4));
            _context.ChangeTracker.Clear();

            // Without invalidating cache, count should still return cached value 1
            var count2 = await _service.GetWishlistCountAsync();
            Assert.Equal(1, count2);

            // After invalidating cache, returns 2
            _service.InvalidateWishlistCache();
            var count3 = await _service.GetWishlistCountAsync();
            Assert.Equal(2, count3);
        }
    }
}

