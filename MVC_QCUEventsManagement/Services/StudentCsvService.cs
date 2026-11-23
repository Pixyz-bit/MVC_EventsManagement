using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using Microsoft.VisualBasic.FileIO;
using MVC_QCUEventsManagement.ViewModels.Admin;

namespace MVC_QCUEventsManagement.Services;

public sealed class StudentCsvService(StudentService students)
{
    public const string Header = "StudentId,FirstName,MiddleName,LastName,Gender,CampusBranch,Department,Program,Email,BirthDate";
    public async Task<StudentImportReport> ImportAsync(Stream stream, string password, CancellationToken ct)
    {
        StudentService.ValidatePassword(password);
        using var parser = new TextFieldParser(new StreamReader(stream, new UTF8Encoding(false, true), false)) { TextFieldType = FieldType.Delimited, HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = false };
        parser.SetDelimiters(",");
        string[]? header;
        try { header = parser.ReadFields(); } catch (MalformedLineException) { throw new ValidationException("The CSV header is malformed."); }
        var expected = Header.Split(','); var legacy = expected.Take(8).Concat(new[] { "YearLevel", "Section", "Email", "BirthDate" }).ToArray();
        var columns = header?.Select(h => h.Trim().TrimStart('\uFEFF')).ToArray() ?? [];
        var old = columns.SequenceEqual(legacy, StringComparer.OrdinalIgnoreCase);
        if (!old && !columns.SequenceEqual(expected, StringComparer.OrdinalIgnoreCase)) throw new ValidationException("Use the downloaded 10-column template or the supported 12-column legacy header.");
        var inputs = new List<(long Row, StudentProfileInput Input)>(); var errors = new List<StudentImportError>(); var count = 0;
        while (!parser.EndOfData)
        {
            ct.ThrowIfCancellationRequested(); var row = parser.LineNumber;
            if (++count > 200) throw new ValidationException("Upload at most 200 student rows per file. No rows from this file were imported.");
            string[]? fields;
            try { fields = parser.ReadFields(); } catch (MalformedLineException) { errors.Add(new(row, "Malformed CSV quoting.")); continue; }
            if (fields is null || fields.All(string.IsNullOrWhiteSpace)) continue;
            if (fields.Length != columns.Length) { errors.Add(new(row, $"Expected {columns.Length} columns.")); continue; }
            var dateText = fields[old ? 11 : 9].Trim(); DateOnly? birth = null;
            if (dateText.Length > 0)
            {
                if (!DateOnly.TryParseExact(dateText, new[] { "yyyy-MM-dd", "MM/dd/yyyy", "M/d/yyyy" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) { errors.Add(new(row, "BirthDate must use yyyy-MM-dd or MM/dd/yyyy.")); continue; }
                birth = date;
            }
            inputs.Add((row, new() { StudentId = fields[0], FirstName = fields[1], MiddleName = fields[2], LastName = fields[3], Gender = string.IsNullOrWhiteSpace(fields[4]) ? "Not Specified" : fields[4].Trim(), Campus = string.IsNullOrWhiteSpace(fields[5]) ? "San Bartolome" : fields[5].Trim(), College = fields[6].Trim(), Program = fields[7].Trim(), Email = fields[old ? 10 : 8], BirthDate = birth, Password = password }));
        }
        var created = 0;
        foreach (var (row, input) in inputs)
        {
            try { await students.CreateAsync(input, ct, imported: true); created++; }
            catch (ValidationException ex) { errors.Add(new(row, ex.Message)); }
        }
        return new(created, errors.OrderBy(e => e.Row).ToList());
    }
}
