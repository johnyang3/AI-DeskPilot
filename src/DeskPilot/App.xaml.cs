using System.Globalization;
using System.Windows;

namespace DeskPilot;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // CurrentUICulture follows the Windows display language by default.
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.CurrentUICulture;
        base.OnStartup(e);
    }
}
