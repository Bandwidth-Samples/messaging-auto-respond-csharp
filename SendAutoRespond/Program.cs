using Bandwidth.Standard.Api;
using Bandwidth.Standard.Client;
using Bandwidth.Standard.Model;
using Newtonsoft.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

string BW_USERNAME;
string BW_PASSWORD;
string BW_MESSAGING_APPLICATION_ID;
string BW_ACCOUNT_ID;
string BW_NUMBER;
string USER_NUMBER;

//Setting up environment variables
try
{
    BW_USERNAME = Environment.GetEnvironmentVariable("BW_USERNAME");
    BW_PASSWORD = Environment.GetEnvironmentVariable("BW_PASSWORD");
    BW_MESSAGING_APPLICATION_ID = Environment.GetEnvironmentVariable("BW_MESSAGING_APPLICATION_ID");
    BW_ACCOUNT_ID = Environment.GetEnvironmentVariable("BW_ACCOUNT_ID");
    BW_NUMBER = Environment.GetEnvironmentVariable("BW_NUMBER");
    USER_NUMBER = Environment.GetEnvironmentVariable("USER_NUMBER");
}
catch (Exception)
{
    Console.WriteLine("Please set the environmental variables defined in the README");
    throw;
}

Configuration configuration = new Configuration();
configuration.Username = BW_USERNAME;
configuration.Password = BW_PASSWORD;

app.MapPost("/callbacks/inbound/messaging", async (HttpContext context) =>
{
    var requestBody = new List<object>();
    using(var streamReader = new StreamReader(context.Request.Body))
    {
        var body = await streamReader.ReadToEndAsync();
        requestBody = JsonConvert.DeserializeObject<List<object>>(body);
    }

    var type = (string)((dynamic)requestBody[0]).type;
    
    if(type.Equals("message-received"))
    {
        var from = (string)((dynamic)requestBody[0]).message.from;
        var to = (string)((dynamic)requestBody[0]).message.to[0];
        var text = (string)((dynamic)requestBody[0]).message.text;
        Console.WriteLine($"Message received from '{from}' to '{to}' with text '{text}'.");

        string autoResponseMessage = autoResponse(text);

        MessageRequest request = new MessageRequest(
            applicationId: BW_MESSAGING_APPLICATION_ID,
            to: new List<string> { from },
            from: to,
            text: autoResponseMessage
        );

        MessagesApi apiInstance = new MessagesApi(configuration);
        try
        {
            // Send a message
            var result = await apiInstance.CreateMessageAsync(BW_ACCOUNT_ID, request);
            Console.WriteLine($"\nSending Auto Response\nTo: {from}\nFrom: {to}\nText: {autoResponseMessage}");
        }
        catch (ApiException e)
        {
            Console.WriteLine("Exception when calling MessagesApi.CreateMessage: " + e.Message);
        }
    }
    else
    {
        Console.WriteLine("Message type does not match endpoint. This endpoint is used for inbound messages only.");
        Console.WriteLine("Outbound message callbacks should be sent to /callbacks/outbound/messaging.");
    }
});

app.MapPost("/callbacks/outbound/messaging/status", async (HttpContext context) =>
{
    var requestBody = new List<object>();
    using(var streamReader = new StreamReader(context.Request.Body))
    {
        var body = await streamReader.ReadToEndAsync();
        requestBody = JsonConvert.DeserializeObject<List<object>>(body);
    }

    var type = (string)((dynamic)requestBody[0]).type;
    
    switch (type)
    {
        case "message-sending":
            Console.WriteLine("message-sending type is only for MMS.");
            break;
        case "message-delivered":
            Console.WriteLine("Your message has been handed off to the Bandwidth's MMSC network, but has not been confirmed at the downstream carrier.");
            break;
        case "message-failed":
            Console.WriteLine("For MMS and Group Messages, you will only receive this callback if you have enabled delivery receipts on MMS.");
            break;
        default:
            Console.WriteLine("Message type does not match endpoint. This endpoint is used for message status callbacks only.");
            break;
    }
});

String autoResponse(String inboundText)
{
    var command = inboundText.ToLower();

    Dictionary<string, string> responseMap = new Dictionary<string, string>();
    responseMap.Add("stop","STOP: OK, you'll no longer receive messages from us.");
    responseMap.Add("quit","QUIT: OK, you'll no longer receive messages from us.");
    responseMap.Add("help","Valid words are: STOP, QUIT, HELP, and INFO. Reply STOP or QUIT to opt out.");
    responseMap.Add("info","INFO: This is the test responder service. Reply STOP or QUIT to opt out.");
    responseMap.Add("default","Please respond with a valid word. Reply HELP for help.");

    var mapVal = responseMap.GetValueOrDefault(command, responseMap["default"]);

    return "[Auto Response] " + mapVal;
}

app.Run();
