using BazookaLens.Diagnostics;

namespace BazookaLens.Tests;

public sealed class ReShadeExportProbeTests
{
    private static readonly HashSet<string> ReShadeAddonExports =
    [
        "ReShadeRegisterAddon",
        "ReShadeUnregisterAddon",
        "ReShadeRegisterEvent",
        "ReShadeUnregisterEvent",
    ];

    [Fact]
    public void ProbeModulesDetectsAddonExportsInProxyNamedDxgi()
    {
        var module = new ReShadeModuleCandidate(
            "dxgi.dll",
            @"C:\Game\dxgi.dll",
            (nint)0x1000,
            "ReShade",
            "ReShade",
            "crosire",
            "dxgi.dll");

        var result = ReShadeExportProbe.ProbeModules(
            [module],
            (_, exportName) => ReShadeAddonExports.Contains(exportName) ? (nint)0x2000 : nint.Zero);

        Assert.True(result.Detected);
        Assert.Equal("dxgi.dll", result.ModuleName);
        Assert.Equal(@"C:\Game\dxgi.dll", result.ModulePath);
        Assert.Equal(1, result.ScannedModuleCount);
        Assert.Equal(result.RequiredExports.Count, result.FoundExports.Count);
        var candidate = Assert.Single(result.CandidateModules);
        Assert.True(candidate.LooksLikeReShade);
    }

    [Fact]
    public void ProbeModulesReportsReShadeRuntimeCandidateWithoutAddonExports()
    {
        var module = new ReShadeModuleCandidate(
            "dxgi.dll",
            @"C:\Game\dxgi.dll",
            (nint)0x1000,
            "ReShade",
            "ReShade",
            "crosire",
            "dxgi.dll");

        var result = ReShadeExportProbe.ProbeModules([module], (_, _) => nint.Zero);

        Assert.False(result.Detected);
        Assert.Empty(result.FoundExports);
        var candidate = Assert.Single(result.CandidateModules);
        Assert.True(candidate.LooksLikeReShade);
        Assert.Empty(candidate.FoundExports);
    }

    [Fact]
    public void ProbeModulesIgnoresOrdinaryModulesWithoutExportsOrMetadata()
    {
        var modules = new[]
        {
            new ReShadeModuleCandidate("kernel32.dll", @"C:\Windows\System32\kernel32.dll", (nint)0x1000, null, "Microsoft Windows", "Microsoft", "kernel32.dll"),
            new ReShadeModuleCandidate("game.dll", @"C:\Game\game.dll", (nint)0x2000, "Game", "Game", "Game Studio", "game.dll"),
        };

        var result = ReShadeExportProbe.ProbeModules(modules, (_, _) => nint.Zero);

        Assert.False(result.Detected);
        Assert.Equal(2, result.ScannedModuleCount);
        Assert.Empty(result.CandidateModules);
    }
}
