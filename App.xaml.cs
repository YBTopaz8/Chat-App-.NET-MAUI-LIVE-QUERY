using FlowHub_MAUI.Utilities.OtherUtils;
using Parse;
using Parse.Infrastructure;
using System.Diagnostics;

namespace LiveQueryChatAppMAUI;

public partial class App : Application
{
    public App()
    {

        InitializeComponent();
        InitializeParseClient();
    }
    private void CurrentDomain_FirstChanceException(object? sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
    {
        Debug.WriteLine($"********** UNHANDLED EXCEPTION! Details: {e.Exception} | {e.Exception.InnerException?.Message} | {e.Exception.Source} " +
            $"| {e.Exception.StackTrace} | {e.Exception.Message} || {e.Exception.Data.Values} {e.Exception.HelpLink}");

        //var home = IPlatformApplication.Current!.Services.GetService<HomePageVM>();
        //await home.ExitingApp();
    }
    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }
    public static bool InitializeParseClient()
    {
        try
        {
            // Check for internet connection
            if (Connectivity.NetworkAccess != NetworkAccess.Internet)
            {
                Console.WriteLine("No Internet Connection: Unable to initialize ParseClient.");
                return false;
            }

            // Validate API Keys
            if (string.IsNullOrEmpty(APIKeys.ApplicationId) || // PUT IN YOUR APP ID HERE
                string.IsNullOrEmpty(APIKeys.ServerUri) || // PUT IN YOUR ServerUri ID HERE
                string.IsNullOrEmpty(APIKeys.DotNetKEY)) // PUT IN YOUR DotNetKEY ID HERE
                //You can use your Master Key instead of DOTNET but beware as it is the...Master Key
            {
                Console.WriteLine("Invalid API Keys: Unable to initialize ParseClient.");
                return false;
            }

            // Create ParseClient
            ParseClient client = new ParseClient(new ServerConnectionData
            {
                ApplicationID = APIKeys.ApplicationId,
                ServerURI = APIKeys.ServerUri,
                Key = APIKeys.DotNetKEY,

            }
            );
            HostManifestData manifest = new HostManifestData()
            {
                Version = "1.0.0",
                Identifier = "com.yvanbrunel.flowhub",
                Name = "Flowhub",
            };

            client.Publicize();


            Debug.WriteLine("ParseClient initialized successfully.");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing ParseClient: {ex.Message}");
            return false;
        }
    }
}