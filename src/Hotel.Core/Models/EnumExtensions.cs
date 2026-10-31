using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;

namespace Hotel.Core.Models;

public static class EnumExtensions
{
    private static readonly ConcurrentDictionary<Enum, string> Descriptions = new();

    public static string GetDescription(this Enum value) =>
        Descriptions.GetOrAdd(value, static v =>
            v.GetType().GetField(v.ToString())?.GetCustomAttribute<DescriptionAttribute>()?.Description
            ?? v.ToString());
}
