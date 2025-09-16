# Paradigm.Logging.HttpDebugLogger

A convenient class for logging your HTTP messages. You can configure what is logged via the ``HttpDebugLoggerConfiguration`` class.

## Usage
You can use it on it's own, or combine it with a tool like Refit.

### Refit

Using DI:

```csharp
services.AddTransient<HttpDebugLogger>();
services.Configure //todo

services.AddRefitClient(...).AddHttpMessageHandler<HttpDebugLogger>();
```

Using ``RestService.For``:

```csharp
var settings = new RefitSettings()
{
    HttpMessageHandlerFactory = () => new HttpDebugLogger(...),
};
```

