using System;
using System.Text;

using Dalamud.Game.Config;
using Dalamud.Plugin.Services;

namespace BazookaLens.Diagnostics;

internal sealed record GameConfigSnapshot(
    string ScreenMode,
    string ScreenLeft,
    string ScreenTop,
    string ScreenWidth,
    string ScreenHeight,
    string FullScreenWidth,
    string FullScreenHeight,
    string GraphicsRezoScale,
    string GraphicsRezoUpscaleType)
{
    public static GameConfigSnapshot Capture(IGameConfig gameConfig)
    {
        return new GameConfigSnapshot(
            ReadUInt(gameConfig, SystemConfigOption.ScreenMode),
            ReadUInt(gameConfig, SystemConfigOption.ScreenLeft),
            ReadUInt(gameConfig, SystemConfigOption.ScreenTop),
            ReadUInt(gameConfig, SystemConfigOption.ScreenWidth),
            ReadUInt(gameConfig, SystemConfigOption.ScreenHeight),
            ReadUInt(gameConfig, SystemConfigOption.FullScreenWidth),
            ReadUInt(gameConfig, SystemConfigOption.FullScreenHeight),
            ReadUInt(gameConfig, SystemConfigOption.GraphicsRezoScale),
            ReadUInt(gameConfig, SystemConfigOption.GraphicsRezoUpscaleType));
    }

    public string ToLogBlock(string label = "GameConfig")
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{label}:");
        sb.AppendLine($"  ScreenMode={this.ScreenMode}");
        sb.AppendLine($"  ScreenLeft={this.ScreenLeft}");
        sb.AppendLine($"  ScreenTop={this.ScreenTop}");
        sb.AppendLine($"  ScreenWidth={this.ScreenWidth}");
        sb.AppendLine($"  ScreenHeight={this.ScreenHeight}");
        sb.AppendLine($"  FullScreenWidth={this.FullScreenWidth}");
        sb.AppendLine($"  FullScreenHeight={this.FullScreenHeight}");
        sb.AppendLine($"  GraphicsRezoScale={this.GraphicsRezoScale}");
        sb.AppendLine($"  GraphicsRezoUpscaleType={this.GraphicsRezoUpscaleType}");
        return sb.ToString();
    }

    public uint? TryGetScreenMode()
    {
        return uint.TryParse(this.ScreenMode, out var mode) ? mode : null;
    }

    private static string ReadUInt(IGameConfig gameConfig, SystemConfigOption option)
    {
        try
        {
            return gameConfig.TryGet(option, out uint value) ? value.ToString() : "<unavailable>";
        }
        catch (Exception ex)
        {
            return $"<error: {ex.GetType().Name}: {ex.Message}>";
        }
    }
}
