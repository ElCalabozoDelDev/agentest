using System.Text.Json.Serialization;

namespace agentest.Models;

[JsonConverter(typeof(JsonStringEnumConverter<ThemeMode>))]
public enum ThemeMode
{
    System,
    Light,
    Dark
}

public sealed class AppSettings
{
    public ThemeMode Theme { get; set; } = ThemeMode.System;
}
