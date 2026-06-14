using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MessagePipe.Extensions.Mediator;

/// <summary>
/// 
/// </summary>
public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public IServiceCollection GetAddMessagePipeSender()
        {
            // 使用 TryAddSingleton 防止重复注册，Sender 本身是线程安全的且无状态
            services.TryAddSingleton<ISender, MessagePipeSender>();
            return services;
        }
    }
}
