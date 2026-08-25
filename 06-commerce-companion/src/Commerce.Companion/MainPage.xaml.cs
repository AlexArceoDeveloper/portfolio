using System.Globalization;

namespace Commerce.Companion;

public partial class MainPage : ContentPage
{
    private readonly CommerceClient _commerceClient;

    public MainPage(CommerceClient commerceClient)
    {
        InitializeComponent();
        _commerceClient = commerceClient;
    }

    private async void OnSubmitClicked(object? sender, EventArgs eventArgs)
    {
        if (!Uri.TryCreate(ApiUrlEntry.Text?.Trim(), UriKind.Absolute, out var apiBaseUri) ||
            (apiBaseUri.Scheme != Uri.UriSchemeHttps && !apiBaseUri.IsLoopback))
        {
            ResultLabel.Text = "Use an HTTPS API URL, or an HTTP URL that points to localhost.";
            return;
        }

        if (!decimal.TryParse(
                AmountEntry.Text,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var amount) ||
            amount <= 0)
        {
            ResultLabel.Text = "Enter a positive amount using a dot as the decimal separator.";
            return;
        }

        var orderId = OrderIdEntry.Text?.Trim();
        var currency = CurrencyEntry.Text?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(orderId) || currency?.Length != 3)
        {
            ResultLabel.Text = "Order ID and a three-letter currency are required.";
            return;
        }

        SubmitButton.IsEnabled = false;
        ResultLabel.Text = "Sending sandbox payment...";
        try
        {
            var result = await _commerceClient.CreatePaymentAsync(
                apiBaseUri,
                new PaymentDraft(orderId, amount, currency),
                CancellationToken.None);
            ResultLabel.Text =
                $"{result.Status} · {result.Currency} {result.Amount:0.00}\n" +
                $"Risk: {result.Risk.Decision} ({result.Risk.Score}/100)\n" +
                $"Payment: {result.PaymentId}";
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            ResultLabel.Text = $"The API could not be reached: {exception.Message}";
        }
        finally
        {
            SubmitButton.IsEnabled = true;
        }
    }
}
