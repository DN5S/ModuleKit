using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using TLink.Core.Configuration;
using TLink.Core.Module;

namespace TLink.Core.UI;

public class MainWindow : Window, IDisposable
{
    private readonly ModuleManager moduleManager;
    private readonly PluginConfiguration configuration;
    private readonly Action openConfigWindow;
    
    public MainWindow(ModuleManager moduleManager, PluginConfiguration configuration, Action openConfigWindow) 
        : base("TataruLink###TataruLinkMain", ImGuiWindowFlags.None)
    {
        this.moduleManager = moduleManager ?? throw new ArgumentNullException(nameof(moduleManager));
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        this.openConfigWindow = openConfigWindow ?? throw new ArgumentNullException(nameof(openConfigWindow));
        
        Size = new Vector2(800, 600);
        SizeCondition = ImGuiCond.FirstUseEver;
    }
    
    public override void Draw()
    {
        DrawHeader();
        
        ImGui.Separator();
        
        if (moduleManager.LoadedModules.Count == 0)
        {
            DrawNoModulesMessage();
        }
        else
        {
            DrawModuleTabs();
        }
    }
    
    private void DrawHeader()
    {
        ImGui.Text("TataruLink - Module Management System");
        ImGui.SameLine();
        
        // Settings button on the right
        var buttonWidth = 100;
        ImGui.SetCursorPosX(ImGui.GetWindowWidth() - buttonWidth - ImGui.GetStyle().WindowPadding.X);
        
        if (ImGui.Button("Settings", new Vector2(buttonWidth, 0)))
        {
            openConfigWindow();
        }
        
        ImGui.TextDisabled($"Loaded Modules: {moduleManager.LoadedModules.Count}");
    }
    
    private void DrawNoModulesMessage()
    {
        var windowSize = ImGui.GetWindowSize();
        var textHeight = ImGui.GetTextLineHeight() * 4;
        
        ImGui.SetCursorPosY((windowSize.Y - textHeight) / 2);
        
        LayoutHelpers.CenteredText("No modules are currently loaded.");
        LayoutHelpers.CenteredText("Check the settings to enable modules.");
        
        ImGui.Spacing();
        
        LayoutHelpers.DrawCenteredButtons(
            ("Open Settings", () => openConfigWindow())
        );
    }
    
    private void DrawModuleTabs()
    {
        using var tabBar = ImRaii.TabBar("ModuleTabs");
        if (!tabBar) return;
        
        foreach (var module in moduleManager.LoadedModules)
        {
            using var tab = ImRaii.TabItem(module.Name);
            if (!tab) continue;
            
            DrawModuleContent(module);
        }
        
        // Add a tab for the overview
        using var overviewTab = ImRaii.TabItem("Overview");
        if (overviewTab)
        {
            DrawOverview();
        }
    }
    
    private void DrawModuleContent(IModule module)
    {
        ImGui.Text($"Module: {module.Name}");
        ImGui.TextDisabled($"Version: {module.Version}");
        
        // Show module configuration status
        var moduleConfig = configuration.GetModuleConfig(module.Name);
        if (moduleConfig.IsEnabled)
        {
            ImGui.SameLine();
            ImGui.TextColored(LayoutHelpers.Colors.Enabled, "[Enabled]");
        }
        else
        {
            ImGui.SameLine();
            ImGui.TextColored(LayoutHelpers.Colors.Disabled, "[Disabled in config]");
        }
        
        if (module.Dependencies.Length > 0)
        {
            ImGui.TextDisabled($"Dependencies: {string.Join(", ", module.Dependencies)}");
        }
        
        ImGui.Separator();
        ImGui.Spacing();
        
        // Let the module draw its own UI
        using (ImRaii.Child("ModuleContent", new Vector2(0, 0), false))
        {
            try
            {
                module.DrawUI();
            }
            catch (Exception ex)
            {
                ImGui.TextColored(LayoutHelpers.Colors.Error, $"Error drawing module UI: {ex.Message}");
            }
        }
    }
    
    private void DrawOverview()
    {
        ImGui.Text("System Overview");
        ImGui.Separator();
        
        // Display configuration statistics
        if (LayoutHelpers.BeginSection("Configuration"))
        {
            var allModuleConfigs = configuration.GetAllModuleConfigs();
            var enabledCount = allModuleConfigs.Count(c => c.Value.IsEnabled);
            var disabledCount = allModuleConfigs.Count - enabledCount;
            
            ImGui.Text($"Total Configured Modules: {allModuleConfigs.Count}");
            ImGui.SameLine();
            ImGui.TextColored(LayoutHelpers.Colors.Success, $"Enabled: {enabledCount}");
            ImGui.SameLine();
            ImGui.TextColored(LayoutHelpers.Colors.Warning, $"Disabled: {disabledCount}");
            
            ImGui.Text($"Currently Loaded: {moduleManager.LoadedModules.Count}");
            
            if (disabledCount > 0)
            {
                ImGui.TextDisabled("Some modules are disabled. Check settings to enable them.");
            }
            LayoutHelpers.EndSection();
        }
        
        ImGui.Spacing();
        
        if (LayoutHelpers.BeginSection("Loaded Modules"))
        {
            if (ImGui.BeginTable("ModuleOverviewTable", 4, 
                ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable))
            {
                ImGui.TableSetupColumn("Module", ImGuiTableColumnFlags.WidthFixed, 150);
                ImGui.TableSetupColumn("Version", ImGuiTableColumnFlags.WidthFixed, 80);
                ImGui.TableSetupColumn("Dependencies", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthFixed, 100);
                ImGui.TableHeadersRow();
                
                foreach (var module in moduleManager.LoadedModules)
                {
                    ImGui.TableNextRow();
                    
                    ImGui.TableNextColumn();
                    ImGui.Text(module.Name);
                    
                    ImGui.TableNextColumn();
                    ImGui.TextDisabled(module.Version);
                    
                    ImGui.TableNextColumn();
                    var deps = module.Dependencies.Length > 0 
                        ? string.Join(", ", module.Dependencies) 
                        : "None";
                    ImGui.TextDisabled(deps);
                    
                    ImGui.TableNextColumn();
                    ImGui.TextColored(LayoutHelpers.Colors.Success, "Active");
                }
                
                ImGui.EndTable();
            }
            LayoutHelpers.EndSection();
        }
        
        ImGui.Spacing();
        
        if (LayoutHelpers.BeginSection("Statistics"))
        {
            ImGui.Text($"Total Modules Loaded: {moduleManager.LoadedModules.Count}");
            
            // Count modules with dependencies
            var modulesWithDeps = 0;
            foreach (var module in moduleManager.LoadedModules)
            {
                if (module.Dependencies.Length > 0)
                    modulesWithDeps++;
            }
            
            ImGui.Text($"Modules with Dependencies: {modulesWithDeps}");
            LayoutHelpers.EndSection();
        }
        
        ImGui.Spacing();
        
        if (ImGui.Button("Open Configuration"))
        {
            openConfigWindow();
        }
        
        ImGui.SameLine();
        
        if (ImGui.Button("Reload All Modules"))
        {
            // This would reload all modules - implementation would go here
            ImGui.OpenPopup("ReloadConfirmation");
        }

        using var popup = ImRaii.Popup("ReloadConfirmation");
        if (popup)
        {
            ImGui.Text("Are you sure you want to reload all modules?");
            ImGui.TextColored(LayoutHelpers.Colors.Warning, "This may cause temporary interruption.");
            ImGui.Spacing();
            
            LayoutHelpers.DrawCenteredButtons(
                ("Yes, Reload", ImGui.CloseCurrentPopup),
                ("Cancel", ImGui.CloseCurrentPopup)
            );
        }
    }
    
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
