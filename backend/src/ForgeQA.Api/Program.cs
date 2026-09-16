using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using ForgeQA.Api.Auth;
using ForgeQA.Api.Middleware;
using ForgeQA.Application;
using ForgeQA.Application.Bugs;
using ForgeQA.Application.Telemetry;
using ForgeQA.Infrastructure;
using ForgeQA.Infrastructure.Auth;
using ForgeQA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddHttpContextAccessor();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer()
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, ProjectApiKeyAuthenticationHandler>(
        ProjectApiKeyDefaults.AuthenticationScheme, _ => { });

// Bound lazily from IConfiguration when the handler is first resolved, so config overrides
// applied after CreateBuilder() (e.g. WebApplicationFactory in tests) are honored. Reading
// builder.Configuration eagerly here would capture a stale value in the closure.
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IConfiguration>((options, configuration) =>
    {
        var jwtSection = configuration.GetSection(JwtOptions.SectionName);
        var jwtSecret = jwtSection["Secret"] ?? throw new InvalidOperationException("Jwt:Secret is not configured.");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(jwtSecret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Default", policy =>
    {
        policy.WithOrigins(corsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Partitioned per Project API key (falling back to remote IP for JWT-authenticated or
    // unauthenticated callers) so one runtime key being noisy never throttles another Project.
    options.AddPolicy(RateLimiting.BugIngestionPolicy, httpContext =>
    {
        var partitionKey = httpContext.User.FindFirst(ProjectApiKeyDefaults.ProjectApiKeyIdClaim)?.Value
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";

        var maxPerMinute = httpContext.RequestServices.GetRequiredService<IOptions<BugReportingOptions>>().Value.MaxRuntimeReportsPerMinutePerKey;

        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            Window = TimeSpan.FromMinutes(1),
            PermitLimit = maxPerMinute,
            QueueLimit = 0
        });
    });

    // Same per-key partitioning as BugIngestionPolicy, but telemetry's expected volume is much
    // higher than bug reports, so it gets its own, more permissive limit.
    options.AddPolicy(RateLimiting.TelemetryIngestionPolicy, httpContext =>
    {
        var partitionKey = httpContext.User.FindFirst(ProjectApiKeyDefaults.ProjectApiKeyIdClaim)?.Value
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";

        var maxPerMinute = httpContext.RequestServices.GetRequiredService<IOptions<TelemetryOptions>>().Value.MaxBatchesPerMinutePerKey;

        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            Window = TimeSpan.FromMinutes(1),
            PermitLimit = maxPerMinute,
            QueueLimit = 0
        });
    });
});

builder.Services.AddHealthChecks()
    .AddNpgSql(sp => sp.GetRequiredService<IConfiguration>().GetConnectionString("Default")!, name: "postgres");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "ForgeQA API", Version = "v1" });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Enter 'Bearer {your JWT token}'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
    {
        { new OpenApiSecuritySchemeReference("Bearer"), new List<string>() }
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ForgeQADbContext>();
    await dbContext.Database.MigrateAsync();
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Default");
app.UseAuthentication();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program { }
