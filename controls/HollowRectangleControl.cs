using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace preveview;

public enum RectangleMode
{
    z_none = 0,
    Active = 1,
    Inactive = 2,
    Minimized = 3
}

public class HollowRectangleControl : ClickThroughControl
{
    PreviewForm ParentPreviewForm;

    readonly Pen ActivePen;
    readonly Pen InactivePen;
    readonly Pen MinimizedPen;

    public RectangleMode Mode;

    public readonly float BorderWidth;

    public HollowRectangleControl(PreviewForm parent, Color activeColor, Color inactiveColor, Color minimizedColor, float borderWidth)
    {
        ParentPreviewForm = parent;
        this.Left = 0;
        this.Top = 0;
        this.Width = parent.Width;
        this.Height = parent.Height;
        this.Visible = true;

        BorderWidth = borderWidth;

        Mode = RectangleMode.Inactive;

        ActivePen = new Pen(activeColor, borderWidth);
        InactivePen = new Pen(inactiveColor, borderWidth);
        MinimizedPen = new Pen(minimizedColor, borderWidth);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        try
        {
            base.OnPaint(e);

            // make sure graphics are smooth
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            // clear the old border
            e.Graphics.Clear(Color.Transparent);

            // draw the rectangle
            e.Graphics.DrawRectangle(
                Mode switch {
                    RectangleMode.Active => ActivePen,
                    RectangleMode.Minimized => MinimizedPen,
                    _ => InactivePen
                },
                0,
                0,
                this.Width,
                this.Height
            );
        }
        catch
        {}
    }
    
    public void ForceRepaint(bool fromUxThread = false)
    {
        if(fromUxThread)
        {
            this.InvokePaint(this, new PaintEventArgs(this.CreateGraphics(), this.ClientRectangle));
        }
        else
        {
            ParentPreviewForm.RunActionOnFormThread(
                () =>
                {
                    this.InvokePaint(this, new PaintEventArgs(this.CreateGraphics(), this.ClientRectangle));
                }
            );
        }
    }

    ~HollowRectangleControl()
    {
        ActivePen.Dispose();
        InactivePen.Dispose();
        MinimizedPen.Dispose();
    }
}