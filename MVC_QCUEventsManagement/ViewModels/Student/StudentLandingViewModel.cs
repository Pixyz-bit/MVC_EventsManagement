namespace MVC_QCUEventsManagement.ViewModels.Student;

public sealed record StudentLandingViewModel(string DisplayName)
{
    public string Title => "Student portal";
}
