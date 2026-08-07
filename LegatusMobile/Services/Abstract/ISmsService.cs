using LegatusMobile.Services.Models;

namespace LegatusMobile.Services.Abstract;

public interface ISmsService
{
    Task<bool> RequestSmsPermissionAsync();
    Task StartListeningAsync();
    Task StopListeningAsync();
    event EventHandler<SmsEventArgs> OnSmsReceived;
}