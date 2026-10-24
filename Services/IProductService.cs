using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WebUIMVC.Data;
using WebUIMVC.Models;

namespace WebUIMVC.Services
{
    public interface IProductService
    {
        Task<List<Product>> GetDealsFeedAsync(CancellationToken cancellationToken = default);

        Task MarkDealsAsReadAsync(CancellationToken cancellationToken = default);

        Task<int> ArchiveReadDealsAsync(CancellationToken cancellationToken = default);

        Task<List<Product>> GetWishlistAsync(CancellationToken cancellationToken = default);

        Task<int> GetWishlistCountAsync(CancellationToken cancellationToken = default);

        Task<int> AddToWishlistAsync(string asin, CancellationToken cancellationToken = default);

        Task<int> RemoveFromWishlistAsync(string asin, CancellationToken cancellationToken = default);

        Task<ProductAdminViewModel> GetProductAdminDataAsync(CancellationToken cancellationToken = default);

        Task<int> GetDeletePreviewCountAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default);

        Task<List<Product>> GetDeletePreviewProductsAsync(DateTime start, DateTime end, int maxPreview = 100, CancellationToken cancellationToken = default);

        Task<int> DeleteProductsByDateRangeAsync(DateTime start, DateTime end, bool excludeWishlist = true, CancellationToken cancellationToken = default);

        void InvalidateFeedCache();

        void InvalidateWishlistCache();
    }
}

