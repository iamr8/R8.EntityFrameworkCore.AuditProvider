using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace R8.EntityFrameworkAuditProvider
{
    public static class EntityFrameworkAuditProviderExtensions
    {
        /// <summary>
        /// Registers <see cref="EntityFrameworkAuditProviderInterceptor"/> as a singleton service to audit changes in <see cref="DbContext"/>.
        /// </summary>
        /// <param name="services">A <see cref="IServiceCollection"/> to add services to.</param>
        /// <param name="options">An <see cref="EntityFrameworkAuditProviderOptions"/> to configure audit provider.</param>
        /// <remarks>Don't forget to call <see cref="AddEntityFrameworkAuditProviderInterceptor"/> in your <see cref="DbContextOptionsBuilder"/>.</remarks>
        public static IServiceCollection AddEntityFrameworkAuditProvider(this IServiceCollection services, Action<EntityFrameworkAuditProviderOptions>? options = null)
        {
            var opt = new EntityFrameworkAuditProviderOptions();
            options?.Invoke(opt);
            services.TryAddSingleton<EntityFrameworkAuditProviderOptions>(_ =>
            {
                EntityFrameworkAuditProviderOptions.JsonStaticOptions = opt.JsonOptions;
                return opt;
            });
            services.TryAddSingleton<EntityFrameworkAuditProviderInterceptor>();
            return services;
        }

        /// <summary>
        /// Adds <see cref="EntityFrameworkAuditProviderInterceptor"/> to <see cref="DbContextOptionsBuilder"/> as an interceptor.
        /// </summary>
        /// <param name="builder">A <see cref="DbContextOptionsBuilder"/> to add interceptor to.</param>
        /// <param name="serviceProvider">A <see cref="IServiceProvider"/> to get <see cref="EntityFrameworkAuditProviderInterceptor"/> from.</param>
        public static DbContextOptionsBuilder AddEntityFrameworkAuditProviderInterceptor(this DbContextOptionsBuilder builder, IServiceProvider serviceProvider)
        {
            builder.AddInterceptors(serviceProvider.GetService<EntityFrameworkAuditProviderInterceptor>());
            return builder;
        }
    }
}