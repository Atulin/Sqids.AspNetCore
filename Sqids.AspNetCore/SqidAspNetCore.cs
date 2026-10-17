using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Sqids;

/// <summary>
/// MVC / Razor Pages binder for <see cref="Sqid"/> and <c>Sqid?</c> from route, query, form and header values.
/// A single instance is shared, so binding allocates nothing beyond what the value providers already did.
/// </summary>
public sealed class SqidModelBinder : IModelBinder
{
    public static readonly SqidModelBinder Instance = new();

    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var modelName = bindingContext.ModelName;
        var valueResult = bindingContext.ValueProvider.GetValue(modelName);
        if (valueResult == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }

        bindingContext.ModelState.SetModelValue(modelName, valueResult);

        var text = valueResult.FirstValue;
        var metadata = bindingContext.ModelMetadata;

        if (string.IsNullOrWhiteSpace(text))
        {
            if (metadata.IsNullableValueType)
            {
                bindingContext.Result = ModelBindingResult.Success(null);
            }
            else
            {
                bindingContext.ModelState.TryAddModelError(
                    modelName,
                    metadata.ModelBindingMessageProvider.ValueMustNotBeNullAccessor(valueResult.ToString()));
            }

            return Task.CompletedTask;
        }

        if (Sqid.TryParse(text.AsSpan(), out var sqid, out _))
        {
            bindingContext.Result = ModelBindingResult.Success(sqid);
        }
        else
        {
            bindingContext.ModelState.TryAddModelError(
                modelName,
                metadata.ModelBindingMessageProvider.ValueIsInvalidAccessor(valueResult.ToString()));
        }

        return Task.CompletedTask;
    }
}

public sealed class SqidModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Metadata.UnderlyingOrModelType == typeof(Sqid) ? SqidModelBinder.Instance : null;
    }
}

/// <summary>
/// Route constraint <c>{id:sqid}</c>: the segment must be a canonical Sqid, otherwise the route does not match (404 instead of 400).
/// Works in Razor Pages (<c>@page "{id:sqid}"</c>), MVC attribute routes and Minimal APIs.
/// </summary>
public sealed class SqidRouteConstraint : IRouteConstraint
{
    public const string Name = "sqid";

    public bool Match(HttpContext? httpContext, IRouter? route, string routeKey, RouteValueDictionary values, RouteDirection routeDirection)
    {
        if (!values.TryGetValue(routeKey, out var value) || value is null)
        {
            return false;
        }

        return value switch
        {
            Sqid => true, // URL generation with a Sqid route value
            string text => Sqid.TryParse(text.AsSpan(), CultureInfo.CurrentCulture, out _),
            _ => Sqid.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, out _),
        };
    }
}

public static class SqidServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers everything needed to use <see cref="Sqid"/> in Razor Pages, MVC and Minimal APIs:
        /// the process-wide codec, the MVC model binder and the <c>sqid</c> route constraint.
        /// JSON (<c>[JsonConverter]</c>) and Minimal API binding (<c>TryParse</c>) are built into the type and need no registration.
        /// </summary>
        public IServiceCollection AddSqids(Action<SqidOptions>? configure = null)
        {
            if (configure is not null)
            {
                var options = new SqidOptions();
                configure(options);
                Sqid.Configure(options);
            }

            services.AddSingleton(static _ => Sqid.Codec);
            services.Configure<MvcOptions>(static o => o.ModelBinderProviders.Insert(0, new SqidModelBinderProvider()));
            services.Configure<RouteOptions>(static o => o.ConstraintMap[SqidRouteConstraint.Name] = typeof(SqidRouteConstraint));
            return services;
        }
    }
}
