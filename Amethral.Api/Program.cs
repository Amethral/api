// Program.cs
using Microsoft.EntityFrameworkCore;
using Amethral.Api.Data;
using Amethral.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// DB Context
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Service Métier
builder.Services.AddScoped<AuthService>();

// CORS (Important pour que Vue.js puisse appeler l'API)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowWeb",
        policy => policy.WithOrigins("http://localhost:8080") // Ton URL Vue.js
                        .AllowAnyMethod()
                        .AllowAnyHeader());
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowWeb");
app.UseAuthorization();
app.MapControllers();

app.Run();