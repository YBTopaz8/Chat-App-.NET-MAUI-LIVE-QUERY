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
        ParseClient.Instance.RegisterSubclass(typeof(TestChat));
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

           

            // Create ParseClient
            ParseClient client = new ParseClient(new ServerConnectionData
            {
                ApplicationID = "ZPcqJVoyIqDYODqknB8KR3cgffA4zx67LfXtB85v",
                ServerURI = "https://flowhub.b4a.io",
                Key = "IuUtTIslbe94qp7bbPZ7DvTJC0MQMxHid3hJWeCb",

            }
            );
            //HostManifestData manifest = new HostManifestData()
            //{
            //    Version = "1.0.0",
            //    Identifier = "com.yvanbrunel.flowhub",
            //    Name = "Flowhub",
            //};

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