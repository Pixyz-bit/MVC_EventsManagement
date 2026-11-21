using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MVC_QCUEventsManagement.Tests;

public sealed class PageRoutingTests
{
    [Theory]
    [InlineData("/login", HttpStatusCode.OK, "action=\"/login\"")]
    [InlineData("/access-denied", HttpStatusCode.Forbidden, "href=\"/login\"")]
    [InlineData("/Home/Privacy", HttpStatusCode.OK, "Privacy Policy")]
    [InlineData("/Home/Error", HttpStatusCode.OK, "Request ID:")]
    public async Task Page_controllers_render_their_matching_views_at_existing_urls(string path, HttpStatusCode status, string expected)
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
        });
        using var response = await client.GetAsync(path);

        Assert.Equal(status, response.StatusCode);
        Assert.Contains(expected, await response.Content.ReadAsStringAsync());
    }
}
