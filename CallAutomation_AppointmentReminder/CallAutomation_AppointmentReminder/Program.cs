using Azure.Communication;
using Azure.Communication.CallAutomation;
using Azure.Messaging;
using CallAutomation_AppointmentReminder;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//Fetch configuration and add call automation as singleton service
var callConfigurationSection = builder.Configuration.GetSection(nameof(CallConfiguration));
builder.Services.Configure<CallConfiguration>(callConfigurationSection);
builder.Services.AddSingleton(new CallAutomationClient(new Uri("<pma-dev-url>"), callConfigurationSection["ConnectionString"]));

var app = builder.Build();

var sourceIdentity = await app.ProvisionAzureCommunicationServicesIdentity(callConfigurationSection["ConnectionString"]);

// Api to initiate out bound call
app.MapPost("/api/call", async (CallAutomationClient callAutomationClient, IOptions<CallConfiguration> callConfiguration, ILogger<Program> logger) =>
{
    var source = new CallSource(new CommunicationUserIdentifier(sourceIdentity))
    {
        CallerId = new PhoneNumberIdentifier(callConfiguration.Value.SourcePhoneNumber)
    };
    
    var target = new PhoneNumberIdentifier(callConfiguration.Value.TargetPhoneNumber);
    //If using Acs User MRI
    //var target = new CommunicationUserIdentifier("<8:acs:..ACS User MRI..>");

    var createCallOption = new CreateCallOptions(source,
        new List<CommunicationIdentifier>() { target },
        new Uri(callConfiguration.Value.CallbackEventUri));

    createCallOption.AzureCognitiveServicesEndpointUrl = new Uri(callConfigurationSection["CognitiveServiceEndpoint"]);
    var response = await callAutomationClient.CreateCallAsync(createCallOption).ConfigureAwait(false);

    logger.LogInformation($"Reponse from create call: {response.GetRawResponse()}" +
        $"CallConnection Id : {response.Value.CallConnection.CallConnectionId}");
});

//api to handle call back events
app.MapPost("/api/callbacks", async (CloudEvent[] cloudEvents, CallAutomationClient callAutomationClient, IOptions<CallConfiguration> callConfiguration, ILogger<Program> logger) =>
{
    foreach (var cloudEvent in cloudEvents)
    {
        logger.LogInformation($"Event received: {JsonConvert.SerializeObject(cloudEvent)}");

        CallAutomationEventBase @event = CallAutomationEventParser.Parse(cloudEvent);
        var callConnection = callAutomationClient.GetCallConnection(@event.CallConnectionId);
        var callConnectionMedia = callConnection.GetCallMedia();
        if (@event is CallConnected)
        {
            
            //Initiate recognition as call connected event is received
            logger.LogInformation($"CallConnected event received for call connection id: {@event.CallConnectionId}");

            var choices = new List<RecognizeChoice>
            {
                new RecognizeChoice("Confirm", new List<string> { "Confirm", "First", "One"})
                {
                    Tone = DtmfTone.One
                },
                new RecognizeChoice("Cancel", new List<string> { "Cancel", "Second", "Two"})
                {
                    Tone = DtmfTone.Two
                }
            };

            var playSource = new TextSource("Hello, This is a reminder for your apointment at 2 PM, Say Confirm to confirm your appointment or Cancel to cancel the appointment. Thank you!");

            var recognizeOptions =
                new CallMediaRecognizeChoiceOptions(
                    targetParticipant: CommunicationIdentifier.FromRawId(callConfiguration.Value.TargetPhoneNumber), //CommunicationIdentifier.FromRawId("<8:acs:..ACS User MRI..>"),
                    recognizeChoices: choices)
                {
                    InterruptPrompt = true,
                    InitialSilenceTimeout = TimeSpan.FromSeconds(5),
                    Prompt = playSource,
                    OperationContext = "AppointmentReminderMenu"
                };

            //Start recognition 
            await callConnectionMedia.StartRecognizingAsync(recognizeOptions);
           
        }
        if (@event is RecognizeCompleted { OperationContext: "AppointmentReminderMenu" })
        {
            // Play audio once recognition is completed sucessfully
            logger.LogInformation($"RecognizeCompleted event received for call connection id: {@event.CallConnectionId}");
            var recognizeCompletedEvent = (RecognizeCompleted)@event;

            string labelDetected = null;
            string phraseDetected = null;
            switch(recognizeCompletedEvent.RecognizeResult)
            {
                case ChoiceResult choiceResult:
                    logger.LogInformation($"Choice result received for call connection id: {@event.CallConnectionId}");
                    labelDetected = choiceResult.Label;
                    phraseDetected = choiceResult.RecognizedPhrase;
                    //If choice is detected by phrase, choiceResult.RecognizedPhrase will have the phrase detected,
                    // if choice is detected using dtmf tone, phrase will be null
                    logger.LogInformation($"Phrased Detected: {phraseDetected ?? "Label detected using dtmf tone"}");
                    break;
                default:
                    logger.LogError($"Unexpected recognize event result identified for connection id: {@event.CallConnectionId}");
                    break;
            }
            
            var playSource = Utils.GetTextPromotForLable(labelDetected, callConfiguration);

            // Play text prompt for dtmf response
            await callConnectionMedia.PlayToAllAsync(playSource, new PlayOptions { OperationContext = "ResponseToChoice", Loop = false });
        }
        if (@event is RecognizeFailed { OperationContext: "AppointmentReminderMenu" })
        {
            logger.LogInformation($"RecognizeFailed event received for call connection id: {@event.CallConnectionId}");
            var recognizeFailedEvent = (RecognizeFailed)@event;

            // Check for time out, and then play audio message
            if (recognizeFailedEvent.ReasonCode.Equals(ReasonCode.RecognizeInitialSilenceTimedOut))
            {
                logger.LogInformation($"Recognition timed out for call connection id: {@event.CallConnectionId}");
                var playSource = new TextSource("No input recieved and recognition timed out, Disconnecting the call. Thank you!");

                //Play audio for time out
                await callConnectionMedia.PlayToAllAsync(playSource, new PlayOptions { OperationContext = "ResponseToChoice", Loop = false });
            }

            //Check for invalid speech option or invalid tone detection
            //TODO: Add incorrect tone detected check 
            if (recognizeFailedEvent.ReasonCode.Equals(ReasonCode.RecognizeSpeechOptionNotMatched))
            {
                logger.LogInformation($"Recognition failed for invalid speech detected, connection id: {@event.CallConnectionId}");
                var playSource = new TextSource("Invalid speech phrase detected, Disconnecting the call. Thank you!");

                //Play text prompt for speech option not matched
                await callConnectionMedia.PlayToAllAsync(playSource, new PlayOptions { OperationContext = "ResponseToChoice", Loop = false });
            }
        }
        if (@event is PlayCompleted { OperationContext: "ResponseToChoice" })
        {
            logger.LogInformation($"PlayCompleted event received for call connection id: {@event.CallConnectionId}");
            await callConnection.HangUpAsync(forEveryone: true);
        }
        if (@event is PlayFailed { OperationContext: "ResponseToChoice" })
        {
            logger.LogInformation($"PlayFailed event received for call connection id: {@event.CallConnectionId}");
            await callConnection.HangUpAsync(forEveryone: true);
        }
    }
    return Results.Ok();
}).Produces(StatusCodes.Status200OK);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
           Path.Combine(builder.Environment.ContentRootPath, "audio")),
    RequestPath = "/audio"
});

app.UseHttpsRedirection();
app.Run();
