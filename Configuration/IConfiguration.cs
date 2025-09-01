namespace ModuleKit.Configuration;

public interface IConfiguration
{
    T Get<T>(string key, T defaultValue = default!);
    void Set<T>(string key, T value);
    void Save();
    void Load();
    void Reset();
}