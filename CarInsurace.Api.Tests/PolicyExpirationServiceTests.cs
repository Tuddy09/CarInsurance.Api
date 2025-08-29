using CarInsurance.Api.Data;
using CarInsurance.Api.Models;
using CarInsurance.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace CarInsurace.Api.Tests;

public class PolicyExpirationServiceTests
{
    private static AppDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task ProcessExpiredPoliciesAsync_NoMidnightTransition_ExitsEarly()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var mockLogger = new Mock<ILogger<PolicyExpirationService>>();
        var mockTimeProvider = new Mock<ITimeProvider>();

        // Set time to 10:30 AM, so one hour ago is 9:30 AM, no midnight transition
        var currentTime = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        mockTimeProvider.Setup(x => x.UtcNow).Returns(currentTime);
        
        var service = new PolicyExpirationService(context, mockLogger.Object, mockTimeProvider.Object);

        // Act
        await service.ProcessExpiredPoliciesAsync();

        // Assert
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No midnight transition in the last hour")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        // Verify no database operations
        var logCount = await context.PolicyExpirationLogs.CountAsync();
        Assert.Equal(0, logCount);
    }

    [Fact]
    public async Task ProcessExpiredPoliciesAsync_WithExpiredPolicies_ProcessesSuccessfully()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var mockLogger = new Mock<ILogger<PolicyExpirationService>>();
        var mockTimeProvider = new Mock<ITimeProvider>();
        
        // Set time to 00:30 (30 minutes after midnight) - this ensures midnight transition
        var currentTime = new DateTime(2024, 1, 2, 0, 30, 0, DateTimeKind.Utc);
        mockTimeProvider.Setup(x => x.UtcNow).Returns(currentTime);
        
        // Create test data with policy expiring on January 1st (the day that crossed midnight)
        var owner = new Owner { Id = 1, Name = "Test Owner", Email = "test@example.com" };
        var car = new Car 
        { 
            Id = 1, 
            Vin = "TEST12345", 
            Make = "Toyota", 
            Model = "Camry", 
            YearOfManufacture = 2020, 
            OwnerId = 1, 
            Owner = owner 
        };

        var expiredPolicy = new InsurancePolicy
        {
            Id = 1,
            CarId = 1,
            Car = car,
            Provider = "Garanti",
            StartDate = new DateOnly(2023, 1, 1),
            EndDate = new DateOnly(2024, 1, 1)
        };

        context.Owners.Add(owner);
        context.Cars.Add(car);
        context.Policies.Add(expiredPolicy);
        await context.SaveChangesAsync();

        var service = new PolicyExpirationService(context, mockLogger.Object, mockTimeProvider.Object);

        // Act
        await service.ProcessExpiredPoliciesAsync();

        // Assert
        var logEntries = await context.PolicyExpirationLogs.ToListAsync();
        Assert.Single(logEntries);

        var logEntry = logEntries.First();
        Assert.Equal(1, logEntry.PolicyId);
        Assert.Equal(new DateOnly(2024, 1, 1), logEntry.ExpirationDate);
        Assert.Contains("TEST12345", logEntry.LogMessage);
        Assert.Contains("Test Owner", logEntry.LogMessage);
        Assert.Contains("Garanti", logEntry.LogMessage);
        Assert.Equal(currentTime, logEntry.ProcessedAt);

        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Insurance policy 1 for car TEST12345")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Processed 1 expired policies")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessExpiredPoliciesAsync_AlreadyProcessedPolicies_SkipsProcessing()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var mockLogger = new Mock<ILogger<PolicyExpirationService>>();
        var mockTimeProvider = new Mock<ITimeProvider>();
        
        // Set time to 00:30
        var currentTime = new DateTime(2024, 1, 2, 0, 30, 0, DateTimeKind.Utc);
        mockTimeProvider.Setup(x => x.UtcNow).Returns(currentTime);
        
        // Create test data
        var owner = new Owner { Id = 1, Name = "Test Owner", Email = "test@example.com" };
        var car = new Car 
        { 
            Id = 1, 
            Vin = "TEST12345", 
            Make = "Toyota", 
            Model = "Camry", 
            YearOfManufacture = 2020, 
            OwnerId = 1, 
            Owner = owner 
        };

        var expiredPolicy = new InsurancePolicy
        {
            Id = 1,
            CarId = 1,
            Car = car,
            Provider = "Garanti",
            StartDate = new DateOnly(2023, 1, 1),
            EndDate = new DateOnly(2024, 1, 1)
        };

        // Add existing log entry for this policy
        var existingLog = new PolicyExpirationLog
        {
            Id = 1,
            PolicyId = 1,
            ExpirationDate = new DateOnly(2024, 1, 1),
            ProcessedAt = currentTime.AddHours(-2),
            LogMessage = "Already processed"
        };

        context.Owners.Add(owner);
        context.Cars.Add(car);
        context.Policies.Add(expiredPolicy);
        context.PolicyExpirationLogs.Add(existingLog);
        await context.SaveChangesAsync();

        var service = new PolicyExpirationService(context, mockLogger.Object, mockTimeProvider.Object);

        // Act
        await service.ProcessExpiredPoliciesAsync();

        // Assert
        var logCount = await context.PolicyExpirationLogs.CountAsync();
        Assert.Equal(1, logCount); // Should still have only the original log

        var logEntry = await context.PolicyExpirationLogs.FirstAsync();
        Assert.Equal("Already processed", logEntry.LogMessage);

        // Verify "no new expired policies" message was logged
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No new expired policies to process")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}