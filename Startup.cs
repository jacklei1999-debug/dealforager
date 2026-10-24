using System;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WebUIMVC.Data;
using WebUIMVC.Services;

namespace WebUIMVC
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllersWithViews();
            services.AddSignalR();
            services.AddMemoryCache();

            services.AddDbContext<Context>(options =>
            {
                var connStr = Configuration.GetConnectionString("DealForagerDb") ?? "Data Source=DealA.db";
                var csBuilder = new SqliteConnectionStringBuilder(connStr);
                if (!Path.IsPathRooted(csBuilder.DataSource))
                {
                    csBuilder.DataSource = Path.Combine(AppContext.BaseDirectory, csBuilder.DataSource);
                }

                options.UseSqlite(
                    csBuilder.ToString(),
                    sqliteOptions => sqliteOptions.CommandTimeout(30));

                // Suppress EF Core internal cancellation interruption noise when background tokens cancel
                options.ConfigureWarnings(w => w.Log(
                    (RelationalEventId.ConnectionError, LogLevel.Debug)));
            });

            services.AddScoped<IProductService, ProductService>();
            services.AddHttpClient<FetchData>();

            // Register the background fetch service
            services.AddHostedService<FetchBackgroundService>();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            // Ensure database and performance indexes are created
            using (var scope = app.ApplicationServices.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<Context>();
                context.Database.EnsureCreated();

                context.Database.ExecuteSqlRaw(@"
                    PRAGMA journal_mode = WAL;
                    PRAGMA busy_timeout = 30000;
                    PRAGMA synchronous = NORMAL;
                    CREATE INDEX IF NOT EXISTS IX_Products_asin ON Products(asin);
                    CREATE INDEX IF NOT EXISTS IX_Products_readit_savingspercent ON Products(readit, savingspercent DESC);
                    CREATE INDEX IF NOT EXISTS IX_Products_intertTime ON Products(intertTime);
                ");
            }

            if (env.IsDevelopment() || Configuration.GetValue<bool>("DisplayDetailedErrors", false))
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHub<WebUIMVC.Hubs.NotificationHub>("/notificationHub");
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
