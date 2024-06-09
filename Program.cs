using System.Diagnostics;
using System.Text.Json;

namespace preveview;

static class Program
{
    public static readonly ContextMenuStrip TRAY_MENU = new ContextMenuStrip();

    public static readonly string CONFIG_PATH = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

    public static bool ExitingApp = false;

    public static SynchronizationContext? CONTEXT;

    private static Configuration? ConfigurationContents;

    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        try
        {
            //ApplicationConfiguration.Initialize();
   

            // load config
            var json = File.ReadAllText(CONFIG_PATH);
            ConfigurationContents = System.Text.Json.JsonSerializer.Deserialize<Configuration?>(json) ?? new Configuration();


            // load menu for tray icon
            TRAY_MENU.Items.Add("Exit", null, OnExitMenuItemClick);
            TRAY_MENU.Items.Add("-", null, null);
            TRAY_MENU.Items.Add("Backup", null, OnBackupMenuItemClick);
            TRAY_MENU.Items.Add("Save", null, OnSaveMenuItemClick);
            TRAY_MENU.Items.Add(
                new ToolStripMenuItem("Toggle Visibility", null, OnToggleVisibilityClick)
                {
                    CheckOnClick = true,
                    Checked = true
                }
            );
            TRAY_MENU.Items.Add("-", null, null);


            // load tray icon
            NotifyIcon trayIcon = new NotifyIcon();
            trayIcon.Icon = Icon.ExtractAssociatedIcon(Environment.GetCommandLineArgs()[0]);
            trayIcon.Visible = true;
            trayIcon.ContextMenuStrip = TRAY_MENU;
            trayIcon.MouseDown += (sender, e) =>
            {
                TRAY_MENU.Show(Cursor.Position);
            };

            // capture the current sync context
            CONTEXT = SynchronizationContext.Current;

            // start monitor
            var monitor = new WindowMonitor(ConfigurationContents);
            monitor.StartMonitoring();


            // poll until all forms are closed
            Application.Run();


            // clean up
            ExitingApp = true;
            trayIcon.Visible = false;


            // exit program
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    public static void PostOnMainThread(Action action)
    {
        if(CONTEXT != null)
        {
            CONTEXT.Post((state) => action(), null);
        }
    }

    private static void OnToggleVisibilityClick(object? sender, EventArgs e)
    {
        foreach (Form deltaForm in Application.OpenForms)
        {
            if (deltaForm is PreviewForm)
            {
                deltaForm.Visible = !deltaForm.Visible;
            }
        }
    }

    private static void OnBackupMenuItemClick(object? sender, EventArgs e)
    {
        if(ConfigurationContents == null)
        {
            MessageBox.Show("No configuration loaded.", Application.ProductName, MessageBoxButtons.OK,MessageBoxIcon.Exclamation);
            return;
        }
        int backupIncrementer = 0;
        string backupFileName;
        do
        {
            backupIncrementer++;
            backupFileName = GetBackupPath(backupIncrementer);
        } while(File.Exists(backupFileName));

        try
        {
            ConfigurationContents.Save(backupFileName);
                
            MessageBox.Show(
                string.Format(
                    "Configuration backup complete, '{0}'.",
                    backupFileName
                ),
                Application.ProductName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }
        catch(Exception exception)
        {
            MessageBox.Show(
                string.Format(
                    "Backup failed, the message was: '{0}'.",
                    exception.Message
                ),
                Application.ProductName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }

    private static string GetBackupPath(int incrementer)
    {
        return string.Format(
            "{0}.backup{1}",
            CONFIG_PATH,
            incrementer
        );
    }

    private static void OnSaveMenuItemClick(object? sender, EventArgs e)
    {
        if(ConfigurationContents == null)
        {
            MessageBox.Show("No configuration loaded.", Application.ProductName, MessageBoxButtons.OK,MessageBoxIcon.Exclamation);
            return;
        }

        if(MessageBox.Show("Overwrite configuration?", Application.ProductName, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            try
            {
                ConfigurationContents.Save();
                
                MessageBox.Show("Configuration saved.", Application.ProductName, MessageBoxButtons.OK,MessageBoxIcon.Information);
            }
            catch(Exception exception)
            {
                MessageBox.Show(
                    string.Format(
                        "Save failed, the message was: '{0}'.",
                        exception.Message
                    ),
                    "",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }
    }

    private static void OnExitMenuItemClick(object? sender, EventArgs e)
    {
        Application.Exit();
    }
}