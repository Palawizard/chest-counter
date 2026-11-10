using System.Globalization;

namespace ChestCounter;

internal static class Text
{
    public static readonly bool French = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fr";

    public static string Language => French ? "fr" : "en";

    public static string AppName => French ? "Compteur de coffres" : "Chest Counter";

    public static string Pick(string french, string english) => French ? french : english;
}
