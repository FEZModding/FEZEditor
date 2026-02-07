using System.Reflection;
using Microsoft.Xna.Framework;

namespace FezEditor.Tools;

public static class GameExtensions
{
    private static readonly Lock Lock = new();

    private static readonly List<object> Services = new();

    #region Service Creation with DI

    public static T CreateService<T>(this Game game) where T : class
    {
        var service = CreateInstance<T>(game);
        game.AddService(service);
        return service;
    }

    public static T CreateService<TInterface, T>(this Game game)
        where T : class, TInterface
        where TInterface : class
    {
        var service = CreateInstance<T>(game);
        game.AddService<TInterface>(service);
        return service;
    }

    #endregion

    #region Component Creation with DI

    public static T CreateComponent<T>(this Game game) where T : class, IGameComponent
    {
        var component = CreateInstance<T>(game);
        game.AddComponent(component);
        return component;
    }

    #endregion

    #region Instance Creation

    private static T CreateInstance<T>(Game game) where T : class
    {
        return (T)CreateInstance(game, typeof(T));
    }

    private static object CreateInstance(Game game, Type type)
    {
        var constructors = type.GetConstructors();
        foreach (var ctor in constructors.OrderByDescending(c => c.GetParameters().Length))
        {
            if (TryResolveConstructor(game, ctor, out var args))
            {
                return ctor.Invoke(args);
            }
        }

        throw new InvalidOperationException(
            $"Cannot create {type.Name}: no constructor with resolvable dependencies");
    }

    private static bool TryResolveConstructor(Game game, ConstructorInfo ctor, out object?[] args)
    {
        var parameters = ctor.GetParameters();
        args = new object?[parameters.Length];

        for (var i = 0; i < parameters.Length; i++)
        {
            var paramType = parameters[i].ParameterType;
            if (TryResolveParameter(game, paramType, out var value))
            {
                args[i] = value;
            }
            else if (parameters[i].HasDefaultValue)
            {
                args[i] = parameters[i].DefaultValue;
            }
            else
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryResolveParameter(Game game, Type paramType, out object? value)
    {
        // Special case: inject Game itself
        if (paramType == typeof(Game) || paramType.IsInstanceOfType(game))
        {
            value = game;
            return true;
        }

        // Handle Lazy<T> for deferred resolution
        if (paramType.IsGenericType && paramType.GetGenericTypeDefinition() == typeof(Lazy<>))
        {
            var innerType = paramType.GetGenericArguments()[0];
            value = CreateLazyResolver(game, innerType);
            return true;
        }

        // Try to get existing service
        var service = game.Services.GetService(paramType);
        if (service != null)
        {
            value = service;
            return true;
        }

        value = null;
        return false;
    }

    private static object CreateLazyResolver(Game game, Type serviceType)
    {
        var lazyType = typeof(Lazy<>).MakeGenericType(serviceType);
        var funcType = typeof(Func<>).MakeGenericType(serviceType);

        var resolver = () => game.Services.GetService(serviceType)
            ?? throw new InvalidOperationException($"Service {serviceType.Name} not registered");

        var typedDelegate = Delegate.CreateDelegate(funcType,
            resolver.Target,
            resolver.Method);

        return Activator.CreateInstance(lazyType, typedDelegate)!;
    }

    #endregion

    #region Service Management

    private static void AddService<T>(this Game game, T service)
    {
        if (service is not IUpdateable &&
            service is not IDrawable &&
            service is not IGameComponent &&
            service is not IComparable)
        {
            game.Services.AddService(typeof(T), service);
            Services.Add(service!);
        }
    }

    public static T GetService<T>(this Game game) where T : class
    {
        var @object = game.Services.GetService(typeof(T));
        if (@object is not T service)
        {
            throw new InvalidCastException($"Could not find or cast service {typeof(T).FullName}");
        }
        
        return service;
    }

    public static T? TryGetService<T>(this Game game) where T : class
    {
        return game.Services.GetService(typeof(T)) as T;
    }

    public static void RemoveService<T>(this Game game)
    {
        var @object = game.Services.GetService(typeof(T));
        if (@object is T service)
        {
            game.Services.RemoveService(typeof(T));
            if (service is IDisposable disposable)
            {
                disposable.Dispose();
            }
            Services.Remove(service);
        }
    }

    public static void RemoveServices(this Game game)
    {
        foreach (var service in Services)
        {
            game.Services.RemoveService(service.GetType());
            if (service is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        Services.Clear();
    }

    #endregion

    #region Component Management

    public static void AddComponent<T>(this Game game, T component) where T : IGameComponent
    {
        lock (Lock)
        {
            game.Components.Add(component);
        }
    }

    public static T GetComponent<T>(this Game game) where T : IGameComponent
    {
        lock (Lock)
        {
            return game.Components.OfType<T>().First();
        }
    }

    public static T? TryGetComponent<T>(this Game game) where T : class, IGameComponent
    {
        lock (Lock)
        {
            return game.Components.OfType<T>().FirstOrDefault();
        }
    }

    public static void RemoveComponent<T>(this Game game, T component) where T : IGameComponent
    {
        if (component is IDisposable disposable)
        {
            disposable.Dispose();
        }

        lock (Lock)
        {
            game.Components.Remove(component);
        }
    }

    #endregion
}