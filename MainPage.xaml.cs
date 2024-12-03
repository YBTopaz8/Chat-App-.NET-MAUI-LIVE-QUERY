using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Parse;
using Parse.Abstractions.Internal;
using Parse.LiveQuery;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;

namespace LiveQueryChatAppMAUI;

public partial class MainPage : ContentPage
{
    int count = 0;


    public MainPage(ViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        
    }
    
    private void OnCounterClicked(object sender, EventArgs e)
    {
        count++;
    }


    public static T MapToModelFromParseObject<T>(ParseObject parseObject) where T : new()
    {
        var model = new T();
        var properties = typeof(T).GetProperties();

        foreach (var property in properties)
        {
            try
            {

                // Check if the ParseObject contains the property name
                if (parseObject.ContainsKey(property.Name))
                {
                    var value = parseObject[property.Name];

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
    //[RelayCommand]
    //public void QuickSignUp()
    //{
    //    ParseUser signUpUser = new ParseUser();
    //    //signUpUser.Username = CurrentUserLocal.UserName;
    //    signUpUser.Email = "me@me.com";
    //    signUpUser.Username = "YBTopaz8";
    //    signUpUser.Password = "Yvan";
    //    ParseClient.Instance.SignUpAsync(signUpUser);
    //}
    [RelayCommand]
    async Task SetupLiveQueries()
    {
        LiveClient = new ParseLiveQueryClient();
        await  SetupLiveQuery();
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

    
    async Task SetupLiveQuery()
    {
        try
        {
            var query = ParseClient.Instance.GetQuery("TestChat");
            //.WhereEqualTo("IsDeleted", false);

            var sub = LiveClient!.Subscribe(query);
            LiveClient.RegisterListener(this);


            sub.HandleSubscribe(async query =>
            {
                await Shell.Current.DisplayAlert("Subscribed", "Subscribed to query", "OK");
                Debug.WriteLine($"Subscribed to query: {query.GetClassName()}");
            })
            .HandleEvents((query, objEvent, obj) =>
            {
                var newComment = MainPage.MapToModelFromParseObject<TestChat>(obj);
                if (objEvent == Subscription.Event.Create)
                {
                    Messages?.Add(newComment);
                }
                else if (objEvent == Subscription.Event.Update)
                {
                    var f = Messages.FirstOrDefault(newComment);
                    if (f is not null)
                    {
                        Messages.Remove(f);
                        Messages.Add(newComment);
                    }
                }
                else if (objEvent == Subscription.Event.Delete)
                {
                    var f = Messages.FirstOrDefault(newComment);
                    if (f is not null)
                    {
                        Messages.Remove(f);
                    }
                }
                Debug.WriteLine($"Event {objEvent} occurred for object: {obj.ObjectId}");
            })
            .HandleError((query, exception) =>
            {
                Debug.WriteLine($"Error in query for class {query.GetClassName()}: {exception.Message}");
            })
            .HandleUnsubscribe(query =>
            {
                Debug.WriteLine($"Unsubscribed from query: {query.GetClassName()}");
            });

            // Connect asynchronously
            await Task.Run(() => LiveClient.ConnectIfNeeded());
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

    [RelayCommand]
    public void SendMessage()
    {
        TestChat chat = new();
        chat.Msg = SelectedMsg.Msg;
        chat.Username = "YBTopaz8";
        
        var pChat = MainPage.MapToParseObject(chat, "TestChat");
        _=pChat.SaveAsync();
        
    }

    [RelayCommand]
    public void DeleteMsg()
    {
        SelectedMsg.IsDeleted = true;
        var pChat = MainPage.MapToParseObject(SelectedMsg, "TestChat");
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
