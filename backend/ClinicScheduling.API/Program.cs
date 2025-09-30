var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularApp",
        policy =>
        {
            policy.WithOrigins("http://localhost:4200", "http://localhost:4201")
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

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAngularApp");

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

