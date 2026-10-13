using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace RestaurantPos.Tests;

// The ASP.NET MVC admin page: server-rendered list, form post with validation,
// anti-forgery token and Post/Redirect/Get.
[Collection("api")]
public class MenuAdminMvcTests(PosApiFactory factory)
{
    // The default test client keeps cookies (anti-forgery, TempData) and follows redirects.
    private readonly HttpClient _client = factory.CreateClient();

    private static string Token(string html)
    {
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"");
        Assert.True(match.Success, "Anti-forgery token not found in the page.");
        return match.Groups[1].Value;
    }

    private async Task<HttpResponseMessage> PostFormAsync(string name, string category, string price)
    {
        var page = await _client.GetStringAsync("/admin/menu");
        return await _client.PostAsync("/admin/menu", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["NewItem.Name"] = name,
            ["NewItem.Category"] = category,
            ["NewItem.Price"] = price,
            ["__RequestVerificationToken"] = Token(page)
        }));
    }

    [Fact]
    public async Task Admin_page_lists_menu_items()
    {
        var name = $"Lassi-{Guid.NewGuid():N}";
        var created = await _client.PostAsJsonAsync("/menu", new { name, category = "Drinks", priceCents = 450 });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var r = await _client.GetAsync("/admin/menu");
        var html = await r.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("text/html", r.Content.Headers.ContentType?.MediaType);
        Assert.Contains(name, html);
        Assert.Contains("4.50", html);
    }

    [Fact]
    public async Task Valid_form_post_creates_the_item_and_redirects_back_to_the_list()
    {
        var name = $"Paneer-{Guid.NewGuid():N}";
        var category = $"Cat-{Guid.NewGuid():N}";

        var r = await PostFormAsync(name, category, "12.50");
        var html = await r.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains($"Added &quot;{name}&quot;.", html);

        var items = await _client.GetFromJsonAsync<JsonElement>($"/menu?category={category}");
        var item = Assert.Single(items.EnumerateArray());
        Assert.Equal(1250, item.GetProperty("priceCents").GetInt64());
    }

    [Fact]
    public async Task Invalid_form_post_shows_validation_errors_and_saves_nothing()
    {
        var category = $"Cat-{Guid.NewGuid():N}";

        var r = await PostFormAsync("", category, "0");
        var html = await r.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("Name is required.", html);
        Assert.Contains("Price must be between 0.01 and 100000.", html);

        var items = await _client.GetFromJsonAsync<JsonElement>($"/menu?category={category}");
        Assert.Empty(items.EnumerateArray());
    }

    [Fact]
    public async Task Form_post_without_anti_forgery_token_is_rejected()
    {
        var r = await _client.PostAsync("/admin/menu", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["NewItem.Name"] = "NoToken",
            ["NewItem.Category"] = "Test",
            ["NewItem.Price"] = "1.00"
        }));

        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }
}
