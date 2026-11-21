using System.ComponentModel.DataAnnotations;
using MVC_QCUEventsManagement.ViewModels.Admin;

namespace MVC_QCUEventsManagement.Tests;

public sealed class EventAudienceTests
{
    [Theory]
    [InlineData("BSCpE", "College of Engineering")]
    [InlineData("BSEd", "College of Education")]
    public void Removed_programs_are_unavailable_and_rejected_by_server_validation(string code, string department)
    {
        Assert.DoesNotContain(CreateEventInput.ProgramOptions, option => option.Code == code);
        var input = ValidInput(code, department);
        var errors = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(input, new ValidationContext(input), errors, true));
        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(CreateEventInput.Programs)));
    }

    [Theory]
    [InlineData("BSIE", "College of Engineering")]
    [InlineData("BSECE", "College of Engineering")]
    [InlineData("BECEd", "College of Education")]
    public void Remaining_engineering_and_education_programs_are_accepted(string code, string department)
    {
        var input = ValidInput(code, department);
        var errors = new List<ValidationResult>();

        Assert.True(Validator.TryValidateObject(input, new ValidationContext(input), errors, true));
    }

    private static CreateEventInput ValidInput(string code, string department) => new()
    {
        Title = "Campus Research Forum", VenueLocation = "Hall A", MaxCapacity = 20,
        EventDate = new DateOnly(2030, 1, 10), StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(12, 0),
        RegStart = new(2030, 1, 1), RegEnd = new(2030, 1, 9), Confirm = true,
        TargetDepartment = department, Programs = [code]
    };
}
