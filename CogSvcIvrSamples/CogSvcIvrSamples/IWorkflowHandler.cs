using Azure.Communication.CallAutomation;

namespace CogSvcIvrSamples
{
    public interface IWorkflowHandler
    {
        Task HandleAsync(string callerId,
            CallAutomationEventData @event,
            CallConnection callConnection,
            CallMedia callConnectionMedia);
    }
}
