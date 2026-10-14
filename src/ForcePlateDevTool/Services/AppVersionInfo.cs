using System.Reflection;

namespace ForcePlateDevTool.Services;

public sealed record AppVersionInfo(
    string Version,
    string FileVersion,
    string Commit)
{
    public static AppVersionInfo Current
    {
        get
        {
            var assembly = Assembly.GetEntryAssembly()
                ?? Assembly.GetExecutingAssembly();

            string informational =
                assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                    ?.InformationalVersion
                ?? "Unknown";

            string fileVersion =
                assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()
                    ?.Version
                ?? "Unknown";

            string commit = "Unknown";
            int plusIndex = informational.IndexOf('+');
            if (plusIndex >= 0 && plusIndex < informational.Length - 1)
            {
                commit = informational[(plusIndex + 1)..];
            }

            return new AppVersionInfo(
                informational,
                fileVersion,
                commit);
        }
    }

    public string DisplayVersion =>
        string.Equals(Commit, "Unknown", StringComparison.Ordinal)
            ? Version
            : $"{Version} ({Commit})";
}