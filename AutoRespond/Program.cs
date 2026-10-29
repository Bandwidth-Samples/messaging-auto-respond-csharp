using Bandwidth.Standard.Api;
using Bandwidth.Standard.Client;
using Bandwidth.Standard.Model;
using Newtonsoft.Json;

//Setting up environment variables
string? BW_ACCOUNT_ID = Environment.GetEnvironmentVariable("BW_ACCOUNT_ID");
string? BW_CLIENT_ID = Environment.GetEnvironmentVariable("BW_CLIENT_ID");
string? BW_CLIENT_SECRET = Environment.GetEnvironmentVariable("BW_CLIENT_SECRET");
string? BW_MESSAGING_APPLICATION_ID = Environment.GetEnvironmentVariable("BW_MESSAGING_APPLICATION_ID");
string? BW_NUMBER = Environment.GetEnvironmentVariable("BW_NUMBER");
string? LOCAL_PORT = Environment.GetEnvironmentVariable("LOCAL_PORT");

if (BW_ACCOUNT_ID == null || BW_CLIENT_ID == null || BW_CLIENT_SECRET == null || BW_MESSAGING_APPLICATION_ID == null
    || BW_NUMBER == null || !int.TryParse(LOCAL_PORT, out int port))
{
    Console.WriteLine("Please set the environmental variables defined in the README");
    Environment.Exit(1);
    return;
}

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://localhost:{port}");
var app = builder.Build();

Configuration configuration = new Configuration();
configuration.OAuthClientId = BW_CLIENT_ID;
configuration.OAuthClientSecret = BW_CLIENT_SECRET;

var statusMessages = new Dictionary<StatusCallbackTypeEnum, string>
{
    [StatusCallbackTypeEnum.Sending] = "message-sending type is only for MMS.",
    [StatusCallbackTypeEnum.Delivered] = "Your message has been handed off to the Bandwidth's MMSC network, but has not been confirmed at the downstream carrier.",
    [StatusCallbackTypeEnum.Failed] = "For MMS and Group Messages, you will only receive this callback if you have enabled delivery receipts on MMS."
};

var replies = new Dictionary<string, string>
{
    ["stop"] = "STOP: OK, you'll no longer receive messages from us.",
    ["quit"] = "QUIT: OK, you'll no longer receive messages from us.",
    ["help"] = "Valid words are: STOP, QUIT, HELP, and INFO. Reply STOP or QUIT to opt out.",
    ["info"] = "INFO: This is the test responder service. Reply STOP or QUIT to opt out."
};
const string DEFAULT_REPLY = "Please respond with a valid word. Reply HELP for help.";

async Task<T?> ReadBody<T>(HttpContext context)
{
    try
    {
        using var streamReader = new StreamReader(context.Request.Body);
        return JsonConvert.DeserializeObject<T>(await streamReader.ReadToEndAsync());
    }
    catch (JsonException)
    {
        return default;
    }
}

string AutoReply(string? text)
{
    return replies.GetValueOrDefault((text ?? "").Trim().ToLower(), DEFAULT_REPLY);
}

async Task SendReply(string to, string text)
{
    MessageRequest messageRequest = new MessageRequest(
        applicationId: BW_MESSAGING_APPLICATION_ID,
        to: new List<string> { to },
        from: BW_NUMBER,
        text: text
    );

    try
    {
        MessagesApi apiInstance = new MessagesApi(configuration);
        Message message = await apiInstance.CreateMessageAsync(BW_ACCOUNT_ID, messageRequest);
        Console.WriteLine($"Sent reply {message.Id}\nTo: {to}\nText: {text}");
    }
    catch (Exception e)
    {
        Console.WriteLine($"Failed to send reply: {e.Message}");
    }
}

app.MapPost("/callbacks/outbound/messaging/status", async (HttpContext context) =>
{
    var callbacks = await ReadBody<List<StatusCallback>>(context);
    if (callbacks == null || !callbacks.All(callback => statusMessages.ContainsKey(callback.Type)))
    {
        return Results.BadRequest();
    }

    foreach (var callback in callbacks)
    {
        Console.WriteLine(statusMessages[callback.Type]);
    }

    return Results.NoContent();
});

app.MapPost("/callbacks/inbound/messaging", async (HttpContext context) =>
{
    var callbacks = await ReadBody<List<InboundCallback>>(context);
    if (callbacks == null || !callbacks.All(callback => callback.Type == InboundCallbackTypeEnum.MessageReceived))
    {
        return Results.BadRequest();
    }

    foreach (var callback in callbacks)
    {
        var message = callback.Message;
        Console.WriteLine($"{callback.Description}\nTo: {message.To[0]}\nFrom: {message.From}\nText: {message.Text}");
        await SendReply(message.From, AutoReply(message.Text));
    }

    return Results.NoContent();
});

app.Run();
