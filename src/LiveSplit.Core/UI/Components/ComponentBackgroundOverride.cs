using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;

namespace LiveSplit.UI.Components;

/// <summary>
/// Suppresses component backgrounds for one draw without changing the saved component settings.
/// Components are separate plug-in assemblies, so use their existing settings property convention.
/// </summary>
internal sealed class ComponentBackgroundOverride : IDisposable
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> BackgroundProperties = new();
    private readonly object settings;
    private readonly List<(PropertyInfo Property, Color Color)> originalColors = [];

    public ComponentBackgroundOverride(IComponent component)
    {
        PropertyInfo settingsProperty = component.GetType().GetProperty("Settings",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        settings = settingsProperty?.GetValue(component);
        if (settings == null)
        {
            return;
        }

        PropertyInfo[] properties = BackgroundProperties.GetOrAdd(settings.GetType(), type =>
            type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(property => property.PropertyType == typeof(Color)
                    && property.CanRead && property.CanWrite && property.GetIndexParameters().Length == 0
                    && property.Name is "BackgroundColor" or "BackgroundColor2"
                        or "CurrentSplitTopColor" or "CurrentSplitBottomColor")
                .ToArray());

        try
        {
            foreach (PropertyInfo property in properties)
            {
                originalColors.Add((property, (Color)property.GetValue(settings)));
                property.SetValue(settings, Color.Transparent);
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        foreach ((PropertyInfo property, Color color) in originalColors)
        {
            property.SetValue(settings, color);
        }

        originalColors.Clear();
    }
}
