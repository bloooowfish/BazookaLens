using System;
using System.Text;

namespace BazookaLens.Diagnostics;

internal sealed class RuntimeStatusService
{
    public string BuildStatusText()
    {
        PluginServices.Log.Debug("Building Bazooka Lens runtime status snapshot.");

        var config = GameConfigSnapshot.Capture(PluginServices.GameConfig);
        var render = RenderResolutionSnapshot.Capture();

        var sb = new StringBuilder();
        sb.AppendLine("Bazooka Lens runtime status");
        sb.AppendLine($"TimestampLocal={DateTimeOffset.Now:O}");
        sb.AppendLine($"TimestampUtc={DateTimeOffset.UtcNow:O}");
        sb.Append(config.ToLogBlock());
        sb.Append(render.ToLogBlock());
        return sb.ToString();
    }
}
