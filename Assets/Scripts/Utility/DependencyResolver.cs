using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

public static class DependencyResolver
{
    private static readonly Dictionary<Type, Type> types = new Dictionary<Type, Type>();
    private static readonly Dictionary<Type, object> instances = new Dictionary<Type, object>();
    private static readonly Dictionary<Type, ConstructorInfo> constructorCache = new Dictionary<Type, ConstructorInfo>();
    private static readonly HashSet<Type> resolutionStack = new HashSet<Type>();
    private static readonly object lockObject = new object();

    /// <summary>
    /// Register interface-implementation mapping
    /// </summary>
    public static void Register<TInterface, TImplementation>()
        where TImplementation : class, TInterface
    {
        lock (lockObject)
        {
            types[typeof(TInterface)] = typeof(TImplementation);
            Debug.Log($"[DependencyResolver] Registered {typeof(TInterface).Name} -> {typeof(TImplementation).Name}");
        }
    }

    /// <summary>
    /// Register concrete type
    /// </summary>
    public static void Register<T>() where T : class
    {
        Register<T, T>();
    }

    /// <summary>
    /// Register singleton instance
    /// </summary>
    public static void Register<T>(T instance) where T : class
    {
        if (instance == null)
        {
            Debug.LogError($"[DependencyResolver] Cannot register null instance for type {typeof(T).Name}");
            return;
        }

        lock (lockObject)
        {
            instances[typeof(T)] = instance;
            Debug.Log($"[DependencyResolver] Registered singleton instance of {typeof(T).Name}");
        }
    }

    /// <summary>
    /// Resolve dependency with comprehensive error handling
    /// </summary>
    public static T Resolve<T>() where T : class
    {
        return Resolve(typeof(T)) as T;
    }

    /// <summary>
    /// Check if type can be resolved
    /// </summary>
    public static bool CanResolve<T>()
    {
        return CanResolve(typeof(T));
    }

    public static bool CanResolve(Type type)
    {
        lock (lockObject)
        {
            return instances.ContainsKey(type) || types.ContainsKey(type) || HasDefaultConstructor(type);
        }
    }

    private static object Resolve(Type type)
    {
        if (type == null)
        {
            Debug.LogError("[DependencyResolver] Cannot resolve null type");
            return null;
        }

        lock (lockObject)
        {
            // Check for circular dependencies
            if (resolutionStack.Contains(type))
            {
                string stackTrace = string.Join(" -> ", resolutionStack.Select(t => t.Name));
                Debug.LogError($"[DependencyResolver] Circular dependency detected: {stackTrace} -> {type.Name}");
                return null;
            }

            try
            {
                resolutionStack.Add(type);

                // Return existing instance
                if (instances.TryGetValue(type, out object existingInstance))
                {
                    return existingInstance;
                }

                // Resolve from registered types
                if (types.TryGetValue(type, out Type concreteType))
                {
                    return CreateInstance(concreteType);
                }

                // Try to create concrete type directly
                if (!type.IsInterface && !type.IsAbstract)
                {
                    return CreateInstance(type);
                }

                Debug.LogError($"[DependencyResolver] Cannot resolve type {type.Name}. No registration found.");
                return null;
            }
            finally
            {
                resolutionStack.Remove(type);
            }
        }
    }

    private static object CreateInstance(Type type)
    {
        try
        {
            ConstructorInfo constructor = GetBestConstructor(type);
            if (constructor == null)
            {
                Debug.LogError($"[DependencyResolver] No suitable constructor found for {type.Name}");
                return null;
            }

            ParameterInfo[] parameters = constructor.GetParameters();
            object[] args = new object[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                args[i] = Resolve(parameters[i].ParameterType);
                if (args[i] == null && !parameters[i].ParameterType.IsValueType)
                {
                    Debug.LogError($"[DependencyResolver] Failed to resolve parameter {parameters[i].Name} of type {parameters[i].ParameterType.Name} for {type.Name}");
                    return null;
                }
            }

            object instance = constructor.Invoke(args);
            instances[type] = instance; // Cache the instance

            Debug.Log($"[DependencyResolver] Created instance of {type.Name}");
            return instance;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DependencyResolver] Error creating instance of {type.Name}: {ex.Message}");
            return null;
        }
    }

    private static ConstructorInfo GetBestConstructor(Type type)
    {
        if (constructorCache.TryGetValue(type, out ConstructorInfo cachedConstructor))
        {
            return cachedConstructor;
        }

        ConstructorInfo[] constructors = type.GetConstructors();

        // Prefer parameterless constructor
        ConstructorInfo parameterlessConstructor = constructors.FirstOrDefault(c => c.GetParameters().Length == 0);
        if (parameterlessConstructor != null)
        {
            constructorCache[type] = parameterlessConstructor;
            return parameterlessConstructor;
        }

        // Otherwise, take the first constructor
        ConstructorInfo firstConstructor = constructors.FirstOrDefault();
        if (firstConstructor != null)
        {
            constructorCache[type] = firstConstructor;
        }

        return firstConstructor;
    }

    private static bool HasDefaultConstructor(Type type)
    {
        return type.GetConstructors().Any(c => c.GetParameters().Length == 0);
    }

    /// <summary>
    /// Clear all registrations and instances
    /// </summary>
    public static void Clear()
    {
        lock (lockObject)
        {
            types.Clear();
            instances.Clear();
            constructorCache.Clear();
            resolutionStack.Clear();
            Debug.Log("[DependencyResolver] Cleared all registrations");
        }
    }

    /// <summary>
    /// Get debug information about current registrations
    /// </summary>
    public static string GetDebugInfo()
    {
        lock (lockObject)
        {
            var info = new System.Text.StringBuilder();
            info.AppendLine("=== Dependency Resolver Debug Info ===");
            info.AppendLine($"Registered Types: {types.Count}");
            info.AppendLine($"Cached Instances: {instances.Count}");

            foreach (var kvp in types)
            {
                info.AppendLine($"  {kvp.Key.Name} -> {kvp.Value.Name}");
            }

            return info.ToString();
        }
    }
}