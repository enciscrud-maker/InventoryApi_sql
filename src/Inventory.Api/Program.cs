using System.Text;
using System.Security.Claims;
using System.Text.Json.Serialization;
using FluentValidation;
using Inventory.Api.Infrastructure;
using Inventory.Application.Abstractions;
using Inventory.Application.Dtos;
using Inventory.Application.Services;
using Inventory.Application.Validators;
using Inventory.Domain;
using Inventory.Infrastructure;
using Inventory.Infrastructure.Persistence;
using Inventory.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

// ---------- options ----------
builder.Services.AddOptions<JwtOptions>().Bind(cfg.GetSection("Jwt"))
    .Validate(o => !string.IsNullOrEmpty(o.Key) && o.Key.Length >= 32, "Jwt:Key must be at least 32 characters.").ValidateOnStart();
builder.Services.Configure<StorageOptions>(o =>
{
    var root = cfg["Storage:RootPath"] ?? "uploads";
    o.RootPath = Path.IsPathRooted(root) ? root : Path.Combine(builder.Environment.ContentRootPath, root);
});
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = 12 * 1024 * 1024);

// ---------- infrastructure + application ----------
builder.Services.AddInfrastructure(cfg);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();
builder.Services.AddScoped<IHierarchyValidator, HierarchyValidator>();
builder.Services.AddScoped<IMasterService<CategoryRequest, CategoryDto>, CategoryService>();
builder.Services.AddScoped<IMasterService<GroupRequest, GroupDto>, GroupService>();
builder.Services.AddScoped<IMasterService<TypeRequest, TypeDto>, TypeService>();
builder.Services.AddScoped<IMasterService<BrandRequest, BrandDto>, BrandService>();
builder.Services.AddScoped<IMasterService<ModelRequest, ModelDto>, ModelService>();
builder.Services.AddScoped<IMasterService<BusinessUnitRequest, BusinessUnitDto>, BusinessUnitService>();
builder.Services.AddScoped<IMasterService<LocationRequest, LocationDto>, LocationService>();
builder.Services.AddScoped<IMasterService<AssetStatusRequest, AssetStatusDto>, AssetStatusService>();
builder.Services.AddScoped<IMasterService<ConditionRequest, ConditionDto>, ConditionService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<EmployeeService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<AssetService>();
builder.Services.AddScoped<AssetImportService>();
builder.Services.AddScoped<GoodService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<ReportService>();

// ---------- MVC / JSON ----------
builder.Services.AddControllers(o => o.Filters.Add<ValidationFilter>())
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();

// ---------- authentication / authorization (permissions come from CFG_* tables -> JWT claims) ----------
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((o, jwt) =>
    {
        var j = jwt.Value;
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = j.Issuer, ValidateAudience = true, ValidAudience = j.Audience,
            ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(j.Key)),
            ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30), NameClaimType = "name", RoleClaimType = "role"
        };
        o.Events = new JwtBearerEvents
        {
            OnTokenValidated = async ctx =>
            {
                var revocation = ctx.HttpContext.RequestServices.GetRequiredService<ITokenRevocation>();
                var jti = ctx.Principal?.FindFirst("jti")?.Value;
                if (ctx.Principal?.FindFirst("token_use")?.Value != "access" || (jti is not null && revocation.IsRevoked(jti)))
                {
                    ctx.Fail("Invalid or revoked token.");
                    return;
                }

                if (ctx.Principal?.HasClaim("is_main_admin", "true") != true &&
                    int.TryParse(ctx.Principal?.FindFirst("sub")?.Value, out var userId))
                {
                    var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                    var isMainAdmin = await db.Users.AsNoTracking().AnyAsync(
                        user => user.UserId == userId &&
                                (user.IsMainAdmin || user.Employee.EmployeeNik == RoleNames.MainAdminNik),
                        ctx.HttpContext.RequestAborted);
                    var identity = ctx.Principal?.Identity as ClaimsIdentity;
                    if (isMainAdmin && identity is not null)
                        identity.AddClaim(new Claim("is_main_admin", "true"));
                }
            }
        };
    });
builder.Services.AddAuthorization(o =>
{
    foreach (var p in Permissions.All)
        o.AddPolicy(p, policy => policy.RequireAuthenticatedUser().RequireAssertion(ctx =>
            ctx.User.HasClaim("is_main_admin", "true") || ctx.User.HasClaim("permission", p)));
});

// ---------- CORS ----------
var origins = cfg.GetSection("Cors:Origins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
{
    if (origins.Length > 0) p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("Content-Disposition");
}));

// ---------- Swagger ----------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Inventory API", Version = "v1", Description = "Asset inventory, Goods In/Out, stock audit and reporting." });
    var xml = Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.xml");
    if (File.Exists(xml)) c.IncludeXmlComments(xml);
    c.SchemaFilter<EnumSchemaFilter>();
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", In = ParameterLocation.Header, Description = "Paste the access token (without 'Bearer ')."
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = Array.Empty<string>()
    });
});

var app = builder.Build();

app.UseMiddleware<ExceptionMiddleware>();
app.UseStatusCodePages();
if (cfg.GetValue<bool>("Swagger:Enabled") || app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DbSeeder.InitializeAsync(db, scope.ServiceProvider.GetRequiredService<IPasswordHasher>(), cfg);
}

app.Run();

public partial class Program { }
