using Azure.Communication;
using Azure.Communication.CallAutomation;
using Microsoft.Extensions.Options;

namespace CallAutomation_AppointmentReminder
{
    public static class Utils
    {
        public static async Task<Azure.Response> RecognizeDtmfOrPlayForLabelAsync(CallMedia callconnectionMedia, CallConfiguration callConfiguration, string label)
        { 
            if (string.Equals(label, "MakeAppointment", StringComparison.OrdinalIgnoreCase))
            {
                // DTMF for entering phone number to reserve appointment followed by #
                return await RecognizeDtmfAsync(callconnectionMedia, label, callConfiguration);
            }
            if (string.Equals(label, "CancelAppointment", StringComparison.OrdinalIgnoreCase))
            {
                // DTMF for entering 10 digit appointment number followed by #
                return await RecognizeDtmfAsync(callconnectionMedia, label, callConfiguration);
            }
            if (string.Equals(label, "CustomerSupport", StringComparison.OrdinalIgnoreCase))
            {
                // Play audio (Using TTS): You have reached customer support, Thank you!
                return await PlayAudioAsync(callconnectionMedia, label, callConfiguration);
            }

            throw new Exception("Invalid or no label detected!!");
        }

        private static async Task<Azure.Response> RecognizeDtmfAsync(CallMedia callconnectionMedia, string label, CallConfiguration callConfiguration)
        {
            var recognizeOptions =
                new CallMediaRecognizeDtmfOptions(CommunicationIdentifier.FromRawId(callConfiguration.TargetPhoneNumber), maxTonesToCollect: 10)
                {
                    InterruptPrompt = true,
                    InitialSilenceTimeout = TimeSpan.FromSeconds(5),
                    Prompt = GetAudioForlabel(label, callConfiguration),
                    OperationContext = label,
                    StopTones = new List<DtmfTone> { DtmfTone.Pound }
                };

            return await callconnectionMedia.StartRecognizingAsync(recognizeOptions);
        }

        private static async Task<Azure.Response> PlayAudioAsync(CallMedia callconnectionMedia, string label, CallConfiguration callConfiguration)
        {
            return await callconnectionMedia.PlayToAllAsync(GetAudioForlabel(label, callConfiguration), new PlayOptions { OperationContext = label, Loop = false });
        }

        private static PlaySource GetAudioForlabel(string label, CallConfiguration callConfiguration)
        {
            FileSource playSource;

            if (string.Equals(label, "MakeAppointment", StringComparison.OrdinalIgnoreCase))
            {
                    return new FileSource(new Uri(callConfiguration.AppBaseUri + callConfiguration.MakeAppointmentAudio));
            }
            if (string.Equals(label, "CancelAppointment", StringComparison.OrdinalIgnoreCase))
            {
                return new FileSource(new Uri(callConfiguration.AppBaseUri + callConfiguration.CancelAppointmentAudio));
            }
            if (string.Equals(label, "CustomerSupport", StringComparison.OrdinalIgnoreCase))
            {
                return new FileSource(new Uri(callConfiguration.AppBaseUri + callConfiguration.CustomerSupportAudio));
            }

            throw new Exception($"Invalid label detected!!");
        }
    }
}
