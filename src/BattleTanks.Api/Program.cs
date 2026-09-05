using BattleTanks.Application;
using BattleTanks.GameServer;
using BattleTanks.Infrastructure;
using DotNetEnv;
using Microsoft.OpenApi.Models;

// Load .env file climbing up directories (supports running from src/ subdirectory)
Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// -----------------------------------------------------------------------
// Services
// -----------------------------------------------------------------------
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddGameServer();

// CORS for Angular frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:4200", "http://127.0.0.1:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Swagger with JWT Bearer support
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title   = "BattleTanks API",
        Version = "v1",
        Description = "REST API for Battle Tanks Multiplayer — Auth & Rooms"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = SecuritySchemeType.Http,
        Scheme       = "bearer",
        BearerFormat = "JWT",
        In           = ParameterLocation.Header,
        Description  = "Enter your JWT token. Example: eyJhbGci..."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id   = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// -----------------------------------------------------------------------
// Pipeline
// -----------------------------------------------------------------------
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowFrontend");

app.UseHttpsRedirection();

app.UseAuthentication(); // must come before UseAuthorization
app.UseAuthorization();

app.MapControllers();
app.MapHub<GameHub>(GameHub.Route);

app.Run();
