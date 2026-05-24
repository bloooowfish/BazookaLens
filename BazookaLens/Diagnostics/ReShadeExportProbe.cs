using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using BazookaLens.Interop;

namespace BazookaLens.Diagnostics;

internal sealed record ReShadeExportProbeResult(
    bool Detected,
    string? ModuleName,
    string? ModulePath,
    IReadOnlyList<string> FoundExports,
    IReadOnlyList<string> RequiredExports,
    int ScannedModuleCount,
    IReadOnlyList<ReShadeModuleScanResult> CandidateModules);

internal sealed record ReShadeModuleCandidate(
    string ModuleName,
    string? ModulePath,
    nint BaseAddress,
    string? FileDescription,
    string? ProductName,
    string? CompanyName,
    string? OriginalFilename);

internal sealed record ReShadeModuleScanResult(
    string ModuleName,
    string? ModulePath,
    nint BaseAddress,
    string? FileDescription,
    string? ProductName,
    string? CompanyName,
    string? OriginalFilename,
    bool LooksLikeReShade,
    IReadOnlyList<string> FoundExports);

internal sealed class ReShadeExportProbe
{
    private static readonly string[] RequiredExports =
    [
        "ReShadeRegisterAddon",
        "ReShadeUnregisterAddon",
        "ReShadeRegisterEvent",
        "ReShadeUnregisterEvent",
    ];

    public ReShadeExportProbeResult Probe()
    {
        PluginServices.Log.Debug("Starting ReShade export probe.");

        var modules = Process.GetCurrentProcess()
            .Modules
            .Cast<ProcessModule>()
            .Select(ToModuleCandidate)
            .ToArray();

        PluginServices.Log.Information("ReShade export probe scanning all process modules: Count={ModuleCount}", modules.Length);
        var result = ProbeModules(modules, NativeMethods.GetProcAddress);

        foreach (var candidate in result.CandidateModules)
        {
            PluginServices.Log.Information(
                "ReShade module candidate scanned: Name={ModuleName}, Path={ModulePath}, Base={BaseAddress}, Product={ProductName}, Description={FileDescription}, Company={CompanyName}, OriginalFilename={OriginalFilename}, LooksLikeReShade={LooksLikeReShade}, FoundExports={FoundExports}",
                candidate.ModuleName,
                candidate.ModulePath ?? "<module-path-null>",
                candidate.BaseAddress,
                candidate.ProductName ?? "<null>",
                candidate.FileDescription ?? "<null>",
                candidate.CompanyName ?? "<null>",
                candidate.OriginalFilename ?? "<null>",
                candidate.LooksLikeReShade,
                string.Join(",", candidate.FoundExports));
        }

        if (result.Detected)
        {
            PluginServices.Log.Information(
                "ReShade addon exports detected: Module={ModuleName}, Path={ModulePath}, ScannedModules={ScannedModuleCount}",
                result.ModuleName ?? "<module-name-null>",
                result.ModulePath ?? "<module-path-null>",
                result.ScannedModuleCount);
        }
        else if (result.CandidateModules.Any(candidate => candidate.LooksLikeReShade))
        {
            PluginServices.Log.Information(
                "ReShade runtime candidate was found, but all required addon exports were not detected: ScannedModules={ScannedModuleCount}",
                result.ScannedModuleCount);
        }
        else
        {
            PluginServices.Log.Information(
                "ReShade export probe did not find addon exports or ReShade module metadata: ScannedModules={ScannedModuleCount}",
                result.ScannedModuleCount);
        }

        return result;
    }

    public string Format()
    {
        var result = this.Probe();
        if (result.Detected)
            return $"ReShade addon exports detected: {result.ModuleName} ({result.ModulePath}); modules scanned={result.ScannedModuleCount}.";

        var reShadeCandidates = result.CandidateModules
            .Where(candidate => candidate.LooksLikeReShade)
            .ToArray();

        if (reShadeCandidates.Length > 0)
            return $"ReShade runtime candidate found, but addon exports are incomplete or unavailable: {FormatCandidates(reShadeCandidates)}; modules scanned={result.ScannedModuleCount}.";

        if (result.CandidateModules.Count > 0)
            return $"Partial ReShade addon exports found, but the required export set is incomplete: {FormatCandidates(result.CandidateModules)}; modules scanned={result.ScannedModuleCount}.";

        return $"ReShade addon exports not detected; modules scanned={result.ScannedModuleCount}.";
    }

    internal static ReShadeExportProbeResult ProbeModules(
        IEnumerable<ReShadeModuleCandidate> modules,
        Func<nint, string, nint> getProcAddress)
    {
        var scannedModuleCount = 0;
        var candidates = new List<ReShadeModuleScanResult>();
        ReShadeModuleScanResult? detectedModule = null;

        foreach (var module in modules)
        {
            scannedModuleCount++;
            var scanResult = ScanModule(module, getProcAddress);
            if (scanResult.FoundExports.Count > 0 || scanResult.LooksLikeReShade)
                candidates.Add(scanResult);

            if (detectedModule is null && scanResult.FoundExports.Count == RequiredExports.Length)
                detectedModule = scanResult;
        }

        return detectedModule is not null
            ? new ReShadeExportProbeResult(
                true,
                detectedModule.ModuleName,
                detectedModule.ModulePath,
                detectedModule.FoundExports,
                RequiredExports,
                scannedModuleCount,
                candidates)
            : new ReShadeExportProbeResult(
                false,
                null,
                null,
                Array.Empty<string>(),
                RequiredExports,
                scannedModuleCount,
                candidates);
    }

    private static ReShadeModuleScanResult ScanModule(
        ReShadeModuleCandidate module,
        Func<nint, string, nint> getProcAddress)
    {
        var foundExports = RequiredExports
            .Where(export => getProcAddress(module.BaseAddress, export) != nint.Zero)
            .ToArray();

        return new ReShadeModuleScanResult(
            module.ModuleName,
            module.ModulePath,
            module.BaseAddress,
            module.FileDescription,
            module.ProductName,
            module.CompanyName,
            module.OriginalFilename,
            LooksLikeReShade(module),
            foundExports);
    }

    private static ReShadeModuleCandidate ToModuleCandidate(ProcessModule module)
    {
        var version = SafeFileVersionInfo(module);

        return new ReShadeModuleCandidate(
            SafeModuleName(module),
            SafeModulePath(module),
            module.BaseAddress,
            version?.FileDescription,
            version?.ProductName,
            version?.CompanyName,
            version?.OriginalFilename);
    }

    private static bool LooksLikeReShade(ReShadeModuleCandidate module)
    {
        return ContainsReShadeMarker(module.ModuleName)
            || ContainsReShadeMarker(module.ModulePath)
            || ContainsReShadeMarker(module.FileDescription)
            || ContainsReShadeMarker(module.ProductName)
            || ContainsReShadeMarker(module.CompanyName)
            || ContainsReShadeMarker(module.OriginalFilename);
    }

    private static bool ContainsReShadeMarker(string? value)
    {
        return value?.Contains("reshade", StringComparison.OrdinalIgnoreCase) is true
            || value?.Contains("crosire", StringComparison.OrdinalIgnoreCase) is true;
    }

    private static string FormatCandidates(IReadOnlyList<ReShadeModuleScanResult> candidates)
    {
        return string.Join(
            "; ",
            candidates.Take(3).Select(candidate =>
                $"{candidate.ModuleName} ({candidate.ModulePath ?? "<module-path-null>"}, exports={candidate.FoundExports.Count}/{RequiredExports.Length})"));
    }

    private static string SafeModuleName(ProcessModule module)
    {
        try
        {
            return module.ModuleName;
        }
        catch (Exception ex)
        {
            return $"<module-name-error:{ex.GetType().Name}>";
        }
    }

    private static string? SafeModulePath(ProcessModule module)
    {
        try
        {
            return module.FileName;
        }
        catch (Exception ex)
        {
            return $"<module-path-error:{ex.GetType().Name}>";
        }
    }

    private static FileVersionInfo? SafeFileVersionInfo(ProcessModule module)
    {
        try
        {
            return module.FileVersionInfo;
        }
        catch
        {
            return null;
        }
    }
}
