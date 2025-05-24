using CommunityToolkit.Maui;
using FlowHub_MAUI.Utilities.OtherUtils;
using LiveQueryChatAppMAUI.ViewModel;
using Microsoft.Extensions.Logging;
using YBSeedrClient;
using YBSeedrClient.Abstractions;

namespace LiveQueryChatAppMAUI;
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });
        builder.Services.AddSingleton<MainPage>();
        builder.Services.AddSingleton<HomePageViewModel>();

#if DEBUG
        builder.Logging.AddDebug();
#endif


        builder.Services.AddSingleton<HttpClient>(static sp =>
        {
            var httpClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true })
            {
                BaseAddress = new Uri("https://www.seedr.cc/rest/")
            };

            string seedrEmail = APIKeys.SeedrEmail;
            string seedrPassword = APIKeys.SeedrPassword;
            if (!string.IsNullOrWhiteSpace(seedrEmail) && !string.IsNullOrWhiteSpace(seedrPassword))
            {
                var authToken = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{seedrEmail}:{seedrPassword}"));
                httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authToken);
            }
            httpClient.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            return httpClient;
        });

        builder.Services.AddSingleton<IBrowserLauncher, MauiBrowserLauncher>();

        // Register SeedrApiService
        builder.Services.AddSingleton<ISeedrApiService, SeedrApiService>();

        return builder.Build();
    }
}
