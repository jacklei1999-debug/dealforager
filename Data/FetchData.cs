using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace WebUIMVC.Data;

public class FetchData
{

    private readonly Context _context;
    private readonly HttpClient _client;
    private readonly ILogger<FetchData> _logger;

    public FetchData(Context context, HttpClient client, ILogger<FetchData> logger)
    {
        _context = context;
        _client = client;
        _logger = logger;
    }

    public async Task<int> GetProducts(string cat, string sorttype, string miniSaving, CancellationToken cancellationToken = default)
    {
        Product? lastProduct = null;
        bool theEnd = false;
        int newAddedProducts = 0;

        while (!theEnd)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string url = lastProduct == null
                ? $"https://dealforager.com/api/products?cat={cat}&domain=1&sort={sorttype}&minSavings={miniSaving}"
                : $"https://dealforager.com/api/products?cat={cat}&domain=1&sort={sorttype}&minSavings={miniSaving}&a={lastProduct.asin}&p={lastProduct.pricedifference}&s={lastProduct.savingspercent}&d={lastProduct.dealscore}&u={lastProduct.usedprice}";

            _logger.LogDebug("Fetching deals from {Url}", url);

            string responseJson;
            try
            {
                responseJson = await _client.GetStringAsync(url, cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Failed to fetch data from API endpoint");
                break;
            }

            var productsArray = JArray.Parse(responseJson);
            _logger.LogInformation("Pulled items: {Count}", productsArray.Count);

            if (productsArray.Count == 0)
            {
                theEnd = true;
                break;
            }

            try
            {
                // Extract unique ASINs in the current batch
                var batchAsins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in productsArray)
                {
                    string? a = (string?)item["asin"];
                    if (!string.IsNullOrEmpty(a))
                    {
                        batchAsins.Add(a);
                    }
                }

                // Retrieve existing matching products from DB
                var existingProductsList = await _context.Products
                    .Where(p => batchAsins.Contains(p.asin))
                    .ToListAsync(cancellationToken);

                // Safely build lookup map to guard against any historical duplicate ASIN rows
                var existingProductsMap = new Dictionary<string, Product>(StringComparer.OrdinalIgnoreCase);
                foreach (var prod in existingProductsList)
                {
                    if (!existingProductsMap.TryGetValue(prod.asin, out var current) || prod.intertTime > current.intertTime)
                    {
                        existingProductsMap[prod.asin] = prod;
                    }
                }

                Product? batchLastProduct = null;

                foreach (var item in productsArray)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string? asin = (string?)item["asin"];
                    if (string.IsNullOrEmpty(asin))
                    {
                        continue;
                    }

                    // Decode image characters using efficient string builder
                    string imageUrl = "i";
                    var imageToken = item["image"];
                    if (imageToken != null)
                    {
                        int[]? asciiValues = imageToken.ToObject<int[]>();
                        if (asciiValues != null && asciiValues.Length > 0)
                        {
                            imageUrl = string.Create(asciiValues.Length, asciiValues, (span, vals) =>
                            {
                                for (int i = 0; i < vals.Length; i++)
                                {
                                    span[i] = (char)vals[i];
                                }
                            });
                        }
                    }

                    int savingspercent = (int?)item["savingspercent"] ?? 0;
                    int dealscore = (int?)item["dealscore"] ?? 0;
                    int pricedifference = (int?)item["pricedifference"] ?? 0;
                    int usedprice = (int?)item["usedprice"] ?? 0;
                    DateTime updatedAt = (DateTime?)item["updated_at"] ?? DateTime.UtcNow;
                    string rootcat = (string?)item["rootcat"] ?? string.Empty;

                    if (!existingProductsMap.TryGetValue(asin, out var existingProduct))
                    {
                        var newProduct = new Product
                        {
                            asin = asin,
                            imageURL = imageUrl,
                            dealscore = dealscore,
                            pricedifference = pricedifference,
                            title = "e",
                            updated_at = updatedAt,
                            savingspercent = savingspercent,
                            usedprice = usedprice,
                            intertTime = DateTime.Now,
                            rootcat = rootcat,
                            readit = 1,
                            liked = 0
                        };

                        _context.Products.Add(newProduct);
                        existingProductsMap[asin] = newProduct;
                        newAddedProducts++;
                        batchLastProduct = newProduct;
                    }
                    else if (savingspercent > 6000 && (DateTime.Now - existingProduct.intertTime).TotalDays > 5.0)
                    {
                        // In-place update of existing deal
                        existingProduct.imageURL = imageUrl;
                        existingProduct.dealscore = dealscore;
                        existingProduct.pricedifference = pricedifference;
                        existingProduct.updated_at = updatedAt;
                        existingProduct.savingspercent = savingspercent;
                        existingProduct.usedprice = usedprice;
                        existingProduct.intertTime = DateTime.Now;
                        existingProduct.rootcat = rootcat;
                        existingProduct.readit = 1;
                        existingProduct.liked = 1;
                        newAddedProducts++;
                        batchLastProduct = existingProduct;
                    }
                    else
                    {
                        batchLastProduct = existingProduct;
                    }
                }

                lastProduct = batchLastProduct;

                // Synchronize concurrent SQLite writes across parallel background workers
                await DbLock.WriteLock.WaitAsync(cancellationToken);
                try
                {
                    await _context.SaveChangesAsync(cancellationToken);
                }
                finally
                {
                    DbLock.WriteLock.Release();
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing incoming batch of products");
            }

            if (productsArray.Count < 64)
            {
                theEnd = true;
            }
        }

        _logger.LogInformation("{Time} -- {Count} added", DateTime.Now, newAddedProducts);
        return newAddedProducts;
    }
}
