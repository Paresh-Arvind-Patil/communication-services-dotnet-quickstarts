using Azure.Communication.CallAutomation;
using Azure.Communication.Identity;

namespace CallAutomation_AppointmentReminder
{
    public static class CallAutomationMediaHelper
    {
       public static PlaySource GetTextPromptForLable(string labelDetected)
       {
            PlaySource playSource;

            if (labelDetected.Equals("Confirm", StringComparison.OrdinalIgnoreCase))
            {
                playSource = new TextSource("You have chosen to confirm the appointment. Your appointment has been confirmed. Disconnecting the call, Thank you!");
            }
            else if (labelDetected.Equals("Cancel", StringComparison.OrdinalIgnoreCase))
            {
                playSource = new TextSource("You have chosen to cancel the appointment. Your appointment has been canceled. Disconnecting the call, Thank you!");
            }
            else
            {
                playSource = new TextSource("Invalid label detected, Disconnecting the call. Thank you!");
            }

            return playSource;
       }

        public async static Task<string> ProvisionAzureCommunicationServicesIdentity(string connectionString)
        {
            var client = new CommunicationIdentityClient(connectionString);
            var user = await client.CreateUserAsync().ConfigureAwait(false);
            return user.Value.Id;
        }
    }
}
