using System.Net;
using System.Net.Http.Json;
using Kelvinvale.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Kelvinvale.Tests.IntegrationTests;

[TestFixture]
public class AuditLogEndpointTests
{
    private CustomWebApplicationFactory _factory;
    private HttpClient _client;

    [SetUp]
    public void SetUp()
    {
        _factory = new CustomWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task InstructionsEndpoint_WhenCalled_GeneratesExpectedAuditRecord()
    {
        // 1. Arrange: Prepare a request for InstructionsController
        var productId = CustomWebApplicationFactory.AliceIsaProductId;
        var callerId = CustomWebApplicationFactory.CustomerAliceId;

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/products/{productId}/instructions");

        request.Headers.Add("X-Caller-Id", callerId.ToString());
        request.Content = JsonContent.Create(new
        {
            Type = "Subscription",
            AmountPence = 50000,
            FundCode = "GLB-EQ-ACC"
        });

        // 2. Act: Send the HTTP request
        var response = await _client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        //  3. Query: Inspect the database
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KelvinvaleDbContext>();

        var auditEntry = await dbContext.AuditLogs
            .OrderByDescending(a => a.Timestamp)
            .FirstOrDefaultAsync(a => a.CallerId == callerId && a.EntityName == "Instructions");

        // 4. Assert: Verify compliance details
        Assert.That(auditEntry, Is.Not.Null);
        Assert.That(auditEntry!.CallerRole, Is.EqualTo("Customer"));
        Assert.That(auditEntry.Action, Is.EqualTo($"POST /api/v1/products/{productId}/instructions"));
        Assert.That(auditEntry.Details, Does.Contain("201"));
        Assert.That(auditEntry.Details, Does.Contain("StatusCode").IgnoreCase);
    }
}