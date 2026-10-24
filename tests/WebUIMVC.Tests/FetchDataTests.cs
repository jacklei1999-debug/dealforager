using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WebUIMVC.Data;
using Xunit;

namespace WebUIMVC.Tests
{
    public class FetchDataTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly Context _context;

        public FetchDataTests()
        {
            _connection = new SqliteConnection("Filename=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<Context>()
                .UseSqlite(_connection)
                .Options;

            _context = new Context(options);
            _context.Database.EnsureCreated();
        }

        public void Dispose()
        {
            _context.Dispose();
            _connection.Dispose();
        }

        private class FakeHttpMessageHandler : HttpMessageHandler
        {
            private readonly string _responseContent;

            public FakeHttpMessageHandler(string responseContent)
            {
                _responseContent = responseContent;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(_responseContent)
                };
                return Task.FromResult(response);
            }
        }

        [Fact]
        public async Task GetProducts_WhenDatabaseHasExistingDuplicateAsins_DoesNotThrow()
        {
            // Seed DB with historical duplicate ASINs
            _context.Products.Add(new Product { asin = "DUPASIN", intertTime = DateTime.UtcNow.AddDays(-5), readit = 3 });
            _context.Products.Add(new Product { asin = "DUPASIN", intertTime = DateTime.UtcNow.AddDays(-2), readit = 3 });
            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            // Mock external API returning the duplicate ASIN
            var jsonResponse = "[{\"asin\": \"DUPASIN\", \"savingspercent\": 7000, \"dealscore\": 100, \"pricedifference\": 50, \"usedprice\": 20, \"updated_at\": \"2026-10-23T12:00:00Z\", \"rootcat\": \"test\", \"image\": [105]}]";
            var httpClient = new HttpClient(new FakeHttpMessageHandler(jsonResponse));
            var fetchData = new FetchData(_context, httpClient, NullLogger<FetchData>.Instance);

            // Must not throw ArgumentException: An item with the same key has already been added
            var added = await fetchData.GetProducts("testcat", "1", "50", CancellationToken.None);

            // Successfully processed
            Assert.True(added >= 0);
        }

        [Fact]
        public async Task GetProducts_WhenIncomingBatchContainsDuplicateAsins_DoesNotThrow()
        {
            // Mock external API returning the same ASIN twice in one response
            var jsonResponse = "[{\"asin\": \"BATCHDUP\", \"savingspercent\": 5000, \"dealscore\": 100, \"pricedifference\": 50, \"usedprice\": 20, \"updated_at\": \"2026-10-23T12:00:00Z\", \"rootcat\": \"test\", \"image\": [105]}," +
                               "{\"asin\": \"BATCHDUP\", \"savingspercent\": 5000, \"dealscore\": 100, \"pricedifference\": 50, \"usedprice\": 20, \"updated_at\": \"2026-10-23T12:00:00Z\", \"rootcat\": \"test\", \"image\": [105]}]";
            var httpClient = new HttpClient(new FakeHttpMessageHandler(jsonResponse));
            var fetchData = new FetchData(_context, httpClient, NullLogger<FetchData>.Instance);

            var added = await fetchData.GetProducts("testcat", "1", "50", CancellationToken.None);

            Assert.Equal(1, added);
            var countInDb = await _context.Products.CountAsync(p => p.asin == "BATCHDUP");
            Assert.Equal(1, countInDb);
        }
    }
}

