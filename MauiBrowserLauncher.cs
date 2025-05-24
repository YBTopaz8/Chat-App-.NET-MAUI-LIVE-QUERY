using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YBSeedrClient.Abstractions;

namespace LiveQueryChatAppMAUI;

public class MauiBrowserLauncher : IBrowserLauncher
{
    public async Task<bool> OpenAsync(string uri, SeedrBrowserLaunchMode mode = SeedrBrowserLaunchMode.SystemPreferred)
    {
        try
        {
            BrowserLaunchMode mauiMode = mode switch
            {
                SeedrBrowserLaunchMode.SystemPreferred => Microsoft.Maui.ApplicationModel.BrowserLaunchMode.SystemPreferred,
                SeedrBrowserLaunchMode.Private => Microsoft.Maui.ApplicationModel.BrowserLaunchMode.External,
                _ => Microsoft.Maui.ApplicationModel.BrowserLaunchMode.SystemPreferred
            };

            await Browser.Default.OpenAsync(uri, mauiMode);
            return true;
        }
        catch (Exception ex)
        {
            // Log error (e.g., using MAUI's logging or a simple Debug.WriteLine)
            System.Diagnostics.Debug.WriteLine($"Error opening browser: {ex.Message}");
            return false;
        }
    }
}