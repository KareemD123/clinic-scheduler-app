var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add HTTP logging
builder.Services.AddHttpLogging(logging =>
{
    logging.LoggingFields = Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.All;
    logging.RequestHeaders.Add("sec-ch-ua");
    logging.ResponseHeaders.Add("MyResponseHeader");
    logging.MediaTypeOptions.AddText("application/javascript");
    logging.RequestBodyLogLimit = 4096;
    logging.ResponseBodyLogLimit = 4096;
});

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularApp",
        policy =>
        {
            policy.WithOrigins(
                "http://localhost:4200", 
                "http://localhost:4201", 
                "http://localhost:5000",
                "https://clinic-scheduler-cpcfhfeha8hpb6gs.canadacentral-01.azurewebsites.net",
                "https://brave-island-0fb1dfd0f.2.azurestaticapps.net",
                "https://*.azurestaticapps.net",
                "https://*.vercel.app"
            )
            .AllowAnyHeader()
            .AllowAnyMethod();
        });
});

// Register JSON Database (Singleton so it persists across requests)
builder.Services.AddSingleton<ClinicScheduling.Infrastructure.Data.JsonDatabase>(sp => 
{
    var dbPath = Path.Combine(Directory.GetCurrentDirectory(), "database.json");
    return new ClinicScheduling.Infrastructure.Data.JsonDatabase(dbPath);
});

// Register Unit of Work (Scoped for per-request lifecycle)
builder.Services.AddScoped<ClinicScheduling.Domain.Interfaces.IUnitOfWork, ClinicScheduling.Infrastructure.UnitOfWork.JsonUnitOfWork>();

// Register Application Services
builder.Services.AddScoped<ClinicScheduling.Application.Services.IPatientService, ClinicScheduling.Application.Services.PatientService>();
builder.Services.AddScoped<ClinicScheduling.Application.Services.IAppointmentService, ClinicScheduling.Application.Services.AppointmentService>();
builder.Services.AddScoped<ClinicScheduling.Application.Services.IBillingService, ClinicScheduling.Application.Services.BillingService>();

var app = builder.Build();

// Add startup logging
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("=== Clinic Scheduler API Starting ===");
logger.LogInformation("Environment: {Environment}", app.Environment.EnvironmentName);
logger.LogInformation("URLs: {Urls}", Environment.GetEnvironmentVariable("ASPNETCORE_URLS"));

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Enable HTTP logging middleware
app.UseHttpLogging();

app.UseCors("AllowAngularApp");

// Only use HTTPS redirection in development
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthorization();

// Add health check endpoint for Azure
app.MapGet("/health", () => "Healthy");

app.MapControllers();

logger.LogInformation("=== Clinic Scheduler API Started Successfully ===");
logger.LogInformation("Health check available at: /health");
logger.LogInformation("Patients API available at: /api/patients");

app.Run();

