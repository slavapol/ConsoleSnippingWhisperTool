using System.Drawing.Imaging;

namespace ConsoleSnippingTool.Win32;

public sealed partial class Win32Platform
{
    private const int TooltipMaxLength = 63;
    private const int BalloonMilliseconds = 3000;

    private readonly ToolStripMenuItem _showItem;
    private readonly ToolStripMenuItem _hideItem;
    private readonly ContextMenuStrip _trayMenu;
    private readonly NotifyIcon _trayIcon;

    // From Bitmap.GetHicon; Icon.FromHandle does not own it, so it is destroyed with the tray icon.
    private readonly nint _trayIconHandle;

    public void SetTrayTooltip(string text) =>
        _trayIcon.Text = text.Length <= TooltipMaxLength ? text : text[..TooltipMaxLength];

    public void SetTrayMenuState(bool showEnabled, bool hideEnabled)
    {
        _showItem.Enabled = showEnabled;
        _hideItem.Enabled = hideEnabled;
    }

    public void ShowBalloon(string title, string text) =>
        _trayIcon.ShowBalloonTip(BalloonMilliseconds, title, text, ToolTipIcon.Warning);

    public void RemoveTrayIcon()
    {
        _trayIcon.Visible = false;
        _trayIcon.Icon?.Dispose();
        _trayIcon.Dispose();
        _trayMenu.Dispose();
        NativeMethods.DestroyIcon(_trayIconHandle);
    }

    /// The tray glyph drawn in code, no .ico asset: a 2-px rectangle outline with a small tick at each corner, at
    /// 16×16 or 32×32 depending on the tray's small-icon size (the shell scales from there).
    private static nint CreateTrayIconHandle()
    {
        var size = SystemInformation.SmallIconSize.Width > 16 ? 32 : 16;
        var scale = size / 16;
        var inset = 4 * scale;
        var stroke = 2 * scale;
        var side = size - 2 * inset;
        var tick = 2 * scale;
        var near = scale;
        var far = size - near - tick;

        using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        using (var brush = new SolidBrush(Color.FromArgb(0, 120, 215)))
        {
            graphics.Clear(Color.Transparent);
            graphics.FillRectangle(brush, inset, inset, side, stroke);                  // top
            graphics.FillRectangle(brush, inset, inset + side - stroke, side, stroke);  // bottom
            graphics.FillRectangle(brush, inset, inset, stroke, side);                  // left
            graphics.FillRectangle(brush, inset + side - stroke, inset, stroke, side);  // right
            graphics.FillRectangle(brush, near, near, tick, tick);
            graphics.FillRectangle(brush, far, near, tick, tick);
            graphics.FillRectangle(brush, near, far, tick, tick);
            graphics.FillRectangle(brush, far, far, tick, tick);
        }

        return bitmap.GetHicon();
    }
}
