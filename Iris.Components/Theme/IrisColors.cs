using System;
namespace Iris.Cloud.Client.Shared
{
    public static class IrisColors
    {
        public const string _Blue = "52, 152, 219";
        public const string Blue = $"rgb({_Blue})";
        public const string _DeepBlue = "13, 110, 253";
        public const string DeepBlue = $"rgb({_DeepBlue})";
        public const string _Green = "0, 188, 140";
        public const string Green = $"rgb({_Green})";
        public const string _Orange = "253, 126, 20";
        public const string Orange = $"rgb({_Orange})";
        public const string _Cyan = "0, 255, 255";
        public const string Cyan = $"rgb({_Cyan})";
        public const string _Purple = "147, 16, 242";
        public const string Purple = $"rgb({_Purple})";
        public const string _Pink = "232, 62, 140";
        public const string Pink = $"rgb({_Pink})";
        public const string _Red = "220, 53, 69";
        public const string Red = $"rgb({_Red})";
        public const string _Indigo = "75, 0, 130";
        public const string Indigo = $"rgb({_Indigo})";
        public const string _Violet = "238, 130, 238";
        public const string Violet = $"rgb({_Violet})";

        public static string RGB(string s)
            => $"rgb({s})";

        public static string Background(string s)
            => $"rgba({s}, 0.1)";

        public static string Hover(string s)
            => $"rgba({s}, 0.2)";

        public static string Opacity(string s, double o)
            => $"rgba({s}, {o})";
    }
}

