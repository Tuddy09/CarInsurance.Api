namespace CarInsurance.Api.Services;

public interface ITimeProvider
{
    DateTime UtcNow { get; }
}