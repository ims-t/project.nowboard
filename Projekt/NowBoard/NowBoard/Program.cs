using NowBoard.Components;
using NowBoard.Data.Setup;

var builder = WebApplication.CreateBuilder(args);
var otdSetup = new OpenTransportDataSetup();
var nowBoardSetup = new NowBoardSetup();

builder.Configuration.AddJsonFile($"appsettings.{builder.Configuration["ENVIRONMENT"]}.secrets.json", false);
builder.Configuration.GetRequiredSection("OpenTransportData")
                     .Bind(otdSetup);
builder.Configuration.GetRequiredSection("NowBoardSetup")
                     .Bind(nowBoardSetup);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddSingleton(s => otdSetup);
builder.Services.AddSingleton(nowBoardSetup);
builder.Services.AddHttpClient("OjpAPI", client =>
{
    client.BaseAddress = new Uri("https://api.opentransportdata.swiss/ojp20/");
});

// Make the named client available as the default HttpClient
builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("OjpAPI"));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();