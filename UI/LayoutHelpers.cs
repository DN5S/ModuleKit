using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace TLink.Core.UI;

public static class LayoutHelpers
{
    #region Colors
    
    /// <summary>
    /// Common UI colors for consistent theming
    /// </summary>
    public static class Colors
    {
        // Status colors
        public static readonly Vector4 Success = new(0.3f, 1.0f, 0.3f, 1.0f);
        public static readonly Vector4 Warning = new(1.0f, 0.5f, 0.0f, 1.0f);
        public static readonly Vector4 Error = new(1.0f, 0.3f, 0.3f, 1.0f);
        public static readonly Vector4 Info = new(0.3f, 0.8f, 1.0f, 1.0f);
        
        // Module status colors
        public static readonly Vector4 Enabled = Success;
        public static readonly Vector4 Disabled = new(1.0f, 0.0f, 0.0f, 1.0f);
        public static readonly Vector4 Pending = new(1.0f, 1.0f, 0.0f, 1.0f);
        
        // Text colors
        public static readonly Vector4 TextDefault = new(1.0f, 1.0f, 1.0f, 1.0f);
        public static readonly Vector4 TextDisabled = new(0.5f, 0.5f, 0.5f, 1.0f);
        public static readonly Vector4 TextMuted = new(0.7f, 0.7f, 0.7f, 1.0f);
        
        /// <summary>
        /// Returns a color with modified alpha
        /// </summary>
        public static Vector4 WithAlpha(Vector4 color, float alpha)
        {
            return color with { W = alpha };
        }
    }
    
    #endregion
    
    #region Layout Containers
    
    public static IDisposable BeginPanel(string id, Vector2? size = null, bool border = true)
    {
        var disposables = new CompositeDisposable();
        
        if (border)
            disposables.Add(ImRaii.PushStyle(ImGuiStyleVar.ChildBorderSize, 1f));
        
        disposables.Add(ImRaii.Child(
            id, 
            size ?? Vector2.Zero, 
            border, 
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse
        ));
        
        return disposables;
    }
    
    public static IDisposable BeginFormField(string label, float labelWidth = 150f)
    {
        ImGui.Text(label);
        ImGui.SameLine(labelWidth * ImGuiHelpers.GlobalScale);
        ImGui.SetNextItemWidth(-1);
        return new DummyDisposable();
    }
    
    public static IDisposable BeginTabView(string id)
    {
        return ImRaii.TabBar(id);
    }
    
    public static IDisposable BeginTab(string label)
    {
        return ImRaii.TabItem(label);
    }
    
    public static bool BeginSection(string title, bool defaultOpen = true)
    {
        var flags = defaultOpen ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None;
        return ImGui.TreeNodeEx(title, flags);
    }
    
    public static void EndSection()
    {
        ImGui.TreePop();
    }
    
    public static IDisposable BeginCard()
    {
        var disposables = new CompositeDisposable();
        disposables.Add(ImRaii.PushStyle(ImGuiStyleVar.FramePadding, new Vector2(10, 10)));
        disposables.Add(ImRaii.PushStyle(ImGuiStyleVar.ChildBorderSize, 1f));
        disposables.Add(ImRaii.PushColor(ImGuiCol.ChildBg, ImGui.GetColorU32(ImGuiCol.FrameBg)));
        return disposables;
    }
    
    public static void HelpTooltip(string text)
    {
        ImGui.TextDisabled("(?)");
        if (ImGui.IsItemHovered())
        {
            using var tooltip = ImRaii.Tooltip();
            if (tooltip)
            {
                using var wrap = ImRaii.TextWrapPos(ImGui.GetFontSize() * 35.0f);
                ImGui.Text(text);
            }
        }
    }
    
    public static void CenterNextWindow()
    {
        var center = ImGui.GetMainViewport().GetCenter();
        ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
    }
    
    public static bool IconButton(FontAwesomeIcon icon, string id = "")
    {
        using var font = ImRaii.PushFont(UiBuilder.IconFont);
        return ImGui.Button($"{icon.ToIconString()}##{id}");
    }
    
    public static void Separator(string text = "")
    {
        if (string.IsNullOrEmpty(text))
        {
            ImGui.Separator();
            return;
        }
        
        var windowWidth = ImGui.GetWindowWidth();
        var textWidth = ImGui.CalcTextSize(text).X;
        
        ImGui.Separator();
        ImGui.SameLine(windowWidth / 2 - textWidth / 2);
        ImGui.Text(text);
        ImGui.Separator();
    }
    
    public static IDisposable BeginPopup(string id, ImGuiWindowFlags flags = ImGuiWindowFlags.None)
    {
        return ImRaii.Popup(id, flags);
    }
    
    public static IDisposable BeginGroup()
    {
        return ImRaii.Group();
    }
    
    public static IDisposable WithTextWrap(float wrapWidth = 0)
    {
        return ImRaii.TextWrapPos(wrapWidth == 0 ? ImGui.GetContentRegionAvail().X : wrapWidth);
    }
    
    public static IDisposable WithDisabled(bool disabled = true)
    {
        return ImRaii.Disabled(disabled);
    }
    
    public static IDisposable WithIndent(float indent = 0)
    {
        if (indent > 0)
            return ImRaii.PushStyle(ImGuiStyleVar.IndentSpacing, indent);
        
        return new ImRaii.Indent();
    }
    
    public static IDisposable WithFont(ImFontPtr font)
    {
        return ImRaii.PushFont(font);
    }
    
    public static IDisposable WithColor(ImGuiCol colorType, uint color)
    {
        return ImRaii.PushColor(colorType, color);
    }
    
    public static IDisposable WithStyle(ImGuiStyleVar style, float value)
    {
        return ImRaii.PushStyle(style, value);
    }
    
    public static IDisposable WithStyle(ImGuiStyleVar style, Vector2 value)
    {
        return ImRaii.PushStyle(style, value);
    }
    
    #endregion
    
    #region Popup Helpers
    
    /// <summary>
    /// Draws a modal confirmation popup with proper RAII handling
    /// </summary>
    public static bool DrawModalConfirmation(
        string popupId,
        ref bool isOpen,
        string message,
        string? warningMessage = null,
        string confirmText = "Yes",
        string cancelText = "Cancel",
        Action? onConfirm = null,
        Action? onCancel = null,
        ImGuiWindowFlags windowFlags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove)
    {
        using var popup = ImRaii.PopupModal(popupId, ref isOpen, windowFlags);
        if (!popup) return false;

        ImGui.SetWindowSize(new Vector2(400, 0));
        
        ImGui.Text(message);
        
        if (!string.IsNullOrEmpty(warningMessage))
        {
            ImGui.Spacing();
            ImGui.TextColored(Colors.Warning, warningMessage);
        }
        
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        
        DrawCenteredButtons(
            (confirmText, () => {
                onConfirm?.Invoke();
                ImGui.CloseCurrentPopup();
            }),
            (cancelText, () => {
                onCancel?.Invoke();
                ImGui.CloseCurrentPopup();
            })
        );
        
        return true;
    }
    
    /// <summary>
    /// Draws a list of items in a modal popup with scrolling support
    /// </summary>
    public static bool DrawListModal(
        string popupId,
        ref bool isOpen,
        string title,
        string[] items,
        string confirmText = "Continue",
        string cancelText = "Cancel",
        Action? onConfirm = null,
        Action? onCancel = null,
        Vector4? itemColor = null)
    {
        var flags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove;
        using var popup = ImRaii.PopupModal(popupId, ref isOpen, flags);
        if (!popup) return false;

        ImGui.SetWindowSize(new Vector2(400, 0));
        
        ImGui.Text(title);
        ImGui.Spacing();
        
        // Create a scrollable list if there are many items
        var listHeight = Math.Min(items.Length * 25, 150);
        using (ImRaii.Child("ItemList", new Vector2(0, listHeight), true))
        {
            foreach (var item in items)
            {
                if (itemColor.HasValue)
                    ImGui.TextColored(itemColor.Value, $"  • {item}");
                else
                    ImGui.Text($"  • {item}");
            }
        }
        
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        
        DrawCenteredButtons(
            (confirmText, () => {
                onConfirm?.Invoke();
                ImGui.CloseCurrentPopup();
            }),
            (cancelText, () => {
                onCancel?.Invoke();
                ImGui.CloseCurrentPopup();
            })
        );
        
        return true;
    }
    
    #endregion
    
    #region Common UI Components
    
    /// <summary>
    /// Draws centered buttons in a horizontal layout
    /// </summary>
    public static void DrawCenteredButtons(params (string text, Action action)[] buttons)
    {
        if (buttons.Length == 0) return;
        
        const float buttonWidth = 120f;
        const float spacing = 10f;
        
        var totalWidth = buttonWidth * buttons.Length + spacing * (buttons.Length - 1);
        var startX = (ImGui.GetContentRegionAvail().X - totalWidth) / 2;
        
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + startX);
        
        for (var i = 0; i < buttons.Length; i++)
        {
            if (i > 0) 
            {
                ImGui.SameLine();
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() - spacing + spacing);
            }
            
            if (ImGui.Button(buttons[i].text, new Vector2(buttonWidth, 0)))
            {
                buttons[i].action();
            }
        }
    }
    
    /// <summary>
    /// Draws module status text with the appropriate color
    /// </summary>
    public static void DrawModuleStatus(bool isEnabled, bool isLoaded)
    {
        switch (isEnabled)
        {
            case true when isLoaded:
                ImGui.TextColored(Colors.Enabled, "Loaded");
                break;
            case true when !isLoaded:
                ImGui.TextColored(Colors.Pending, "Enabled");
                break;
            default:
                ImGui.TextColored(Colors.Disabled, "Disabled");
                break;
        }
    }
    
    /// <summary>
    /// Draws text with a tooltip on hover
    /// </summary>
    public static void TextWithTooltip(string text, string tooltip)
    {
        ImGui.Text(text);
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(tooltip);
        }
    }
    
    /// <summary>
    /// Creates a toggle button that changes color based on state
    /// </summary>
    public static bool ToggleButton(string label, ref bool value, string? tooltip = null)
    {
        using (ImRaii.PushColor(ImGuiCol.Button, value ? Colors.Success : Colors.TextDisabled))
        {
            if (ImGui.Button(label))
            {
                value = !value;
                if (!string.IsNullOrEmpty(tooltip) && ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(tooltip);
                }
                return true;
            }
        }
        return false;
    }
    
    /// <summary>
    /// Draws centered text
    /// </summary>
    public static void CenteredText(string text)
    {
        var windowWidth = ImGui.GetWindowSize().X;
        var textWidth = ImGui.CalcTextSize(text).X;
        ImGui.SetCursorPosX((windowWidth - textWidth) * 0.5f);
        ImGui.Text(text);
    }
    
    /// <summary>
    /// Draws multiple spacing elements
    /// </summary>
    public static void Spacing(int count = 1)
    {
        for (var i = 0; i < count; i++)
            ImGui.Spacing();
    }
    
    #endregion
    
    #region Table Helpers
    
    /// <summary>
    /// Draws a table header with standard styling
    /// </summary>
    public static void SetupTableColumns(params (string name, ImGuiTableColumnFlags flags, float width)[] columns)
    {
        foreach (var (name, flags, width) in columns)
        {
            ImGui.TableSetupColumn(name, flags, width);
        }
        ImGui.TableHeadersRow();
    }
    
    #endregion
    
    #region Private Classes
    
    private class DummyDisposable : IDisposable
    {
        public void Dispose() { }
    }
    
    private class CompositeDisposable : IDisposable
    {
        private readonly List<IDisposable> disposables = new();
        
        public void Add(IDisposable disposable)
        {
            disposables.Add(disposable);
        }
        
        public void Dispose()
        {
            for (var i = disposables.Count - 1; i >= 0; i--)
            {
                disposables[i].Dispose();
            }
        }
    }
    
    #endregion
}
