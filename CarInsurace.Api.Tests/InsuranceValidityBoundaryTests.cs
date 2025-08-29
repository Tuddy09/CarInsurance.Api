using CarInsurance.Api.Controllers;
using CarInsurance.Api.Data;
using CarInsurance.Api.Dtos;
using CarInsurance.Api.Models;
using CarInsurance.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarInsurace.Api.Tests;

public class InsuranceValidityBoundaryTests
{
    private static AppDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        
        // Seed test data
        var owner = new Owner { Id = 1, Name = "Test Owner", Email = "test@example.com" };
        var car = new Car { Id = 1, Vin = "TEST123", Make = "TestMake", Model = "TestModel", YearOfManufacture = 2020, OwnerId = 1, Owner = owner };
        
        context.Owners.Add(owner);
        context.Cars.Add(car);
        
        // Add insurance policy for boundary testing
        context.Policies.Add(new InsurancePolicy 
        { 
            Id = 1, 
            CarId = 1, 
            Provider = "TestProvider", 
            StartDate = new DateOnly(2024, 1, 1), 
            EndDate = new DateOnly(2024, 12, 31) 
        });
        
        context.SaveChanges();
        return context;
    }

    [Fact]
    public void Test_ExactPolicyStartDate_ReturnsValid()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var service = new CarService(context);
        var controller = new CarsController(service);
        
        // Act - Test exact policy start date (boundary condition)
        var result = controller.IsInsuranceValid(1, "2024-01-01").Result;
        
        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<InsuranceValidityResponse>(okResult.Value);
        Assert.True(response.Valid);
        Assert.Equal("2024-01-01", response.Date);
    }

    [Fact]
    public void Test_ExactPolicyEndDate_ReturnsValid()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var service = new CarService(context);
        var controller = new CarsController(service);
        
        // Act - Test exact policy end date (boundary condition)
        var result = controller.IsInsuranceValid(1, "2024-12-31").Result;
        
        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<InsuranceValidityResponse>(okResult.Value);
        Assert.True(response.Valid);
        Assert.Equal("2024-12-31", response.Date);
    }

    [Fact]
    public void Test_OneDayBeforePolicy_ReturnsInvalid()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var service = new CarService(context);
        var controller = new CarsController(service);
        
        // Act - Test one day before policy starts
        var result = controller.IsInsuranceValid(1, "2023-12-31").Result;
        
        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<InsuranceValidityResponse>(okResult.Value);
        Assert.False(response.Valid);
        Assert.Equal("2023-12-31", response.Date);
    }

    [Fact]
    public void Test_OneDayAfterPolicy_ReturnsInvalid()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var service = new CarService(context);
        var controller = new CarsController(service);
        
        // Act - Test one day after policy ends
        var result = controller.IsInsuranceValid(1, "2025-01-01").Result;
        
        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<InsuranceValidityResponse>(okResult.Value);
        Assert.False(response.Valid);
        Assert.Equal("2025-01-01", response.Date);
    }

    [Fact]
    public void Test_NonExistentCarId_ReturnsNotFound()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var service = new CarService(context);
        var controller = new CarsController(service);
        
        // Act - Test non-existent car ID
        var result = controller.IsInsuranceValid(999, "2024-06-15").Result;
        
        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.Equal("Car 999 not found", notFoundResult.Value);
    }

    [Fact]
    public void Test_ImpossibleDate_ReturnsBadRequest()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var service = new CarService(context);
        var controller = new CarsController(service);
        
        // Act - Test impossible date (February 30)
        var result = controller.IsInsuranceValid(1, "2024-02-30").Result;
        
        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Invalid date format. Use YYYY-MM-DD.", badRequestResult.Value);
    }
}