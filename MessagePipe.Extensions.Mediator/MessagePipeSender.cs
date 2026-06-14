using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Reflection;

namespace MessagePipe.Extensions.Mediator;

/// <summary>
/// 请求标记接口，TResponse 为协变返回类型
/// </summary>
public interface IRequest<out TResponse> { }

/// <summary>
/// 无返回值占位类型
/// </summary>
public struct Unit { }

/// <summary>
/// 无返回值命令接口（默认返回 Unit）
/// </summary>
public interface ICommand : ICommand<Unit> { }

/// <summary>
/// 命令接口，继承 IRequest，用于 CQRS 写操作
/// </summary>
public interface ICommand<out TResponse> : IRequest<TResponse> { }

/// <summary>
/// 消息发送器抽象，解耦业务层对具体中介框架的依赖
/// </summary>
public interface ISender
{
    /// <summary>
    /// 发送请求并返回带类型的结果
    /// </summary>
    Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 发送无返回值命令
    /// </summary>
    Task SendAsync(ICommand request, CancellationToken cancellationToken = default);
}

/// <summary>
/// 基于 MessagePipe 的发送器实现。
/// 通过反射 + 缓存将 IRequest&lt;TResponse&gt; 路由到对应的 IAsyncRequestHandler，
/// 避免直接依赖 MessagePipe 的 ISender，保持框架可替换性。
/// </summary>
public class MessagePipeSender(IServiceProvider provider) : ISender
{
    /// <summary>
    /// 执行器缓存：Key = (请求类型, 响应类型)，Value = 委托化的执行函数。
    /// 避免每次请求都做反射查找，提升性能。
    /// </summary>
    private readonly ConcurrentDictionary<(Type Request, Type Response), Func<object, CancellationToken, IServiceProvider, Task<object>>> _cache = new();

    public async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestType = request.GetType();
        var key = (requestType, typeof(TResponse));

        // 从缓存获取或首次构建执行器委托
        var executor = _cache.GetOrAdd(key, static k =>
        {
            var (reqType, resType) = k;
            // 通过反射调用泛型方法 CreateExecutor<TReq, TRes>，生成强类型委托
            var createMethod = typeof(MessagePipeSender).GetMethod(nameof(CreateExecutor), BindingFlags.NonPublic | BindingFlags.Static)!;
            var genericCreate = createMethod.MakeGenericMethod(reqType, resType);
            return (Func<object, CancellationToken, IServiceProvider, Task<object>>)genericCreate.Invoke(null, null)!;
        });

        var result = await executor(request, cancellationToken, provider);
        return (TResponse)result;
    }

    /// <summary>
    /// 无返回值命令转发：统一走 SendAsync&lt;Unit&gt;
    /// </summary>
    public Task SendAsync(ICommand request, CancellationToken cancellationToken = default)
        => SendAsync<Unit>(request, cancellationToken);

    /// <summary>
    /// 构建强类型执行器委托。
    /// 将 IAsyncRequestHandler&lt;TReq, TRes&gt; 从 DI 容器解析并调用，
    /// 返回 object 以便统一存入缓存。
    /// </summary>
    private static Func<object, CancellationToken, IServiceProvider, Task<object>> CreateExecutor<TReq, TRes>()
    {
        return async (req, ct, provider) =>
        {
            // 从 Scoped 容器解析对应的 Handler 实例
            var handler = provider.GetRequiredService<IAsyncRequestHandler<TReq, TRes>>();
            var result = await handler.InvokeAsync((TReq)req, ct);
            return result!;
        };
    }
}
