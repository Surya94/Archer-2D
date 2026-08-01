using UnityEngine;
using System;

public static class DependencyInjectionManager
{
    private static bool isInitialized = false;

    /// <summary>
    /// Initialize dependency injection system
    /// </summary>
    public static void Initialize()
    {
        if (isInitialized)
        {
            Debug.LogWarning("[DependencyInjectionManager] Already initialized");
            return;
        }

        try
        {
            Debug.Log("[DependencyInjectionManager] Initializing dependency injection...");

            // Register your dependencies here
            RegisterDependencies();

            isInitialized = true;
            Debug.Log("[DependencyInjectionManager] Dependency injection initialized successfully");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DependencyInjectionManager] Failed to initialize: {ex.Message}");
        }
    }

    /// <summary>
    /// Register all application dependencies
    /// </summary>
    private static void RegisterDependencies()
    {
        // Register your dependencies here
        DependencyResolver.Register<ScoreManager>();

        // Example of interface registration:
        // DependencyResolver.Register<IDataService, DataService>();

        Debug.Log("[DependencyInjectionManager] Dependencies registered");
    }

    /// <summary>
    /// Resolve and initialize core services
    /// </summary>
    public static void InitializeServices()
    {
        if (!isInitialized)
        {
            Debug.LogError("[DependencyInjectionManager] Must call Initialize() before InitializeServices()");
            return;
        }

        try
        {
            Debug.Log("[DependencyInjectionManager] Initializing services...");

            // Resolve and initialize your services
            var scoreManager = DependencyResolver.Resolve<ScoreManager>();
            if (scoreManager == null)
            {
                Debug.LogError("[DependencyInjectionManager] Failed to resolve ScoreManager");
                return;
            }

            // Initialize other services as needed

            Debug.Log("[DependencyInjectionManager] Services initialized successfully");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DependencyInjectionManager] Failed to initialize services: {ex.Message}");
        }
    }

    /// <summary>
    /// Cleanup dependency injection system
    /// </summary>
    public static void Cleanup()
    {
        if (!isInitialized)
            return;

        try
        {
            Debug.Log("[DependencyInjectionManager] Cleaning up dependency injection...");

            DependencyResolver.Clear();
            isInitialized = false;

            Debug.Log("[DependencyInjectionManager] Cleanup completed");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DependencyInjectionManager] Error during cleanup: {ex.Message}");
        }
    }

    /// <summary>
    /// Check if the system is properly initialized
    /// </summary>
    public static bool IsInitialized => isInitialized;

    /// <summary>
    /// Get debug information about the dependency injection system
    /// </summary>
    public static string GetDebugInfo()
    {
        var info = new System.Text.StringBuilder();
        info.AppendLine("=== Dependency Injection Manager Debug Info ===");
        info.AppendLine($"Initialized: {isInitialized}");

        if (isInitialized)
        {
            info.AppendLine(DependencyResolver.GetDebugInfo());
        }

        return info.ToString();
    }
}