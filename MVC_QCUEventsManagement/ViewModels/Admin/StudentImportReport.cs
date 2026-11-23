namespace MVC_QCUEventsManagement.ViewModels.Admin;

public sealed record StudentImportReport(int Created, IReadOnlyList<StudentImportError> Errors);
