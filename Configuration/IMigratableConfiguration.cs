namespace ModuleKit.Configuration;

public interface IMigratableConfiguration
{
    int ConfigVersion { get; set; }
    
    void Migrate(int fromVersion, int toVersion);
}