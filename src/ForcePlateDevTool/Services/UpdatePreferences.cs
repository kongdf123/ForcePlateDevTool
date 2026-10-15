using System.IO;

namespace ForcePlateDevTool.Services;

public static class UpdatePreferences
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ForcePlateDevTool");

    private static readonly string SkippedVersionPath =
        Path.Combine(SettingsDirectory, "SkippedUpdateVersion.txt");

    public static bool IsSkipped(string tag)
    {
        try
        {
            return File.Exists(SkippedVersionPath) &&
                string.Equals(
                    File.ReadAllText(SkippedVersionPath).Trim(),
                    tag,
                    StringComparison.OrdinalIgnoreCase);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static void Skip(string tag)
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            File.WriteAllText(SkippedVersionPath, tag);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}