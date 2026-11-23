namespace MVC_QCUEventsManagement.ViewModels.Admin;

public sealed record EventWorkflowViewModel(int EventId, string Current, bool FromHistory = false);
