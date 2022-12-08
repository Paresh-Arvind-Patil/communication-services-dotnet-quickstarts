using Azure.Communication.Identity;
using Azure.Communication;

namespace CallAutomation_AppointmentReminder
{
    /// <summary>
    /// Configuration assoociated with the call.
    /// </summary>
    public class CallConfiguration
    {
        public CallConfiguration()
        {

        }

        /// <summary>
        /// The connectionstring of Azure Communication Service resource.
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// The phone number to add to the call
        /// </summary>
        public string TargetPhoneNumber { get; set; }

        /// <summary>
        /// The phone number associated with the source. 
        /// </summary>
        public string SourcePhoneNumber { get; set; }

        /// <summary>
        /// The base url of the applicaiton.
        /// </summary>
        public string AppBaseUri { get; set; }

        /// <summary>
        /// The base url of the applicaiton.
        /// </summary>
        public string EventCallBackRoute { get; set; }

        /// <summary>
        /// The callback url of the application where notification would be received.
        /// </summary>
        public string CallbackEventUri => $"{AppBaseUri}" + EventCallBackRoute;

        /// <summary>
        /// Invalid input audio file route
        /// </summary>
        public string InvalidInputAudio { get; set; }

        /// <summary>
        /// Time out audio file route
        /// </summary>
        public string TimedoutAudio { get; set; }

        public string AppointmentChoiceMenu { get; set; }

        public string MakeAppointmentAudio { get; set; }

        public string AppointmentStatusAudio { get; set; }

        public string CancelAppointmentAudio { get; set; }
        public string ConfirmedAppointmentAudio { get; set; }

        public string CustomerSupportAudio { get; set; }

        public string SpeechOptionNotMatchedAudio { get; set; }

    }
}
