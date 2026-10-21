using LinkShortener.Api.Configuration;
using LinkShortener.Application.Links;
using LinkShortener.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
    };
});
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IShortCodeGenerator, ShortCodeGenerator>();
builder.Services.AddScoped<LinkService>();
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddLinkShortenerOptions(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.MapLinkShortenerEndpoints();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<LinkDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.Run();

public partial class Program;
