using LegatusMobile.Services.Abstract;
using LegatusMobile.Services.Models;
using System.Net.Http.Json;

namespace LegatusMobile.Services.Concrete;

public class SmsProcessingService(
    ISmsService smsService,
    HttpClient httpClient,
    ITokenService tokenService) : ISmsProcessingService
{
    private readonly ISmsService _smsService = smsService;
    private readonly HttpClient _httpClient = httpClient;
    private readonly ITokenService _tokenService = tokenService;
    private bool _isProcessing;

    public event Action<string>? OnDeliveryFailed;

    public async Task StartProcessingAsync()
    {
        if (_isProcessing) return;

        var token = await _tokenService.GetTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            throw new InvalidOperationException("Token bulunamadı. Lütfen önce token giriniz.");
        }

        var hasPermission = await _smsService.RequestSmsPermissionAsync();
        if (!hasPermission)
        {
            throw new InvalidOperationException("SMS izinleri alınamadı.");
        }

        _smsService.OnSmsReceived += HandleSmsReceived;

        await _smsService.StartListeningAsync();

        _isProcessing = true;
    }

    public async Task StopProcessingAsync()
    {
        if (!_isProcessing) return;

        _smsService.OnSmsReceived -= HandleSmsReceived;

        await _smsService.StopListeningAsync();

        _isProcessing = false;
    }

    public (bool IsValid, int CompanyId) ValidateSender(string sender)
    {
        var match = ExclusiveSmsListeners.ValidSenders
            .FirstOrDefault(kvp => sender.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase));

        return (!string.IsNullOrEmpty(match.Key), match.Value);
    }

    public async Task ProcessSmsAsync(string sender, string message)
    {
        var (isValid, companyId) = ValidateSender(sender);
        if (!isValid || companyId == 0) return;

        var token = await _tokenService.GetTokenAsync();
        if (string.IsNullOrEmpty(token))
            return;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("ReceiveSms", new
            {
                PhoneNumber = sender,
                Message = message,
                Token = token,
                ReceivedAt = DateTime.Now,
                InsuranceCompanyGatewayId = companyId
            });

            if (!response.IsSuccessStatusCode)
                OnDeliveryFailed?.Invoke(await ReadFailureReason(response));
        }
        catch (Exception ex)
        {
            OnDeliveryFailed?.Invoke($"SMS iletilemedi: {ex.Message}");
        }
    }

    private static async Task<string> ReadFailureReason(HttpResponseMessage response)
    {
        var body = (await response.Content.ReadAsStringAsync()).Trim();

        return string.IsNullOrWhiteSpace(body)
            ? $"SMS iletilemedi (HTTP {(int)response.StatusCode})."
            : body;
    }

    private async void HandleSmsReceived(object sender, SmsEventArgs args) => await ProcessSmsAsync(args.Sender, args.Message);
}