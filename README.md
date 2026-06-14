# MessagePipe.Extensions.Mediator

`MessagePipe.Extensions.Mediator` 是一个基于 [MessagePipe](https://github.com/Cysharp/MessagePipe) 的轻量级 Mediator / CQRS 扩展包。

它提供一个接近 MediatR 风格的 `ISender` 抽象，让业务层只依赖 `IRequest<TResponse>`、`ICommand` 和 `ISender`，而不需要直接依赖 MessagePipe 的发送接口。

## 特性

- MediatR 风格的 `ISender.SendAsync(...)` 调用方式
- 支持有返回值请求：`IRequest<TResponse>`
- 支持无返回值命令：`ICommand`
- 基于 MessagePipe 的 `IAsyncRequestHandler<TRequest, TResponse>` 执行请求处理
- 通过 `(RequestType, ResponseType)` 缓存执行委托，减少重复反射开销
- 适合 CQRS、应用层命令和查询分发场景

## 环境要求

- .NET 10.0
- MessagePipe 1.8.2
- Microsoft.Extensions.DependencyInjection.Abstractions 10.0.9

## 安装

通过 NuGet 安装：

```bash
dotnet add package MessagePipe.Extensions.Mediator
```

如果使用项目引用：

```xml
<ProjectReference Include="..\MessagePipe.Extensions.Mediator\MessagePipe.Extensions.Mediator.csproj" />
```

## 快速开始

### 1. 定义请求

```csharp
using MessagePipe.Extensions.Mediator;

public sealed record GetUserQuery(Guid UserId) : IRequest<UserDto>;

public sealed record UserDto(Guid Id, string Name);
```

### 2. 实现 MessagePipe Handler

```csharp
using MessagePipe;

public sealed class GetUserQueryHandler : IAsyncRequestHandler<GetUserQuery, UserDto>
{
    public ValueTask<UserDto> InvokeAsync(GetUserQuery request, CancellationToken cancellationToken = default)
    {
        var user = new UserDto(request.UserId, "Alice");
        return ValueTask.FromResult(user);
    }
}
```

### 3. 注册服务

```csharp
using MessagePipe;
using MessagePipe.Extensions.Mediator;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services.AddMessagePipe();
services.AddTransient<IAsyncRequestHandler<GetUserQuery, UserDto>, GetUserQueryHandler>();
services.GetAddMessagePipeSender();

var provider = services.BuildServiceProvider();
```

### 4. 发送请求

```csharp
var sender = provider.GetRequiredService<ISender>();

var user = await sender.SendAsync(new GetUserQuery(Guid.NewGuid()));

Console.WriteLine(user.Name);
```

## 命令示例

对于无返回值命令，可以实现 `ICommand`：

```csharp
using MessagePipe.Extensions.Mediator;

public sealed record CreateUserCommand(string Name) : ICommand;
```

无返回值命令会自动映射为 `ICommand<Unit>`：

```csharp
using MessagePipe;
using MessagePipe.Extensions.Mediator;

public sealed class CreateUserCommandHandler : IAsyncRequestHandler<CreateUserCommand, Unit>
{
    public async ValueTask<Unit> InvokeAsync(CreateUserCommand request, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        return default;
    }
}
```

注册并发送：

```csharp
services.AddTransient<IAsyncRequestHandler<CreateUserCommand, Unit>, CreateUserCommandHandler>();

await sender.SendAsync(new CreateUserCommand("Alice"));
```

## API 概览

### `IRequest<TResponse>`

有返回值请求的标记接口。

```csharp
public interface IRequest<out TResponse> { }
```

### `ICommand`

无返回值命令接口，默认返回 `Unit`。

```csharp
public interface ICommand : ICommand<Unit> { }
```

### `ICommand<TResponse>`

有返回值命令接口，继承自 `IRequest<TResponse>`。

```csharp
public interface ICommand<out TResponse> : IRequest<TResponse> { }
```

### `ISender`

业务层推荐依赖的发送器抽象。

```csharp
public interface ISender
{
    Task<TResponse> SendAsync<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken = default);

    Task SendAsync(
        ICommand request,
        CancellationToken cancellationToken = default);
}
```

## 设计说明

`MessagePipeSender` 会根据运行时请求类型和响应类型解析对应的：

```csharp
IAsyncRequestHandler<TRequest, TResponse>
```

首次发送某个请求类型时，会通过反射构建强类型执行委托，并缓存到 `ConcurrentDictionary` 中。之后相同 `(RequestType, ResponseType)` 的请求会复用缓存委托，避免每次调用都重复反射。

这使业务代码可以保持简洁：

```csharp
public sealed class UserService(ISender sender)
{
    public Task<UserDto> GetUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return sender.SendAsync(new GetUserQuery(userId), cancellationToken);
    }
}
```

## 适用场景

- 希望使用 MessagePipe 的性能和 DI 集成
- 希望业务层保持 MediatR 风格的 `ISender` 抽象
- CQRS 命令 / 查询分发
- 应用层 Use Case、Command、Query 解耦
- 希望后续可替换底层 mediator 实现

## 注意事项

- 请求处理器必须注册到 DI 容器中，否则发送请求时会抛出解析异常。
- `ICommand` 默认会被当作 `ICommand<Unit>` 处理，因此对应 Handler 需要实现 `IAsyncRequestHandler<TCommand, Unit>`。
- 当前包面向 `net10.0`。

## License

MIT
