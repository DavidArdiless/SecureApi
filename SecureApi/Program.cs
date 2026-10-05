using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using SecureApi.Data;
using SecureApi.Dtos;
using SecureApi.Models;
using SecureApi.Security;
using SecureApi.Validation;

var builder = WebApplication.CreateBuilder(args);

// --- Variables de entorno con prefijo ---
// El README documenta las variables como SecureApi__Jwt__Secret y
// SecureApi__Seed__AdminPassword. El provider de entorno que registra
// CreateBuilder NO usa prefijo, así que sin esta línea esas variables se
// ignoran en silencio y la app se queda con los valores de desarrollo.
// Registrarlo último también le da prioridad sobre los appsettings.
builder.Configuration.AddEnvironmentVariables(prefix: "SecureApi__");

// --- Configuración de JWT ---
// El secreto no vive en el repo. En Development se toma de
// appsettings.Development.json; fuera de Development es obligatorio y, si
// falta, la app NO arranca. Arrancar con un secreto público sería peor que
// no arrancar: cualquiera podría firmar tokens de admin.
bool esDesarrollo = builder.Environment.IsDevelopment();

string? jwtSecretConfigurado = builder.Configuration["Jwt:Secret"];
if (string.IsNullOrWhiteSpace(jwtSecretConfigurado) && !esDesarrollo)
{
    throw new InvalidOperationException(
        "Falta el secreto JWT. Configurá SecureApi__Jwt__Secret (o Jwt__Secret) " +
        "con al menos 32 caracteres. La API no arranca con el secreto de " +
        "desarrollo fuera del entorno Development.");
}

string jwtSecret = jwtSecretConfigurado ?? "dev-only-secret-cambiar-en-produccion-minimo-32-caracteres";
string jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "SecureApi";
var jwtLifetime = TimeSpan.FromHours(2);

builder.Services.AddSingleton(new JwtService(jwtSecret, jwtIssuer, jwtLifetime));
builder.Services.AddSingleton<ProveedorStore>();
builder.Services.AddSingleton<UsuarioStore>();

// --- Rate limiting: mitiga fuerza bruta y abuso, con lo que ya trae ASP.NET Core ---
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Nota: la partición es por IP de la conexión. Detrás de un proxy o load
    // balancer hay que sumar UseForwardedHeaders, o todos los clientes caen
    // en la misma partición y se bloquean entre ellos.

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

    // VerificarPassword corre PBKDF2 incluso si el usuario no existe, para
    // que el tiempo de respuesta no delate qué usuarios son válidos.
    if (!usuarios.VerificarPassword(usuario, request.Password))
    {
        // Mensaje deliberadamente genérico: no revela si falló el usuario o la contraseña.
        return Results.Unauthorized();
    }

    string token = jwt.GenerateToken(usuario!.Id, usuario.NombreUsuario, usuario.Rol);
    return Results.Ok(new LoginResponse
    {
        Token = token,
        ExpiraUtc = DateTime.UtcNow.Add(jwtLifetime),
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
    var actualizado = store.Actualizar(id, new Proveedor
    {
        RazonSocial = request.RazonSocial,
        Cuit = request.Cuit,
        Email = request.Email,
        Telefono = request.Telefono,
        Rubro = request.Rubro,
    });

    // Se devuelve lo que quedó guardado, no lo que mandó el cliente: el
    // store preserva FechaAlta y Activo, que no viajan en el request.
    return actualizado is null ? Results.NotFound() : Results.Ok(MapToResponse(actualizado));
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
