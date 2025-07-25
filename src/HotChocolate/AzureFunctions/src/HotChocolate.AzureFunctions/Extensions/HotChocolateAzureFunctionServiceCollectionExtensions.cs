using ChilliCream.Nitro.App;
using HotChocolate;
using HotChocolate.AspNetCore;
using HotChocolate.AzureFunctions;
using HotChocolate.Execution;
using HotChocolate.Execution.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using MiddlewareFactory = HotChocolate.AspNetCore.MiddlewareFactory;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides DI extension methods to configure a GraphQL server.
/// </summary>
public static class HotChocolateAzureFunctionServiceCollectionExtensions
{
    /// <summary>
    /// Adds a GraphQL server and Azure Functions integration services to the DI.
    /// </summary>
    /// <param name="services">
    /// The <see cref="IServiceCollection"/>.
    /// </param>
    /// <param name="maxAllowedRequestSize">
    /// The max allowed GraphQL request size.
    /// </param>
    /// <param name="apiRoute">
    /// The API route that was used in the GraphQL Azure Function.
    /// </param>
    /// <param name="schemaName">
    /// The name of the schema that shall be used by this Azure Function.
    /// </param>
    /// <returns>
    /// Returns the <see cref="IRequestExecutorBuilder"/> so that configuration can be chained.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// The <see cref="IServiceCollection"/> is <c>null</c>.
    /// </exception>
    public static IRequestExecutorBuilder AddGraphQLFunction(
        this IServiceCollection services,
        int maxAllowedRequestSize = GraphQLAzureFunctionsConstants.DefaultMaxRequests,
        string apiRoute = GraphQLAzureFunctionsConstants.DefaultGraphQLRoute,
        string? schemaName = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var executorBuilder =
            services.AddGraphQLServer(maxAllowedRequestSize: maxAllowedRequestSize);

        // Register AzFunc Custom Binding Extensions for In-Process Functions.
        // NOTE: This does not work for Isolated Process due to (but is not harmful at all to
        // an isolated process; it just remains dormant):
        // 1) Bindings always execute in-process and values must be marshaled between
        // the Host Process & the Isolated Process Worker!
        // 2) Currently only String values are supported (due to the above complexities).
        // More info here (using Blob binding docs):
        // https://docs.microsoft.com/en-us/azure/azure-functions/functions-bindings-storage-blob-input#usage
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IExtensionConfigProvider, GraphQLExtensions>());

        // Add the Request Executor Dependency...
        services.AddAzureFunctionsGraphQLRequestExecutor(apiRoute, schemaName);

        return executorBuilder;
    }

    /// <summary>
    /// Internal method to add the Request Executor dependency for Azure Functions both
    /// in-process and isolate-process. Normal configuration should use AddGraphQLFunction()
    /// extension instead that correctly calls this internally.
    /// </summary>
    private static IServiceCollection AddAzureFunctionsGraphQLRequestExecutor(
        this IServiceCollection services,
        string apiRoute = GraphQLAzureFunctionsConstants.DefaultGraphQLRoute,
        string? schemaName = null)
    {
        services.AddSingleton<IGraphQLRequestExecutor>(sp =>
        {
            PathString path = apiRoute.TrimEnd('/');
            var options = new GraphQLServerOptions();

            foreach (var configure in sp.GetServices<Action<GraphQLServerOptions>>())
            {
                configure(options);
            }

            // We need to set the ServeMode to Embedded to ensure that the GraphQL IDE is
            // working since the isolation mode does not allow us to take control over the response
            // object.
            options.Tool.ServeMode = GraphQLToolServeMode.Embedded;

            schemaName ??= ISchemaDefinition.DefaultName;
            var executorProvider = sp.GetRequiredService<IRequestExecutorProvider>();
            var executorEvents = sp.GetRequiredService<IRequestExecutorEvents>();
            var formOptions = sp.GetRequiredService<IOptions<FormOptions>>();
            var executor = new HttpRequestExecutorProxy(executorProvider, executorEvents, schemaName);

            var pipeline = new PipelineBuilder()
                .Use(MiddlewareFactory.CreateCancellationMiddleware())
                .Use(MiddlewareFactory.CreateWebSocketSubscriptionMiddleware(executor))
                .Use(MiddlewareFactory.CreateHttpPostMiddleware(executor))
                .Use(MiddlewareFactory.CreateHttpMultipartMiddleware(executor, formOptions))
                .Use(MiddlewareFactory.CreateHttpGetMiddleware(executor))
                .Use(MiddlewareFactory.CreateHttpGetSchemaMiddleware(executor, path, MiddlewareRoutingType.Integrated))
                .UseNitroApp(path)
                .Compile(sp);

            return new DefaultGraphQLRequestExecutor(pipeline, options);
        });

        return services;
    }

    /// <summary>
    /// Modifies the GraphQL functions options.
    /// </summary>
    /// <param name="builder">
    /// The <see cref="IRequestExecutorBuilder"/>.
    /// </param>
    /// <param name="configure">
    /// A delegate to modify the options.
    /// </param>
    /// <returns>
    /// Returns <see cref="IRequestExecutorBuilder"/> so that configuration can be chained.
    /// </returns>
    public static IRequestExecutorBuilder ModifyFunctionOptions(
        this IRequestExecutorBuilder builder,
        Action<GraphQLServerOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddSingleton(configure);
        return builder;
    }

    private static PipelineBuilder UseNitroApp(
        this PipelineBuilder requestPipeline,
        PathString path)
    {
        ArgumentNullException.ThrowIfNull(requestPipeline);

        path = path.ToString().TrimEnd('/');
        var fileProvider = CreateFileProvider();
        var forwarderAccessor = new HttpForwarderAccessor();

        return requestPipeline
            .Use(next =>
            {
                var middleware = new NitroAppOptionsFileMiddleware(next, path);
                return middleware.Invoke;
            })
            .Use(next =>
            {
                var middleware = new NitroAppCdnMiddleware(next, path, forwarderAccessor);
                return middleware.Invoke;
            })
            .Use(next =>
            {
                var middleware = new NitroAppDefaultFileMiddleware(next, fileProvider, path);
                return middleware.Invoke;
            })
            .Use(next =>
            {
                var middleware = new NitroAppStaticFileMiddleware(next, fileProvider, path);
                return middleware.Invoke;
            });
    }

    private static IFileProvider CreateFileProvider()
    {
        var type = typeof(NitroAppStaticFileMiddleware);
        var resourceNamespace = type.Namespace + ".Resources";

        return new EmbeddedFileProvider(type.Assembly, resourceNamespace);
    }
}
