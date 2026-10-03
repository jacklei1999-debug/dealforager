using DealForager.Shared;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using WebUIMVC.Hubs;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;

namespace WebUIMVC.Services
{
    public class FetchBackgroundService : BackgroundService
    {
        private readonly ILogger<FetchBackgroundService> _logger;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public FetchBackgroundService(ILogger<FetchBackgroundService> logger, IHubContext<NotificationHub> hubContext, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _hubContext = hubContext;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Fetch Background Service starting...");

            // Load configurations from JSON file
            var configPath = Path.Combine(AppContext.BaseDirectory, "fetchConfigs.json");

            if (!File.Exists(configPath))
            {
                _logger.LogWarning($"Config file not found: {configPath}. Fetch service disabled.");
                return;
            }

            var jsonContent = await File.ReadAllTextAsync(configPath, stoppingToken);
            var configs = JsonSerializer.Deserialize<List<FetchConfig>>(jsonContent, _jsonOptions);

            if (configs == null || configs.Count == 0)
            {
                _logger.LogWarning("No configurations found in fetchConfigs.json. Fetch service disabled.");
                return;
            }

            _logger.LogInformation($"Loaded {configs.Count} fetch configurations");
            foreach (var config in configs)
            {
                _logger.LogInformation($"  {config.DisplayName} Category: {config.Category}");
            }

            // Run all configurations in parallel
            var tasks = new List<Task>();
            foreach (var config in configs)
            {
                tasks.Add(RunConfigAsync(config, stoppingToken));
            }

            await Task.WhenAll(tasks);
        }

        private async Task RunConfigAsync(FetchConfig config, CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    int newProducts = await FetchProductsAsync(config);
                    if (newProducts > 0)
                    {
                        await _hubContext.Clients.All.SendAsync("newDeals", newProducts);
                    }

                    _logger.LogInformation($"{config.DisplayName} Sleeping for {config.Interval} seconds...");
                    await Task.Delay(config.Interval * 1000, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Service is stopping
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"{config.DisplayName} Error: {ex.Message}. Restarting in 10 seconds...");
                    await Task.Delay(10000, stoppingToken);
                }
            }
        }

        private async Task<int> FetchProductsAsync(FetchConfig config)
        {
            string sorttype = "1";

            _logger.LogInformation($"{config.DisplayName} Starting fetch...");

            // 1. Get the state of the DB *before* the fetch
            HashSet<string> asinsBefore;
            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<Context>();
                asinsBefore = context.Products.Select(p => p.asin).ToHashSet();
            }

            // 2. Let the shared library do its work (fetching and saving)
            var fetcher = new FetchData();
            await fetcher.GetProducts(config.Category, sorttype, config.MinSavings);

            // 3. Get the state of the DB *after* the fetch
            HashSet<string> asinsAfter;
            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<Context>();
                asinsAfter = context.Products.Select(p => p.asin).ToHashSet();
            }

            // 4. Compare the before and after states
            asinsAfter.ExceptWith(asinsBefore);
            int newProductsCount = asinsAfter.Count;

            if (newProductsCount > 0)
            {
                _logger.LogInformation($"Detected {newProductsCount} new products for {config.DisplayName}.");
            }
            else
            {
                _logger.LogInformation($"No new products detected for {config.DisplayName}.");
            }

            return newProductsCount;
        }
    }
}
