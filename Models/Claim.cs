namespace CarInsurance.Api.Models;

public class Claim
{
    // This needs a mention
    // There was a posible interpretation that a Claim could only exist if there was an active InsurancePolicy for the Car on the ClaimDate.
    // This should be clarified and was not mentioned in the requirements, thus not implemented.
    public long Id { get; set; }

    public long CarId { get; set; }
    public Car Car { get; set; } = default!;

    public DateOnly ClaimDate { get; set; }
    public required string Description { get; set; }
    public decimal Amount { get; set; }
}