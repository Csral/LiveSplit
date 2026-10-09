using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.Options.SettingsFactories;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Xunit;

namespace LiveSplit.Tests.UI;

public class TransparentComponentBackgroundMust
{
    [Theory]
    [InlineData(LayoutMode.Vertical)]
    [InlineData(LayoutMode.Horizontal)]
    public void RemoveOpaqueTimerBackgroundAndRestoreItWhenUnchecked(LayoutMode mode) => RunOnSta(() =>
    {
        LiveSplitState state = CreateState();
        var timer = new Timer();
        timer.Settings.BackgroundColor = Color.Black;
        timer.Settings.BackgroundColor2 = Color.Black;
        timer.Update(null, state, 300, 100, mode);
        var renderer = new ComponentRenderer { VisibleComponents = [timer] };

        using Bitmap opaque = Render(renderer, state, mode);
        Assert.Equal(255, opaque.GetPixel(0, 0).A);

        state.LayoutSettings.TransparentBackground = true;
        using Bitmap transparent = Render(renderer, state, mode);
        Assert.Equal(0, transparent.GetPixel(0, 0).A);
        Assert.True(HasVisibleContent(transparent));
        Assert.Equal(Color.Black, timer.Settings.BackgroundColor);

        state.LayoutSettings.TransparentBackground = false;
        using Bitmap restored = Render(renderer, state, mode);
        Assert.Equal(255, restored.GetPixel(0, 0).A);
    });

    [Fact]
    public void RemoveAlternatingSplitRowsAndCurrentSplitHighlight() => RunOnSta(() =>
    {
        LiveSplitState state = CreateState();
        state.CurrentPhase = TimerPhase.Paused;
        state.CurrentSplitIndex = 0;
        // Splits and Subsplits expose the same type names in separate plug-in assemblies.
        Type splitsType = Assembly.Load("LiveSplit.Splits").GetType("LiveSplit.UI.Components.SplitsComponent");
        using var splits = (IComponent)Activator.CreateInstance(splitsType, state);
        object settings = splits.GetSettingsControl(LayoutMode.Vertical);
        settings.GetType().GetProperty("BackgroundColor").SetValue(settings, Color.Black);
        settings.GetType().GetProperty("BackgroundColor2").SetValue(settings, Color.DimGray);
        settings.GetType().GetProperty("CurrentSplitTopColor").SetValue(settings, Color.Blue);
        settings.GetType().GetProperty("CurrentSplitBottomColor").SetValue(settings, Color.Blue);
        var renderer = new ComponentRenderer { VisibleComponents = [splits] };
        // The first draw builds the visible rows; the next update assigns their segments.
        using Bitmap initialFrame = Render(renderer, state, LayoutMode.Vertical);
        splits.Update(null, state, 300, 100, LayoutMode.Vertical);

        using Bitmap opaque = Render(renderer, state, LayoutMode.Vertical);
        Assert.True(opaque.GetPixel(0, 10).A > 0);
        Assert.True(HasVisibleContent(opaque));

        state.LayoutSettings.TransparentBackground = true;
        using Bitmap transparent = Render(renderer, state, LayoutMode.Vertical);
        Assert.Equal(0, transparent.GetPixel(0, 10).A);
        Assert.True(HasVisibleContent(transparent));
        Assert.Equal(Color.Black, settings.GetType().GetProperty("BackgroundColor").GetValue(settings));
        Assert.Equal(Color.DimGray, settings.GetType().GetProperty("BackgroundColor2").GetValue(settings));
        Assert.Equal(Color.Blue, settings.GetType().GetProperty("CurrentSplitTopColor").GetValue(settings));
        Assert.Single(renderer.VisibleComponents);
    });

    private static LiveSplitState CreateState()
    {
        var layout = new Layout { Settings = new StandardLayoutSettingsFactory().Create() };
        var run = new Run(new StandardComparisonGeneratorsFactory());
        run.Add(new Segment("Vengefly King"));
        run.Add(new Segment("Gruz Mother"));
        return new LiveSplitState(run, null, layout, layout.Settings, new LiveSplit.Options.Settings())
        {
            CurrentComparison = Run.PersonalBestComparisonName
        };
    }

    private static Bitmap Render(ComponentRenderer renderer, LiveSplitState state, LayoutMode mode)
    {
        renderer.CalculateOverallSize(mode);
        int height = mode == LayoutMode.Vertical ? Math.Max(100, (int)Math.Ceiling(renderer.OverallSize)) : 100;
        var bitmap = new Bitmap(300, height, PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(bitmap);
        using var region = new Region(new Rectangle(0, 0, 300, height));
        graphics.TextRenderingHint = TextRenderingHint.AntiAlias;
        graphics.Clear(Color.Transparent);
        renderer.Render(graphics, state, 300, height, mode, region);
        Assert.Single(renderer.VisibleComponents);
        return bitmap;
    }

    private static bool HasVisibleContent(Bitmap bitmap)
    {
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 1; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).A > 100)
                {
                    return true;
                }
            }
        }

        return false;
    }

    // Keep the WinForms synchronization context off xUnit's worker threads.
    private static void RunOnSta(Action action)
    {
        Exception error = null;
        var thread = new System.Threading.Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { error = ex; }
        }) { IsBackground = true };
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(10000), "Component rendering timed out.");

        if (error != null)
        {
            ExceptionDispatchInfo.Capture(error).Throw();
        }
    }
}
