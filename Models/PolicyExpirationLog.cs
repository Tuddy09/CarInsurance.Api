namespace CarInsurance.Api.Models;

public class PolicyExpirationLog
{
    public long Id { get; set; }
    public long PolicyId { get; set; }
    public InsurancePolicy Policy { get; set; } = default!;
    public DateOnly ExpirationDate { get; set; }
    public DateTime ProcessedAt { get; set; }
    public string LogMessage { get; set; } = default!;
}