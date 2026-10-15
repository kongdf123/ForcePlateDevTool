using System.Diagnostics;
using System.Windows;
using ForcePlateDevTool.Services;

namespace ForcePlateDevTool.Windows;

public partial class UpdateNotificationWindow : Window
{
    private readonly UpdateRelease _release;

    public UpdateNotificationWindow(UpdateRelease release)
    {
        InitializeComponent();

        _release = release;
        TxtVersion.Text = $"当前版本：{AppVersionInfo.Current.Version}\n" +
                          $"最新版本：{release.Version}";
        TxtReleaseNotes.Text = string.IsNullOrWhiteSpace(release.ReleaseNotes)
            ? "该版本没有提供更新说明。"
            : release.ReleaseNotes;
    }

    private void Download_Click(object sender, RoutedEventArgs e)
    {
        string url = _release.InstallerUrl ?? _release.ReleaseUrl;

        try
        {
            Process.Start(new ProcessStartInfo(url)
            {
                UseShellExecute = true
            });
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"无法打开下载链接：{ex.Message}\n\n{_release.ReleaseUrl}",
                "打开下载链接失败",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void RemindLater_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void SkipVersion_Click(object sender, RoutedEventArgs e)
    {
        UpdatePreferences.Skip(_release.TagName);
        Close();
    }
}