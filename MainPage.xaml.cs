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
        s.Msg = "Updated";
        Vm.SelectedMsg = s;
       await Vm.UpdateMessage();
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
     _= SetupLiveQuery();
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
        var curU = await ParseClient.Instance.CurrentUserController.GetAsync(ParseClient.Instance);
        ParseACL aCL = new();
        aCL.SetWriteAccess(curU, true);
        
        try
        {
            var query = ParseClient.Instance.GetQuery("TestChat").WhereEqualTo("ACL",curU);
            //.WhereEqualTo("IsDeleted", false);

            var sub = LiveClient!.Subscribe(query);
            LiveClient.RegisterListener(this);


            sub.HandleSubscribe(async query =>
            {
                await Shell.Current.DisplayAlert("Subscribed", $"Subscribed to query{query.GetClassName()}", "OK");
                Debug.WriteLine($"Subscribed to query: {query.GetClassName()}");
            })
            .HandleEvents((query, objEvent, obj) =>
            {
                var newComment = MainPage.MapToModelFromParseObject<TestChat>(obj);
                               
                obj.TryGetValue("objectId", out string objId);
                newComment.ObjectId = objId;

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


    [ObservableProperty]
    string? message;


    [RelayCommand]
    public async void SendMessage()
    {
        TestChat chat = new();
        chat.Msg = Message;
        chat.Username = "YBTopaz8";
        
        var pChat = MainPage.MapToParseObject(chat, "TestChat"); //here, the objectid is not set, so when it goes to server, server will think it's new, but it's not.
        
        var curU = await ParseClient.Instance.CurrentUserController.GetAsync(ParseClient.Instance);
        //ParseACL aCL = new();
        //aCL.SetWriteAccess(curU, true);
        //aCL.SetReadAccess(curU, true);
        //pChat.ACL = aCL;
        await pChat.SaveAsync();


        // Step 1: Query the existing object by a unique identifier (e.g., Username, Msg)
        var query = ParseClient.Instance.GetQuery("TestChat")
            .WhereEqualTo("ACL", curU); // Filter condition

        var existingChat = await query.FindAsync(); // Fetch the first matching object 
    }
    public async Task UpdateMessage()
    {
        try
        {
            // Step 1: Query the existing object by a unique identifier (e.g., Username, Msg)
            var query = ParseClient.Instance.GetQuery("TestChat")
                .WhereEqualTo("Username", "YBTopaz8"); // Filter condition
                

            var existingChat = await query.FirstAsync(); // Fetch the first matching object

            // Step 2: Update the properties of the fetched object
            existingChat["Msg"] = "NowYB"; // Update message or other properties as needed

            // Step 3: Save the updated object back to the server
            await existingChat.SaveAsync();

            Console.WriteLine("Message updated successfully!");
        }
        catch (ParseFailureException ex) when (ex.Code == ParseFailureException.ErrorCode.ObjectNotFound)
        {
            Console.WriteLine("No matching object found to update.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating message: {ex.Message}");
        }
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
