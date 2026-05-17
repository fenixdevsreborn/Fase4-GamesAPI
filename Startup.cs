using Amazon.DynamoDBv2;
using Amazon.XRay.Recorder.Handlers.AwsSdk;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ms_games.Messaging;
using ms_games.Observability;
using ms_games.Repositories;
using ms_games.Services;
using System.Text;
using System.Text.Json.Serialization;

namespace ms_games;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    public void ConfigureServices(IServiceCollection services)
    {
      services.AddLogging(config =>
      {
        config.AddConsole();
        config.SetMinimumLevel(LogLevel.Information);
      });

      services.AddControllers()
        .AddJsonOptions(options =>
        {
          options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
          options.JsonSerializerOptions.WriteIndented = true;
        });

      services.AddSwaggerGen(options =>
      {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
          Title = "Games API",
          Version = "v1",
          Description = "Microservico de catalogo e compra de games"
        });

        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
          In = ParameterLocation.Header,
          Description = "Enter the Bearer token",
          Name = "Authorization",
          Type = SecuritySchemeType.Http,
          BearerFormat = "JWT",
          Scheme = "Bearer"
        });

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
          {
            new OpenApiSecurityScheme
            {
              Reference = new OpenApiReference
              {
                Type = ReferenceType.SecurityScheme,
                Id = "Bearer"
              }
            },
            Array.Empty<string>()
          }
        });
      });

      ConfigureJwt(services);
      ConfigureDistributedCache(services);

      services.AddDefaultAWSOptions(Configuration.GetAWSOptions());
      services.AddAWSService<IAmazonDynamoDB>();

      services.AddSingleton<IMessagePublisher, RabbitMqPublisher>();
      services.AddScoped<IGameService, GameService>();
      services.AddScoped<GameRepository>();
      services.AddScoped<IGameSearchRepository, ElasticGameSearchRepository>();
      services.AddHealthChecks();

      AWSSDKHandler.RegisterXRayForAllServices();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
              options.SwaggerEndpoint("/swagger/v1/swagger.json", "Games API v1");
              options.RoutePrefix = string.Empty;
            });
        }

        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseMiddleware<XRayMiddleware>();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.MapHealthChecks("/health");
            endpoints.MapGet("/", async context =>
            {
                await context.Response.WriteAsync("Games API running on Kubernetes");
            });
        });
    }

    private void ConfigureJwt(IServiceCollection services)
    {
      var jwtSecret = Configuration["Jwt:Secret"];
      var jwtIssuer = Configuration["Jwt:Issuer"];
      var jwtAudience = Configuration["Jwt:Audience"];

      if (string.IsNullOrEmpty(jwtSecret))
      {
        throw new InvalidOperationException("JWT Secret is not configured");
      }

      var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
      {
        KeyId = Configuration["Jwt:KeyId"] ?? "ms-users-api-signing-key"
      };

      services.AddAuthentication("Bearer")
        .AddJwtBearer("Bearer", options =>
        {
          options.MapInboundClaims = false;
          options.RequireHttpsMetadata = false;
          options.TokenValidationParameters = new TokenValidationParameters
          {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            IssuerSigningKeyResolver = (token, securityToken, kid, validationParameters) => new[] { key },
            ValidateIssuer = !string.IsNullOrEmpty(jwtIssuer),
            ValidIssuer = jwtIssuer,
            ValidateAudience = !string.IsNullOrEmpty(jwtAudience),
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(5),
            NameClaimType = "sub",
            RoleClaimType = "role"
          };

          options.Events = new JwtBearerEvents
          {
            OnAuthenticationFailed = context =>
            {
              Console.WriteLine("Games API JWT authentication failed: " + context.Exception.Message);
              return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
              var claims = context.Principal?.Claims.Select(c => $"{c.Type}={c.Value}");
              Console.WriteLine("Games API JWT token validated. Claims: " + string.Join(", ", claims ?? Array.Empty<string>()));
              return Task.CompletedTask;
            }
          };
        });

      services.AddAuthorization();
    }

    private void ConfigureDistributedCache(IServiceCollection services)
    {
      var redisHost = Configuration["Redis:Host"]
        ?? throw new InvalidOperationException("Redis configuration 'Redis:Host' is not configured");
      var redisPort = Configuration["Redis:Port"] ?? "6379";
      var instanceName = Configuration["Redis:InstanceName"] ?? "games-api:";

      services.AddStackExchangeRedisCache(options =>
      {
        options.Configuration = $"{redisHost}:{redisPort}";
        options.InstanceName = instanceName;
      });
    }
}
