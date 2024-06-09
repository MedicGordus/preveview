using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

using static preveview.user32_api;

namespace preveview;

public class WindowMonitor
{
    public const int DEFAULT_MILLISECONDS_DELAY_TO_MOVE = 500;

    public const double DEFAULT_OPACITY = 0.75;

    public const int DEFAULT_MAGNETIZE_PIXELS = 25;

    public const int DEFAULT_SCREEN_INDEX = 0;

    public const int DEFAULT_MONITOR_INTERVAL = 250;

    public const bool DEFAULT_SHOW_TITLE_BAR = false;

    public const int DEFAULT_BORDER_WIDTH = 7;

    public static readonly ArgbConfiguration DEFAULT_ACTIVE_ARGB = new ArgbConfiguration() {
        Alpha = 255,
        Red = 34,
        Green = 177,
        Blue = 76
    };

    public static readonly ArgbConfiguration DEFAULT_INACTIVE_ARGB = new ArgbConfiguration() {
        Alpha = 10,
        Red = 0,
        Green = 0,
        Blue = 0
    };

    public static readonly ArgbConfiguration DEFAULT_MINIMIZED_ARGB = new ArgbConfiguration() {
        Alpha = 255,
        Red = 237,
        Green = 28,
        Blue = 36
    };

    private readonly Configuration Configuration;

    private readonly List<WindowConfigurationJson> WindowConfigurations;

    private Task? MonitorTask;

    private readonly int MonitorMillisecondInterval;

    private readonly Dictionary<PreviewForm, WindowConfigurationJson> MatchingConfigs;

    private int PreviewsWaitingToLoad;

    public WindowMonitor(Configuration configuration)
    {
        Configuration = configuration;
        WindowConfigurations = new List<WindowConfigurationJson>(Configuration.Windows ?? []);

        MatchingConfigs = [];

        PreviewsWaitingToLoad = 0;

        MonitorMillisecondInterval = Configuration.MonitorMillisecondInterval ?? DEFAULT_MONITOR_INTERVAL;

        ValidateConfiguration();
    }

    private void ValidateConfiguration()
    {
        List<WindowConfigurationJson> titlesToRemove = [];

        foreach(var deltaWindow in WindowConfigurations)
        {
            // flag any window with no title, negative or zero height/width to be removed
            if(deltaWindow.Title == null | deltaWindow.Height <= 0 | deltaWindow.Width <= 0 | (deltaWindow.Disabled ?? false) == true)
            {
                titlesToRemove.Add(deltaWindow);
            }
        }

        foreach(var deltaWindow in titlesToRemove)
        {
            WindowConfigurations.Remove(deltaWindow);
        }
    }

    public void StartMonitoring()
    {
        EnumWindows((hWnd, lParam) =>
        {
            var windowTitle = GetWindowTitle(hWnd);

            if(windowTitle != "")
            {
                if (IsTitleConfigured(windowTitle, WindowConfigurations))
                {
                    if(LoadMatchingPreviewAndCheckForChanges(hWnd, windowTitle, WindowConfigurations))
                    {
                        Configuration.Save();
                    }
                }
            }

            return true;
        }, IntPtr.Zero);

        Application.DoEvents();

        MonitorTask = Task.Run(MonitorAsync);
    }

    private async Task MonitorAsync()
    {
        // load the configurations that are not open
        List<WindowConfigurationJson>? windowConfigurationsNotOpen = RetrieveNonOpenedConfigs() ?? [];

        // capture our process so we don't look at it
        int thisProcessId = Process.GetCurrentProcess().Id;

        // special flag used to avoid invalid cross thread operations to the ux message queue
        bool needToReloadConfigurationsNotOpen = false;

        while(!Program.ExitingApp)
        {
            // special logic that works around the cross thread issues of new preview windows loading
            if(needToReloadConfigurationsNotOpen & PreviewsWaitingToLoad == 0)
            {
                windowConfigurationsNotOpen = RetrieveNonOpenedConfigs() ?? [];
                needToReloadConfigurationsNotOpen = false;
            }

            //// collect list of preview forms
            //
            List<PreviewForm> previewForms = [];
            //
            foreach (Form deltaForm in Application.OpenForms)
            {
                if(deltaForm is PreviewForm previewForm)
                {
                    previewForms.Add(previewForm);
                }
            }
            //
            ////

            List<PreviewForm> previewFormsFound = [];

            IntPtr activeWindowHandle = GetForegroundWindow();
            
            EnumWindows((hWnd, lParam) =>
            {
                // make sure we aren't checking our windows (or it will loop forever)
                GetWindowThreadProcessId(hWnd, out int processId);
                if(processId == thisProcessId)
                {
                    return true;
                }

                var windowTitle = GetWindowTitle(hWnd);

                if(windowTitle != "")
                {
                    PreviewForm? foundForm = previewForms.FirstOrDefault(_item => _item.WindowHandle == hWnd);

                    if(foundForm != null)
                    {
                        // flag as found
                        previewFormsFound.Add(foundForm);

                        // update border based on (active, inactive, minimized)
                        if(hWnd == activeWindowHandle)
                        {
                            foundForm.UpdateBorder(RectangleMode.Active);
                        }
                        else if (user32_api.IsIconic(hWnd))
                        {
                            foundForm.UpdateBorder(RectangleMode.Minimized);
                        }
                        else
                        {
                            foundForm.UpdateBorder(RectangleMode.Inactive);
                        }
                    }
                    // only do checks if there are not windows loading
                    else if(PreviewsWaitingToLoad == 0)
                    {
                        // form is not loaded already so we check if we need to load it
                        if (IsTitleConfigured(windowTitle, windowConfigurationsNotOpen))
                        {
                            // this is not guaranteed to load previews, but it may
                            LoadMatchingPreviewAndCheckForChanges(hWnd, windowTitle, windowConfigurationsNotOpen);

                            // turn the flag on so that once the ux message queue processes, our loop here (at the top) will update which previews loaded/not loaded
                            needToReloadConfigurationsNotOpen = true;
                        }
                    }
                }

                return true;
            }, IntPtr.Zero);

            // only do checks if there are not windows loading
            if(PreviewsWaitingToLoad == 0)
            {
                //// close forms that are no longer open
                //
                List<PreviewForm> previewFormsNotFound = previewForms.Except(previewFormsFound).ToList();
                //
                foreach(var deltaForm in previewFormsNotFound)
                {

                    // window is closing, add config to the not loaded list
                    if(MatchingConfigs.ContainsKey(deltaForm))
                    {
                        windowConfigurationsNotOpen.Add(MatchingConfigs[deltaForm]);
                        MatchingConfigs.Remove(deltaForm);
                    }

                    deltaForm.RunActionOnFormThread(
                        () =>
                        {
                            deltaForm.Close();
                            deltaForm.Dispose();
                        }
                    );

                    // cycle application thread
                    Application.DoEvents();
                }
                //
                ////
            }

            await Task.Delay(MonitorMillisecondInterval).ConfigureAwait(false);
        }
    }

    private List<WindowConfigurationJson>? RetrieveNonOpenedConfigs()
    {
        Exception? exception = null;

        do
        {
            try
            {
                List<WindowConfigurationJson>? windowConfigurationsNotOpen = WindowConfigurations ?? [];
                //
                foreach(KeyValuePair<PreviewForm, WindowConfigurationJson> deltaEntry in MatchingConfigs)
                {
                    if(windowConfigurationsNotOpen.Contains(deltaEntry.Value))
                    {
                        windowConfigurationsNotOpen.Remove(deltaEntry.Value);
                    }
                }

                return windowConfigurationsNotOpen;
            }
            catch(Exception e)
            {
                exception = e;
            }
        } while (exception != null);

        // it can never get here
        return null;
    }

    private string GetWindowTitle(IntPtr hWnd)
    {
        var titleStringBuilder = new StringBuilder(256);
        GetWindowText(hWnd, titleStringBuilder, 256);
        return titleStringBuilder.ToString();
    }

    private bool IsTitleConfigured(string title, List<WindowConfigurationJson>? windowsToCheck)
    {
        if(windowsToCheck == null)
        {
            return false;
        }

        return windowsToCheck.Any(t => t.Title != null && t.Title.Trim().Length != 0 && WildcardMatch(title, t.Title));
    }

    private bool WildcardMatch(string text, string pattern)
    {
        return Regex.IsMatch(text, pattern);
    }
    
    private bool LoadMatchingPreviewAndCheckForChanges(IntPtr hWnd, string windowTitle, List<WindowConfigurationJson>? windowsToCheck)
    {
        bool changed = false;

        if(windowsToCheck != null)
        {
            List<WindowConfigurationJson> configurationsToAdd = [];
            List<WindowConfigurationJson> configurationsToRemove = [];

            // load exact matches before we check pattern matching
            List<WindowConfigurationJson> matchedWindows = 
            (
                from
                    _item in windowsToCheck
                where
                    _item.Title?.ToLower() == windowTitle.ToLower()
                select
                    _item
            ).ToList();

            if(matchedWindows.Count > 0)
            {
                LoadPreview(matchedWindows[0], hWnd, windowTitle);
                return false;
            }

            foreach (var windowConfig in windowsToCheck)
            {
                if(windowConfig.Title != null)
                {
                    if(WildcardMatch(windowTitle, windowConfig.Title))
                    {
                        if(windowTitle.ToLower() != windowConfig.Title.ToLower())
                        {
                            // if it was a pattern match, we need to remove it (user can reactivate if they need to)
                            if(!configurationsToRemove.Contains(windowConfig))
                            {
                                configurationsToRemove.Add(windowConfig);
                            }

                            // add this config
                            configurationsToAdd.Add(
                                new WindowConfigurationJson {
                                    Title = windowTitle,
                                    TitleLabelOverride = windowConfig.TitleLabelOverride,
                                    X = windowConfig.X,
                                    Y = windowConfig.Y,
                                    Width = windowConfig.Width,
                                    Height = windowConfig.Height,
                                    Hotkeys = windowConfig.Hotkeys,
                                    Opacity = windowConfig.Opacity,
                                    ScreenIndex = windowConfig.ScreenIndex,
                                    Disabled = false
                                }
                            );
                        }

                        LoadPreview(windowConfig, hWnd, windowTitle);
                    }
                }
            }

            // remove any wildcards that were fulfilled
            foreach(var windowConfig in configurationsToRemove)
            {
                windowConfig.Disabled = true;
            }

            // add the unique windows that were derived from pattern matches
            foreach(var windowConfig in configurationsToAdd)
            {
                windowsToCheck.Add(windowConfig);
            }

            changed = true;
        }

        return changed;
    }

    private void LoadPreview(WindowConfigurationJson windowConfig, IntPtr hWnd, string windowTitle)
    {
        Interlocked.Increment(ref PreviewsWaitingToLoad);

        Action action = () => {
            ToolStripMenuItem menuItem = new ToolStripMenuItem(
                windowTitle,
                GetIconFromWindow(hWnd)
            );
            var preview = new PreviewForm(
                hWnd,
                Configuration.MillisecondDelayToMove ?? DEFAULT_MILLISECONDS_DELAY_TO_MOVE,
                windowConfig.Opacity ?? Configuration.BaseOpacity ?? DEFAULT_OPACITY,
                Configuration.MagnetizePixelDistance ?? DEFAULT_MAGNETIZE_PIXELS,
                windowConfig.ScreenIndex ?? Configuration.DefaultScreenIndex ?? DEFAULT_SCREEN_INDEX,
                windowConfig.TitleLabelOverride ?? windowTitle,
                string.Format(
                    "Preview: {0}",
                    windowTitle
                ),
                windowConfig.X, 
                windowConfig.Y,
                windowConfig.Width,
                windowConfig.Height,
                windowConfig.ShowTitleBar ?? DEFAULT_SHOW_TITLE_BAR,
                windowConfig.BorderWidth ?? Configuration.BaseBorderWidth ?? DEFAULT_BORDER_WIDTH,
                Color.FromArgb(
                    windowConfig.ActiveArgb?.Alpha ?? Configuration.BaseActiveArgb?.Alpha ?? DEFAULT_ACTIVE_ARGB.Alpha,
                    windowConfig.ActiveArgb?.Red ?? Configuration.BaseActiveArgb?.Red ?? DEFAULT_ACTIVE_ARGB.Red,
                    windowConfig.ActiveArgb?.Green ?? Configuration.BaseActiveArgb?.Green ?? DEFAULT_ACTIVE_ARGB.Green,
                    windowConfig.ActiveArgb?.Blue ?? Configuration.BaseActiveArgb?.Blue ?? DEFAULT_ACTIVE_ARGB.Blue
                ),
                Color.FromArgb(
                    windowConfig.InactiveArgb?.Alpha ?? Configuration.BaseInactiveArgb?.Alpha ?? DEFAULT_INACTIVE_ARGB.Alpha,
                    windowConfig.InactiveArgb?.Red ?? Configuration.BaseInactiveArgb?.Red ?? DEFAULT_INACTIVE_ARGB.Red,
                    windowConfig.InactiveArgb?.Green ?? Configuration.BaseInactiveArgb?.Green ?? DEFAULT_INACTIVE_ARGB.Green,
                    windowConfig.InactiveArgb?.Blue ?? Configuration.BaseInactiveArgb?.Blue ?? DEFAULT_INACTIVE_ARGB.Blue
                ),
                Color.FromArgb(
                    windowConfig.MinimizedArgb?.Alpha ?? Configuration.BaseMinimizedArgb?.Alpha ?? DEFAULT_MINIMIZED_ARGB.Alpha,
                    windowConfig.MinimizedArgb?.Red ?? Configuration.BaseMinimizedArgb?.Red ?? DEFAULT_MINIMIZED_ARGB.Red,
                    windowConfig.MinimizedArgb?.Green ?? Configuration.BaseMinimizedArgb?.Green ?? DEFAULT_MINIMIZED_ARGB.Green,
                    windowConfig.MinimizedArgb?.Blue ?? Configuration.BaseMinimizedArgb?.Blue ?? DEFAULT_MINIMIZED_ARGB.Blue
                ),
                menuItem,
                (left, top) => {
                    windowConfig.X = left;
                    windowConfig.Y = top;
                }
            )
            {
                // BackgroundImage = CaptureWindow(hWnd, windowConfig.Width, windowConfig.Height),
                BackgroundImageLayout = ImageLayout.Stretch
            };

            MatchingConfigs.Add(preview, windowConfig);

            preview.Show();

            Application.DoEvents();

            Interlocked.Decrement(ref PreviewsWaitingToLoad);
        };

        Program.PostOnMainThread(action);
    }

    public static Bitmap CaptureWindow(IntPtr handle, int outputWidth, int outputHeight)
    {
        Bitmap bmp;
        
        // Get the size of the window
        var rect = new RECT();
        GetWindowRect(handle, ref rect);

        using(Bitmap fullscaleCapture = new Bitmap(rect.right - rect.left, rect.bottom - rect.top))
        {
            using(Graphics memoryGraphics = Graphics.FromImage(fullscaleCapture))
            {
                IntPtr dc1 = memoryGraphics.GetHdc();
                PrintWindow(handle, dc1, PW_RENDERFULLCONTENT);
                memoryGraphics.ReleaseHdc(dc1);
            }

            bmp = new Bitmap(outputWidth, outputHeight);
            using (Graphics shrunkGraphics = Graphics.FromImage(bmp))
            {
                shrunkGraphics.DrawImage(fullscaleCapture, 0, 0, outputWidth, outputHeight);
            }
        }

        return bmp;
    }

    public static Bitmap? GetIconFromWindow(IntPtr windowHandle)
    {
        if (windowHandle != IntPtr.Zero)
        {
            IntPtr hIcon = SendMessage(windowHandle, WM_GETICON, (IntPtr)1, IntPtr.Zero);

            if(hIcon != IntPtr.Zero)
            {
                using(Icon icon = Icon.FromHandle(hIcon))
                {
                    if (icon != null && icon.Handle != IntPtr.Zero)
                    {
                        return icon.ToBitmap();
                    }
                }
            }
        }

        return null;
    }
}