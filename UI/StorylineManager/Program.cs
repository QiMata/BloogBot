using System.Net;
using StorylineManager;
using StorylineManager.Services;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(
    builder.Configuration["StorylineManager:Urls"] ??
    builder.Configuration["Urls"] ??
    "http://127.0.0.1:5157");

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.Configure<StorylineManagerOptions>(
    builder.Configuration.GetSection(StorylineManagerOptions.SectionName));
builder.Services.AddHttpClient<StorylineApiClient>((provider, client) =>
{
    var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<StorylineManagerOptions>>().Value;
    client.BaseAddress = new Uri(NormalizeApiBaseUrl(options.ApiBaseUrl));
});

var app = builder.Build();

app.Use(async (context, next) =>
{
    var remoteAddress = context.Connection.RemoteIpAddress;
    if (remoteAddress is not null && !IPAddress.IsLoopback(remoteAddress))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsync("Storyline Manager accepts loopback clients only.");
        return;
    }

    await next();
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();

static string NormalizeApiBaseUrl(string value)
{
    var url = string.IsNullOrWhiteSpace(value)
        ? "http://127.0.0.1:5147/api/storylines/v1/"
        : value.Trim();
    return url.EndsWith("/", StringComparison.Ordinal) ? url : $"{url}/";
}
