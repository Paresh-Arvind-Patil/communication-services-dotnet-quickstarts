using Azure.Communication;
using Azure.Communication.CallAutomation;
using Azure.Communication.CallAutomation.Models;
using Azure.Messaging;
using CallAutomation_AppointmentReminder;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//Fetch configuration and add call automation as singleton service
var callConfigurationSection = builder.Configuration.GetSection(nameof(CallConfiguration));
builder.Services.Configure<CallConfiguration>(callConfigurationSection);
builder.Services.AddSingleton(new CallAutomationClient(pmaEndpoint: new Uri("https://x-pma-euno-01.plat.skype.com:6448/"), callConfigurationSection["ConnectionString"]));
//https://x-pma-euno-01.plat.skype.com:6448/
//https://pma-dev-papati.plat-dev.skype.net:6448/
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

    var createCallOption = new CreateCallOptions(source,
        new List<CommunicationIdentifier>() { target },
        new Uri(callConfiguration.Value.CallbackEventUri));

    var response = await callAutomationClient.CreateCallAsync(createCallOption).ConfigureAwait(false);

    logger.LogInformation($"Reponse from create call: {response.GetRawResponse()}" +
        $"CallConnection Id : {response.Value.CallConnection.CallConnectionId}");
});

/*
Scenario:
//Appointment portal
1. Recognize Choice Menu, 
    - 1 to make a appointment
            -- Recognize DTMF : Enter your phone number to confirm appointment
                    -- Play audio for confirmation of Appointment using TTS
                            --End Call on play event completed/failed
    - 2 for cancel appointment
            -- Recognize DTMF : Enter your appointment number to cancel appointment
                    -- Play audio for confirmation of Appointment using TTS
                            --End Call on play event completed/failed
    - 3 for customer support
            -- play audio for support using TTS
                    -- End Call on play event completed/failed
 */

//api to handle call back events
app.MapPost("/api/callbacks", async (CloudEvent[] cloudEvents, CallAutomationClient callAutomationClient, IOptions<CallConfiguration> callConfiguration, ILogger<Program> logger) =>
{
    foreach (var cloudEvent in cloudEvents)
    {
        //logger.LogInformation($"Event received: {JsonConvert.SerializeObject(cloudEvent)}");

        CallAutomationEventBase @event = CallAutomationEventParser.Parse(cloudEvent);
        var callConnection = callAutomationClient.GetCallConnection(@event.CallConnectionId);
        var callConnectionMedia = callConnection.GetCallMedia();

        if (@event is CallConnected)
        {
            //Initiate recognition (Choice) as call connected event is received
            logger.LogInformation($"CallConnected event received for call connection id: {@event.CallConnectionId}");
           
            var choices = new List<RecognizeChoice>
            {
                new RecognizeChoice("MakeAppointment", new List<string> { "One", "First", "Make appointment", "Appointment"})
                {
                    Tone = DtmfTone.One
                },
                new RecognizeChoice("CancelAppointment", new List<string> { "Two", "Second", "Cancel appointment", "Cancel" })
                {
                    Tone = DtmfTone.Three
                },
                new RecognizeChoice("CustomerSupport", new List<string> { "Three", "Third", "Customer Support", "Support", "Help" })
                {
                    Tone = DtmfTone.Four
                },
            };

            var recognizeOptions =
                new CallMediaRecognizeChoiceOptions(CommunicationIdentifier.FromRawId(callConfiguration.Value.TargetPhoneNumber), choices)
                {
                    InterruptPrompt = true,
                    InitialSilenceTimeout = TimeSpan.FromSeconds(5),
                    Prompt = new FileSource(new Uri(callConfiguration.Value.AppBaseUri + callConfiguration.Value.AppointmentChoiceMenu)),
                    OperationContext = "AppointmentChoiceMenu"
                };

            //Start recognition 
            await callConnectionMedia.StartRecognizingAsync(recognizeOptions);
        }
        if (@event is RecognizeCompleted { OperationContext: "AppointmentChoiceMenu" })
        {
            // Play audio once recognition is completed sucessfully
            logger.LogInformation($"RecognizeCompleted (AppointmentChoiceMenu) event received for call connection id: {@event.CallConnectionId}");
            var recognizeCompletedEvent = (RecognizeCompleted)@event;

            //Do i need CallMediaRecognitionType since I already know from the OperationContext that its Choice and not DTMF ?
            if (CallMediaRecognitionType.Choices.Equals(recognizeCompletedEvent.RecognitionType))
            {
                //Fetch Label - Identifier of the choice           
                var label = recognizeCompletedEvent.ChoiceResult.Label;
                // recognizedPhrase - Is null if choice is detected using Dtmf tone
                var recognizedPhrase = recognizeCompletedEvent.ChoiceResult?.RecognizedPhrase;
                Console.WriteLine($"Lable Recognized: {label}, and Phrased Recognized: {recognizedPhrase}");
                await Utils.RecognizeDtmfOrPlayForLabelAsync(callConnectionMedia, callConfiguration.Value, label).ConfigureAwait(false);
            }
        }
        if (@event is RecognizeFailed { OperationContext: "AppointmentChoiceMenu" })
        {
            logger.LogInformation($"RecognizeFailed event received for call connection id: {@event.CallConnectionId}");
            var recognizeFailedEvent = (RecognizeFailed)@event;
            
            // Check for time out, and then play audio message
            if (ReasonCode.RecognizeInitialSilenceTimedOut.Equals(recognizeFailedEvent.ReasonCode))
            {
                logger.LogInformation($"Recognition timed out for call connection id: {@event.CallConnectionId}");
                
                var playSource = new TextSource("Recognition failed for appointment choice, due to timeout, Disconnecting the call. Thank you for using play text to speech!")
                {
                    SourceLocale = "en-US",
                    VoiceGender = GenderType.Female,
                    VoiceName = "en-GB-OliviaNeural"
                };

                //var playSource = new FileSource(new Uri(callConfiguration.Value.AppBaseUri + callConfiguration.Value.TimedoutAudio));
                //Play audio for time out
                await callConnectionMedia.PlayToAllAsync(playSource, new PlayOptions { OperationContext = "PlayAudioCompletedEndCall", Loop = false });
            }

            //Invalid Speech detected
            if (ReasonCode.RecognizeSpeechOptionNotMatched.Equals(recognizeFailedEvent.ReasonCode))
            {
                logger.LogInformation($"Recognition speech option did not match for call connection id: {@event.CallConnectionId}");
                //var playSource = new FileSource(new Uri(callConfiguration.Value.AppBaseUri + callConfiguration.Value.SpeechOptionNotMatchedAudio));

                var playSource = new TextSource("Recognition failed for appointment choice, as speech option did not matched, Disconnecting the call. Thank you for using play text to speech!")
                {
                    SourceLocale = "en-US",
                    VoiceGender = GenderType.Female,
                    VoiceName = "en-GB-OliviaNeural"
                };

                //Play audio for time out
                await callConnectionMedia.PlayToAllAsync(playSource, new PlayOptions { OperationContext = "PlayAudioCompletedEndCall", Loop = false });
            }
        }
        if (@event is RecognizeCompleted { OperationContext: "MakeAppointment" })
        {
            logger.LogInformation($"RecognizeCompleted (MakeAppointment) event received for call connection id: {@event.CallConnectionId}");
            var recognizeCompletedEvent = (RecognizeCompleted)@event;

            // Check for time out, and then play audio message

            var playSource = new TextSource("Your appointment has been confirmed, Disconnecting the call. Thank you for using play text to speech!")
            {
                SourceLocale = "en-US",
                VoiceGender = GenderType.Female,
                VoiceName = "en-GB-OliviaNeural"
            };

            //Play audio for time out
            await callConnectionMedia.PlayToAllAsync(playSource, new PlayOptions { OperationContext = "PlayAudioCompletedEndCall", Loop = false });
             
        }
        if (@event is RecognizeFailed { OperationContext: "MakeAppointment" })
        {
            logger.LogInformation($"RecognizeFailed event received for call connection id: {@event.CallConnectionId}");
            var recognizeFailedEvent = (RecognizeFailed)@event;

            // Check for time out, and then play audio message
            if (recognizeFailedEvent.ReasonCode.Equals(ReasonCode.RecognizeInitialSilenceTimedOut))
            {
                logger.LogInformation($"Recognition timed out for call connection id: {@event.CallConnectionId}");
                //var playSource = new FileSource(new Uri(callConfiguration.Value.AppBaseUri + callConfiguration.Value.TimedoutAudio));

                var playSource = new TextSource("Recognition failed to make appointment, due to timeout, Disconnecting the call. Thank you for using play text to speech!")
                {
                    SourceLocale = "en-US",
                    VoiceGender = GenderType.Female,
                    VoiceName = "en-GB-OliviaNeural"
                };

                //Play audio for time out
                await callConnectionMedia.PlayToAllAsync(playSource, new PlayOptions { OperationContext = "PlayAudioCompletedEndCall", Loop = false });
            }
        }
        if (@event is RecognizeCompleted { OperationContext: "CancelAppointment" })
        {
            logger.LogInformation($"RecognizeCompleted (CancelAppointment) event received for call connection id: {@event.CallConnectionId}");
            var recognizeCompletedEvent = (RecognizeCompleted)@event;

            var playSource = new TextSource("Your appointment has been cancelled, Disconnecting the call. Thank you for using play text to speech!")
            {
                SourceLocale = "en-US",
                VoiceGender = GenderType.Female,
                VoiceName = "en-GB-OliviaNeural"
            };

            // Check for time out, and then play audio message
            //Play audio for time out
            await callConnectionMedia.PlayToAllAsync(playSource, new PlayOptions { OperationContext = "PlayAudioCompletedEndCall", Loop = false });


        }
        if (@event is RecognizeFailed { OperationContext: "CancelAppointment" })
        {
            logger.LogInformation($"RecognizeFailed (CancelAppointment) event received for call connection id: {@event.CallConnectionId}");
            var recognizeFailedEvent = (RecognizeFailed)@event;

            // Check for time out, and then play audio message
            if (recognizeFailedEvent.ReasonCode.Equals(ReasonCode.RecognizeInitialSilenceTimedOut))
            {
                logger.LogInformation($"Recognition timed out (CancelAppointment) for call connection id: {@event.CallConnectionId}");
                //var playSource = new FileSource(new Uri(callConfiguration.Value.AppBaseUri + callConfiguration.Value.TimedoutAudio));
                var playSource = new TextSource("Recognition failed to cancel appointment, due to timeout, Disconnecting the call. Thank you for using play text to speech!")
                {
                    SourceLocale = "en-US",
                    VoiceGender = GenderType.Female,
                    VoiceName = "en-GB-OliviaNeural"
                };
                //Play audio for time out
                await callConnectionMedia.PlayToAllAsync(playSource, new PlayOptions { OperationContext = "PlayAudioCompletedEndCall", Loop = false });
            }
        }
        if (@event is PlayCompleted { OperationContext: "PlayAudioCompletedEndCall" })
        {
            logger.LogInformation($"PlayCompleted event received for call connection id: {@event.CallConnectionId}");
            await callConnection.HangUpAsync(forEveryone: true);
        }
        if (@event is PlayFailed { OperationContext: "PlayAudioCompletedEndCall" })
        {
            logger.LogInformation($"PlayFailed event received for call connection id: {@event.CallConnectionId}");
            await callConnection.HangUpAsync(forEveryone: true);
        }
        if (@event is PlayCompleted { OperationContext: "CustomerSupport" })
        {
            logger.LogInformation($"PlayCompleted event received for call connection id: {@event.CallConnectionId}");
            await callConnection.HangUpAsync(forEveryone: true);
        }
        if (@event is PlayFailed { OperationContext: "CustomerSupport" })
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
