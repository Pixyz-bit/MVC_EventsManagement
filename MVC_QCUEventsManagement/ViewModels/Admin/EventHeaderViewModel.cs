namespace MVC_QCUEventsManagement.ViewModels.Admin;

public sealed record EventHeaderViewModel(string Title, string Status, DateTime Start, DateTime End,
    string Venue, int Registered, int Capacity, int? ManageEventId = null);
