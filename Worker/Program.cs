using System.Text;
using System.Diagnostics;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient("KurierRelay");
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddControllers();

var app = builder.Build();

app.MapPost("/api/kurier/relay", async (HttpContext context, IHttpClientFactory factory, ILogger<Program> logger) =>
{
    var config = app.Configuration.GetSection("Kurier");
    var kurierUrl = config["BaseUrl"] ?? "https://www.kurierservicos.com.br/wsservicos/";
    var timeout = int.TryParse(config["TimeoutSeconds"], out var t) ? t : 100;

    var client = factory.CreateClient("KurierRelay");
    client.Timeout = TimeSpan.FromSeconds(timeout);

    string requestBody;
    using (var reader = new StreamReader(context.Request.Body))
        requestBody = await reader.ReadToEndAsync();

    var contentType = context.Request.ContentType?.Contains("xml") == true ? "application/xml" : "application/json";
    if (string.IsNullOrWhiteSpace(requestBody))
    {
        logger.LogWarning("Relay: Body vazio");
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("Body vazio");
        return;
    }

    var request = new HttpRequestMessage(HttpMethod.Post, kurierUrl)
    {
        Content = new StringContent(requestBody, Encoding.UTF8, contentType)
    };

    var policy = Policy.WrapAsync(
        Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(timeout)),
        HttpPolicyExtensions.HandleTransientHttpError().WaitAndRetryAsync(3, i => TimeSpan.FromSeconds(Math.Pow(2, i)))
    );

    try
    {
        var stopwatch = Stopwatch.StartNew();
        var response = await policy.ExecuteAsync(() => client.SendAsync(request));
        stopwatch.Stop();

        var result = await response.Content.ReadAsStringAsync();
        logger.LogInformation("Kurier relay [{Status}] - {Elapsed}ms", response.StatusCode, stopwatch.ElapsedMilliseconds);

        context.Response.StatusCode = (int)response.StatusCode;
        await context.Response.WriteAsync(result);
    }
    catch (TimeoutRejectedException)
    {
        logger.LogError("Timeout connecting to Kurier");
        context.Response.StatusCode = StatusCodes.Status504GatewayTimeout;
        await context.Response.WriteAsync("Kurier timeout");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error in Kurier relay");
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsync("Internal relay error");
    }
});

