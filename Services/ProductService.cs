using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using WebUIMVC.Data;
using WebUIMVC.Models;

namespace WebUIMVC.Services
{
    public class ProductService : IProductService
    {
        private const string FeedCacheKey = "DealsFeed_Active";
        private const string WishlistCountCacheKey = "Wishlist_Count";

        private readonly Context _context;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<ProductService> _logger;

        public ProductService(Context context, IMemoryCache memoryCache, ILogger<ProductService> logger)
        {
            _context = context;
            _memoryCache = memoryCache;
            _logger = logger;
        }

        public async Task<List<Product>> GetDealsFeedAsync(CancellationToken cancellationToken = default)
        {
            if (_memoryCache.TryGetValue(FeedCacheKey, out List<Product>? cachedFeed) && cachedFeed != null)
            {
                return cachedFeed;
            }

            // Exclude wishlisted items (readit == 4) and archived items (readit == 3)
            // Query push-down: perform filtering and descending sort directly in SQLite
            var deals = await _context.Products
                .AsNoTracking()
                .Where(i => i.readit <= 2)
                .OrderByDescending(x => x.savingspercent)
                .ToListAsync(cancellationToken);

            var cacheOptions = new MemoryCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromSeconds(30),
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2)
            };

            _memoryCache.Set(FeedCacheKey, deals, cacheOptions);
            return deals;
        }

        public async Task MarkDealsAsReadAsync(CancellationToken cancellationToken = default)
        {
            await DbLock.WriteLock.WaitAsync(cancellationToken);
            try
            {
                int updated = await _context.Products
                    .Where(i => i.readit <= 1)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.readit, 2), cancellationToken);

                if (updated > 0)
                {
                    _logger.LogDebug("Marked {Count} products as readit = 2", updated);
                    InvalidateFeedCache();
                }
            }
            finally
            {
                DbLock.WriteLock.Release();
            }
        }

        public async Task<int> ArchiveReadDealsAsync(CancellationToken cancellationToken = default)
        {
            await DbLock.WriteLock.WaitAsync(cancellationToken);
            try
            {
                int updated = await _context.Products
                    .Where(i => i.readit == 2)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.readit, 3), cancellationToken);

                InvalidateFeedCache();
                _logger.LogInformation("Archived {Count} items.", updated);
                return updated;
            }
            finally
            {
                DbLock.WriteLock.Release();
            }
        }

        public async Task<List<Product>> GetWishlistAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Products
                .AsNoTracking()
                .Where(i => i.readit == 4)
                .OrderByDescending(x => x.savingspercent)
                .ToListAsync(cancellationToken);
        }

        public async Task<int> GetWishlistCountAsync(CancellationToken cancellationToken = default)
        {
            return await _memoryCache.GetOrCreateAsync(WishlistCountCacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                return await _context.Products
                    .AsNoTracking()
                    .CountAsync(p => p.readit == 4, cancellationToken);
            });
        }

        public async Task<int> AddToWishlistAsync(string asin, CancellationToken cancellationToken = default)
        {
            await DbLock.WriteLock.WaitAsync(cancellationToken);
            try
            {
                await _context.Products
                    .Where(p => p.asin == asin)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.readit, 4), cancellationToken);
            }
            finally
            {
                DbLock.WriteLock.Release();
            }

            InvalidateWishlistCache();
            InvalidateFeedCache();
            return await GetWishlistCountAsync(cancellationToken);
        }

        public async Task<int> RemoveFromWishlistAsync(string asin, CancellationToken cancellationToken = default)
        {
            await DbLock.WriteLock.WaitAsync(cancellationToken);
            try
            {
                // 3 = archived
                await _context.Products
                    .Where(p => p.asin == asin)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.readit, 3), cancellationToken);
            }
            finally
            {
                DbLock.WriteLock.Release();
            }

            InvalidateWishlistCache();
            InvalidateFeedCache();
            return await GetWishlistCountAsync(cancellationToken);
        }

        public async Task<ProductAdminViewModel> GetProductAdminDataAsync(CancellationToken cancellationToken = default)
        {
            var totalCount = await _context.Products.CountAsync(cancellationToken);
            var oldest = await _context.Products
                .AsNoTracking()
                .OrderBy(x => x.intertTime)
                .FirstOrDefaultAsync(cancellationToken);

            return new ProductAdminViewModel
            {
                TotalCount = totalCount,
                OldestProduct = oldest
            };
        }

        public async Task<int> GetDeletePreviewCountAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default)
        {
            return await _context.Products
                .AsNoTracking()
                .CountAsync(p => p.readit != 4 && p.intertTime >= start && p.intertTime <= end, cancellationToken);
        }

        public async Task<List<Product>> GetDeletePreviewProductsAsync(DateTime start, DateTime end, int maxPreview = 100, CancellationToken cancellationToken = default)
        {
            return await _context.Products
                .AsNoTracking()
                .Where(p => p.readit != 4 && p.intertTime >= start && p.intertTime <= end)
                .Take(maxPreview)
                .ToListAsync(cancellationToken);
        }

        public async Task<int> DeleteProductsByDateRangeAsync(DateTime start, DateTime end, bool excludeWishlist = true, CancellationToken cancellationToken = default)
        {
            await DbLock.WriteLock.WaitAsync(cancellationToken);
            try
            {
                var query = _context.Products.Where(p => p.intertTime >= start && p.intertTime <= end);
                if (excludeWishlist)
                {
                    query = query.Where(p => p.readit != 4);
                }

                int count = await query.ExecuteDeleteAsync(cancellationToken);
                if (count > 0)
                {
                    InvalidateFeedCache();
                    InvalidateWishlistCache();
                }
                return count;
            }
            finally
            {
                DbLock.WriteLock.Release();
            }
        }

        public void InvalidateFeedCache()
        {
            _memoryCache.Remove(FeedCacheKey);
        }

        public void InvalidateWishlistCache()
        {
            _memoryCache.Remove(WishlistCountCacheKey);
        }
    }
}
