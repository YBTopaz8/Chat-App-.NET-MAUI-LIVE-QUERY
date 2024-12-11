using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using Parse.LiveQuery;
using Parse;
using Parse.Abstractions.Platform.Objects;
using System.Reactive.Linq;
namespace LiveQueryChatAppMAUI;

public partial class MainPage : ContentPage
{
    int count = 0;

    public ViewModel Vm { get; }

    public MainPage(ViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        Vm = vm;
    }
    
    private void OnCounterClicked(object sender, EventArgs e)
    {
        count++;
    }


    public static T MapToModelFromParseObject<T>(IObjectState objState) where T : new()
    {
        var model = new T();
        var properties = typeof(T).GetProperties();

        foreach (var property in properties)
        {
            try
            {

                // Check if the ParseObject contains the property name
                if (objState.ContainsKey(property.Name))
                {
                    var value = objState[property.Name];
                    
                    if (value != null)
                    {
                        // Handle special types like DateTimeOffset
                        if (property.PropertyType == typeof(DateTimeOffset) && value is DateTime dateTime)
                        {
                            property.SetValue(model, new DateTimeOffset(dateTime));
                            continue;
                        }

                        // Handle string as string
                        if (property.PropertyType == typeof(string) && value is string objectIdStr)
                        {
                            property.SetValue(model, new string(objectIdStr));
                            continue;
                        }
                        if (property.PropertyType == typeof(string) && property.Name.Equals("objectId)"))
                        {
                            property.SetValue(model,value.ToString());
                        }

                        if (property.CanWrite && property.PropertyType.IsAssignableFrom(value.GetType()))
                        {
                            property.SetValue(model, value);
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                // Log and skip the property
                Debug.WriteLine($"Error mapping property '{property.Name}': {ex.Message}");
            }
        }

        return model;
    }


    public static ParseObject MapToParseObject<T>(T model, string className)
    {
        var parseObject = new ParseObject(className);

        // Get the properties of the class
        var properties = typeof(T).GetProperties();

        foreach (var property in properties)
        {
            try
            {
                var value = property.GetValue(model);
                if (value != null)
                {
                    // Handle special types like DateTimeOffset
                    if (property.PropertyType == typeof(DateTimeOffset))
                    {
                        var val = (DateTimeOffset?)value;
                        if (val is not null)
                        {
                            parseObject[property.Name] = val.Value.Date;
                        }
                        continue;
                    }

                    // Handle string as string (required for Parse compatibility)
                    if (property.PropertyType == typeof(string))
                    {
                        if(value != null && !string.IsNullOrEmpty(value.ToString()))
                        {
                            parseObject[property.Name] = value.ToString();
                            continue;
                        }
                    }

                    // Add a fallback check for unsupported complex types
                    if (value!.GetType().Namespace?.StartsWith("Realms") == true)
                    {
                        Debug.WriteLine($"Skipped unsupported Realm type: {property.Name}");
                        continue;
                    }

                    // For other types, directly set the value
                    parseObject[property.Name] = value;
                }
            }
            catch (Exception ex)
            {
                // Log the exception for this particular property, but continue with the next one
                Debug.WriteLine($"Error when mapping property '{property.Name}': {ex.Message}");
            }
        }

        return parseObject;
    }

    private void Button_Clicked(object sender, EventArgs e)
    {
        var mod = (Button)sender;
        var s = (TestChat)mod.BindingContext;
        Vm.SelectedMsg = s;
        Vm.DeleteMsg();
    }

    private void Button_Clicked_1(object sender, EventArgs e)
    {
        var mod = (Button)sender;
        var s = (TestChat)mod.BindingContext;
        s.Msg = "Updated";
        Vm.SelectedMsg = s;
        Vm.SendMessage();
    }
}

public partial class ViewModel : ObservableObject, IParseLiveQueryClientCallbacks
{
    [ObservableProperty]
    ObservableCollection<TestChat> messages=new();
    [ObservableProperty]
    string? username;
    [ObservableProperty]
    TestChat selectedMsg =new();

    public ViewModel()
    {

    }
    [RelayCommand]
    async Task SetupLiveQueries()
    {
        LiveClient = new ParseLiveQueryClient();
        SetupLiveQuery();
    }
    [RelayCommand]
    public async Task LoginUser()
    {
        
       
        ParseUser signUpUser = new ParseUser();
        //signUpUser.Username = CurrentUserLocal.UserName;
        signUpUser.Email = "8brunel@gmail.com";
        signUpUser.Username = "YBTopaz8";
        signUpUser.Password = "Yvan";
        //signUpUser.Password = CurrentUserLocal.UserPassword;
        var usr = await ParseClient.Instance.LogInAsync(signUpUser.Email, signUpUser.Password!);
        if (usr is not null)
        {
            //Debug.WriteLine("Login OK");
            await Shell.Current.DisplayAlert("Login", "Login OK", "OK");
            var e = await ParseClient.Instance.CurrentUserController.GetCurrentSessionTokenAsync(ParseClient.Instance.Services);
            Debug.WriteLine(e);
            var ee = await ParseClient.Instance.GetCurrentSessionAsync();

            Debug.WriteLine(ee.SessionToken);
            Debug.WriteLine(ee.ObjectId);
            Debug.WriteLine(ee.Keys.FirstOrDefault());
        }
    }
    public void OnLiveQueryClientConnected(ParseLiveQueryClient client)
    {
        Debug.WriteLine("Client Connected");
    }

    public void OnLiveQueryClientDisconnected(ParseLiveQueryClient client, bool userInitiated)
    {
        Debug.WriteLine("Client Disconnected");
    }

    public void OnLiveQueryError(ParseLiveQueryClient client, LiveQueryException reason)
    {
        Debug.WriteLine("Error " + reason.Message);
    }

    public void OnSocketError(ParseLiveQueryClient client, Exception reason)
    {
        Debug.WriteLine("Socket Error ");
    }
    public ParseLiveQueryClient? LiveClient { get; set; }

    void SetupLiveQuery()
    {
        try
        {
            var songsQuery = ParseClient.Instance.GetQuery("TestChat"); // Specify the generic type here

            // Explicitly specify the type argument for Subscribe
            var subscription = LiveClient.Subscribe(songsQuery);
            
            // Connect to the LiveQuery server
            LiveClient.ConnectIfNeeded();
            LiveClient.OnConnected
            .Subscribe(client => Debug.WriteLine("LiveQuery connected."));

            LiveClient.OnDisconnected
                .Subscribe(info => Debug.WriteLine(info.userInitiated
                    ? "LiveQuery disconnected by user."
                    : "LiveQuery disconnected by server."));

            LiveClient.OnError
                .Subscribe(ex => Debug.WriteLine($"Error in LiveQuery: {ex.Message}"));

            LiveClient.OnSubscribed
                .Subscribe(e => Debug.WriteLine($"Subscribed to query: {e.requestId}"));

            // Listen to object events (create, update, delete)
            LiveClient.OnObjectEvent
                .Where(e => e.subscription == subscription)
                .Subscribe(e =>
                {
                    var evtType = e.evt;
                                        
                    TestChat? newComment = MainPage.MapToModelFromParseObject<TestChat>(e.objState);
                    newComment.ObjectId = e.objState.ObjectId;
                    switch (evtType)
                    {
                        case Subscription.Event.Create:
                            Messages?.Add(newComment);
                            break;
                        case Subscription.Event.Update:
                            var existing = Messages?.FirstOrDefault(m => m.ObjectId == newComment.ObjectId);
                            if (existing is not null)
                            {
                                Messages.Remove(existing);
                                Messages.Add(newComment);
                            }
                            break;
                        case Subscription.Event.Delete:
                            var toDelete = Messages?.FirstOrDefault(m => m.ObjectId == newComment.ObjectId);
                            if (toDelete is not null)
                            {
                                Messages.Remove(toDelete);
                            }
                            break;
                    }

                    Debug.WriteLine($"Event {evtType} occurred for object: {newComment.ObjectId}");
                });

            // Handle errors from the WebSocket
            LiveClient.OnError
                .Subscribe(ex =>
                {
                    Debug.WriteLine($"Error in LiveQuery WebSocket: {ex.Message}");
                });

        }
        catch (IOException ex)
        {
            Console.WriteLine($"Connection error: {ex.Message}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SetupLiveQuery encountered an error: {ex.Message}");
        }
    }

    [ObservableProperty]
    string? message;


    [RelayCommand]
    public void SendMessage()
    {
        TestChat chat = new();
        chat.Msg = Message;
        chat.Username = "YBTopaz8";
        
        var pChat = MainPage.MapToParseObject(chat, "TestChat");
        if (string.IsNullOrEmpty(pChat.ObjectId))
        {
            if (!string.IsNullOrEmpty(SelectedMsg.ObjectId))
            {
                pChat.Set("objectId", SelectedMsg.ObjectId);
                pChat.Set("ObjectId", SelectedMsg.ObjectId);
            }
        }

        _ =pChat.SaveAsync();
        
    }

    [RelayCommand]
    public void DeleteMsg()
    {
        SelectedMsg.IsDeleted = true;
        var pChat = MainPage.MapToParseObject(SelectedMsg, "TestChat");
        pChat.ObjectId = SelectedMsg.ObjectId;
        _ = pChat.DeleteAsync();
    }
}
public partial class TestChat : ObservableObject
{
    [ObservableProperty]
    string objectId=string.Empty;
    [ObservableProperty]
    string? msg;
    [ObservableProperty]
    string? username;
    [ObservableProperty]
    string platform = $"{DeviceInfo.Platform.ToString()} version {DeviceInfo.VersionString}";
    [ObservableProperty]
    bool isDeleted;
}
