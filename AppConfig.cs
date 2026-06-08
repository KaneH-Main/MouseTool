using System.Text.Json;

namespace MouseTool;

/// <summary>
/// 持久化的用户配置，保存到 %AppData%\MouseTool\config.json。
/// 字段用基元类型，便于 JSON 序列化与向后兼容。
/// </summary>
public sealed class AppConfig
{
    public int IntervalMs { get; set; } = 100;
    public int Button { get; set; }          // 0=左 1=右 2=中
    public int Kind { get; set; }            // 0=单击 1=双击
    public int RepeatCount { get; set; }
    public bool FixedPosition { get; set; }
    public int X { get; set; }
    public int Y { get; set; }

    public uint ClickModifiers { get; set; }
    public uint ClickVk { get; set; } = 0x75; // 默认 F6
    public uint HoldModifiers { get; set; }
    public uint HoldVk { get; set; } = 0x76;  // 默认 F7

    private static string Dir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MouseTool");

    private static string FilePath => Path.Combine(Dir, "config.json");

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                string json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            }
        }
        catch
        {
            // 配置损坏时回退到默认值，不影响启动。
        }
        return new AppConfig();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
        catch
        {
            // 保存失败（如无写权限）静默忽略，不阻塞关闭。
        }
    }
}
