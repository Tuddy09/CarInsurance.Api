using CarInsurance.Api.Data;
using CarInsurance.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CarInsurance.Api.Services;

public class PolicyExpirationService
{
    private readonly AppDbContext _db;
    private readonly ILogger<PolicyExpirationService> _logger;
    private readonly ITimeProvider _timeProvider;

    public PolicyExpirationService(AppDbContext db, ILogger<PolicyExpirationService> logger, ITimeProvider timeProvider)
    {
        _db = db;
        _logger = logger;
        _timeProvider = timeProvider;
    }
    
    public async Task ProcessExpiredPoliciesAsync()
    {
        var now = _timeProvider.UtcNow;
        var oneHourAgo = now.AddHours(-1);

        // This is the crucial check. A policy expiration event happens exactly at midnight.
        // We only need to run the query if our window includes midnight
        if (oneHourAgo.Date == now.Date)
        {
            _logger.LogInformation("No midnight transition in the last hour. No policies to process at {ProcessTime}", now);
            return; // Exit early, no database query needed
        }

        var targetExpirationDate = DateOnly.FromDateTime(oneHourAgo);

        var expiredPolicies = await _db.Policies
            .Include(p => p.Car)
            .ThenInclude(c => c.Owner)
            .Where(p => p.EndDate == targetExpirationDate)
            .Where(p => !_db.PolicyExpirationLogs.Any(log => log.PolicyId == p.Id))
            .ToListAsync();

        if (!expiredPolicies.Any())
        {
            _logger.LogInformation("No new expired policies to process at {ProcessTime}", now);
            return;
        }

        foreach (var policy in expiredPolicies)
        {
            var logMessage = $"Insurance policy {policy.Id} for car {policy.Car.Vin} (Owner: {policy.Car.Owner.Name}) " +
                             $"provided by {policy.Provider} expired on {policy.EndDate:yyyy-MM-dd}";

            _logger.LogWarning(logMessage);

            var expirationLog = new PolicyExpirationLog
            {
                PolicyId = policy.Id,
                ExpirationDate = policy.EndDate,
                ProcessedAt = now,
                LogMessage = logMessage
            };

            _db.PolicyExpirationLogs.Add(expirationLog);
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("Processed {Count} expired policies", expiredPolicies.Count);
    }
}