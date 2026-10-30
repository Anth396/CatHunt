using System;

public static class EnumExtension
{
    // Enum value named like the given string (e.g. a state asset's name), default if there is none
    public static T ParseEnum<T>(string input) where T : struct
    {
        T enumValue;
        if (Enum.TryParse(input, true, out enumValue)) return enumValue;
        return default(T);
    }
}
