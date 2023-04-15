using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class DependencyResolver 
{
    private static readonly Dictionary<Type, Type> types = new Dictionary<Type, Type>();
    private static readonly Dictionary<Type, object> instances = new Dictionary<Type, object>();

    public static void Register<T, U>() where U : T
    {
        types[typeof(T)] = typeof(U);
    }

    public static void Register<T>()
    {
        Register<T, T>();
    }

    public static void Register<T>(T t)
    {
        instances[typeof(T)] = t;
    }

    public static T Resolve<T>()
    {
        return (T)Resolve(typeof(T));
    }

    private static object Resolve(Type type)
    {
        object instance = null;
        if (instances.ContainsKey(type))
        {
            instance = instances[type];
        }
        else
        {
            if (types.ContainsKey(type))
            {
                var concreteType = types[type];
                var defaultConstructor = concreteType.GetConstructors()[0];
                var defaultParams = defaultConstructor.GetParameters();
                var parameters = defaultParams.Select(param => Resolve(param.ParameterType)).ToArray();
                instance = defaultConstructor.Invoke(parameters);
                instances[type] = instance;
            }
        }
        return instance;
    }
}
