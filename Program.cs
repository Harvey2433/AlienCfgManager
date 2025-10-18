using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;
using System;
using System.IO;

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

    // 保持 ToString() 简洁，主要用于 JSON 序列化和简化数据结构
    public override string ToString() => $"{FeatureName,-25} | KeyCode: {KeyCode,-5} | Type: {KeyType}";
}

// ===========================================
// 键代码转译模式
// ===========================================
public enum KeyMapMode
{
    /// <summary> GLFW 键码 (适用于新版 Minecraft 和您的模组正值) </summary>
    GLFW,
    /// <summary> 标准 ASCII/Windows VK 键码 (适用于低位冲突键码) </summary>
    ASCII_VK
}

// ===========================================
// 键代码转译辅助类 (已更新为双模式和104键)
// ===========================================
public static class KeyMapHelper
{
    // 完整的 GLFW Key Codes 映射
    private static readonly Dictionary<int, string> GLFWKeyMap = new Dictionary<int, string>();
    // 完整的 ASCII/Windows VK Key Codes 映射
    private static readonly Dictionary<int, string> ASCIIKeyMap = new Dictionary<int, string>();

    static KeyMapHelper()
    {
        // -----------------------------------
        // GLFW KeyMap 初始化 (包含完整的 104 键)
        // -----------------------------------

        // A-Z (65 to 90) & 0-9 (48 to 57)
        for (int i = 0; i < 26; i++) GLFWKeyMap.Add(65 + i, ((char)(65 + i)).ToString());
        for (int i = 0; i < 10; i++) GLFWKeyMap.Add(48 + i, i.ToString());

        // 主 GLFW 键 (高位键码)
        GLFWKeyMap.Add(32, "SPACE");
        GLFWKeyMap.Add(256, "ESCAPE");
        GLFWKeyMap.Add(257, "ENTER");
        GLFWKeyMap.Add(258, "TAB");
        GLFWKeyMap.Add(259, "BACKSPACE");
        GLFWKeyMap.Add(260, "INSERT");
        GLFWKeyMap.Add(261, "DELETE");
        GLFWKeyMap.Add(262, "RIGHT ARROW");
        GLFWKeyMap.Add(263, "LEFT ARROW");
        GLFWKeyMap.Add(264, "DOWN ARROW");
        GLFWKeyMap.Add(265, "UP ARROW");
        GLFWKeyMap.Add(266, "PAGE UP");
        GLFWKeyMap.Add(267, "PAGE DOWN");
        GLFWKeyMap.Add(268, "HOME");
        GLFWKeyMap.Add(269, "END");
        GLFWKeyMap.Add(280, "CAPS LOCK");
        GLFWKeyMap.Add(281, "SCROLL LOCK");
        GLFWKeyMap.Add(282, "NUM LOCK");
        GLFWKeyMap.Add(283, "PRINT SCREEN");
        GLFWKeyMap.Add(284, "PAUSE");

        // 功能键 (F1 - F25)
        for (int i = 0; i < 25; i++) GLFWKeyMap.Add(290 + i, $"F{i + 1}");

        // 小键盘 (NUMPAD)
        for (int i = 0; i < 10; i++) GLFWKeyMap.Add(320 + i, $"NUMPAD {i}");
        GLFWKeyMap.Add(330, "NUMPAD DECIMAL");
        GLFWKeyMap.Add(331, "NUMPAD DIVIDE");
        GLFWKeyMap.Add(332, "NUMPAD MULTIPLY");
        GLFWKeyMap.Add(333, "NUMPAD SUBTRACT");
        GLFWKeyMap.Add(334, "NUMPAD ADD");
        GLFWKeyMap.Add(335, "NUMPAD ENTER");
        GLFWKeyMap.Add(336, "NUMPAD EQUAL"); // 可能的补充键

        // 修饰键/控制键
        GLFWKeyMap.Add(340, "LEFT SHIFT");
        GLFWKeyMap.Add(341, "LEFT CONTROL");
        GLFWKeyMap.Add(342, "LEFT ALT");
        GLFWKeyMap.Add(343, "LEFT SUPER (WIN)"); // 补充
        GLFWKeyMap.Add(344, "RIGHT SHIFT");
        GLFWKeyMap.Add(345, "RIGHT CONTROL");
        GLFWKeyMap.Add(346, "RIGHT ALT");
        GLFWKeyMap.Add(347, "RIGHT SUPER (WIN)"); // 补充
        GLFWKeyMap.Add(348, "MENU"); // 补充

        // 符号键
        GLFWKeyMap.Add(39, "APOSTROPHE (' \")");
        GLFWKeyMap.Add(44, "COMMA (, <)");
        GLFWKeyMap.Add(45, "MINUS (- _)");
        GLFWKeyMap.Add(46, "PERIOD (. >)");
        GLFWKeyMap.Add(47, "SLASH (/ ?)");
        GLFWKeyMap.Add(59, "SEMICOLON (; :)");
        GLFWKeyMap.Add(61, "EQUAL (= +)");
        GLFWKeyMap.Add(91, "LEFT BRACKET ([ {)");
        GLFWKeyMap.Add(92, "BACKSLASH (\\ |)");
        GLFWKeyMap.Add(93, "RIGHT BRACKET (] })");
        GLFWKeyMap.Add(96, "GRAVE ACCENT (` ~)");

        // -----------------------------------
        // ASCII / Windows VK KeyMap 初始化 (包含完整的 104 键常用 VK Codes)
        // -----------------------------------
        // A-Z (65 to 90) & 0-9 (48 to 57)
        for (int i = 0; i < 26; i++) ASCIIKeyMap.Add(65 + i, ((char)(65 + i)).ToString());
        for (int i = 0; i < 10; i++) ASCIIKeyMap.Add(48 + i, i.ToString());

        // 鼠标 (VK Codes 1, 2, 4-6)
        ASCIIKeyMap.Add(1, "MOUSE1 (L)");
        ASCIIKeyMap.Add(2, "MOUSE2 (R)");
        ASCIIKeyMap.Add(4, "MOUSE3 (M)");
        ASCIIKeyMap.Add(5, "MOUSE4 (X1)");
        ASCIIKeyMap.Add(6, "MOUSE5 (X2)");

        // 主要控制键 (VK Codes)
        ASCIIKeyMap.Add(8, "BACKSPACE");
        ASCIIKeyMap.Add(9, "TAB");
        // ASCIIKeyMap.Add(12, "CLEAR"); // NumPad 5
        ASCIIKeyMap.Add(13, "ENTER");

        // Modifiers & System
        ASCIIKeyMap.Add(16, "SHIFT (Any)");
        ASCIIKeyMap.Add(17, "CONTROL (Any)");
        ASCIIKeyMap.Add(18, "ALT (Any)");
        ASCIIKeyMap.Add(19, "PAUSE");
        ASCIIKeyMap.Add(20, "CAPS LOCK");
        ASCIIKeyMap.Add(27, "ESCAPE");
        ASCIIKeyMap.Add(32, "SPACE");

        // Navigation / Editting
        ASCIIKeyMap.Add(33, "PAGE UP");
        ASCIIKeyMap.Add(34, "PAGE DOWN");
        ASCIIKeyMap.Add(35, "END");
        ASCIIKeyMap.Add(36, "HOME");
        ASCIIKeyMap.Add(37, "LEFT ARROW");
        ASCIIKeyMap.Add(38, "UP ARROW");
        ASCIIKeyMap.Add(39, "RIGHT ARROW");
        ASCIIKeyMap.Add(40, "DOWN ARROW");
        ASCIIKeyMap.Add(44, "PRINT SCREEN");
        ASCIIKeyMap.Add(45, "INSERT");
        ASCIIKeyMap.Add(46, "DELETE");

        // Windows Key
        ASCIIKeyMap.Add(91, "LEFT WIN");
        ASCIIKeyMap.Add(92, "RIGHT WIN");
        ASCIIKeyMap.Add(93, "APPLICATIONS");

        // Numpad Keys
        ASCIIKeyMap.Add(96, "NUMPAD 0");
        ASCIIKeyMap.Add(97, "NUMPAD 1");
        ASCIIKeyMap.Add(98, "NUMPAD 2");
        ASCIIKeyMap.Add(99, "NUMPAD 3");
        ASCIIKeyMap.Add(100, "NUMPAD 4");
        ASCIIKeyMap.Add(101, "NUMPAD 5");
        ASCIIKeyMap.Add(102, "NUMPAD 6");
        ASCIIKeyMap.Add(103, "NUMPAD 7");
        ASCIIKeyMap.Add(104, "NUMPAD 8");
        ASCIIKeyMap.Add(105, "NUMPAD 9");
        ASCIIKeyMap.Add(106, "MULTIPLY (*)");
        ASCIIKeyMap.Add(107, "ADD (+)");
        ASCIIKeyMap.Add(108, "SEPARATOR");
        ASCIIKeyMap.Add(109, "SUBTRACT (-)");
        ASCIIKeyMap.Add(110, "DECIMAL (.)");
        ASCIIKeyMap.Add(111, "DIVIDE (/)");

        // F1-F24 (VK codes)
        for (int i = 0; i < 24; i++) ASCIIKeyMap.Add(112 + i, $"F{i + 1}");

        // Lock Keys
        ASCIIKeyMap.Add(144, "NUM LOCK");
        ASCIIKeyMap.Add(145, "SCROLL LOCK");

        // Symbol Keys (VK Codes 186-222)
        ASCIIKeyMap.Add(186, "; / :");
        ASCIIKeyMap.Add(187, "= / +");
        ASCIIKeyMap.Add(188, ", / <");
        ASCIIKeyMap.Add(189, "- / _");
        ASCIIKeyMap.Add(190, ". / >");
        ASCIIKeyMap.Add(191, "/ / ?");
        ASCIIKeyMap.Add(192, "` / ~");
        ASCIIKeyMap.Add(219, "[ / {");
        ASCIIKeyMap.Add(220, "\\ / |");
        ASCIIKeyMap.Add(221, "] / }");
        ASCIIKeyMap.Add(222, "' / \"");
    }

    /// <summary> 根据键代码和模式获取按键名称 </summary>
    public static string GetKeyName(int keyCode, KeyMapMode mode)
    {
        if (keyCode == -1)
        {
            return "NONE";
        }

        // 1. 模组自定义负值键码 (始终优先处理，与模式无关)
        if (keyCode < -1)
        {
            if (keyCode == -2) return "MOUSE LEFT";
            if (keyCode == -3) return "MOUSE RIGHT";
            if (keyCode == -4) return "MOUSE MIDDLE";
            // 鼠标键 4, 5, 6... (Math.Abs(keyCode) - 4)
            return "MOUSE " + (Math.Abs(keyCode) - 4);
        }

        // 2. 正值键码 (根据模式转译)
        Dictionary<int, string> map = mode == KeyMapMode.GLFW ? GLFWKeyMap : ASCIIKeyMap;

        if (map.TryGetValue(keyCode, out string? keyName))
        {
            return keyName;
        }

        // 3. Fallback: 无法转译
        return $"[Code:{keyCode}]";
    }
}

// ===========================================
// 配置管理类
// ===========================================
public class ConfigManager
{
    private Dictionary<string, string> _fullConfig = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private const string KeySuffix = "_Key";
    private const string HoldSuffix = "_Key_hold";

    // ... (LoadConfig, ExtractKeybinds, ExportKeybindsToJson, ImportKeybindsFromJson, OverwriteConfig, ExportModifiedConfig 保持不变)

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
            // 确保中文直接输出, 而非转义
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


    /// <summary> 整理所有Key不为-1的功能项，并在控制台输出，同时提供模式切换和文件写入选项 </summary>
    public void ExportActiveKeybindsToText(List<FeatureKeybind> keybinds, string exportFileName, string currentCfgName)
    {
        ConsoleHelper.Info("开始整理并导出已绑定的功能模块...");

        // 筛选 KeyCode 不为 -1 的项，并按功能名排序
        var activeKeybinds = keybinds.Where(k => k.KeyCode != -1).OrderBy(k => k.FeatureName).ToList();

        if (activeKeybinds.Count == 0)
        {
            ConsoleHelper.Error("未找到任何已绑定的功能模块 (KeyCode 不为 -1)");
            return;
        }

        KeyMapMode currentMode = KeyMapMode.GLFW; // 默认模式为 GLFW
        List<string> outputLines = new List<string>();
        bool interactiveLoop = true;

        while (interactiveLoop)
        {
            // 清空控制台并重新显示标题和状态
            ConsoleHelper.ClearScreen();
            ConsoleHelper.ShowStatus($"{Path.GetFileName(currentCfgName)} (活动键位输出)", ConsoleColor.Green);

            // 1. 准备文本内容
            const int TotalWidth = 84;

            outputLines.Clear(); // 清空上次循环的输出内容
            outputLines.Add(new string('=', TotalWidth));
            outputLines.Add($"  活动功能模块列表 (Key != -1) - 数量: {activeKeybinds.Count}  ");
            outputLines.Add($"  当前转译模式: {currentMode}  ");
            outputLines.Add(new string('=', TotalWidth));
            outputLines.Add($"{"功能模块名",-25} | {"键值",-10} | {"按键名称",-20} | 类型");
            outputLines.Add(new string('-', TotalWidth));

            int untranslatedCount = 0;

            foreach (var keybind in activeKeybinds)
            {
                // 获取转译后的按键名称，使用当前模式
                string keyName = KeyMapHelper.GetKeyName(keybind.KeyCode, currentMode);

                if (keyName.StartsWith("[Code:"))
                {
                    untranslatedCount++;
                }

                // 格式化输出行
                string line = $"{keybind.FeatureName,-25} | {keybind.KeyCode,-10} | {keyName,-20} | {keybind.KeyType}";
                outputLines.Add(line);
            }
            outputLines.Add(new string('=', TotalWidth));

            // 2. 输出到控制台
            ConsoleHelper.Info("\n[已绑定的功能模块列表]:");
            foreach (var line in outputLines)
            {
                // 突出显示实际的 Keybinds
                if (!line.StartsWith("=") && !line.StartsWith("-") && !line.Contains("模块名") && !line.Contains("当前转译模式:"))
                {
                    // 对未转译的键码使用红色突出显示
                    if (line.Contains("[Code:"))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                    }
                    Console.WriteLine(line);
                    Console.ForegroundColor = ConsoleColor.Gray;
                }
                else
                {
                    Console.WriteLine(line);
                }
            }

            // 3. 交互式菜单
            ConsoleHelper.DrawSeparator();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("请选择操作：");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine($" [M] 切换转译模式 (当前: {currentMode}，{(currentMode == KeyMapMode.GLFW ? "切换到ASCII/VK" : "切换到GLFW")})");
            Console.WriteLine($" [W] 写入当前列表到文件 ({exportFileName}) 并返回主菜单");
            Console.WriteLine(" [B] 返回主菜单 (不写入文件)");
            ConsoleHelper.DrawSeparator();
            Console.Write("请输入选项: ");

            string? choice = Console.ReadLine()?.Trim().ToUpper();

            if (choice == "W")
            {
                interactiveLoop = false;
                break; // 跳出循环执行文件写入
            }
            else if (choice == "M")
            {
                currentMode = currentMode == KeyMapMode.GLFW ? KeyMapMode.ASCII_VK : KeyMapMode.GLFW;
                ConsoleHelper.Info($"模式已切换到 {currentMode}，正在重新转译并显示...");
                System.Threading.Thread.Sleep(500); // 暂停一下以便用户看到提示
                continue; // 重新开始循环，用新模式显示
            }
            else if (choice == "B")
            {
                ConsoleHelper.Info("操作已取消，返回主菜单。");
                return; // 直接返回，不执行文件写入
            }
            else
            {
                ConsoleHelper.Error("无效的选项，请重新输入。");
                ConsoleHelper.Pause(); // 暂停一下以便用户看到错误信息
            }
        }

        // 4. 输出到文件 (只有在 'W' 被选择时才会执行到这里)
        try
        {
            File.WriteAllLines(exportFileName, outputLines);
            ConsoleHelper.Success($"\n 活跃模块列表已成功导出");
            ConsoleHelper.Info($"文本文件路径：{Path.GetFullPath(exportFileName)}");
        }
        catch (Exception ex)
        {
            ConsoleHelper.Error($"导出文本文件失败: {ex.Message}");
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
        Console.WriteLine("                AlienCfgManager                       ");
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

            // 修正：移除路径两侧的双引号
            fileName = fileName.Trim('"');

            if (allowedExtensions.Length > 0)
            {
                bool isValid = allowedExtensions.Any(ext => fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase));

                if (!isValid)
                {
                    Error($"文件扩展名必须是 {allowedExtsPrompt}，请重新输入或输入 exit 退出。");
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
                Console.WriteLine(" [2] 提取键值数据并导出 (.json)");
                Console.WriteLine(" [3] 导入键值数据覆盖并导出 (.cfg/.txt)");
                // 新增选项
                Console.WriteLine(" [4] 整理并导出已绑定功能模块");
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
            // --- 选项 2, 3, 4 必须在载入 CFG 后才能执行 ---
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
            // --- 选项 4: 导出活跃功能模块 (新增交互逻辑) ---
            else if (choice == "4")
            {
                // 1. 提取所有键位绑定 (必须先有数据才能筛选)
                var keybinds = manager.ExtractKeybinds();

                if (keybinds.Count > 0)
                {
                    // 2. 导出活跃模块到控制台和文件 (包含切换模式和写入逻辑)
                    manager.ExportActiveKeybindsToText(keybinds, "activitymodule.txt", currentCfgFile);
                }
                else
                {
                    ConsoleHelper.Error("未提取到任何键位绑定, 操作终止");
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