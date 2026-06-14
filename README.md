# MessagePipe.Extensions.Mediator

A high-performance, zero-allocation MediatR-style `ISender` wrapper for [MessagePipe](https://github.com/Cysharp/MessagePipe). 

在享受 MessagePipe **零分配（Zero-Allocation）** 和**极致性能**的同时，找回 MediatR 中**自动类型推断**和**统一发送入口**的丝滑开发体验。

## ✨ Features

- 🚀 **Automatic Type Inference**: No need to specify `<TRequest, TResponse>` manually. The compiler infers the response type automatically.
- ⚡ **Extreme Performance**: Uses `ConcurrentDictionary` and compiled delegates to achieve near-zero reflection overhead after the first call.
- 🗑️ **Zero Heap Allocation**: Uses `static` lambdas to completely prevent closure allocations (Zero GC).
- 🛡️ **Full Pipeline Support**: Perfectly integrates with MessagePipe's `IAsyncRequestHandler` filters (Authorization, Validation, Logging, etc.).
- 🧩 **Seamless Migration**: API design highly respects MediatR, drastically reducing the learning curve for teams migrating to MessagePipe.

## 📦 Installation

```bash
dotnet add package MessagePipe.Extensions.Mediator

🛠️ Usage

public record GetUserQuery(Guid Id) : IRequest<UserDto>;
public record CreateUserCommand(string Name) : ICommand;
public record UserDto(string Name);

using MessagePipe;
using MessagePipe.Extensions.Mediator;

builder.Services.AddMessagePipe(); // Register MessagePipe core
builder.Services.AddMessagePipeSender(); // Register our high-performance ISender

public class UserService
{
    private readonly ISender _sender;

    public UserService(ISender sender) => _sender = sender;

    public async Task DoWork()
    {
        // Compiler automatically infers UserDto!
        var user = await _sender.SendAsync(new GetUserQuery(Guid.NewGuid()));
        
        // Fire and forget commands
        await _sender.SendAsync(new CreateUserCommand("Alice"));
    }
}
