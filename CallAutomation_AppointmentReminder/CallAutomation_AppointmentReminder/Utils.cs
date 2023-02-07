using Azure.Communication.CallAutomation;
using Microsoft.Extensions.Options;

namespace CallAutomation_AppointmentReminder
{
    public static class Utils
    {
       public static PlaySource GetTextPromotForLable(string labelDetected, IOptions<CallConfiguration> callConfiguration)
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
    }
}
