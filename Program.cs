using GeminiTest.Data;
using GeminiTest.Models;
using GeminiTest.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging; 
using static GeminiController;

var builder = WebApplication.CreateBuilder(args);

// ? Load configuration for Gemini API
builder.Services.Configure<GeminiSettings>(builder.Configuration.GetSection("GeminiSettings"));

builder.Logging.ClearProviders(); // Clear default logging providers
builder.Logging.AddConsole(); // Add Console logging
builder.Logging.AddDebug();   // Add Debug logging
// ? Register necessary services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.Configure<IdentityOptions>(options =>
{
    options.SignIn.RequireConfirmedEmail = true; // Ensure email must be confirmed
});

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
    });
    options.OperationFilter<SecurityRequirementsOperationFilter>();
});
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();

builder.Services.AddHttpClient();

builder.Services.AddAuthorization();
builder.Services.AddIdentityApiEndpoints<ApplicationUser>()
    .AddEntityFrameworkStores<DataContext>();

builder.Services.AddDbContext<DataContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
// ? CORS Configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy => policy.WithOrigins("http://localhost:3000") // Allow frontend
                        .AllowAnyMethod()
                        .AllowAnyHeader());
});

var app = builder.Build();

// ? Enable Swagger UI for development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.MapIdentityApi<ApplicationUser>();
// ? Apply CORS Policy BEFORE Controllers
app.UseCors("AllowFrontend");

// ? Middleware Pipeline
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
