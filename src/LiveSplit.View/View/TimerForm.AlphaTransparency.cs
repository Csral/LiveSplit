using LiveSplit.Utils;
using System;
using System.Drawing;

namespace LiveSplit.View;

public partial class TimerForm
{
    private bool alphaTransparencyActive;
    private bool UsesAlphaTransparency => Layout.Settings.AlphaTransparency || Layout.Settings.TransparentBackground;

    private void UpdateAlphaTransparency()
    {
        if (alphaTransparencyActive == UsesAlphaTransparency)
        {
            return;
        }

        const int GWL_EXSTYLE = -20;
        const uint WS_EX_LAYERED = 0x00080000;

        Opacity = 1;
        uint style = GetWindowLong(Handle, GWL_EXSTYLE);
        // Reset the layered style to clear any previous SetLayeredWindowAttributes state.
        SetWindowLong(Handle, GWL_EXSTYLE, style & ~WS_EX_LAYERED);
        alphaTransparencyActive = UsesAlphaTransparency;
        if (alphaTransparencyActive)
        {
            SetWindowLong(Handle, GWL_EXSTYLE, style | WS_EX_LAYERED);
        }
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        alphaTransparencyActive = false;
        base.OnHandleDestroyed(e);
    }

    private void PaintAlphaWindow()
    {
        if (WindowState == System.Windows.Forms.FormWindowState.Minimized || Width <= 0 || Height <= 0)
        {
            return;
        }

        FixSize();
        KeepLayoutSize();
        MaintainMinimumSize();

        using Bitmap bitmap = LayeredWindowRenderer.CreateSurface(Size, Layout.Settings.Opacity);
        using var graphics = Graphics.FromImage(bitmap);

        // A layered window is a complete frame; partial invalidations must not erase other components.
        using var region = new Region(new Rectangle(Point.Empty, bitmap.Size));
        Region previousRegion = UpdateRegion;
        try
        {
            UpdateRegion = region;
            PaintForm(graphics, region);
        }
        finally
        {
            UpdateRegion = previousRegion;
        }

        if (bitmap.Size != Size)
        {
            Invalidate();
            return;
        }

        LayeredWindowRenderer.Draw(Handle, bitmap, Layout.Settings.Opacity);
    }
}
