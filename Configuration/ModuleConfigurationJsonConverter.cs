using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ModuleKit.Configuration;

public class ModuleConfigurationJsonConverter : JsonConverter<ModuleConfiguration>
{
    private const string TypeDiscriminator = "$type";
    private readonly Dictionary<string, Type> safeTypeRegistry;
    
    public ModuleConfigurationJsonConverter(Dictionary<string, Type>? safeTypeRegistry = null)
    {
        this.safeTypeRegistry = safeTypeRegistry ?? new Dictionary<string, Type>();
        
        // Always include a base type
        if (!this.safeTypeRegistry.ContainsKey(typeof(ModuleConfiguration).FullName!))
        {
            this.safeTypeRegistry[typeof(ModuleConfiguration).FullName!] = typeof(ModuleConfiguration);
        }
    }
    
    public override ModuleConfiguration? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected start of object");
        }
        
        using var jsonDoc = JsonDocument.ParseValue(ref reader);
        var root = jsonDoc.RootElement;
        
        if (!root.TryGetProperty(TypeDiscriminator, out var typeElement))
        {
            // No type discriminator, deserialize as a base class with validation
            var config = JsonSerializer.Deserialize<ModuleConfiguration>(root.GetRawText(), options);
            ValidateDeserializedObject(config);
            return config;
        }
        
        var typeName = typeElement.GetString();
        if (string.IsNullOrEmpty(typeName))
        {
            var config = JsonSerializer.Deserialize<ModuleConfiguration>(root.GetRawText(), options);
            ValidateDeserializedObject(config);
            return config;
        }
        
        // Sanitize type name to prevent path traversal or assembly loading attacks
        if (typeName.Contains("..") || typeName.Contains("/") || typeName.Contains("\\") || 
            typeName.Contains(",") || typeName.Contains("[") || typeName.Contains("]"))
        {
            throw new JsonException($"Invalid type name format: '{typeName}'. Potential security risk detected.");
        }
        
        // Only allow types from the safe registry
        if (!safeTypeRegistry.TryGetValue(typeName, out var targetType))
        {
            throw new JsonException($"Type '{typeName}' is not in the safe type registry. Deserialization blocked for security.");
        }
        
        // Verify type still derives from ModuleConfiguration
        if (!typeof(ModuleConfiguration).IsAssignableFrom(targetType))
        {
            throw new JsonException($"Type '{typeName}' does not derive from ModuleConfiguration");
        }
        
        // Additional security check: ensure the type is not a system type
        if (targetType.Namespace?.StartsWith("System") == true || 
            targetType.Namespace?.StartsWith("Microsoft") == true)
        {
            throw new JsonException($"Cannot deserialize system type '{typeName}' for security reasons");
        }
        
        try
        {
            // Deserialize as the specific type with additional error handling
            var result = (ModuleConfiguration?)JsonSerializer.Deserialize(root.GetRawText(), targetType, options);
            ValidateDeserializedObject(result);
            return result;
        }
        catch (Exception ex) when (ex is not JsonException)
        {
            // Wrap non-JSON exceptions to prevent information leakage
            throw new JsonException($"Failed to deserialize configuration of type '{targetType.Name}'", ex);
        }
    }
    
    private static void ValidateDeserializedObject(ModuleConfiguration? config)
    {
        if (config == null) return;
        
        // Validate basic properties are within expected bounds
        if (config.ModuleName.Length > 256)
        {
            throw new JsonException("Module name exceeds maximum length");
        }
    }
    
    public override void Write(Utf8JsonWriter writer, ModuleConfiguration value, JsonSerializerOptions options)
    {
        var actualType = value.GetType();
        
        writer.WriteStartObject();
        
        // Write type discriminator if this is a derived type
        if (actualType != typeof(ModuleConfiguration))
        {
            writer.WriteString(TypeDiscriminator, actualType.FullName);
        }
        
        // Write all properties
        var properties = actualType.GetProperties();
        foreach (var property in properties)
        {
            if (property.Name == TypeDiscriminator || !property.CanRead)
                continue;
            
            var propValue = property.GetValue(value);
            writer.WritePropertyName(property.Name);
            JsonSerializer.Serialize(writer, propValue, property.PropertyType, options);
        }
        
        writer.WriteEndObject();
    }
    
    public void RegisterSafeType(Type type)
    {
        if (!typeof(ModuleConfiguration).IsAssignableFrom(type))
        {
            throw new ArgumentException($"Type {type.FullName} must derive from ModuleConfiguration");
        }
        
        safeTypeRegistry[type.FullName!] = type;
    }
    
    public void RegisterSafeTypes(IEnumerable<Type> types)
    {
        foreach (var type in types)
        {
            RegisterSafeType(type);
        }
    }
}
