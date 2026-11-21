namespace MVC_QCUEventsManagement.ViewModels.Account;

public sealed record AccessDeniedViewModel(bool IsAuthenticated)
{
    public string ReturnLabel => IsAuthenticated ? "Return to your portal" : "Sign in";
}
