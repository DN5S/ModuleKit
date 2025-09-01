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
        
        // Always include base type
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
            // No type discriminator, deserialize as base class
            return JsonSerializer.Deserialize<ModuleConfiguration>(root.GetRawText(), options);
        }
        
        var typeName = typeElement.GetString();
        if (string.IsNullOrEmpty(typeName))
        {
            return JsonSerializer.Deserialize<ModuleConfiguration>(root.GetRawText(), options);
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
        
        // Deserialize as the specific type
        return (ModuleConfiguration?)JsonSerializer.Deserialize(root.GetRawText(), targetType, options);
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
