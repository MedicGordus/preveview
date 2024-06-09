using System;
using System.Drawing;
using System.Drawing.Design;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace preveview;

public class HollowCircleControl : ClickThroughControl
{
    private double fillPercentage = 0.0;

    PreviewForm ParentPreviewForm;

    const int RadiusBuffer = 2;
    const int DiameterBuffer = RadiusBuffer * 2;

    const int Radius = 25;
    
    const int Diameter = Radius * 2;

    const float PenWidth = 2f;

    static readonly Color BACKCOLOR = Color.FromArgb(255, 0, 162, 232);

    Task? BackgroundTask;
    Task? PreviousBackgroundTask;

    readonly Pen PiePen;

    readonly Brush EllipseBrush;

    public HollowCircleControl(PreviewForm parent)
    {
        ParentPreviewForm = parent;
        this.Left = -RadiusBuffer;
        this.Top = -RadiusBuffer;
        this.Width = Diameter + DiameterBuffer;
        this.Height = Diameter + DiameterBuffer;
        this.Visible = false;
        this.BackColor = BACKCOLOR;
        PiePen = new Pen(Color.Black, PenWidth);
        EllipseBrush = new SolidBrush(Color.Black);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        try
        {
            base.OnPaint(e);

            // make sure graphics are smooth
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            if(fillPercentage < 1.0)
            {
                int startAngle = 0;
                int sweepAngle = (int)Math.Round(fillPercentage * (double)360.0,0);

                // Draw the pie chart
                e.Graphics.DrawPie(PiePen, 0, 0, Diameter, Diameter, startAngle, sweepAngle);
            }
            else
            {
                // Draw filled in circle
                e.Graphics.FillEllipse(EllipseBrush, 0, 0, Diameter, Diameter);
            }
        }
        catch
        {}
    }
    

    public void StartMovementTimer()
    {
        PreviousBackgroundTask = BackgroundTask;

        BackgroundTask = Task.Run(MovementTimerAsync);
        this.Left = Control.MousePosition.X - (ParentPreviewForm.Left + Radius);
        this.Top = Control.MousePosition.Y - (ParentPreviewForm.Top + Radius);
        this.Visible = true;
    }

    public async Task MovementTimerAsync()
    {
        if(PreviousBackgroundTask != null)
        {
            await PreviousBackgroundTask.ConfigureAwait(false);
        }

        long? ticksStarted = ParentPreviewForm.TicksStartedToMove;
        while(ticksStarted != null)
        {
            TimeSpan timeElapsed = new TimeSpan(DateTime.Now.Ticks - ticksStarted.Value);

            double percentTimeElapsed = timeElapsed.TotalMilliseconds / (double)ParentPreviewForm.MillisecondDelayToMove;
            if(percentTimeElapsed >= 1.0)
            {
                percentTimeElapsed = 1.0;
            }

            fillPercentage = percentTimeElapsed;

            // re draw the circle
            await RedrawCircleAsync().ConfigureAwait(false);

            await Task.Delay(10).ConfigureAwait(false);

            ticksStarted = ParentPreviewForm.TicksStartedToMove;
        }

        fillPercentage = 0.0;
        await RedrawCircleAsync().ConfigureAwait(false);

        ParentPreviewForm.RunActionOnFormThread(
            () => {
                this.Visible = false;
            }
        );
    }

    private bool AreadyPainting = false;
    private bool ForcePaint = false;

    public async Task RedrawCircleAsync()
    {
        if(AreadyPainting & !ForcePaint)
        {
            return;
        }

        AreadyPainting = true;

        TaskCompletionSource tcs = new TaskCompletionSource();

        Action actionToRun = () => {
            // this doesn't seem to do anything anyway
            //ParentPreviewForm.Title.ForceRepaint();
            
            Invalidate();

            tcs.SetResult();
        };

        ParentPreviewForm.RunActionOnFormThread(actionToRun);

        await tcs.Task;

        AreadyPainting = false;
    }

    ~HollowCircleControl()
    {
        PiePen.Dispose();
        EllipseBrush.Dispose();
    }
}