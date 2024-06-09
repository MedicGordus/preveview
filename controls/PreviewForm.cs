using System.DirectoryServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace preveview;

public class PreviewForm : Form
{
    public const int DEFAULT_PREVIEW_FPS = 10;

    public Point? MouseOffset;

    public bool Moving = false;

    public IntPtr WindowHandle;

    public int MillisecondDelayToMove;

    public long? TicksStartedToMove;

    int MagnetizeDistance;

    int ScreenIndex;

    private readonly HollowCircleControl CircleControl;

    private readonly  TitleLabel TitleLabel;

    public readonly HollowRectangleControl BorderControl;

    public readonly string TitleFromConfiguration;

    public readonly ToolStripMenuItem MenuItem;

    public readonly IntPtr hThumbnailId;

    public readonly double ConfiguredOpacity;

    public Action<int, int> UpdatePositionInConfigurationAction;

    public PreviewForm(IntPtr hWnd, int millisecondDelayToMove, double opacity, int magnetizeDistance, int screenIndex, string configTitle, string myTitle, int left, int top, int width, int height, bool showTitleBar, int borderWidth, Color activeBorderColor, Color inactiveBorderColor, Color minimizedBorderColor, ToolStripMenuItem menuItem, Action<int, int> updatePosition)
    {
        WindowHandle = hWnd;
        MillisecondDelayToMove = millisecondDelayToMove;
        ConfiguredOpacity = opacity;
        MagnetizeDistance = magnetizeDistance;
        ScreenIndex = screenIndex;
        TitleFromConfiguration = configTitle;
        this.Text = myTitle;
        this.Left = left - borderWidth;
        this.Top = top - borderWidth;
        this.Width = width + (2 * borderWidth);
        this.Height = height + (2 * borderWidth);
        MenuItem = menuItem;
        UpdatePositionInConfigurationAction = updatePosition;

        int opacityOf255 = (int)(Math.Round(255d * opacity));

        this.MouseDown += PreviewForm_MouseDown;
        this.MouseMove += PreviewForm_MouseMove;
        this.Click += PreviewForm_Click;
        this.FormClosing += PreviewForm_FormClosing;

        this.MouseHover += PreviewForm_MouseHover;
        this.MouseLeave += PreviewForm_MouseLeave;
        
        this.FormBorderStyle = FormBorderStyle.None;
        this.StartPosition = FormStartPosition.Manual;
        this.TopMost = true;
        this.ShowInTaskbar = false;
        this.DoubleBuffered = true;
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        this.BackColor = Color.FromArgb(opacityOf255,Color.Black);
        this.Opacity = opacity;


        // add a hidden circle for showing the timer to move the preview
        CircleControl = new HollowCircleControl(this);

        // add border control
        BorderControl = new HollowRectangleControl(this, activeBorderColor, inactiveBorderColor, minimizedBorderColor, borderWidth);

        // add title control
        TitleLabel = new TitleLabel(this, configTitle);

        // add controls to the form, firstmost is the topmost
        this.Controls.Add(CircleControl);
        this.Controls.Add(TitleLabel);
        this.Controls.Add(BorderControl);


        // force a repaint of the text
        TitleLabel.ForceRepaint();

        // setup menu item
        MenuItem.Click += PreviewForm_Click;
        Program.TRAY_MENU.Items.Add(MenuItem);


        //// setup the preview
        //
        // Register the thumbnail
        dwm_api.DwmRegisterThumbnail(this.Handle, hWnd, out hThumbnailId);
        //
        // Set the thumbnail properties
        dwm_api.DWM_THUMBNAIL_PROPERTIES props = new dwm_api.DWM_THUMBNAIL_PROPERTIES
        {
            dwFlags =
                dwm_api.DWM_THUMBNAIL_PROPERTIES.DWM_TNP_VISIBLE
            |
                dwm_api.DWM_THUMBNAIL_PROPERTIES.DWM_TNP_RECTDESTINATION
            |
                dwm_api.DWM_THUMBNAIL_PROPERTIES.DWM_TNP_OPACITY
            |
                dwm_api.DWM_THUMBNAIL_PROPERTIES.DWM_TNP_SOURCECLIENTAREAONLY,
            rcDestination = new user32_api.RECT(borderWidth, borderWidth, this.Width - borderWidth, this.Height - borderWidth),
            iOpacity = opacityOf255,
            fVisible = true,
            fSourceClientAreaOnly = !showTitleBar
        };
        dwm_api.DwmUpdateThumbnailProperties(hThumbnailId, ref props);
        //
        // Unregister the thumbnail when the form is closed
        this.FormClosed += PreviewForm_FormClosed;
        //
        ////
    }

    private void PreviewForm_MouseLeave(object? sender, EventArgs e)
    {
        this.Opacity = ConfiguredOpacity;
    }

    private void PreviewForm_MouseHover(object? sender, EventArgs e)
    {
        this.Opacity = 1;
    }

    public void PreviewForm_FormClosed(object? sender, FormClosedEventArgs e)
    {
        dwm_api.DwmUnregisterThumbnail(hThumbnailId);
    }

    public void PreviewForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if(Program.TRAY_MENU.Items.Contains(MenuItem))
        {
            Program.TRAY_MENU.Items.Remove(MenuItem);
        }
    }

    public void PreviewForm_MouseDown(object? sender, MouseEventArgs e) 
    {
        this.Capture = true;
        if (e.Button == MouseButtons.Left)
        {
            this.MouseOffset = new Point(
                Control.MousePosition.X - this.Left,
                Control.MousePosition.Y - this.Top
            );

            TicksStartedToMove = DateTime.Now.Ticks;

            CircleControl.StartMovementTimer();
        }
    }

    public void PreviewForm_MouseMove(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            bool okToMove = false;
            if(this.Moving)
            {
                okToMove = true;
            }
            else if(TicksStartedToMove != null && new TimeSpan(DateTime.Now.Ticks - TicksStartedToMove.Value).TotalMilliseconds >= (double)MillisecondDelayToMove)
            {
                okToMove = true;
            }
            else
            {
                if(this.MouseOffset == null)
                {                        
                    // clear movement time
                    TicksStartedToMove = null;
                }
                else if(Control.MousePosition.X - this.Left != this.MouseOffset.Value.X | Control.MousePosition.Y - this.Top != this.MouseOffset.Value.Y)
                {
                    // clear movement time
                    TicksStartedToMove = null;
                }
            }

            if (okToMove)
            {
                var mousePosition = Control.MousePosition;

                Point mouseOffset = this.MouseOffset ?? new Point(
                        Control.MousePosition.X - this.Left,
                        Control.MousePosition.Y - this.Top
                );
                
                int newLeft = mousePosition.X - mouseOffset.X;
                int newTop = mousePosition.Y - mouseOffset.Y;



                //// Check for magnetizing to edges
                //
                int screenWidth = Screen.AllScreens[ScreenIndex].Bounds.Width;
                int screenHeight = Screen.AllScreens[ScreenIndex].Bounds.Height;
                //
                MagneticDirection screenMagnetic = CheckIfMagnetized(
                    newLeft,
                    newTop,
                    newLeft + this.Width,
                    newTop + this.Height,
                    Screen.AllScreens[ScreenIndex].Bounds.Right,
                    Screen.AllScreens[ScreenIndex].Bounds.Bottom,
                    Screen.AllScreens[ScreenIndex].Bounds.Left,
                    Screen.AllScreens[ScreenIndex].Bounds.Top
                );
                //
                if (screenMagnetic.HasFlag(MagneticDirection.Left))
                {
                    newLeft = Screen.AllScreens[ScreenIndex].Bounds.Left;
                }
                else if (screenMagnetic.HasFlag(MagneticDirection.Right))
                {
                    newLeft = Screen.AllScreens[ScreenIndex].Bounds.Width - this.Width;
                }
                //
                if (screenMagnetic.HasFlag(MagneticDirection.Top))
                {
                    newTop = Screen.AllScreens[ScreenIndex].Bounds.Top;
                }
                else if (screenMagnetic.HasFlag(MagneticDirection.Bottom))
                {
                    newTop = Screen.AllScreens[ScreenIndex].Bounds.Bottom - this.Height;
                }
                //
                // Check for nearby windows and adjust location
                foreach (Form form in Application.OpenForms)
                {
                    if (form != this && form is PreviewForm)
                    {
                        MagneticDirection windowMagnetic = CheckIfMagnetized(
                            newLeft,
                            newTop,
                            newLeft + this.Width,
                            newTop + this.Height,
                            form.Bounds.Left,
                            form.Bounds.Top,
                            form.Bounds.Right,
                            form.Bounds.Bottom
                        );

                        bool secondaryMagnetize = true;

                        if (windowMagnetic.HasFlag(MagneticDirection.Left))
                        {
                            newLeft = form.Bounds.Right;
                        }
                        else if (windowMagnetic.HasFlag(MagneticDirection.Right))
                        {
                            newLeft = form.Bounds.Left - this.Width;
                        }
                        else
                        {
                            secondaryMagnetize = false;
                        }

                        if (secondaryMagnetize)
                        {
                            MagneticDirection secondaryWindowMagnetic = CheckIfMagnetized(
                                newLeft,
                                newTop,
                                newLeft + this.Width,
                                newTop + this.Height,
                                form.Bounds.Right,
                                form.Bounds.Bottom,
                                form.Bounds.Left,
                                form.Bounds.Top
                            );
                            

                            if (secondaryWindowMagnetic.HasFlag(MagneticDirection.Top))
                            {
                                newTop = form.Bounds.Top;
                            }
                            else if (secondaryWindowMagnetic.HasFlag(MagneticDirection.Bottom))
                            {
                                newTop = form.Bounds.Bottom - this.Height;
                            }
                        }

                        // reset flag
                        secondaryMagnetize = true;

                        if (windowMagnetic.HasFlag(MagneticDirection.Top))
                        {
                            newTop = form.Bounds.Bottom;
                        }
                        else if (windowMagnetic.HasFlag(MagneticDirection.Bottom))
                        {
                            newTop = form.Bounds.Top - this.Height;
                        }
                        else
                        {
                            secondaryMagnetize = false;
                        }

                        if (secondaryMagnetize)
                        {
                            MagneticDirection secondaryWindowMagnetic = CheckIfMagnetized(
                                newLeft,
                                newTop,
                                newLeft + this.Width,
                                newTop + this.Height,
                                form.Bounds.Right,
                                form.Bounds.Bottom,
                                form.Bounds.Left,
                                form.Bounds.Top
                            );
                            

                            if (secondaryWindowMagnetic.HasFlag(MagneticDirection.Left))
                            {
                                newLeft = form.Bounds.Left;
                            }
                            else if (secondaryWindowMagnetic.HasFlag(MagneticDirection.Right))
                            {
                                newLeft = form.Bounds.Right - this.Width;
                            }
                        }
                    }
                }
                //
                ////

                
                this.Location = new Point(
                    newLeft,
                    newTop
                );

                this.Moving = true;
            }
        }
    }

    public void UpdateBorder(RectangleMode mode)
    {
        Action action = () => {
            // if the mode changed, we force a repaint
            if(this.BorderControl.Mode != mode)
            {
                this.BorderControl.Mode = mode;
                this.BorderControl.ForceRepaint(true);
            }
        };

        RunActionOnFormThread(action);
    }

    public MagneticDirection CheckIfMagnetized(int left1, int top1, int right1, int bottom1, int left2, int top2, int right2, int bottom2)
    {
        MagneticDirection output = MagneticDirection.None;

        if (Math.Abs(right2 - left1) <= MagnetizeDistance)
        {
            output |= MagneticDirection.Left;
        }
        else if (Math.Abs(left2 - right1) <= MagnetizeDistance)
        {
            output |= MagneticDirection.Right;
        }

        if (Math.Abs(bottom2 - top1) <= MagnetizeDistance)
        {
            output |= MagneticDirection.Top;
        }
        else if (Math.Abs(top2 - bottom1) <= MagnetizeDistance)
        {
            output |= MagneticDirection.Bottom;
        }

        return output;
    }

    [Flags]
    public enum MagneticDirection
    {
        None = 0,
        Left = 1,
        Top  = 2,
        Right = 4,
        Bottom = 8
    }

    public void PreviewForm_Click(object? sender, EventArgs e)
    {
        // clear movement time
        TicksStartedToMove = null;

        // check if user was moving the window, if so, we don't want to switch focus
        if(this.Moving)
        {
            // turn moving flag off
            this.Moving = false;

            UpdatePositionInConfigurationAction(this.Left, this.Top);

            if(CircleControl.Visible)
            {
                CircleControl.Visible = false;
            }
        }
        else
        {
            // Bring the window to the foreground
            user32_api.SetForegroundWindow(WindowHandle);

            // If the window is minimized, restore it
            if (user32_api.IsIconic(WindowHandle))
            {
                user32_api.ShowWindow(WindowHandle, user32_api.SW_RESTORE);
            }
        }
    }

    public void RunActionOnFormThread(Action action)
    {
        if(this.InvokeRequired)
        {
            this.Invoke(action);
        }
        else
        {
            action();
        }
    }
}