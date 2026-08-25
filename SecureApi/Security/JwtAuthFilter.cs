using Microsoft.AspNetCore.Http.HttpResults;

namespace SecureApi.Security;

/// <summary>
/// Filtro para proteger endpoints de Minimal API con el JWT propio.
/// Uso: app.MapGet(...).RequireJwtAuth();
/// </summary>
public static class JwtAuthFilterExtensions
{
    public static TBuilder RequireJwtAuth<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder
    {
        builder.AddEndpointFilter(async (context, next) =>
        {
            var httpContext = context.HttpContext;
            string? authHeader = httpContext.Request.Headers.Authorization;

            if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return Results.Unauthorized();
            }

            string token = authHeader["Bearer ".Length..].Trim();
            var jwtService = httpContext.RequestServices.GetRequiredService<JwtService>();
            var principal = jwtService.ValidateToken(token);

            if (principal is null)
            {
                return Results.Unauthorized();
            }

            httpContext.User = principal;
            return await next(context);
        });

        return builder;
    }
}
