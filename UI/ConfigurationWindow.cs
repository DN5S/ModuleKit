using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using TLink.Core.Configuration;
using TLink.Core.Module;

namespace TLink.Core.UI;

public class ConfigurationWindow : Window, IDisposable
{
    private readonly ModuleManager moduleManager;
    private readonly PluginConfiguration configuration;
    private string selectedModuleName = string.Empty;
    
    public ConfigurationWindow(ModuleManager moduleManager, PluginConfiguration configuration) 
        : base("TataruLink Configuration###TataruLinkConfig", ImGuiWindowFlags.None)
    {
        this.moduleManager = moduleManager ?? throw new ArgumentNullException(nameof(moduleManager));
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        
        Size = new Vector2(700, 500);
        SizeCondition = ImGuiCond.FirstUseEver;
    }
    
    public override void Draw()
    {
        using var tabBar = ImRaii.TabBar("ConfigTabs");
        if (!tabBar) return;
        
        // General settings tab
        using (var generalTab = ImRaii.TabItem("General"))
        {
            if (generalTab)
                DrawGeneralSettings();
        }
        
        // Module tabs
        using (var modulesTab = ImRaii.TabItem("Modules"))
        {
            if (modulesTab)
                DrawModuleSettings();
        }
        
        // Individual module configuration tabs
        foreach (var module in moduleManager.LoadedModules)
        {
            using var moduleTab = ImRaii.TabItem($"{module.Name} Settings");
            if (!moduleTab) continue;
            
            DrawModuleConfiguration(module);
        }
    }
    
    private void DrawGeneralSettings()
    {
        ImGui.Text("General Plugin Settings");
        ImGui.Separator();
        
        if (LayoutHelpers.BeginSection("Plugin Information"))
        {
            ImGui.Text("TataruLink - Translation Module System");
            ImGui.TextDisabled("A modular translation plugin for FFXIV");
            ImGui.Spacing();
            ImGui.Text($"Configuration Version: {configuration.Version}");
            LayoutHelpers.EndSection();
        }
        
        ImGui.Spacing();
        
        if (LayoutHelpers.BeginSection("Configuration Management"))
        {
            if (ImGui.Button("Save Configuration"))
            {
                configuration.Save();
                ImGui.OpenPopup("SaveConfirmation");
            }
            
            ImGui.SameLine();
            
            if (ImGui.Button("Reset to Defaults"))
            {
                ImGui.OpenPopup("ResetConfirmation");
            }
            
            ImGui.SameLine();
            
            if (ImGui.Button("Export Configuration"))
            {
                // Export logic would go here
                ImGui.OpenPopup("ExportInfo");
            }
            
            ImGui.SameLine();
            
            if (ImGui.Button("Import Configuration"))
            {
                // Import logic would go here
                ImGui.OpenPopup("ImportWarning");
            }
            LayoutHelpers.EndSection();
        }
        
        // Popups
        using (var popup = ImRaii.Popup("SaveConfirmation"))
        {
            if (popup)
            {
                ImGui.Text("Configuration saved successfully!");
                if (ImGui.Button("OK"))
                    ImGui.CloseCurrentPopup();
            }
        }
        
        using (var resetPopup = ImRaii.Popup("ResetConfirmation"))
        {
            if (resetPopup)
            {
                ImGui.Text("Are you sure you want to reset all settings to defaults?");
                ImGui.TextColored(LayoutHelpers.Colors.Warning, "This action cannot be undone!");
                
                LayoutHelpers.DrawCenteredButtons(
                    ("Yes, Reset", () => {
                        configuration.Reset();
                        ImGui.CloseCurrentPopup();
                    }),
                    ("Cancel", ImGui.CloseCurrentPopup)
                );
            }
        }
        
        using (var exportPopup = ImRaii.Popup("ExportInfo"))
        {
            if (exportPopup)
            {
                ImGui.Text("Configuration export is not yet implemented.");
                if (ImGui.Button("OK"))
                    ImGui.CloseCurrentPopup();
            }
        }
        
        using (var importPopup = ImRaii.Popup("ImportWarning"))
        {
            if (importPopup)
            {
                ImGui.Text("Configuration import is not yet implemented.");
                if (ImGui.Button("OK"))
                    ImGui.CloseCurrentPopup();
            }
        }
    }
    
    private void DrawModuleSettings()
    {
        ImGui.Text("Module Management");
        ImGui.Separator();
        
        if (LayoutHelpers.BeginSection("Module Management"))
        {
            if (ImGui.BeginTable("ModuleTable", 4, 
                ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable))
            {
                ImGui.TableSetupColumn("Module", ImGuiTableColumnFlags.WidthFixed, 150);
                ImGui.TableSetupColumn("Version", ImGuiTableColumnFlags.WidthFixed, 80);
                ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthFixed, 100);
                ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableHeadersRow();
                
                // Show all registered modules, not just loaded ones
                if (moduleManager.Registry.ModuleInfos.Count == 0)
                {
                    moduleManager.Registry.DiscoverModules();
                }
                
                foreach (var kvp in moduleManager.Registry.ModuleInfos)
                {
                    var moduleName = kvp.Key;
                    var moduleInfo = kvp.Value;
                    
                    ImGui.TableNextRow();
                    
                    ImGui.TableNextColumn();
                    ImGui.Text(moduleName);
                    
                    ImGui.TableNextColumn();
                    ImGui.TextDisabled(moduleInfo.Version);
                    
                    ImGui.TableNextColumn();
                    
                    // Get module configuration to check if enabled
                    var moduleConfig = configuration.GetModuleConfig(moduleName);
                    var isLoaded = moduleManager.LoadedModules.Any(m => m.Name == moduleName);
                    
                    LayoutHelpers.DrawModuleStatus(moduleConfig.IsEnabled, isLoaded);
                    
                    ImGui.TableNextColumn();
                    
                    if (ImGui.Button($"Configure##{moduleName}"))
                    {
                        selectedModuleName = moduleName;
                    }
                    
                    ImGui.SameLine();
                    
                    // Show the current state from configuration
                    var currentEnabled = moduleConfig.IsEnabled;
                    var checkboxEnabled = currentEnabled;
                    
                    if (ImGui.Checkbox($"##Enable{moduleName}", ref checkboxEnabled))
                    {
                        switch (currentEnabled)
                        {
                            // Checkbox was clicked, check if we can actually change it
                            case true when !checkboxEnabled:
                            {
                                // Trying to disable
                                var (canDisable, dependents) = moduleManager.CanDisableModule(moduleName, configuration);
                                if (!canDisable && dependents.Count > 0)
                                {
                                    // Cannot disable - show inline warning
                                    ImGui.SameLine();
                                    ImGui.TextColored(LayoutHelpers.Colors.Warning, "[Has Dependencies]");
                                }
                                else
                                {
                                    // Can disable directly
                                    moduleConfig.IsEnabled = false;
                                    configuration.SetModuleConfig(moduleName, moduleConfig);
                                    configuration.Save();
                                    moduleManager.ApplyConfigurationChanges(configuration);
                                }

                                break;
                            }
                            case false when checkboxEnabled:
                            {
                                // Trying to enable
                                if (!moduleManager.AreDependenciesSatisfied(moduleName, configuration))
                                {
                                    // Cannot enable - show inline warning
                                    ImGui.SameLine();
                                    ImGui.TextColored(LayoutHelpers.Colors.Error, "[Dependencies Missing]");
                                }
                                else
                                {
                                    // Can enable directly
                                    moduleConfig.IsEnabled = true;
                                    configuration.SetModuleConfig(moduleName, moduleConfig);
                                    configuration.Save();
                                    moduleManager.ApplyConfigurationChanges(configuration);
                                }

                                break;
                            }
                        }
                    }
                    
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(currentEnabled ? "Disable module" : "Enable module");
                    }
                }
                
                ImGui.EndTable();
            }
            LayoutHelpers.EndSection();
        }
        
        ImGui.Spacing();
        
        // Show selected module details
        if (!string.IsNullOrEmpty(selectedModuleName))
        {
            if (LayoutHelpers.BeginSection($"Selected Module: {selectedModuleName}"))
            {
                var moduleInfo = moduleManager.GetModuleInfo(selectedModuleName);
                if (moduleInfo != null)
                {
                    ImGui.Text($"Version: {moduleInfo.Version}");
                    if (!string.IsNullOrEmpty(moduleInfo.Author))
                        ImGui.Text($"Author: {moduleInfo.Author}");
                    if (!string.IsNullOrEmpty(moduleInfo.Description))
                        ImGui.TextWrapped($"Description: {moduleInfo.Description}");
                    if (moduleInfo.Dependencies.Length > 0)
                        ImGui.Text($"Dependencies: {string.Join(", ", moduleInfo.Dependencies)}");
                    
                    ImGui.Spacing();
                    if (ImGui.Button("Clear Selection"))
                    {
                        selectedModuleName = string.Empty;
                    }
                }
                LayoutHelpers.EndSection();
            }
            
            ImGui.Spacing();
        }
        
        if (LayoutHelpers.BeginSection("Module Dependencies"))
        {
            ImGui.TextWrapped("Some modules depend on others. Disabling a module will also disable " +
                            "all modules that depend on it.");
            
            ImGui.Spacing();
            
            // Show the dependency tree
            foreach (var kvp in moduleManager.Registry.ModuleInfos)
            {
                if (kvp.Value.Dependencies.Length == 0) continue;
                
                ImGui.Text($"{kvp.Key} depends on: {string.Join(", ", kvp.Value.Dependencies)}");
            }
            LayoutHelpers.EndSection();
        }
    }
    
    private void DrawModuleConfiguration(IModule module)
    {
        ImGui.Text($"Configuration for {module.Name}");
        ImGui.Separator();
        
        // Module info
        ImGui.TextDisabled($"Version: {module.Version}");
        if (module.Dependencies.Length > 0)
        {
            ImGui.TextDisabled($"Dependencies: {string.Join(", ", module.Dependencies)}");
        }
        
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        
        // Let the module draw its own configuration
        using (ImRaii.Child("ModuleConfig", new Vector2(0, 0), false))
        {
            try
            {
                module.DrawConfiguration();
                
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
                
                if (ImGui.Button($"Save {module.Name} Settings"))
                {
                    configuration.Save();
                    ImGui.OpenPopup($"Save{module.Name}Confirmation");
                }

                using var savePopup = ImRaii.Popup($"Save{module.Name}Confirmation");
                if (savePopup)
                {
                    ImGui.Text($"{module.Name} settings saved!");
                    if (ImGui.Button("OK"))
                        ImGui.CloseCurrentPopup();
                }
            }
            catch (Exception ex)
            {
                ImGui.TextColored(LayoutHelpers.Colors.Error, $"Error drawing module configuration: {ex.Message}");
            }
        }
    }
    
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
