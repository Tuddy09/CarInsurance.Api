using CarInsurance.Api.Data;
using CarInsurance.Api.Dtos;
using CarInsurance.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CarInsurance.Api.Services;

public class CarService(AppDbContext db)
{
    private readonly AppDbContext _db = db;

    public async Task<List<CarDto>> ListCarsAsync()
    {
        return await _db.Cars.Include(c => c.Owner)
            .Select(c => new CarDto(c.Id, c.Vin, c.Make, c.Model, c.YearOfManufacture,
                                    c.OwnerId, c.Owner.Name, c.Owner.Email))
            .ToListAsync();
    }

    public async Task<bool> IsInsuranceValidAsync(long carId, DateOnly date)
    {
        if (carId <= 0)
            throw new ArgumentException("Car ID must be a positive number.");

        var carExists = await _db.Cars.AnyAsync(c => c.Id == carId);
        if (!carExists) throw new KeyNotFoundException($"Car {carId} not found");

        var minValidDate = new DateOnly(1900, 1, 1);
        var maxValidDate = DateOnly.FromDateTime(DateTime.Now.AddYears(100));
        
        if (date < minValidDate || date > maxValidDate)
            throw new ArgumentException($"Date must be possible.");

        return await _db.Policies.AnyAsync(p =>
            p.CarId == carId &&
            p.StartDate <= date &&
            p.EndDate >= date // Removed the null check
        );
    }

    public async Task<ClaimDto> RegisterClaimAsync(long carId, CreateClaimRequest request)
    {
        var carExists = await _db.Cars.AnyAsync(c => c.Id == carId);
        if (!carExists) throw new KeyNotFoundException($"Car {carId} not found");

        if (!DateOnly.TryParse(request.ClaimDate, out var claimDate))
            throw new ArgumentException("Invalid claim date format. Use YYYY-MM-DD.");

        if (claimDate > DateOnly.FromDateTime(DateTime.Now))
            throw new ArgumentException("Claim date cannot be in the future.");

        if (request.Amount <= 0)
            throw new ArgumentException("Claim amount must be greater than zero.");

        if (string.IsNullOrWhiteSpace(request.Description))
            throw new ArgumentException("Claim description is required.");

        var claim = new Claim
        {
            CarId = carId,
            ClaimDate = claimDate,
            Description = request.Description.Trim(),
            Amount = request.Amount
        };

        _db.Claims.Add(claim);
        await _db.SaveChangesAsync();

        return new ClaimDto(claim.Id, claim.CarId, claim.ClaimDate.ToString("yyyy-MM-dd"), 
                           claim.Description, claim.Amount);
    }

    public async Task<CarHistoryResponse> GetCarHistoryAsync(long carId)
    {
        var carExists = await _db.Cars.AnyAsync(c => c.Id == carId);
        if (!carExists) throw new KeyNotFoundException($"Car {carId} not found");

        var policies = await _db.Policies
            .Where(p => p.CarId == carId)
            .ToListAsync();

        var claims = await _db.Claims
            .Where(c => c.CarId == carId)
            .ToListAsync();

        var historyItems = new List<HistoryItem>();

        historyItems.AddRange(policies.Select(p => new HistoryItem(
            Date: p.StartDate.ToString("yyyy-MM-dd"),
            Type: "PolicyStart",
            PolicyId: p.Id,
            Provider: p.Provider,
            StartDate: p.StartDate.ToString("yyyy-MM-dd"),
            EndDate: p.EndDate.ToString("yyyy-MM-dd")
        )));

        historyItems.AddRange(policies.Select(p => new HistoryItem(
            Date: p.EndDate.ToString("yyyy-MM-dd"),
            Type: "PolicyEnd",
            PolicyId: p.Id,
            Provider: p.Provider,
            StartDate: p.StartDate.ToString("yyyy-MM-dd"),
            EndDate: p.EndDate.ToString("yyyy-MM-dd")
        )));

        historyItems.AddRange(claims.Select(c => new HistoryItem(
            Date: c.ClaimDate.ToString("yyyy-MM-dd"),
            Type: "Claim",
            ClaimId: c.Id,
            Description: c.Description,
            Amount: c.Amount
        )));

        var sortedHistory = historyItems.OrderBy(h => DateOnly.Parse(h.Date)).ToList();

        return new CarHistoryResponse(carId, sortedHistory);
    }
}
