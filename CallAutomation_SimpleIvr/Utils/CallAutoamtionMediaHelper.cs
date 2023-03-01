using Azure.Communication.CallAutomation;

namespace CallAutomation_SimpleIvr.Utils
{
    public static class CallAutoamtionMediaHelper
    {
        public static (PlaySource, string) GetTextPromptForLable(string labelDetected)
        {
            PlaySource playSource;
            string OperationContext = "ResponseToRecognizeChoice";


            if (labelDetected.Equals("Sales", StringComparison.OrdinalIgnoreCase))
            {
                playSource = new TextSource("You have chosen sales department. Disconnecting the call, Thank you!");
            }
            else if (labelDetected.Equals("Marketing", StringComparison.OrdinalIgnoreCase))
            {
                playSource = new TextSource("You have chosen marketing department. Disconnecting the call, Thank you!");
            }
            else if (labelDetected.Equals("CustomerCare", StringComparison.OrdinalIgnoreCase))
            {
                playSource = new TextSource("You have chosen customer care department. Disconnecting the call, Thank you!");
            }
            else if (labelDetected.Equals("Agent", StringComparison.OrdinalIgnoreCase))
            {
                playSource = new TextSource("You have chosen agent option. Please wait we are adding agent to the call, Thank you!");
                OperationContext = "AddAgentToCall";
            }
            else if (labelDetected.Equals("Hangup", StringComparison.OrdinalIgnoreCase))
            {
                playSource = new TextSource("You have chosen hangup option. Disconnecting the call, Thank you!");
            }
            else
            {
                playSource = new TextSource("Invalid label detected, Disconnecting the call. Thank you!");
            }

            return (playSource, OperationContext);
        }

    }
}
