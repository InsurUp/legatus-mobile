namespace LegatusMobile.Services.Abstract;

public interface ISmsProcessingService
{
    event Action<string>? OnDeliveryFailed;

    Task StartProcessingAsync();
    Task StopProcessingAsync();
    (bool IsValid, int CompanyId) ValidateSender(string sender);
    Task ProcessSmsAsync(string sender, string message);
}
