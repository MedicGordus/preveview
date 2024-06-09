using System.Drawing.Drawing2D;
using System.Security.Cryptography.Xml;


namespace preveview;

public class TitleLabel : ClickThroughControl
{
    private string TitleFromParent;


    public const float TitleFontSize = 16;

    public const string TitleFontName = "Arial";

    public static readonly FontFamily TitleFontFamily = new FontFamily(TitleFontName);

    public static readonly Font TitleFont = new Font(TitleFontFamily, TitleFontSize, FontStyle.Regular);

    public static readonly SolidBrush TitleBrush = new SolidBrush(Color.White);

    PreviewForm ParentPreviewForm;

    public int SurroundingBorderWidth;

    public TitleLabel(PreviewForm parent, string title)
    {
        ParentPreviewForm = parent;
        TitleFromParent = title;

        SurroundingBorderWidth = (int)Math.Round(ParentPreviewForm.BorderControl.BorderWidth,0);

        this.Location = new Point(
            SurroundingBorderWidth,
            SurroundingBorderWidth
        );
        this.Height = parent.Height - (SurroundingBorderWidth * 2);
        this.Width = parent.Width - (SurroundingBorderWidth * 2);
        this.Visible = true;
    }

    public void ForceRepaint()
    {
        ParentPreviewForm.RunActionOnFormThread(
            () =>
            {
                this.Height = ParentPreviewForm.Height - (SurroundingBorderWidth * 2);
                this.Width = ParentPreviewForm.Width - (SurroundingBorderWidth * 2);
                this.InvokePaint(this, new PaintEventArgs(this.CreateGraphics(), this.ClientRectangle));
            }
        );
    }
    
    protected override void OnPaint(PaintEventArgs e)
    {
        try
        {
            base.OnPaint(e);
            
            // make sure graphics are smooth
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            e.Graphics.DrawString(TitleFromParent, TitleFont, TitleBrush, 0, 0);
        }
        catch
        {}
    }

    ~TitleLabel()
    {
        TitleBrush.Dispose();
        TitleFont.Dispose();
        TitleFontFamily.Dispose();
    }
}