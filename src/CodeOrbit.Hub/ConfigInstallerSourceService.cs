using CodeOrbit.Contracts;
using CodeOrbit.Core.Models;
using CodeOrbit.Core.Services;
using CodeOrbit.Core.Sources;

namespace CodeOrbit.Hub;

public sealed class ConfigInstallerSourceService : ICodeOrbitSourceService
{
    private static readonly SourceCapabilitiesDto DefaultCapabilities = new(
        HookInstall: true,
        Approval: true,
        Question: true,
        Transcript: true,
        AlwaysAllow: true);

    public IReadOnlyList<SourceDto> GetSources()
    {
        var loader = new SourcePluginLoader();
        var plugins = loader.LoadPlugins();
        var bundledKeys = loader.GetBundledSourceKeys();

        return plugins
            .Select(adapter =>
            {
                var sourceType = bundledKeys.Contains(adapter.SourceKey) ? "bundled" : "user";
                return new SourceDto(
                    adapter.SourceKey,
                    adapter.DisplayName,
                    adapter.IconName,
                    ConfigInstaller.IsPluginInstalled(adapter.SourceKey),
                    DefaultCapabilities,
                    sourceType);
            })
            .OrderBy(static s => s.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public SourceStatusDto GetSourceStatus(string source)
    {
        var normalized = NormalizeSource(source);
        var loader = new SourcePluginLoader();
        var plugin = loader.LoadPlugins().FirstOrDefault(p =>
            string.Equals(p.SourceKey, normalized, StringComparison.OrdinalIgnoreCase));

        if (plugin == null)
            return new SourceStatusDto(normalized, Supported: false, Installed: false, DisplayName: normalized);

        return new SourceStatusDto(
            normalized,
            Supported: true,
            Installed: ConfigInstaller.IsPluginInstalled(normalized),
            DisplayName: plugin.DisplayName);
    }

    public SourceOperationResultDto Install(string source) =>
        RunSourceOperation(source, "installed", ConfigInstaller.InstallPlugin);

    public SourceOperationResultDto Uninstall(string source) =>
        RunSourceOperation(source, "uninstalled", ConfigInstaller.UninstallPlugin);

    public SourceOperationResultDto Repair(string source) =>
        RunSourceOperation(source, "repaired", ConfigInstaller.InstallPlugin);

    public bool RepairAll()
    {
        var loader = new SourcePluginLoader();
        var plugins = loader.LoadPlugins();
        var allOk = true;

        foreach (var plugin in plugins)
        {
            if (ConfigInstaller.IsPluginInstalled(plugin.SourceKey))
            {
                if (!ConfigInstaller.InstallPlugin(plugin.SourceKey))
                    allOk = false;
            }
        }

        return allOk;
    }

    public RuntimeAssetsDto GetRuntimeAssets() => new(
        ConfigInstaller.RuntimeDirectory,
        ConfigInstaller.RuntimeHookScriptPath,
        ConfigInstaller.RuntimeBridgeExePath,
        ConfigInstaller.AreRuntimeAssetsInstalled());

    public bool RepairRuntimeAssets() => ConfigInstaller.RepairRuntimeAssets();

    private static SourceOperationResultDto RunSourceOperation(
        string source,
        string successVerb,
        Func<string, bool> operation)
    {
        var normalized = NormalizeSource(source);
        var loader = new SourcePluginLoader();
        var plugin = loader.LoadPlugins().FirstOrDefault(p =>
            string.Equals(p.SourceKey, normalized, StringComparison.OrdinalIgnoreCase));

        if (plugin == null)
        {
            return new SourceOperationResultDto(
                normalized,
                Success: false,
                Installed: false,
                Message: $"Unsupported source: {source}");
        }

        var success = operation(normalized);
        return new SourceOperationResultDto(
            normalized,
            success,
            ConfigInstaller.IsPluginInstalled(normalized),
            success ? $"{normalized} {successVerb}" : $"{normalized} operation failed");
    }

    private static string NormalizeSource(string source) =>
        source.Trim().ToLowerInvariant();
}
