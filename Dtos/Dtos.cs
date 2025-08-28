namespace CarInsurance.Api.Dtos;

public record CarDto(long Id, string Vin, string? Make, string? Model, int Year, long OwnerId, string OwnerName, string? OwnerEmail);
public record InsuranceValidityResponse(long CarId, string Date, bool Valid);

public record CreateClaimRequest(string ClaimDate, string Description, decimal Amount);
public record ClaimDto(long Id, long CarId, string ClaimDate, string Description, decimal Amount);

public record CarHistoryResponse(long CarId, List<HistoryItem> History);

public record HistoryItem(
    string Date, 
    string Type,
    long? PolicyId = null,
    string? Provider = null,
    string? StartDate = null,
    string? EndDate = null,
    long? ClaimId = null,
    string? Description = null,
    decimal? Amount = null
);
