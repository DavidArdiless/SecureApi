using System.ComponentModel.DataAnnotations;

namespace SecureApi.Validation;

/// <summary>
/// Valida un DTO usando sus [DataAnnotations] antes de que el endpoint
/// se ejecute. Si algo no cumple, corta la ejecución con 400 y el
/// detalle de qué campo falló, sin necesidad de FluentValidation.
/// </summary>
public class ValidationFilter<T> : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var argument = context.Arguments.OfType<T>().FirstOrDefault();
        if (argument is null)
        {
            return await next(context);
        }

        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(argument);
        bool isValid = Validator.TryValidateObject(argument, validationContext, validationResults, validateAllProperties: true);

        if (!isValid)
        {
            var errors = validationResults
                .GroupBy(r => r.MemberNames.FirstOrDefault() ?? "general")
                .ToDictionary(g => g.Key, g => g.Select(r => r.ErrorMessage ?? "Valor inválido").ToArray());

            return Results.ValidationProblem(errors);
        }

        return await next(context);
    }
}

public static class ValidationFilterExtensions
{
    public static RouteHandlerBuilder WithValidation<T>(this RouteHandlerBuilder builder) where T : class
    {
        return builder.AddEndpointFilter<ValidationFilter<T>>();
    }
}
