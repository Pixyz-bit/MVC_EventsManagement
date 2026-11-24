using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using LocalAdminPreview;
using MVC_QCUEventsManagement.Services;

if (args.Length != 2 || args[0] != "--apply" || !Regex.IsMatch(args[1], "\\A[a-z0-9-]{1,24}\\z") ||
    Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") != "Development")
    throw new InvalidOperationException("Explicit local preview authorization required: ASPNETCORE_ENVIRONMENT=Development, --apply <unique-batch> (lowercase letters/digits/hyphens, max 24).");
var root = Directory.GetCurrentDirectory();
if (!File.Exists(Path.Combine(root, "MVC_QCUEventsManagement.slnx"))) throw new InvalidOperationException("Run from the MVC repository root.");
var legacyRoot = Path.GetFullPath(Path.Combine(root, "..", "241611JalopEventsManagement", "241611JalopEventsManagement"));
if (!File.Exists(Path.Combine(legacyRoot, "Web.config"))) throw new InvalidOperationException("Expected local Web Forms project was not found.");
var batch = args[1];
var output = Path.Combine(root, "PrivateData", "AdminPreview", batch); Directory.CreateDirectory(output);
var credentialPath = Path.Combine(output, "credentials.json");
var credentials = File.Exists(credentialPath) ? JsonNode.Parse(await File.ReadAllTextAsync(credentialPath))!.AsObject() : new JsonObject
{
    ["mvcPassword"] = "Preview-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(12)),
    ["webformsPassword"] = "Preview-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(12))
};
// Preserve generated credentials before the first commit, enabling recovery without password resets.
await File.WriteAllTextAsync(credentialPath, credentials.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
var bannerRoot = Path.Combine(root, "MVC_QCUEventsManagement", "PrivateData", "EventBanners"); Directory.CreateDirectory(bannerRoot);
var bannerName = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("preview-banner:" + batch)))[..32].ToLowerInvariant() + ".png";
var bannerPath = Path.Combine(bannerRoot, bannerName);
if (!File.Exists(bannerPath)) File.Copy(Path.Combine(root, "MVC_QCUEventsManagement", "wwwroot", "images", "qcu-campus.png"), bannerPath);
var now = new EventClock(TimeProvider.System).Now;
var result = new JsonObject { ["batch"] = batch, ["createdPhilippineTime"] = now.ToString("s"), ["applications"] = new JsonArray() };
foreach (var app in new[] { "mvc", "webforms" })
{
    var data = PreviewDataset.Create(app, batch, credentials[app + "Password"]!.GetValue<string>(), now,
        app == "mvc" ? bannerName : "~/Frontend/Assets/campus-clean.jpg");
    var before = await PreviewSeeder.ExistingFingerprintAsync(app, data);
    await PreviewSeeder.SeedAsync(app, data);
    var after = await PreviewSeeder.ExistingFingerprintAsync(app, data);
    if (before != after) throw new InvalidOperationException("Pre-existing rows changed during the preview operation. No automatic cleanup or overwrite was attempted; inspect concurrent app activity.");
    var application = await PreviewSeeder.ReadAsync(app, data);
    application["existingRowsUnchanged"] = true;
    application["baseUrl"] = app == "mvc" ? "http://localhost:5314" : "http://localhost:51717";
    application["studentLogin"] = data.Students[4].User.Email;
    application["studentId"] = data.Students[4].StudentId;
    result["applications"]!.AsArray().Add(application);
    Console.WriteLine($"{app}: verified 9 preview events, 36 students, balanced booking counts, {application["unscannedPasses"]!.AsArray().Count} currently unscanned scanner passes.");
}
await File.WriteAllTextAsync(Path.Combine(output, "manifest.json"), result.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
var access = new System.Text.StringBuilder("FICTIONAL LOCAL PREVIEW ACCOUNTS\nKeep this file private; credentials are intentionally excluded from Git.\n\n");
foreach (var app in result["applications"]!.AsArray())
{
    var name = app!["app"]!.GetValue<string>();
    access.AppendLine(name.ToUpperInvariant()).AppendLine("URL: " + app["baseUrl"])
        .AppendLine("Administrator: " + app["admin"]).AppendLine("Student: " + app["studentLogin"])
        .AppendLine("Student ID: " + app["studentId"]).AppendLine("Password for this batch's accounts: " + credentials[name + "Password"])
        .AppendLine("QR sheets: " + Path.Combine(output, name, "qr-passes.html")).AppendLine();
}
await File.WriteAllTextAsync(Path.Combine(output, "access.txt"), access.ToString());
Console.WriteLine("Private output: " + output);
