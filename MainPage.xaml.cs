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

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (MsgColView is not null)
        {
            Vm.msgColView = MsgColView;
        }
    }
    private void OnCounterClicked(object sender, EventArgs e)
    {
        count++;
    }
    private async void RestoreAllData(object sender, EventArgs e)
    {
        // For TestChat
        TestChat chat1 = new TestChat { Msg = "Test Message 1" };
        TestChat chat2 = new TestChat { Msg = "Test Message 2" };
        var pchat1= ObjectMapper.ClassToDictionary(chat1);
        var pchat2= ObjectMapper.ClassToDictionary(chat2);
        List<object> testChats = new List<object> { pchat1, pchat2 };

        
        Dictionary<string, object> dataToRestore = new Dictionary<string, object>();
        dataToRestore.Add("TestChat", testChats);
        

        var Links = await ParseClient.Instance.CallCloudCodeFunctionAsync<string>("restoreAllData", dataToRestore);
        Debug.WriteLine(Links);
    }

    
    public class GameScore 
    { 
        public string PlayerName { get; set; } 
        public int Score { get; set; } 
    }
    private void OnLogOut(object sender, EventArgs e)
    {
        ParseClient.Instance.LogOut();
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
        Vm.SelectedMsg = (TestChat)mod.BindingContext;
        
        Vm.DeleteMsg();
    }

    private async void Button_Clicked_1(object sender, EventArgs e)
    {
        var mod = (Button)sender;
        var s = (TestChat)mod.BindingContext;        
       await Vm.UpdateMessage(s.Msg);
    }

}

public partial class TestChat : ObservableObject
{
    [ObservableProperty]
    string uniqueKey = Guid.NewGuid().ToString();
    [ObservableProperty]
    string? msg;
    [ObservableProperty]
    string? username;
    [ObservableProperty]
    string platform = $"{DeviceInfo.Platform.ToString()} version {DeviceInfo.VersionString}";
    [ObservableProperty]
    bool isDeleted;
}


public partial class ViewModel : ObservableObject
{

    public CollectionView msgColView;
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
        
        //signUpUser.Password = CurrentUserLocal.UserPassword;
        var usr = await ParseClient.Instance.LogInWithAsync(signUpUser.Email, signUpUser.Password!);


        if (usr is not null)
        {
            //Debug.WriteLine("Login OK");
            await Shell.Current.DisplayAlert("Login", "Login OK", "OK");
            
            var s = await ParseClient.Instance.GetCurrentUser();
            
            if (s is not null)
            {
                await ManageUserRelationsAsync();
            }

            //    Debug.WriteLine(s.Username);
            //    s.Username = "Test";
            //    await s.SaveAsync();
            //    var e = await ParseClient.Instance.CurrentUserController.GetCurrentSessionTokenAsync(ParseClient.Instance.Services);
            //    Debug.WriteLine(e);
            //    var ee = await ParseClient.Instance.GetCurrentSessionAsync();

            //    Debug.WriteLine(ee.SessionToken);
            //    Debug.WriteLine(ee.ObjectId);
            //    Debug.WriteLine(ee.Keys.FirstOrDefault());
        }
    }
    public static async Task AddRelationToUserAsync(ParseUser user, string relationField, IList<ParseObject> relatedObjects)
    {
        if (user == null)
        {
            throw new ArgumentNullException(nameof(user), "User must not be null.");
        }

        if (string.IsNullOrEmpty(relationField))
        {
            throw new ArgumentException("Relation field must not be null or empty.", nameof(relationField));
        }

        if (relatedObjects == null || relatedObjects.Count == 0)
        {
            Debug.WriteLine("No objects provided to add to the relation.");
            return;
        }

        var relation = user.GetRelation<ParseObject>(relationField);

        foreach (var obj in relatedObjects)
        {
            relation.Add(obj);
        }

        await user.SaveAsync();
        Debug.WriteLine($"Added {relatedObjects.Count} objects to the '{relationField}' relation for user '{user.Username}'.");
    }
    public static async Task UpdateUserRelationAsync(ParseUser user, string relationField, IList<ParseObject> toAdd, IList<ParseObject> toRemove)
    {
        if (user == null)
        {
            throw new ArgumentNullException(nameof(user), "User must not be null.");
        }

        if (string.IsNullOrEmpty(relationField))
        {
            throw new ArgumentException("Relation field must not be null or empty.", nameof(relationField));
        }

        var relation = user.GetRelation<ParseObject>(relationField);

        // Add objects to the relation
        if (toAdd != null && toAdd.Count > 0)
        {
            foreach (var obj in toAdd)
            {
                relation.Add(obj);
            }
            Debug.WriteLine($"Added {toAdd.Count} objects to the '{relationField}' relation.");
        }

        // Remove objects from the relation
        if (toRemove != null && toRemove.Count > 0)
        {
         
            foreach (var obj in toRemove)
            {
                relation.Remove(obj);
            }
            Debug.WriteLine($"Removed {toRemove.Count} objects from the '{relationField}' relation.");
        }

        await user.SaveAsync();
    }
    public static async Task DeleteUserRelationAsync(ParseUser user, string relationField)
    {
        if (user == null)
        {
            throw new ArgumentNullException(nameof(user), "User must not be null.");
        }

        if (string.IsNullOrEmpty(relationField))
        {
            throw new ArgumentException("Relation field must not be null or empty.", nameof(relationField));
        }

        var relation = user.GetRelation<ParseObject>(relationField);
        var relatedObjects = await relation.Query.FindAsync();


        foreach (var obj in relatedObjects)
        {
            relation.Remove(obj);
        }

        await user.SaveAsync();
        Debug.WriteLine($"Removed all objects from the '{relationField}' relation for user '{user.Username}'.");
    }
    public static async Task ManageUserRelationsAsync()
    {
        // Get the current user
        var user = await ParseClient.Instance.GetCurrentUser();

        if (user == null)
        {
            Debug.WriteLine("No user is currently logged in.");
            return;
        }

        const string relationField = "friends"; // Example relation field name

        // Create related objects to add
        var relatedObjectsToAdd = new List<ParseObject>
    {
        new ParseObject("Friend") { ["name"] = "YB" },
        new ParseObject("Friend") { ["name"] = "Topaz" }
    };

        // Save related objects to the server before adding to the relation
        foreach (var obj in relatedObjectsToAdd)
        {
            await obj.SaveAsync();
        }

        // Add objects to the relation
        await AddRelationToUserAsync(user, relationField, relatedObjectsToAdd);

        // Query the relation
        var relatedObjects = await GetUserRelationsAsync(user, relationField);

        // Update the relation (add and remove objects)
        var relatedObjectsToRemove = new List<ParseObject> { relatedObjects[0] }; // Remove the first related object
        var newObjectsToAdd = new List<ParseObject>
    {
        new ParseObject("Friend") { ["name"] = "Charlie" }
    };

        foreach (var obj in newObjectsToAdd)
        {
            await obj.SaveAsync();
        }

        await UpdateUserRelationAsync(user, relationField, newObjectsToAdd, relatedObjectsToRemove);

        // Delete the relation
        //await DeleteUserRelationAsync(user, relationField);
    }
    public static async Task<IList<ParseObject>> GetUserRelationsAsync(ParseUser user, string relationField)
    {
        if (user == null)
        {
            throw new ArgumentNullException(nameof(user), "User must not be null.");
        }

        if (string.IsNullOrEmpty(relationField))
        {
            throw new ArgumentException("Relation field must not be null or empty.", nameof(relationField));
        }

        var relation = user.GetRelation<ParseObject>(relationField);
        
        var results = await relation.Query.FindAsync();
        Debug.WriteLine($"Retrieved {results.Count()} objects from the '{relationField}' relation for user '{user.Username}'.");
        return results.ToList();
    }

    public ParseLiveQueryClient? LiveClient { get; set; }

    [ObservableProperty]
    public bool isConnected = false;
    //I Will Just leave all this code in Docs because believe it or not, sometimes even I forget how to use my own lib :D
    void SetupLiveQuery()
    {
        try
        {    
            var query = ParseClient.Instance.GetQuery("TestChat");
            var subscription = LiveClient!.Subscribe(query);

            LiveClient.ConnectIfNeeded();
            int retryDelaySeconds = 5;
            int maxRetries = 10;

            LiveClient.OnConnected
                .Do(_ => Debug.WriteLine("LiveQuery connected."))
                .RetryWhen(errors =>
                    errors
                        .Zip(Observable.Range(1, maxRetries), (error, attempt) => (error, attempt))
                        .SelectMany(tuple =>
                        {
                            if (tuple.attempt > maxRetries)
                            {
                                Debug.WriteLine($"Max retries reached. Error: {tuple.error.Message}");
                                return Observable.Throw<Exception>(tuple.error); // Explicit type here
                            }
                            IsConnected = false;
                            Debug.WriteLine($"Retry attempt {tuple.attempt} after {retryDelaySeconds} seconds...");

                            // Explicit reconnect call before retry delay
                            LiveClient.ConnectIfNeeded(); // revive app!

                            return Observable.Timer(TimeSpan.FromSeconds(retryDelaySeconds)).Select(_ => tuple.error); // Maintain compatible type
                        })
                )
                .Subscribe(
                    _ =>
                    {
                        IsConnected=true;
                        Debug.WriteLine("Reconnected successfully.");
                    },
                    ex => Debug.WriteLine($"Failed to reconnect: {ex.Message}")
                );

            LiveClient.OnError
                .Do(ex =>
                {
                    Debug.WriteLine("LQ Error: " + ex.Message);
                    LiveClient.ConnectIfNeeded();  // Ensure reconnection on errors
                })
                .OnErrorResumeNext(Observable.Empty<Exception>()) // Prevent breaking the stream
                .Subscribe();

            LiveClient.OnDisconnected
                .Do(info => Debug.WriteLine(info.userInitiated
                    ? "User disconnected."
                    : "Server disconnected."))
                .Subscribe();


            LiveClient.OnSubscribed
                .Do(e => Debug.WriteLine("Subscribed to: " + e.requestId))
                .Subscribe();

            int batchSize = 3; // Number of events to release at a time
            TimeSpan throttleTime = TimeSpan.FromMilliseconds(0000);

            LiveClient.OnObjectEvent
            .Where(e => e.subscription == subscription) // Filter relevant events
            .GroupBy(e => e.evt)
            .SelectMany(group =>
            {
                if (group.Key == Subscription.Event.Create)
                {
                    // Apply throttling only to CREATE events
                    return group.Throttle(throttleTime)
                                .Buffer(TimeSpan.FromSeconds(1), 3) // Further control
                                .SelectMany(batch => batch); // Flatten the batch
                }
                else
                {
                    //do something with group !
                    // Pass through other events without throttling
                    return group;
                }
            })
            .Subscribe(e =>
            {
                ProcessEvent(e, Messages);
            });


            // Combine other potential streams
            Observable.CombineLatest(
                LiveClient.OnConnected.Select(_ => "Connected"),
                LiveClient.OnDisconnected.Select(_ => "Disconnected"),
                (connected, disconnected) => $"Status: {connected}, {disconnected}"
            )
            .Throttle(TimeSpan.FromSeconds(1)) // Aggregate status changes
            .Subscribe(status => Debug.WriteLine(status));
        }
        catch (Exception ex)
        {
            Debug.WriteLine("SetupLiveQuery Error: " + ex.Message);
        }
    }
    void ProcessEvent((Subscription.Event evt, object objectDictionnary, Subscription subscription) e,
                  ObservableCollection<TestChat> messages)
    {
        var objData = e.objectDictionnary as Dictionary<string, object>;
        TestChat chat;

        switch (e.evt)
        {
            case Subscription.Event.Enter:
                Debug.WriteLine("Entered");
                break;

            case Subscription.Event.Leave:
                Debug.WriteLine("Left");
                break;

            case Subscription.Event.Create:
                chat = ObjectMapper.MapFromDictionary<TestChat>(objData);
                messages.Add(chat);

                MainThread
                    .BeginInvokeOnMainThread(() => msgColView.ScrollTo(chat, null, ScrollToPosition.End, true));
                
                break;

            case Subscription.Event.Update:
                chat = ObjectMapper.MapFromDictionary<TestChat>(objData);
                var obj = messages.FirstOrDefault(x => x.UniqueKey == chat.UniqueKey);

                if (obj != null)
                {
                    messages[messages.IndexOf(obj)] = chat;
                }
                break;

            case Subscription.Event.Delete:
                chat = ObjectMapper.MapFromDictionary<TestChat>(objData);
                var objToDelete = messages.FirstOrDefault(x => x.UniqueKey == chat.UniqueKey);

                if (objToDelete != null)
                {
                    messages.Remove(objToDelete);
                }
                if (messages.Count>1)
                {
                    //for some interesting reasons, if you call this when messages.count <1 it will crash/disconnect LQ subscription. (or maybe send it to another thread?)
                    MainThread
                        .BeginInvokeOnMainThread(() => msgColView.ScrollTo(messages.LastOrDefault(), null, ScrollToPosition.End, true));
                }
                break;

            default:
                Debug.WriteLine("Unhandled event type.");
                break;
        }

        Debug.WriteLine($"Processed {e.evt} on object {objData?.GetType()}");
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
        await pChat.SaveAsync();
        Message = string.Empty;
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
    private static async Task CreatePostWithComments()
    {
        // Create a new Post
        var post = new Post
        {
            Title = "Understanding Parse Relations",
            Content = "This post explains how to work with relations in Parse."
        };

        await post.SaveAsync();

        Debug.WriteLine($"Post created with ObjectId: {post.ObjectId}");

        // Create Comments
        var comment1 = new Comment
        {
            Text = "Great explanation!",
            Post = post // Set the pointer to the Post
        };

        var comment2 = new Comment
        {
            Text = "Very helpful, thanks!",
            Post = post
        };

        // Save Comments
        await comment1.SaveAsync();
        await comment2.SaveAsync();

        Debug.WriteLine($"Comments created with ObjectIds: {comment1.ObjectId}, {comment2.ObjectId}");

        // Add Comments to Post's relation
        post.Comments.Add(comment1);
        post.Comments.Add(comment2);

        await post.SaveAsync();

        Debug.WriteLine("Comments added to the Post's relation.");
    }

    private static async Task QueryCommentsForPost(ParseClient client, string postId)
    {
        try
        {
            if (client == null)
            {
                Debug.WriteLine("ParseClient is null.");
                return;
            }

            // Create a query for Post with the specified ObjectId
            var queryPost = client.GetQuery<Post>().WhereEqualTo("objectId", postId);

            // Retrieve the first (and only) Post matching the query
            var post = await queryPost.FirstAsync();

            if (post == null)
            {
                Debug.WriteLine("Post not found.");
                return;
            }

            // Get the relation to Comments
            var relation = post.Comments;
            
            // Query related Comments
            var comments = await relation.Query.FindAsync();

            Debug.WriteLine($"Post '{post.Title}' has {comments.Count()} comments:");

            foreach (var comment in comments)
            {
                Debug.WriteLine($"- {comment.Text} (ObjectId: {comment.ObjectId})");
            }
            var e = relation.Query.WhereEqualTo("Text", "Great explanation!");
        }
        catch (ParseFailureException ex)
        {
            Debug.WriteLine($"Parse Exception: {ex.Message}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"General Exception: {ex.Message}");
        }
    }

    private static async Task UpdatePostRelations(ParseClient client, string postId, string commentIdToAdd, string commentIdToRemove)
    {
        try
        {
            if (client == null)
            {
                Debug.WriteLine("ParseClient is null.");
                return;
            }

            // Fetch the Post
            var post = await client.GetQuery<Post>().WhereEqualTo("objectId", postId).FirstAsync();

            if (post == null)
            {
                Debug.WriteLine("Post not found.");
                return;
            }

            // Add a new Comment to the relation
            if (!string.IsNullOrEmpty(commentIdToAdd))
            {
                var newComment = await client.GetQuery<Comment>().WhereEqualTo("objectId", commentIdToAdd).FirstAsync();
                if (newComment == null)
                {
                    // If the Comment doesn't exist, create it
                    newComment = new Comment
                    {
                        Text = "Another insightful comment!",
                        Post = post
                    };
                    await newComment.SaveAsync();
                    Debug.WriteLine($"New Comment created with ObjectId: {newComment.ObjectId}");
                }

                post.Comments.Add(newComment);
                await post.SaveAsync();

                Debug.WriteLine($"Added Comment '{newComment.Text}' to Post.");
            }

            // Remove an existing Comment from the relation
            if (!string.IsNullOrEmpty(commentIdToRemove))
            {
                var commentToRemove = await client.GetQuery<Comment>().WhereEqualTo("objectId", commentIdToRemove).FirstAsync();

                if (commentToRemove != null)
                {
                    post.Comments.Remove(commentToRemove);
                    await post.SaveAsync();

                    Debug.WriteLine($"Removed Comment '{commentToRemove.Text}' from Post.");
                }
                else
                {
                    Debug.WriteLine("Comment to remove not found.");
                }
            }
        }
        catch (ParseFailureException ex)
        {
            Debug.WriteLine($"Parse Exception: {ex.Message}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"General Exception: {ex.Message}");
        }
    }

    private static async Task DeleteComment(ParseClient client, string commentId)
    {
        try
        {
            if (client == null)
            {
                Debug.WriteLine("ParseClient is null.");
                return;
            }

            var comment = await client.GetQuery<Comment>().WhereEqualTo("objectId", commentId).FirstAsync();

            if (comment != null)
            {
                await comment.DeleteAsync();
                Debug.WriteLine($"Comment '{comment.Text}' deleted.");
            }
            else
            {
                Debug.WriteLine("Comment not found.");
            }
        }
        catch (ParseFailureException ex)
        {
            Debug.WriteLine($"Parse Exception: {ex.Message}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"General Exception: {ex.Message}");
        }
    }
    
}



[ParseClassName("Post")]
public class Post : ParseObject
{
    public string Title
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    public string Content
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    // Define the relation to Comments
    public ParseRelation<Comment> Comments
    {
        get => GetRelation<Comment>("comments");
    }
}
[ParseClassName("Comment")]
public class Comment : ParseObject
{
    public string Text
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    // Pointer to the Post
    public ParseObject Post
    {
        get => GetProperty<ParseObject>();
        set => SetProperty(value);
    }
}