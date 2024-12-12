using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using Parse.LiveQuery;
using Parse;
using Parse.Abstractions.Platform.Objects;
using Parse.Abstractions.Internal;
using Parse.Infrastructure;
using System.Reactive.Linq;
using System.Reflection;

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

    private void Button_Clicked(object sender, EventArgs e)
    {
        var mod = (Button)sender;
        var s = (TestChat)mod.BindingContext;
        Vm.SelectedMsg = s;
        Vm.DeleteMsg();
    }

    private async void Button_Clicked_1(object sender, EventArgs e)
    {
        var mod = (Button)sender;
        var s = (TestChat)mod.BindingContext;        
       await Vm.UpdateMessage(s.UniqueKey);
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
    void SetupLiveQueries()
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
            var query = ParseClient.Instance.GetQuery("TestChat");
            var subscription = LiveClient.Subscribe(query);

            LiveClient.ConnectIfNeeded();

            // Rx event streams
            LiveClient.OnConnected
                .Subscribe(_ => Debug.WriteLine("LiveQuery connected."));
            LiveClient.OnDisconnected
                .Subscribe(info => Debug.WriteLine(info.userInitiated
                    ? "User disconnected."
                    : "Server disconnected."));
            LiveClient.OnError
                .Subscribe(ex => Debug.WriteLine("LQ Error: " + ex.Message));
            LiveClient.OnSubscribed
                .Subscribe(e => Debug.WriteLine("Subscribed to: " + e.requestId));

            // Handle object events (Create/Update/Delete)
            LiveClient.OnObjectEvent
                .Where(e => e.subscription == subscription)
                
                .Subscribe(e =>
                {
                    Debug.WriteLine($"Message before {Message?.Length}");
                    TestChat chat = new();
                    var objData = (e.objectDictionnary as Dictionary<string, object>);
                    
                    switch (e.evt)
                    {
                        
                        case Subscription.Event.Enter:
                            Debug.WriteLine("entered");
                            break;
                        case Subscription.Event.Leave:
                            Debug.WriteLine("Left");
                            break;
                            case Subscription.Event.Create:
                            chat = ObjectMapper.MapFromDictionary<TestChat>(objData);
                            Messages.Add(chat);
                            break;
                            case Subscription.Event.Update:
                            chat = ObjectMapper.MapFromDictionary<TestChat>(objData);
                            var obj = Messages.FirstOrDefault(x => x.UniqueKey == chat.UniqueKey);
                            
                            Messages.RemoveAt(Messages.IndexOf(obj));
                            Messages.Add(chat);

                            break;
                            case Subscription.Event.Delete:
                            chat = ObjectMapper.MapFromDictionary<TestChat>(objData);
                            var objj = Messages.FirstOrDefault(x => x.UniqueKey == chat.UniqueKey);

                            Messages.RemoveAt(Messages.IndexOf(objj));
                            break;
                        default:
                            break;
                    }
                    Debug.WriteLine($"Message after {Message.Length}");
                    Debug.WriteLine($"Event {e.evt} on object {e.objectDictionnary.GetType()}");
                });

        }
        catch (Exception ex)
        {
            Debug.WriteLine("SetupLiveQuery Error: " + ex.Message);
        }
    }

    [ObservableProperty]
    string? message;


    [RelayCommand]
    public async Task SendMessage()
    {
        TestChat chat = new();
        chat.Msg = Message;
        chat.Username = "YBTopaz8";
        var pChat = new ParseObject("TestChat");
        pChat["Msg"] = chat.Msg;
        pChat["Username"] = chat.Username;
        pChat["Platform"] = chat.Platform;
        pChat["UniqueKey"] = chat.UniqueKey;
        ParseACL acl = new();
        acl.SetReadAccess(ParseClient.Instance.GetCurrentUser(), true); 
        acl.SetWriteAccess(ParseClient.Instance.GetCurrentUser(), true);
        await pChat.SaveAsync();

    }
    public async Task UpdateMessage(string key)
    {
        try
        {
            // Step 1: Query the existing object by a unique identifier (e.g., Username, Msg)
            var query = ParseClient.Instance.GetQuery("TestChat")
                .WhereEqualTo("UniqueKey",key ); // Filter condition
                

            var existingChat = await query.FirstAsync(); // Fetch the first matching object

            // Step 2: Update the properties of the fetched object
            existingChat["Msg"] = "NowYB"; // Update message or other properties as needed

            
            // Step 3: Save the updated object back to the server
            await existingChat.SaveAsync();

            Debug.WriteLine("Message updated successfully!");
        }
        catch (ParseFailureException ex) when (ex.Code == ParseFailureException.ErrorCode.ObjectNotFound)
        {
            Debug.WriteLine("No matching object found to update.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error updating message: {ex.Message}");
        }
    }

    [RelayCommand]
    public async void DeleteMsg()
    {
        SelectedMsg.IsDeleted = true;
        // Step 1: Query the existing object by a unique identifier (e.g., Username, Msg)
        var query = ParseClient.Instance.GetQuery("TestChat")
            .WhereEqualTo("UniqueKey", SelectedMsg.UniqueKey); // Filter condition 
        var pChat = await query.FirstOrDefaultAsync();
        if (pChat is not null)
        {
            _ = pChat.DeleteAsync();
        }

        //Heads up. handling cross device deletion is tricky. Make sure BOTH devices produce the same UniqueKey (or similar) and that both will that data when they create/update.
    }

}
public partial class TestChat : ObservableObject
{
    [ObservableProperty]
    string uniqueKey=Guid.NewGuid().ToString();
    [ObservableProperty]
    string? msg;
    [ObservableProperty]
    string? username;
    [ObservableProperty]
    string platform = $"{DeviceInfo.Platform.ToString()} version {DeviceInfo.VersionString}";
    [ObservableProperty]
    bool isDeleted;
}



public static class ObjectMapper
{
    /// <summary>
    /// Maps values from a dictionary to an instance of type T.
    /// Logs any keys that don't match properties in T.
    ///     
    /// Helper to Map from Parse Dictionnary Response to Model
    /// Example usage TestChat chat = ObjectMapper.MapFromDictionary<TestChat>(objData);    
    /// </summary>
    public static T MapFromDictionary<T>(IDictionary<string, object> source) where T : new()
    {
        // Create an instance of T
        T target = new T();

        // Get all writable properties of T
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

        // Track unmatched keys
        List<string> unmatchedKeys = new();

        foreach (var kvp in source)
        {
            if (properties.TryGetValue(kvp.Key, out var property))
            {
                try
                {
                    // Convert and assign the value to the property
                    if (kvp.Value != null && property.PropertyType.IsAssignableFrom(kvp.Value.GetType()))
                    {
                        property.SetValue(target, kvp.Value);
                    }
                    else if (kvp.Value != null)
                    {
                        // Attempt conversion for non-directly assignable types
                        var convertedValue = Convert.ChangeType(kvp.Value, property.PropertyType);
                        property.SetValue(target, convertedValue);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to set property {property.Name}: {ex.Message}");
                }
            }
            else
            {
                // Log unmatched keys
                unmatchedKeys.Add(kvp.Key);
            }
        }

        // Log keys that don't match
        if (unmatchedKeys.Count > 0)
        {
            Debug.WriteLine("Unmatched Keys:");
            foreach (var key in unmatchedKeys)
            {
                Debug.WriteLine($"- {key}");
            }
        }

        return target;
    }
}
