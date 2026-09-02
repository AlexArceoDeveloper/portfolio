using FieldAssistant.Core;
using Microsoft.Extensions.Logging;

namespace FieldAssistant.Hybrid;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();
        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddSingleton(new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(20)
        });
        builder.Services.AddSingleton<AgentApiClient>();
        builder.Services.AddSingleton<PromptOutbox>();
        builder.Services.AddSingleton<MainPage>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
