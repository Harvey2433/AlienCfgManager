using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;

// ===========================================
// 数据模型
// ===========================================

/// <summary>
/// 存储一个功能模块的键位绑定信息
/// </summary>
public class FeatureKeybind
{
    public string FeatureName { get; set; } = string.Empty;
    public int KeyCode { get; set; }
    public bool IsHold { get; set; }

    public string KeyType => IsHold ? "Hold" : "Toggle";

    public override string ToString() => $"{FeatureName,-25} | KeyCode: {KeyCode,-5} | Type: {KeyType}";
}

// ===========================================
// 配置管理类
// ===========================================
public class ConfigManager
{
    private Dictionary<string, string> _fullConfig = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private const string KeySuffix = "_Key";
    private const string HoldSuffix = "_Key_hold";

    /// <summary> 读取 CFG 文件并加载所有配置, 增加进度显示 </summary>
    public bool LoadConfig(string filePath)
    {
        if (!File.Exists(filePath))
        {
            ConsoleHelper.Error($"找不到文件 '{filePath}'");
            return false;
        }

        ConsoleHelper.Info($"正在载入配置：{Path.GetFileName(filePath)}...");

        _fullConfig.Clear();
        var lines = File.ReadAllLines(filePath);
        int totalLines = lines.Length;
        int processedCount = 0;

        Console.CursorVisible = false;

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = line.Split(':', 2);
            if (parts.Length == 2)
            {
                var key = parts[0].Trim();
                var value = parts[1].Trim();
                if (!string.IsNullOrWhiteSpace(key) && !_fullConfig.ContainsKey(key))
                {
                    _fullConfig.Add(key, value);
                }
            }
            processedCount++;
            ConsoleHelper.DrawProgressBar(processedCount, totalLines, "Processing");
        }

        Console.CursorVisible = true;
        Console.WriteLine();

        ConsoleHelper.Success($" 成功载入 {_fullConfig.Count} 个配置项");
        return true;
    }

    /// <summary> 从当前配置中提取所有功能键值绑定, 增加进度显示 </summary>
    public List<FeatureKeybind> ExtractKeybinds()
    {
        ConsoleHelper.Info("开始提取功能键位绑定数据...");
        var keybinds = new Dictionary<string, FeatureKeybind>(StringComparer.OrdinalIgnoreCase);
        int totalKeys = _fullConfig.Count;
        int processedCount = 0;

        Console.CursorVisible = false;

        foreach (var kvp in _fullConfig)
        {
            var fullKey = kvp.Key;
            var value = kvp.Value;

            if (fullKey.EndsWith(KeySuffix, StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out int keyCode))
            {
                var featureName = fullKey[..^KeySuffix.Length];
                if (!keybinds.ContainsKey(featureName))
                {
                    keybinds.Add(featureName, new FeatureKeybind { FeatureName = featureName });
                }
                keybinds[featureName].KeyCode = keyCode;
            }
            else if (fullKey.EndsWith(HoldSuffix, StringComparison.OrdinalIgnoreCase) && bool.TryParse(value, out bool isHold))
            {
                var featureName = fullKey[..^HoldSuffix.Length];
                if (!keybinds.ContainsKey(featureName))
                {
                    keybinds.Add(featureName, new FeatureKeybind { FeatureName = featureName });
                }
                keybinds[featureName].IsHold = isHold;
            }

            processedCount++;
            ConsoleHelper.DrawProgressBar(processedCount, totalKeys, "Processing");
        }

        Console.CursorVisible = true;
        Console.WriteLine();

        ConsoleHelper.Success($" 已成功提取 {keybinds.Count} 个功能键位绑定");
        return keybinds.Values.ToList();
    }

    /// <summary> 将提取的键值绑定数据导出为 JSON 文件（仅限 JSON） </summary>
    public void ExportKeybindsToJson(List<FeatureKeybind> keybinds, string exportPath)
    {
        try
        {
            // 关键修复: 确保中文直接输出, 而非转义
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            string jsonString = JsonSerializer.Serialize(keybinds, options);
            File.WriteAllText(exportPath, jsonString);
            ConsoleHelper.Success($"\n 键位数据已成功导出");
            ConsoleHelper.Info($"JSON 文件路径：{Path.GetFullPath(exportPath)}");
        }
        catch (Exception ex)
        {
            ConsoleHelper.Error($"导出 JSON 文件失败: {ex.Message}");
        }
    }

    /// <summary> 从 JSON 文件导入键值绑定数据（仅限 JSON） </summary>
    public List<FeatureKeybind>? ImportKeybindsFromJson(string importPath)
    {
        if (!File.Exists(importPath))
        {
            ConsoleHelper.Error($"找不到导入文件 '{importPath}'");
            return null;
        }

        ConsoleHelper.Info($"正在导入新键位数据：{Path.GetFileName(importPath)}...");

        try
        {
            string jsonString = File.ReadAllText(importPath);
            var keybinds = JsonSerializer.Deserialize<List<FeatureKeybind>>(jsonString);

            if (keybinds == null)
            {
                ConsoleHelper.Error("JSON 文件内容为空或格式不正确, 导入失败");
                return null;
            }

            ConsoleHelper.Success($" 成功导入 {keybinds.Count} 个新的键位绑定数据");
            return keybinds;
        }
        catch (JsonException ex)
        {
            ConsoleHelper.Error($"JSON 格式错误, 导入失败: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            ConsoleHelper.Error($"导入失败: {ex.Message}");
            return null;
        }
    }

    /// <summary> 使用新的键值绑定数据覆盖当前配置中的键值, 增加进度显示 </summary>
    public int OverwriteConfig(List<FeatureKeybind> newKeybinds)
    {
        ConsoleHelper.Info("覆盖原cfg键值...");
        int overwrittenCount = 0;
        int totalKeybinds = newKeybinds.Count;
        int processedCount = 0;

        Console.CursorVisible = false;

        foreach (var keybind in newKeybinds)
        {
            var keyKey = keybind.FeatureName + KeySuffix;
            var holdKey = keybind.FeatureName + HoldSuffix;

            // 覆盖 KeyCode
            if (_fullConfig.ContainsKey(keyKey))
            {
                _fullConfig[keyKey] = keybind.KeyCode.ToString();
                overwrittenCount++;
            }

            // 覆盖 IsHold (Toggle/Hold Type)
            if (_fullConfig.ContainsKey(holdKey))
            {
                _fullConfig[holdKey] = keybind.IsHold.ToString().ToLower();
                overwrittenCount++;
            }

            processedCount++;
            ConsoleHelper.DrawProgressBar(processedCount, totalKeybinds, "Processing");
        }

        Console.CursorVisible = true;
        Console.WriteLine();

        ConsoleHelper.Success($"\n 已成功覆盖 {overwrittenCount} 个键位配置项");
        return overwrittenCount;
    }

    /// <summary> 将修改后的完整配置导出到新的 CFG 文件（仅限 CFG/TXT） </summary>
    public void ExportModifiedConfig(string exportPath)
    {
        try
        {
            var lines = new List<string>();
            foreach (var kvp in _fullConfig)
            {
                lines.Add($"{kvp.Key}:{kvp.Value}");
            }
            File.WriteAllLines(exportPath, lines);
            ConsoleHelper.Success($"\n 文件已成功导出：'{Path.GetFileName(exportPath)}'");
            ConsoleHelper.Info($"文件路径：{Path.GetFullPath(exportPath)}");
        }
        catch (Exception ex)
        {
            ConsoleHelper.Error($"导出修改后的 CFG 文件失败: {ex.Message}");
        }
    }
}

// ===========================================
// 控制台美化辅助类
// ===========================================
public static class ConsoleHelper
{
    private const ConsoleColor DefaultColor = ConsoleColor.Gray;
    private const ConsoleColor AccentColor = ConsoleColor.Magenta;

    public static void ClearScreen()
    {
        Console.Clear();
        ShowWelcomeScreen();
    }

    public static void ShowWelcomeScreen()
    {
        Console.ForegroundColor = AccentColor;
        Console.WriteLine("======================================================");
        Console.WriteLine("          AlienCfgManager (C# .NET 7.0)               ");
        Console.WriteLine("======================================================");
        Console.ForegroundColor = DefaultColor;
        Console.WriteLine();
    }

    public static void ShowStatus(string status, ConsoleColor color)
    {
        Console.Write("当前配置: ");
        Console.ForegroundColor = color;
        Console.WriteLine(status);
        Console.ForegroundColor = DefaultColor;
        DrawSeparator();
    }

    public static void DrawSeparator(int length = 52)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(new string('-', length));
        Console.ForegroundColor = DefaultColor;
    }

    public static void Info(string message)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"[INFO] {message}");
        Console.ForegroundColor = DefaultColor;
    }

    public static void Success(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[Successful] {message}");
        Console.ForegroundColor = DefaultColor;
    }

    public static void Error(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[ERROR] {message}");
        Console.ForegroundColor = DefaultColor;
    }

    /// <summary> 绘制进度条 </summary>
    public static void DrawProgressBar(int current, int total, string label)
    {
        int totalWidth = 50;
        double percentage = (double)current / total;
        int filledWidth = (int)(percentage * totalWidth);
        string progressBar = $"{new string('█', filledWidth)}{new string('-', totalWidth - filledWidth)} ";

        Console.SetCursorPosition(0, Console.CursorTop);

        Console.Write($"[{label}] {progressBar} ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($"{percentage:P0} ");
        Console.ForegroundColor = DefaultColor;
    }

    /// <summary> 获取用户输入的文件名, 并校验后缀 </summary>
    public static string? GetValidFileName(string prompt, params string[] allowedExtensions)
    {
        string allowedExtsPrompt = string.Join(" 或 ", allowedExtensions);

        while (true)
        {
            Console.Write(prompt);
            string? fileName = Console.ReadLine()?.Trim();

            if (fileName?.ToLower() == "exit") return "exit";

            if (string.IsNullOrWhiteSpace(fileName))
            {
                Error("文件名不能为空请重新输入");
                continue;
            }

            if (allowedExtensions.Length > 0)
            {
                bool isValid = allowedExtensions.Any(ext => fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase));

                if (!isValid)
                {
                    Error($"文件扩展名必须是 {allowedExtsPrompt}请重新输入或输入 exit 退出");
                    continue;
                }
            }

            return fileName;
        }
    }

    /// <summary> 运行完成后暂停并返回主菜单 </summary>
    public static void Pause()
    {
        Console.WriteLine();
        DrawSeparator();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("按任意键返回主菜单...");
        Console.ForegroundColor = DefaultColor;
        Console.ReadKey(true); // 读取按键但不显示
    }
}

// ===========================================
// 主程序入口
// ===========================================
public class Program
{
    public static void Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.Title = "AlienCfgManager CFG 键位管理工具 (.NET 7.0)";
        var manager = new ConfigManager();
        string? currentCfgFile = null;

        ConsoleHelper.ShowWelcomeScreen();

        // --- 主程序循环 ---
        while (true)
        {
            // 确保每次循环开始时清屏并显示欢迎信息
            ConsoleHelper.ClearScreen();

            // --- 状态显示 ---
            if (currentCfgFile == null)
            {
                ConsoleHelper.ShowStatus("未载入任何配置文件", ConsoleColor.Red);
            }
            else
            {
                ConsoleHelper.ShowStatus($"{Path.GetFileName(currentCfgFile)}", ConsoleColor.Green);
            }

            // --- 主菜单选项 ---
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("请选择操作：");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine(" [1] 载入待修改的配置文件 (.cfg/.txt)");
            if (currentCfgFile != null)
            {
                Console.WriteLine(" [2] 提取键值数据并导出");
                Console.WriteLine(" [3] 导入键值数据覆盖并导出");
            }
            Console.WriteLine(" [exit] 退出程序");

            ConsoleHelper.DrawSeparator();
            Console.Write("请输入选项: ");

            var choice = Console.ReadLine()?.Trim().ToLower();
            ConsoleHelper.DrawSeparator();

            if (choice == "exit") break;

            // --- 选项 1: 载入/切换 CFG ---
            if (choice == "1")
            {
                ConsoleHelper.Info("开始载入源文件.");
                ConsoleHelper.Info("请输入要载入的配置文件名或完整路径 (.cfg 或 .txt):");
                string? newCfgFile = ConsoleHelper.GetValidFileName(" > ", ".cfg", ".txt");

                if (newCfgFile == "exit") continue;
                if (newCfgFile == null) continue;

                if (manager.LoadConfig(newCfgFile))
                {
                    currentCfgFile = newCfgFile;
                    ConsoleHelper.Info($"已成功设置当前选定配置：{Path.GetFileName(currentCfgFile)}");
                }
                else
                {
                    ConsoleHelper.Error("配置文件载入失败");
                }
            }
            // --- 选项 2 & 3 必须在载入 CFG 后才能执行 ---
            else if (currentCfgFile == null)
            {
                ConsoleHelper.Error("请先选择 [1] 载入一个配置文件才能进行后续操作");
            }
            // --- 选项 2: 提取并导出 JSON ---
            else if (choice == "2")
            {
                ConsoleHelper.Info("开始提取键值数据");

                var keybinds = manager.ExtractKeybinds();
                if (keybinds.Count == 0)
                {
                    ConsoleHelper.Error("未提取到任何键位绑定, 操作终止");
                }
                else
                {
                    ConsoleHelper.Info("\n[功能键位提取预览 (前 5 条)]:");
                    ConsoleHelper.DrawSeparator();
                    foreach (var keybind in keybinds.Take(5))
                    {
                        Console.WriteLine(keybind);
                    }
                    if (keybinds.Count > 5) Console.WriteLine("...");
                    ConsoleHelper.DrawSeparator();

                    ConsoleHelper.Info("请输入导出的文件名或路径, 仅使用json后缀 (.json):");
                    string? exportJsonFile = ConsoleHelper.GetValidFileName(" > ", ".json");
                    if (exportJsonFile == "exit") continue;
                    if (exportJsonFile != null)
                    {
                        manager.ExportKeybindsToJson(keybinds, exportJsonFile);
                    }
                }
            }
            // --- 选项 3: 导入 JSON, 覆盖, 导出新 CFG ---
            else if (choice == "3")
            {
                ConsoleHelper.Info("开始导入键值数据");

                // 1. 导入 JSON
                ConsoleHelper.Info("\n[输入键值数据文件路径或文件名 (.json):");
                string? importJsonFile = ConsoleHelper.GetValidFileName(" > ", ".json");
                if (importJsonFile == "exit") continue;
                if (importJsonFile == null) continue;

                var newKeybinds = manager.ImportKeybindsFromJson(importJsonFile);
                if (newKeybinds == null || newKeybinds.Count == 0)
                {
                    ConsoleHelper.Error("导入数据无效,操作终止");
                }
                else
                {
                    // 2. 覆盖配置
                    manager.OverwriteConfig(newKeybinds);

                    // 3. 导出新 CFG
                    ConsoleHelper.Info("\n请输入导出的新配置文件名 (.cfg 或 .txt):");
                    string? exportCfgFile = ConsoleHelper.GetValidFileName(" > ", ".cfg", ".txt");
                    if (exportCfgFile == "exit") continue;
                    if (exportCfgFile != null)
                    {
                        manager.ExportModifiedConfig(exportCfgFile);
                    }
                }
            }
            else
            {
                ConsoleHelper.Error($"无效选项 '{choice}',请重新输入");
            }

            // --- 运行完成后返回主菜单的逻辑 ---
            ConsoleHelper.Pause();
        }

        Console.WriteLine("\n\n======================================================");
        ConsoleHelper.Info("正在退出...");
        Console.WriteLine("======================================================");
    }
}