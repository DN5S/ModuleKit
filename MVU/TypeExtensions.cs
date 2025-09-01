using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;

namespace ModuleKit.MVU;

public static class TypeExtensions
{
    // Cache for expensive reflection results
    // ConcurrentDictionary is thread-safe for concurrent reads/writes
    private static readonly ConcurrentDictionary<Type, bool> IsRecordCache = new();
    private static readonly ConcurrentDictionary<Type, PropertyInfo?> PropertyCache = new();
    private static readonly ConcurrentDictionary<Type, MethodInfo?> MethodCache = new();
    private static readonly ConcurrentDictionary<Type, ConstructorInfo[]> ConstructorCache = new();
    
    public static bool IsRecord(this Type type)
    {
        // Check cache first
        return IsRecordCache.GetOrAdd(type, static t => DetermineIfRecord(t));
    }
    
    private static bool DetermineIfRecord(Type type)
    {
        try
        {
            // Records are reference types (classes), not value types
            if (type.IsValueType)
                return false;
            
            // Check if the type has EqualityContract property (records have this)
            var equalityContractProperty = PropertyCache.GetOrAdd(
                type,
                static t => t.GetProperty("EqualityContract", 
                    BindingFlags.NonPublic | BindingFlags.Instance)
            );
                
            // Check if the type has <Clone>$ method (another record indicator)
            var cloneMethod = MethodCache.GetOrAdd(
                type,
                static t => t.GetMethod("<Clone>$", 
                    BindingFlags.Public | BindingFlags.Instance)
            );
            
            // Check for a copy constructor (parameter of the same type)
            var constructors = ConstructorCache.GetOrAdd(type, static t => t.GetConstructors());
            var hasCopyConstructor = constructors
                .Any(c => c.GetParameters().Length == 1 && 
                         c.GetParameters()[0].ParameterType == type);
            
            // Consider it a record if it has at least two of these indicators
            // This reduces false positives from classes that might coincidentally have one
            var indicators = new[] 
            { 
                equalityContractProperty != null,
                cloneMethod != null,
                hasCopyConstructor
            }.Count(x => x);
            
            return indicators >= 2;
        }
        catch
        {
            // If reflection fails for any reason, assume it's not a record
            // This ensures the fallback mechanism in the calling code is used
            return false;
        }
    }
    
    /// <summary>
    /// Clear all caches - useful for testing or if types are dynamically generated
    /// </summary>
    public static void ClearCaches()
    {
        IsRecordCache.Clear();
        PropertyCache.Clear();
        MethodCache.Clear();
        ConstructorCache.Clear();
    }
}
