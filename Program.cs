using NRedisStack;
using NRedisStack.RedisStackCommands;
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
using System.Text.Json.Serialization;
using StackExchange.Redis;
using Microsoft.Extensions.Options;
using GeminiTest.Setting;

var builder = WebApplication.CreateBuilder(args);

// ? Load configuration for Gemini API
builder.Services.Configure<GeminiSettings>(builder.Configuration.GetSection("GeminiSettings"));
builder.Services.Configure<RedisSetting>(
    builder.Configuration.GetSection("Redis"));

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
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    // Lấy cấu hình Redis từ IOptions
    var redisSetting = sp.GetRequiredService<IOptions<RedisSetting>>().Value;

    // Cấu hình Redis
    var redisConfig = new ConfigurationOptions
    {
        EndPoints = { { redisSetting.Host, redisSetting.Port } },
        User = redisSetting.User,
        Password = redisSetting.Password
    };

    // Kết nối Redis và xử lý sự kiện lỗi
    var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Redis");
    var connection = ConnectionMultiplexer.Connect(redisConfig);

    connection.ConnectionFailed += (sender, args) => logger.LogError("Redis connection failed: {0}", args.Exception.Message);
    connection.ConnectionRestored += (sender, args) => logger.LogInformation("Redis connection restored.");

    return connection;
});

builder.Services.AddMemoryCache();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<IPromptService, PromptService>();
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
app.UseRouting();

// ? Apply CORS Policy BEFORE Controllers
app.UseCors("AllowFrontend");

// ? Middleware Pipeline
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
