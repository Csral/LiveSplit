using LiveSplit.Options.SettingsFactories;
using LiveSplit.UI;
using LiveSplit.UI.LayoutFactories;
using LiveSplit.UI.LayoutSavers;
using System.Drawing;
using System.IO;
using System.Xml;
using Xunit;

namespace LiveSplit.Tests.UI;

public class LayoutAlphaTransparencyMust
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RoundtripTransparencyAndBackgroundAlpha(bool enabled)
    {
        var layout = new Layout { Settings = new StandardLayoutSettingsFactory().Create() };
        layout.Settings.AlphaTransparency = enabled;
        layout.Settings.TransparentBackground = enabled;
        layout.Settings.BackgroundColor = Color.FromArgb(73, 10, 20, 30);
        layout.Settings.Opacity = 0.8f;
        using var stream = new MemoryStream();
        new XMLLayoutSaver().Save(layout, stream);
        stream.Position = 0;

        ILayout loaded = new XMLLayoutFactory(stream).Create(null);

        Assert.Equal(enabled, loaded.Settings.AlphaTransparency);
        Assert.Equal(enabled, loaded.Settings.TransparentBackground);
        Assert.Equal(layout.Settings.BackgroundColor, loaded.Settings.BackgroundColor);
        Assert.Equal(0.8f, loaded.Settings.Opacity);
    }

    [Fact]
    public void KeepOldLayoutsOpaqueWhenSettingIsMissing()
    {
        var layout = new Layout { Settings = new StandardLayoutSettingsFactory().Create() };
        layout.Settings.AlphaTransparency = true;
        using var stream = new MemoryStream();
        new XMLLayoutSaver().Save(layout, stream);
        stream.Position = 0;
        var document = new XmlDocument();
        document.Load(stream);
        XmlNode setting = document.SelectSingleNode("/Layout/Settings/AlphaTransparency");
        setting.ParentNode.RemoveChild(setting);
        XmlNode backgroundSetting = document.SelectSingleNode("/Layout/Settings/TransparentBackground");
        backgroundSetting.ParentNode.RemoveChild(backgroundSetting);
        using var legacyStream = new MemoryStream();
        document.Save(legacyStream);
        legacyStream.Position = 0;

        ILayout loaded = new XMLLayoutFactory(legacyStream).Create(null);

        Assert.False(loaded.Settings.AlphaTransparency);
        Assert.False(loaded.Settings.TransparentBackground);
    }
}
