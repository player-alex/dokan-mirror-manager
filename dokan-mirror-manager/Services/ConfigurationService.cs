using DokanMirrorManager.Models;
using DokanMirrorManager.Services.Interfaces;
using System.IO;
using System.Text.Json;

namespace DokanMirrorManager.Services;

/// <summary>
/// Service for managing application configuration persistence
/// Handles loading and saving mount configurations to/from JSON files
/// </summary>
public class ConfigurationService : IConfigurationService
{
    private const string ConfigFileName = "mounts.json";
    private readonly SemaphoreSlim _configSaveLock = new(1, 1);
    private readonly string _configPath;

    public ConfigurationService()
    {
        _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);
    }

    /// <summary>
    /// Loads mount configuration from persistent storage
    /// Expands environment variables in source paths (e.g., %USERPROFILE%)
    /// </summary>
    /// <returns>List of mount items loaded from configuration</returns>
    public async Task<List<MountItem>> LoadConfigurationAsync()
    {
        var items = new List<MountItem>();

        try
        {
            if (!File.Exists(_configPath))
                return items;

            var json = await File.ReadAllTextAsync(_configPath);
            var dtos = JsonSerializer.Deserialize<List<MountItemDto>>(json);

            if (dtos != null)
            {
                foreach (var dto in dtos)
                {
                    // Expand environment variables in SourcePath
                    // e.g., %USERPROFILE%\Desktop -> C:\Users\Username\Desktop
                    var expandedSourcePath = Environment.ExpandEnvironmentVariables(dto.SourcePath);

                    var mountItem = new MountItem
                    {
                        SourcePath = expandedSourcePath,
                        OriginalSourcePath = dto.SourcePath, // Keep original path with env vars
                        DestinationLetter = dto.DestinationLetter,
                        AutoMount = dto.AutoMount,
                        IsReadOnly = dto.IsReadOnly,
                        Status = MountStatus.Unmounted
                    };

                    items.Add(mountItem);
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load configuration: {ex.Message}", ex);
        }

        return items;
    }

    /// <summary>
    /// Saves mount configuration to persistent storage
    /// Preserves original paths with environment variables when available
    /// </summary>
    /// <param name="items">Mount items to save</param>
    public async Task SaveConfigurationAsync(IEnumerable<MountItem> items)
    {
        await _configSaveLock.WaitAsync();
        try
        {
            var dtos = items.Select(m => new MountItemDto
            {
                // Save original path (with environment variables) if available
                SourcePath = string.IsNullOrEmpty(m.OriginalSourcePath) ? m.SourcePath : m.OriginalSourcePath,
                DestinationLetter = m.DestinationLetter,
                AutoMount = m.AutoMount,
                IsReadOnly = m.IsReadOnly
            }).ToList();

            var json = JsonSerializer.Serialize(dtos, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_configPath, json);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to save configuration: {ex.Message}", ex);
        }
        finally
        {
            _configSaveLock.Release();
        }
    }
}
