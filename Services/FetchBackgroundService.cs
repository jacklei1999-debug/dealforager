using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WebUIMVC.Data;
using WebUIMVC.Hubs;

namespace WebUIMVC.Services
{
    public class FetchBackgroundService : BackgroundService
    {
        private readonly ILogger<FetchBackgroundService> _logger;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public FetchBackgroundService(
            ILogger<FetchBackgroundService> logger,
            IHubContext<NotificationHub> hubContext,
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration)
        {
            _logger = logger;
            _hubContext = hubContext;
            _scopeFactory = scopeFactory;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Fetch Background Service starting...");

            var configPath = Path.Combine(AppContext.BaseDirectory, "fetchConfigs.json");

            if (!File.Exists(configPath))
            {
                _logger.LogWarning("Config file not found: {ConfigPath}. Fetch service disabled.", configPath);
                return;
            }

            var jsonContent = await File.ReadAllTextAsync(configPath, stoppingToken);
            var configs = JsonSerializer.Deserialize<List<FetchConfig>>(jsonContent, _jsonOptions);

            if (configs == null || configs.Count == 0)
            {
                _logger.LogWarning("No configurations found in fetchConfigs.json. Fetch service disabled.");
                return;
            }

            _logger.LogInformation("Loaded {Count} fetch configurations", configs.Count);
            foreach (var config in configs)
            {
                _logger.LogInformation("  {DisplayName} Category: {Category}", config.DisplayName, config.Category);
            }

            using var fetchWindow = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var durationSeconds = _configuration.GetValue<int>("Fetching:DurationSeconds");
            if (durationSeconds > 0)
            {
                fetchWindow.CancelAfter(TimeSpan.FromSeconds(durationSeconds));
                _logger.LogInformation("Fetching will stop after {Seconds} seconds; the UI will remain available.", durationSeconds);
            }

            var tasks = new List<Task>();
            foreach (var config in configs)
            {
                tasks.Add(RunConfigAsync(config, fetchWindow.Token));
            }

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException) when (fetchWindow.IsCancellationRequested)
            {
            }

            if (!stoppingToken.IsCancellationRequested && fetchWindow.IsCancellationRequested)
            {
                _logger.LogInformation("Test fetch window ended. Background fetching stopped; the UI remains available.");
            }
        }

        private async Task RunConfigAsync(FetchConfig config, CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    int newProducts = await FetchProductsAsync(config, stoppingToken);
                    if (newProducts > 0)
                    {
                        using (var scope = _scopeFactory.CreateScope())
                        {
                            var productService = scope.ServiceProvider.GetService<IProductService>();
                            productService?.InvalidateFeedCache();
                        }

                        await _hubContext.Clients.All.SendAsync("newDeals", newProducts, stoppingToken);
                    }

                    _logger.LogInformation("{DisplayName} Sleeping for {Interval} seconds...", config.DisplayName, config.Interval);
                    await Task.Delay(config.Interval * 1000, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "{DisplayName} Error: {Message}. Restarting in 10 seconds...", config.DisplayName, ex.Message);
                    await Task.Delay(10000, stoppingToken);
                }
            }
        }

        private async Task<int> FetchProductsAsync(FetchConfig config, CancellationToken cancellationToken)
        {
            string sorttype = "1";
            _logger.LogInformation("{DisplayName} Starting fetch...", config.DisplayName);

            int newProductsCount = 0;
            using (var scope = _scopeFactory.CreateScope())
            {
                var fetcher = scope.ServiceProvider.GetRequiredService<FetchData>();
                newProductsCount = await fetcher.GetProducts(config.Category, sorttype, config.MinSavings, cancellationToken);
            }

            if (newProductsCount > 0)
            {
                _logger.LogInformation("Detected {Count} new products for {DisplayName}.", newProductsCount, config.DisplayName);
            }
            else
            {
                _logger.LogInformation("No new products detected for {DisplayName}.", config.DisplayName);
            }

            return newProductsCount;
        }
    }
}
