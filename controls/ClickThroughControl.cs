
namespace preveview;

public class ClickThroughControl : Control
{
    public ClickThroughControl()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        this.BackColor = Color.Transparent;
        this.DoubleBuffered = true;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x84) // WM_NCHITTEST
        {
            // HTTRANSPARENT = -1
            m.Result = (IntPtr)(-1);
            return;
        }
        base.WndProc(ref m);
    }
}