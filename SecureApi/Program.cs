using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using SecureApi.Data;
using SecureApi.Dtos;
using SecureApi.Models;
using SecureApi.Security;
using SecureApi.Validation;

var builder = WebApplication.CreateBuilder(args);

// --- Configuración de JWT ---
// El secreto NUNCA debería vivir en appsettings.json en un repo real.
// Acá se lee de configuración para que en desarrollo funcione con un
// valor por default, pero en producción se espera la variable de
// entorno SecureApi__Jwt__Secret (ver README y appsettings.json).
string jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? "dev-only-secret-cambiar-en-produccion-minimo-32-caracteres";
string jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "SecureApi";

builder.Services.AddSingleton(new JwtService(jwtSecret, jwtIssuer, TimeSpan.FromHours(2)));
builder.Services.AddSingleton<ProveedorStore>();
builder.Services.AddSingleton<UsuarioStore>();

// --- Rate limiting: mitiga fuerza bruta y abuso, con lo que ya trae ASP.NET Core ---
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Límite general para toda la API.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "sin-ip",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));

    // Límite estricto específico para el login, para frenar ataques de fuerza bruta.
    options.AddPolicy("login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "sin-ip",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
});

var app = builder.Build();

app.UseRateLimiter();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// ===================== AUTH =====================

app.MapPost("/api/auth/login", (LoginRequest request, UsuarioStore usuarios, JwtService jwt) =>
{
    var usuario = usuarios.BuscarPorUsuario(request.NombreUsuario);

    if (usuario is null || !PasswordHasher.VerifyPassword(request.Password, usuario.PasswordHash, usuario.PasswordSalt))
    {
        // Mensaje deliberadamente genérico: no revela si falló el usuario o la contraseña.
        return Results.Unauthorized();
    }

    string token = jwt.GenerateToken(usuario.Id, usuario.NombreUsuario, usuario.Rol);
    return Results.Ok(new LoginResponse
    {
        Token = token,
        ExpiraUtc = DateTime.UtcNow.AddHours(2),
    });
})
.WithValidation<LoginRequest>()
.RequireRateLimiting("login")
.WithName("Login")
.WithSummary("Autentica un usuario y devuelve un JWT.");

// ===================== PROVEEDORES (protegido con JWT) =====================

var proveedores = app.MapGroup("/api/proveedores").RequireJwtAuth();

proveedores.MapGet("/", (ProveedorStore store) =>
    Results.Ok(store.ObtenerTodos().Select(MapToResponse)))
    .WithName("ListarProveedores");

proveedores.MapGet("/{id:int}", (int id, ProveedorStore store) =>
{
    var proveedor = store.ObtenerPorId(id);
    return proveedor is null ? Results.NotFound() : Results.Ok(MapToResponse(proveedor));
})
.WithName("ObtenerProveedor");

proveedores.MapPost("/", (ProveedorRequest request, ProveedorStore store) =>
{
    var nuevo = store.Agregar(new Proveedor
    {
        RazonSocial = request.RazonSocial,
        Cuit = request.Cuit,
        Email = request.Email,
        Telefono = request.Telefono,
        Rubro = request.Rubro,
    });

    return Results.Created($"/api/proveedores/{nuevo.Id}", MapToResponse(nuevo));
})
.WithValidation<ProveedorRequest>()
.WithName("CrearProveedor");

proveedores.MapPut("/{id:int}", (int id, ProveedorRequest request, ProveedorStore store) =>
{
    var actualizado = new Proveedor
    {
        RazonSocial = request.RazonSocial,
        Cuit = request.Cuit,
        Email = request.Email,
        Telefono = request.Telefono,
        Rubro = request.Rubro,
    };

    return store.Actualizar(id, actualizado) ? Results.Ok(MapToResponse(actualizado)) : Results.NotFound();
})
.WithValidation<ProveedorRequest>()
.WithName("ActualizarProveedor");

proveedores.MapDelete("/{id:int}", (int id, ProveedorStore store) =>
    store.Eliminar(id) ? Results.NoContent() : Results.NotFound())
    .WithName("EliminarProveedor");

// ===================== HEALTHCHECK =====================

app.MapGet("/health", () => Results.Ok(new { status = "ok", utc = DateTime.UtcNow }))
    .WithName("Health");

app.Run();

static ProveedorResponse MapToResponse(Proveedor p) => new()
{
    Id = p.Id,
    RazonSocial = p.RazonSocial,
    Cuit = p.Cuit,
    Email = p.Email,
    Telefono = p.Telefono,
    Rubro = p.Rubro,
    Activo = p.Activo,
    FechaAlta = p.FechaAlta,
};

// Necesario para que el proyecto de tests pueda referenciar Program
// vía WebApplicationFactory<Program> si en el futuro se agregan tests
// de integración además de los unitarios.
public partial class Program { }
