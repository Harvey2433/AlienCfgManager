using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;
using System;
using System.IO;
using System.Runtime.InteropServices;

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

    /// <summary> 修正：添加 string 返回类型 </summary>
    public override string ToString() => $"{FeatureName,-25} | KeyCode: {KeyCode,-5} | Type: {KeyType}";
}

/// <summary>
/// 用于比较两个 FeatureKeybind 实例的 FeatureName 属性
/// </summary>
public class KeybindComparer : IEqualityComparer<FeatureKeybind>
{
    public bool Equals(FeatureKeybind? x, FeatureKeybind? y)
    {
        if (x == null || y == null) return x == y;
        return x.FeatureName.Equals(y.FeatureName, StringComparison.OrdinalIgnoreCase);
    }

    public int GetHashCode(FeatureKeybind obj)
    {
        return obj.FeatureName.GetHashCode(StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// 专用于对比结果的数据模型，记录独有模块及其来源
/// </summary>
public class UniqueKeybind
{
    public FeatureKeybind Keybind { get; set; } = new FeatureKeybind();
    public string SourceFile { get; set; } = string.Empty; // 独有的来源文件名
}

/// <summary>
/// 专用于存储双向对比结果的数据模型
/// </summary>
public class KeybindComparisonResult
{
    public List<UniqueKeybind> UniqueToMain { get; set; } = new List<UniqueKeybind>();
    public List<UniqueKeybind> UniqueToCompare { get; set; } = new List<UniqueKeybind>();
}


/// <summary>
/// 记录键位修改的历史信息
/// </summary>
public class ModificationRecord
{
    public string FeatureName { get; set; } = string.Empty;
    public int OldKeyCode { get; set; }
    public int NewKeyCode { get; set; }
    public string Timestamp { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

    /// <summary> 修正：添加 string 返回类型 </summary>
    public override string ToString()
    {
        // 确保 KeyCode 转译在历史记录中
        KeyMapMode mode = KeyMapMode.GLFW; // 历史记录默认使用 GLFW 转译
        string oldKeyName = KeyMapHelper.GetKeyName(OldKeyCode, mode);
        string newKeyName = KeyMapHelper.GetKeyName(NewKeyCode, mode);

        return $"{Timestamp} | {FeatureName,-25} | 旧: {oldKeyName} ({OldKeyCode,-5}) | 新: {newKeyName} ({NewKeyCode,-5})";
    }
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
// Windows API P/Invoke 辅助类
// ===========================================
/// <summary>
/// 依赖于 Windows user32.dll 的键盘状态查询
/// [注意] 此功能仅在 Windows 操作系统上有效
/// </summary>
public static class KeyboardNative
{
    // 从 user32.dll 导入 GetAsyncKeyState 函数
    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);

    // Windows 虚拟键码 (VK Codes)
    public const int VK_LSHIFT = 0xA0;    // Left Shift (160)
    public const int VK_RSHIFT = 0xA1;    // Right Shift (161)
    public const int VK_LCONTROL = 0xA2;  // Left Control (162)
    public const int VK_RCONTROL = 0xA3;  // Right Control (163)
    public const int VK_LMENU = 0xA4;     // Left Alt (164)
    public const int VK_RMENU = 0xA5;     // Right Alt (165)
    public const int VK_CAPS_LOCK = 0x14; // Caps Lock (20)
    public const int VK_NUM_LOCK = 0x90; // Num Lock (144)

    /// <summary> 检查特定虚拟键是否处于按下状态 </summary>
    public static bool IsKeyPressed(int vKey)
    {
        // 如果最高位为 1 (即 0x8000)，则表示键在调用时是按下的
        return (GetAsyncKeyState(vKey) & 0x8000) != 0;
    }
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
        // GLFW KeyMap 初始化 (包含完整的 104 键及左右修饰键)
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
        GLFWKeyMap.Add(336, "NUMPAD EQUAL");

        // 修饰键/控制键 (L/R 区分)
        GLFWKeyMap.Add(340, "LEFT SHIFT");
        GLFWKeyMap.Add(341, "LEFT CONTROL");
        GLFWKeyMap.Add(342, "LEFT ALT");
        GLFWKeyMap.Add(343, "LEFT SUPER (WIN)");
        GLFWKeyMap.Add(344, "RIGHT SHIFT");
        GLFWKeyMap.Add(345, "RIGHT CONTROL");
        GLFWKeyMap.Add(346, "RIGHT ALT");
        GLFWKeyMap.Add(347, "RIGHT SUPER (WIN)");
        GLFWKeyMap.Add(348, "MENU");

        // 符号键 (使用 ASCII 值作为键码)
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
        // ASCII / Windows VK KeyMap 初始化 
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
        ASCIIKeyMap.Add(13, "ENTER");

        // Modifiers & System (包含 L/R 区分)
        // 注意：VK 16-18 是 Any Shift/Ctrl/Alt，但我们依赖 P/Invoke 捕获 L/R 版本的 160-165
        ASCIIKeyMap.Add(KeyboardNative.VK_LSHIFT, "LEFT SHIFT"); // 160
        ASCIIKeyMap.Add(KeyboardNative.VK_RSHIFT, "RIGHT SHIFT"); // 161
        ASCIIKeyMap.Add(KeyboardNative.VK_LCONTROL, "LEFT CONTROL"); // 162
        ASCIIKeyMap.Add(KeyboardNative.VK_RCONTROL, "RIGHT CONTROL"); // 163
        ASCIIKeyMap.Add(KeyboardNative.VK_LMENU, "LEFT ALT"); // 164
        ASCIIKeyMap.Add(KeyboardNative.VK_RMENU, "RIGHT ALT"); // 165

        ASCIIKeyMap.Add(19, "PAUSE");
        ASCIIKeyMap.Add(KeyboardNative.VK_CAPS_LOCK, "CAPS LOCK"); // 20
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
        ASCIIKeyMap.Add(KeyboardNative.VK_NUM_LOCK, "NUM LOCK"); // 144
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
    private List<ModificationRecord> _history = new List<ModificationRecord>(); // 历史记录
    private const string KeySuffix = "_Key";
    private const string HoldSuffix = "_Key_hold";

    public List<ModificationRecord> GetHistory() => _history;

    /// <summary> 清空配置 (用于切换主配置时清除对比配置) </summary>
    public void ClearConfig()
    {
        _fullConfig.Clear();
        _history.Clear();
    }

    /// <summary> 读取 CFG 文件并加载所有配置, 增加进度显示 </summary>
    public bool LoadConfig(string filePath)
    {
        if (!File.Exists(filePath))
        {
            ConsoleHelper.Error($"[文件未找到] 无法打开文件: '{Path.GetFileName(filePath)}'");
            return false;
        }

        ConsoleHelper.Info($"[开始载入] 正在解析配置: {Path.GetFileName(filePath)}...");

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

        ConsoleHelper.Success($"[载入完成] 成功载入 {_fullConfig.Count} 个配置项");
        return true;
    }

    /// <summary> 从当前配置中提取所有功能键值绑定, 增加进度显示 </summary>
    public List<FeatureKeybind> ExtractKeybinds()
    {
        ConsoleHelper.Info("正在提取功能键位绑定数据...");
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

        ConsoleHelper.Success($"[提取完成] 已成功提取 {keybinds.Count} 个功能键位绑定");
        return keybinds.Values.ToList();
    }

    // ===========================================
    // 导入导出和覆盖方法
    // ===========================================

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
            ConsoleHelper.Success($"\n[导出成功] 键位数据已成功导出");
            ConsoleHelper.Info($"文件路径：{Path.GetFullPath(exportPath)}");
        }
        catch (Exception ex)
        {
            ConsoleHelper.Error($"[导出失败] 导出 JSON 文件失败: {ex.Message}");
        }
    }

    /// <summary> 从 JSON 文件导入键值绑定数据（仅限 JSON） </summary>
    public List<FeatureKeybind>? ImportKeybindsFromJson(string importPath)
    {
        if (!File.Exists(importPath))
        {
            ConsoleHelper.Error($"[导入失败] 找不到导入文件 '{Path.GetFileName(importPath)}'");
            return null;
        }

        ConsoleHelper.Info($"[开始导入] 正在导入新键位数据：{Path.GetFileName(importPath)}...");

        try
        {
            string jsonString = File.ReadAllText(importPath);
            var keybinds = JsonSerializer.Deserialize<List<FeatureKeybind>>(jsonString);

            if (keybinds == null)
            {
                ConsoleHelper.Error("[导入失败] JSON 文件内容为空或格式不正确");
                return null;
            }

            ConsoleHelper.Success($"[导入成功] 成功导入 {keybinds.Count} 个新的键位绑定数据");
            return keybinds;
        }
        catch (JsonException ex)
        {
            ConsoleHelper.Error($"[格式错误] JSON 格式错误, 导入失败: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            ConsoleHelper.Error($"[导入失败] 未知错误: {ex.Message}");
            return null;
        }
    }

    /// <summary> 使用新的键值绑定数据覆盖当前配置中的键值, 增加进度显示 </summary>
    public int OverwriteConfig(List<FeatureKeybind> newKeybinds)
    {
        ConsoleHelper.Info("正在覆盖原cfg键值...");
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
                // 记录历史
                if (int.TryParse(_fullConfig[keyKey], out int oldKeyCode))
                {
                    _history.Add(new ModificationRecord
                    {
                        FeatureName = keybind.FeatureName,
                        OldKeyCode = oldKeyCode,
                        NewKeyCode = keybind.KeyCode
                    });
                }
                else
                {
                    // 旧值无法解析
                    _history.Add(new ModificationRecord
                    {
                        FeatureName = keybind.FeatureName,
                        OldKeyCode = -1,
                        NewKeyCode = keybind.KeyCode
                    });
                }

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

        ConsoleHelper.Success($"\n[覆盖完成] 已成功覆盖 {overwrittenCount} 个键位配置项");
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
            ConsoleHelper.Success($"\n[导出成功] 文件已成功导出：'{Path.GetFileName(exportPath)}'");
            ConsoleHelper.Info($"文件路径：{Path.GetFullPath(exportPath)}");
        }
        catch (Exception ex)
        {
            ConsoleHelper.Error($"[导出失败] 导出修改后的 CFG 文件失败: {ex.Message}");
        }
    }

    // ===========================================
    // 对比、输出和微调功能
    // ===========================================

    /// <summary> 
    /// 比较当前配置和另一个配置，找出双向独有的活跃模块。
    /// </summary>
    public KeybindComparisonResult CompareActiveKeybinds(ConfigManager otherManager, string mainCfgName, string compareCfgName)
    {
        ConsoleHelper.Info("正在提取并对比两个配置文件的活跃键位 (仅对比模块名)...");

        // 1. 获取两个配置文件的活跃模块列表 (只考虑 KeyCode != -1 的活跃项)
        var thisActiveKeybinds = this.ExtractKeybinds().Where(k => k.KeyCode != -1).ToList();
        var otherActiveKeybinds = otherManager.ExtractKeybinds().Where(k => k.KeyCode != -1).ToList();

        // 2. 提取名称集合 (用于快速查找)
        var thisActiveNames = thisActiveKeybinds.Select(k => k.FeatureName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var otherActiveNames = otherActiveKeybinds.Select(k => k.FeatureName).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 3. 找出【对比配置】独有的模块 (在对比中，不在主配置中)
        var uniqueToCompare = otherActiveKeybinds
            .Where(k => !thisActiveNames.Contains(k.FeatureName))
            .Select(k => new UniqueKeybind
            {
                Keybind = k,
                SourceFile = $"对比独有: {compareCfgName}"
            })
            .OrderBy(uk => uk.Keybind.FeatureName)
            .ToList();

        // 4. 找出【主配置】独有的模块 (在主配置中，不在对比中)
        var uniqueToMain = thisActiveKeybinds
            .Where(k => !otherActiveNames.Contains(k.FeatureName))
            .Select(k => new UniqueKeybind
            {
                Keybind = k,
                SourceFile = $"主配置独有: {mainCfgName}"
            })
            .OrderBy(uk => uk.Keybind.FeatureName)
            .ToList();

        ConsoleHelper.Success($"[对比完成] 发现 主配置独有 {uniqueToMain.Count} 个，对比配置独有 {uniqueToCompare.Count} 个。");

        return new KeybindComparisonResult
        {
            UniqueToMain = uniqueToMain,
            UniqueToCompare = uniqueToCompare
        };
    }

    /// <summary> 专门用于输出对比结果的功能模块列表 (双向输出) </summary>
    public void ExportComparisonKeybindsToText(KeybindComparisonResult comparisonResult, string mainCfgName, string compareCfgName)
    {
        ConsoleHelper.Info("开始整理并输出双向独有的功能模块...");

        int totalUnique = comparisonResult.UniqueToMain.Count + comparisonResult.UniqueToCompare.Count;

        if (totalUnique == 0)
        {
            ConsoleHelper.Error("[无结果] 未找到互相独有的模块。");
            return;
        }

        KeyMapMode currentMode = KeyMapMode.GLFW; // 默认模式为 GLFW

        // 局部函数：用于生成输出行
        void GenerateOutputLines(List<UniqueKeybind> uniqueList, string title, List<string> outputLines, KeyMapMode mode)
        {
            const int TotalWidth = 104;

            outputLines.Add("");
            outputLines.Add(new string('=', TotalWidth));
            outputLines.Add($"  [模块列表]: {title} - 数量: {uniqueList.Count}  ");
            outputLines.Add(new string('=', TotalWidth));
            outputLines.Add($"{"功能模块名",-25} | {"键值",-10} | {"按键名称",-20} | 类型 | 独有来源");
            outputLines.Add(new string('-', TotalWidth));

            foreach (var uniqueItem in uniqueList)
            {
                var keybind = uniqueItem.Keybind;
                string keyName = KeyMapHelper.GetKeyName(keybind.KeyCode, mode);

                string line = $"{uniqueItem.Keybind.FeatureName,-25} | {uniqueItem.Keybind.KeyCode,-10} | {keyName,-20} | {uniqueItem.SourceFile}";
                outputLines.Add(line);
            }
            outputLines.Add(new string('=', TotalWidth));
        }

        while (true) // 交互式模式
        {
            ConsoleHelper.ClearScreen();
            ConsoleHelper.ShowStatus($"双向模块对比结果 [{Path.GetFileName(mainCfgName)} VS {Path.GetFileName(compareCfgName)}]", ConsoleColor.DarkCyan);

            List<string> outputLines = new List<string>();

            // 1. 输出【主配置】独有模块
            GenerateOutputLines(
                comparisonResult.UniqueToMain,
                $"主配置文件 ({Path.GetFileName(mainCfgName)}) 独有模块 (对比配置中缺失)",
                outputLines,
                currentMode
            );

            // 2. 输出【对比配置】独有模块
            GenerateOutputLines(
                comparisonResult.UniqueToCompare,
                $"对比配置文件 ({Path.GetFileName(compareCfgName)}) 独有模块 (主配置中缺失)",
                outputLines,
                currentMode
            );

            // 3. 输出到控制台
            ConsoleHelper.Info($"\n[双向独有模块列表 - 总数: {totalUnique}，当前转译模式: {currentMode}]:");
            foreach (var line in outputLines)
            {
                // 突出显示实际的 Keybinds
                if (!line.StartsWith("=") && !line.StartsWith("-") && !line.Contains("模块列表:") && !line.Contains("功能模块名") && !string.IsNullOrWhiteSpace(line))
                {
                    if (line.Contains("[Code:"))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                    }
                    else if (line.Contains($"主配置独有: {Path.GetFileName(mainCfgName)}"))
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                    }
                    else if (line.Contains($"对比独有: {Path.GetFileName(compareCfgName)}"))
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.White;
                    }
                    Console.WriteLine(line);
                    Console.ForegroundColor = ConsoleColor.Gray;
                }
                else if (line.Contains("模块列表:"))
                {
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine(line);
                    Console.ForegroundColor = ConsoleColor.Gray;
                }
                else
                {
                    Console.WriteLine(line);
                }
            }

            // 4. 交互式菜单
            ConsoleHelper.DrawSeparator();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("请选择操作：");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine($" [M] 切换键码转译模式 (当前: {currentMode})");
            Console.WriteLine(" [B] 返回主菜单");
            ConsoleHelper.DrawSeparator();
            Console.Write("请输入选项: ");

            string? choice = Console.ReadLine()?.Trim().ToUpper();

            if (choice == "M")
            {
                currentMode = currentMode == KeyMapMode.GLFW ? KeyMapMode.ASCII_VK : KeyMapMode.GLFW;
                ConsoleHelper.Info($"模式已切换到 {currentMode}...");
                System.Threading.Thread.Sleep(500);
                continue;
            }
            else if (choice == "B")
            {
                ConsoleHelper.Info("[操作取消] 返回主菜单。");
                return;
            }
            else
            {
                ConsoleHelper.Error("[无效选项] 请重新输入。");
                ConsoleHelper.Pause();
            }
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
            ConsoleHelper.Error("[无结果] 未找到任何已绑定的功能模块 (KeyCode 不为 -1)");
            return;
        }

        KeyMapMode currentMode = KeyMapMode.GLFW; // 默认模式为 GLFW
        List<string> outputLines = new List<string>();

        while (true)
        {
            ConsoleHelper.ClearScreen();
            ConsoleHelper.ShowStatus($"{Path.GetFileName(currentCfgName)} (活动键位输出)", ConsoleColor.DarkCyan);

            // 1. 准备文本内容
            const int TotalWidth = 84;

            outputLines.Clear();
            outputLines.Add(new string('=', TotalWidth));
            outputLines.Add($"  活动功能模块列表 - 数量: {activeKeybinds.Count}  ");
            outputLines.Add($"  当前转译模式: {currentMode}  ");
            outputLines.Add(new string('=', TotalWidth));
            outputLines.Add($"{"功能模块名",-25} | {"键值",-10} | {"按键名称",-20} | 类型");
            outputLines.Add(new string('-', TotalWidth));

            int untranslatedCount = 0;

            foreach (var keybind in activeKeybinds)
            {
                string keyName = KeyMapHelper.GetKeyName(keybind.KeyCode, currentMode);

                if (keyName.StartsWith("[Code:"))
                {
                    untranslatedCount++;
                }

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
                else if (line.Contains("活动功能模块列表"))
                {
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
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
            Console.WriteLine($" [M] 切换键码转译模式 (当前: {currentMode})");
            Console.WriteLine($" [W] 写入当前列表到文件 ({exportFileName})");
            Console.WriteLine(" [B] 返回主菜单");
            ConsoleHelper.DrawSeparator();
            Console.Write("请输入选项: ");

            string? choice = Console.ReadLine()?.Trim().ToUpper();

            if (choice == "W")
            {
                break; // 跳出循环执行文件写入
            }
            else if (choice == "M")
            {
                currentMode = currentMode == KeyMapMode.GLFW ? KeyMapMode.ASCII_VK : KeyMapMode.GLFW;
                ConsoleHelper.Info($"模式已切换到 {currentMode}...");
                System.Threading.Thread.Sleep(500);
                continue;
            }
            else if (choice == "B")
            {
                ConsoleHelper.Info("[操作取消] 返回主菜单。");
                return;
            }
            else
            {
                ConsoleHelper.Error("[无效选项] 请重新输入。");
                ConsoleHelper.Pause();
            }
        }

        // 4. 输出到文件 (只有在 'W' 被选择时才会执行到这里)
        try
        {
            File.WriteAllLines(exportFileName, outputLines);
            ConsoleHelper.Success($"\n[导出成功] 活跃模块列表已成功导出");
            ConsoleHelper.Info($"文件路径：{Path.GetFullPath(exportFileName)}");
        }
        catch (Exception ex)
        {
            ConsoleHelper.Error($"[导出失败] 导出文本文件失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 交互式键位微调功能
    /// </summary>
    public int FineTuneKeybind(string currentCfgName)
    {
        var allKeybinds = this.ExtractKeybinds();

        if (allKeybinds.Count == 0)
        {
            ConsoleHelper.Error("[微调失败] 当前配置中没有键位可供微调。");
            return 0;
        }

        var activeKeybinds = allKeybinds.Where(k => k.KeyCode != -1).ToList();
        KeyMapMode currentMode = KeyMapMode.GLFW;

        int modificationsMade = 0;

        // 1. 模式选择和模块选择总循环
        while (true)
        {
            ConsoleHelper.ClearScreen();
            ConsoleHelper.ShowStatus($"{Path.GetFileName(currentCfgName)} (键位微调模式)", ConsoleColor.Magenta);

            // --- 模块列表和模式选择 ---
            ConsoleHelper.Info($"[当前模式] 键码转译模式: {currentMode}");
            ConsoleHelper.Info($"[活跃模块] 共 {activeKeybinds.Count} 个已绑定模块 (仅供参考):");
            const int TotalWidth = 84;
            Console.WriteLine(new string('=', TotalWidth));
            Console.WriteLine($"{"功能模块名",-25} | {"键值",-10} | {"按键名称",-20} | 类型");
            Console.WriteLine(new string('-', TotalWidth));

            foreach (var keybind in activeKeybinds.Take(10))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"{keybind.FeatureName,-25} | {keybind.KeyCode,-10} | {KeyMapHelper.GetKeyName(keybind.KeyCode, currentMode),-20} | {keybind.KeyType}");
            }
            if (activeKeybinds.Count > 10) Console.WriteLine($"... (还有 {activeKeybinds.Count - 10} 个)");

            ConsoleHelper.DrawSeparator();
            Console.WriteLine("请选择操作： [M] 切换模式, [C] 继续微调/选择模块, [B] 返回主菜单");

            Console.Write("请输入选项: ");
            string? option = Console.ReadLine()?.Trim().ToUpper();

            if (option == "M")
            {
                currentMode = currentMode == KeyMapMode.GLFW ? KeyMapMode.ASCII_VK : KeyMapMode.GLFW;
                ConsoleHelper.Info($"模式已切换到 {currentMode}...");
                System.Threading.Thread.Sleep(500);
                continue;
            }
            else if (option == "B")
            {
                ConsoleHelper.Info($"[操作取消] 微调结束，共修改 {modificationsMade} 项。返回主菜单。");
                return modificationsMade;
            }
            else if (option != "C")
            {
                ConsoleHelper.Error("[无效选项] 请重新输入。");
                ConsoleHelper.Pause();
                continue;
            }

            // 2. 模块名输入和校验阶段
            FeatureKeybind? targetKeybind = null;
            while (targetKeybind == null)
            {
                ConsoleHelper.DrawSeparator();
                Console.Write("请输入待修改的模块英文名 (输入 B 返回): ");
                string? featureName = Console.ReadLine()?.Trim();

                if (featureName?.ToUpper() == "B")
                {
                    return modificationsMade; // 返回外层循环
                }

                targetKeybind = allKeybinds.FirstOrDefault(k => k.FeatureName.Equals(featureName, StringComparison.OrdinalIgnoreCase));

                if (targetKeybind == null)
                {
                    ConsoleHelper.Error($"[输入错误] 模块名 '{featureName}' 不存在。");
                }
            }

            // 3. 目标键位输入和确认阶段
            int newKeyCode;
            string newKeyName;

            while (true)
            {
                ConsoleHelper.DrawSeparator();
                ConsoleHelper.Warning($"[目标模块] {targetKeybind.FeatureName} | 旧键: {KeyMapHelper.GetKeyName(targetKeybind.KeyCode, currentMode)} ({targetKeybind.KeyCode})");
                ConsoleHelper.Info("请按下新的目标键位 (或输入 B 返回，输入 Enter 键保留): ");

                // 捕获按键
                var result = ConsoleHelper.ReadUserKey(currentMode);
                newKeyCode = result.code;
                newKeyName = result.name;

                if (newKeyCode == -999) // 哨兵值：B 返回
                {
                    ConsoleHelper.Info("[操作取消] 取消修改此模块。");
                    goto NextModule; // 跳到外部循环的 NextModule 标签
                }
                if (newKeyCode == -1000) // 哨兵值：捕获失败，重试
                {
                    continue;
                }

                ConsoleHelper.Info($"[已捕获] 键: [代码: {newKeyCode}] -> [名称: {newKeyName}]");
                ConsoleHelper.WritePrompt("是否确认修改为这个键位？ (Y/N/C-继续修改下一个模块)");

                string? confirm = Console.ReadLine()?.Trim().ToUpper();

                if (confirm == "Y")
                {
                    break; // 确认修改，进入步骤 4
                }
                else if (confirm == "N")
                {
                    ConsoleHelper.Info("[键位丢弃] 请重新按下目标键。");
                    continue;
                }
                else if (confirm == "C")
                {
                    ConsoleHelper.Info("[操作取消] 跳过此模块，继续下一个模块微调。");
                    goto NextModule;
                }
                else
                {
                    ConsoleHelper.Error("[输入错误] 输入无效，请重新操作。");
                }
            }

            // 4. 应用修改和记录历史
            string keyKey = targetKeybind.FeatureName + KeySuffix;

            if (_fullConfig.ContainsKey(keyKey))
            {
                int oldKeyCode = targetKeybind.KeyCode;

                _fullConfig[keyKey] = newKeyCode.ToString();

                _history.Add(new ModificationRecord
                {
                    FeatureName = targetKeybind.FeatureName,
                    OldKeyCode = oldKeyCode,
                    NewKeyCode = newKeyCode
                });

                ConsoleHelper.Success($"\n[修改成功] 模块 '{targetKeybind.FeatureName}' 的键位已成功修改为 {newKeyName} ({newKeyCode})。");
                modificationsMade++;
            }
            else
            {
                ConsoleHelper.Error("[修改失败] 在配置字典中找不到该模块的键位条目。");
            }

        // 标签用于跳过此模块的后续操作
        NextModule:
            ConsoleHelper.Pause();
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
        Console.WriteLine("                AlienCfgManager V2.0                  ");
        Console.WriteLine("                CFG 键位管理工具                      ");
        Console.WriteLine("======================================================");
        Console.ForegroundColor = DefaultColor;
        Console.WriteLine();
    }

    public static void ShowStatus(string status, ConsoleColor color)
    {
        Console.Write(" >> ");
        Console.ForegroundColor = color;
        Console.WriteLine(status);
        Console.ForegroundColor = DefaultColor;
        DrawSeparator();
    }

    public static void DrawSeparator(int length = 52, ConsoleColor color = ConsoleColor.DarkGray)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(new string('~', length));
        Console.ForegroundColor = DefaultColor;
    }

    public static void Info(string message)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"[INFO] {message}");
        Console.ForegroundColor = DefaultColor;
    }

    public static void Warning(string message)
    {
        // [WARN] 是标准前缀，不应移除
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[WARN] {message}");
        Console.ForegroundColor = DefaultColor;
    }

    public static void Success(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[SUCCESS] {message}");
        Console.ForegroundColor = DefaultColor;
    }

    public static void Error(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[ERROR] {message}");
        Console.ForegroundColor = DefaultColor;
    }

    public static void WritePrompt(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write(message + " ");
        Console.ForegroundColor = DefaultColor;
    }

    /// <summary> 绘制进度条 </summary>
    public static void DrawProgressBar(int current, int total, string label)
    {
        int totalWidth = 40; // 进度条宽度略微缩小
        double percentage = (double)current / total;
        int filledWidth = (int)(percentage * totalWidth);
        string progressBar = $"[{new string('█', filledWidth)}{new string(' ', totalWidth - filledWidth)}]";

        Console.SetCursorPosition(0, Console.CursorTop);

        Console.Write($"[{label}] {progressBar} ");
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.Write($"{percentage:P0} ");
        Console.ForegroundColor = DefaultColor;
    }

    /// <summary> 获取用户输入的文件名, 并校验后缀 </summary>
    public static string? GetValidFileName(string prompt, params string[] allowedExtensions)
    {
        string allowedExtsPrompt = string.Join(" 或 ", allowedExtensions);

        while (true)
        {
            WritePrompt(prompt);
            string? fileName = Console.ReadLine()?.Trim();

            if (fileName?.ToLower() == "exit" || fileName?.ToLower() == "b") return "exit";

            if (string.IsNullOrWhiteSpace(fileName))
            {
                Error("文件名不能为空请重新输入或输入 exit 退出。");
                continue;
            }

            // 统一路径处理：移除双引号
            fileName = fileName.Trim('"');

            // 转换为绝对路径 (如果输入的是相对路径)
            if (!Path.IsPathRooted(fileName))
            {
                fileName = Path.GetFullPath(fileName);
            }

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
        Console.Write("按任意键继续...");
        Console.ForegroundColor = DefaultColor;
        Console.ReadKey(true);
    }

    /// <summary> 
    /// 尝试使用 P/Invoke 检测 L/R 修饰键或锁键是否被按下。
    /// </summary>
    private static bool HandleModifierKeyPInvoke(KeyMapMode mode, out int keyCode)
    {
        // P/Invoke 检测左右 Shift, Control, Alt (优先级高于 Console.ReadKey)
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (KeyboardNative.IsKeyPressed(KeyboardNative.VK_RSHIFT))
            {
                keyCode = mode == KeyMapMode.GLFW ? 344 : KeyboardNative.VK_RSHIFT;
                return true;
            }
            if (KeyboardNative.IsKeyPressed(KeyboardNative.VK_LSHIFT))
            {
                keyCode = mode == KeyMapMode.GLFW ? 340 : KeyboardNative.VK_LSHIFT;
                return true;
            }
            if (KeyboardNative.IsKeyPressed(KeyboardNative.VK_RCONTROL))
            {
                keyCode = mode == KeyMapMode.GLFW ? 345 : KeyboardNative.VK_RCONTROL;
                return true;
            }
            if (KeyboardNative.IsKeyPressed(KeyboardNative.VK_LCONTROL))
            {
                keyCode = mode == KeyMapMode.GLFW ? 341 : KeyboardNative.VK_LCONTROL;
                return true;
            }
            if (KeyboardNative.IsKeyPressed(KeyboardNative.VK_RMENU))
            {
                keyCode = mode == KeyMapMode.GLFW ? 346 : KeyboardNative.VK_RMENU;
                return true;
            }
            if (KeyboardNative.IsKeyPressed(KeyboardNative.VK_LMENU))
            {
                keyCode = mode == KeyMapMode.GLFW ? 342 : KeyboardNative.VK_LMENU;
                return true;
            }

            // P/Invoke 检测锁键 CapsLock/NumLock
            if (KeyboardNative.IsKeyPressed(KeyboardNative.VK_CAPS_LOCK))
            {
                keyCode = mode == KeyMapMode.GLFW ? 280 : KeyboardNative.VK_CAPS_LOCK;
                return true;
            }
            if (KeyboardNative.IsKeyPressed(KeyboardNative.VK_NUM_LOCK))
            {
                keyCode = mode == KeyMapMode.GLFW ? 282 : KeyboardNative.VK_NUM_LOCK;
                return true;
            }
        }


        keyCode = -1;
        return false;
    }

    /// <summary> 捕获用户按键并映射到 KeyCode，同时支持手动输入 KeyCode </summary>
    public static (int code, string name) ReadUserKey(KeyMapMode mode)
    {
        // P/Invoke 捕获修饰键 (P/Invoke 优先)
        if (HandleModifierKeyPInvoke(mode, out int pInvokeCode))
        {
            string pInvokeName = KeyMapHelper.GetKeyName(pInvokeCode, mode);
            return (pInvokeCode, pInvokeName);
        }

        // Console.ReadKey 捕获其他键
        var keyInfo = Console.ReadKey(true);

        // 检查是否按下 B 键返回
        if (keyInfo.Key == ConsoleKey.B)
        {
            return (-999, "CANCELLED");
        }

        int keyCode = -1;

        // 1. 尝试使用 KeyChar (适用于字母、数字、符号键)
        if (keyInfo.KeyChar != 0 && !char.IsControl(keyInfo.KeyChar))
        {
            if (char.IsLetter(keyInfo.KeyChar))
            {
                keyCode = (int)char.ToUpper(keyInfo.KeyChar);
            }
            else
            {
                keyCode = (int)keyInfo.KeyChar;
            }
        }
        // 2. 尝试使用 ConsoleKey (适用于功能键、箭头等特殊键)
        else
        {
            // 检查 Enter 键 (通常用来保留/取消)
            if (keyInfo.Key == ConsoleKey.Enter)
            {
                return (-1000, "FAILED_RETRY"); // 或者使用 -1 
            }

            switch (keyInfo.Key)
            {
                // --------------- 常用控制键映射 ------------------
                case ConsoleKey.Escape: keyCode = mode == KeyMapMode.GLFW ? 256 : 27; break;
                // case ConsoleKey.Enter: handled above or is a special case.
                case ConsoleKey.Tab: keyCode = mode == KeyMapMode.GLFW ? 258 : 9; break;
                case ConsoleKey.Backspace: keyCode = mode == KeyMapMode.GLFW ? 259 : 8; break;
                case ConsoleKey.Insert: keyCode = mode == KeyMapMode.GLFW ? 260 : 45; break;
                case ConsoleKey.Delete: keyCode = mode == KeyMapMode.GLFW ? 261 : 46; break;
                case ConsoleKey.RightArrow: keyCode = mode == KeyMapMode.GLFW ? 262 : 39; break;
                case ConsoleKey.LeftArrow: keyCode = mode == KeyMapMode.GLFW ? 263 : 37; break;
                case ConsoleKey.DownArrow: keyCode = mode == KeyMapMode.GLFW ? 264 : 40; break;
                case ConsoleKey.UpArrow: keyCode = mode == KeyMapMode.GLFW ? 265 : 38; break;
                case ConsoleKey.PageUp: keyCode = mode == KeyMapMode.GLFW ? 266 : 33; break;
                case ConsoleKey.PageDown: keyCode = mode == KeyMapMode.GLFW ? 267 : 34; break;
                case ConsoleKey.Home: keyCode = mode == KeyMapMode.GLFW ? 268 : 36; break;
                case ConsoleKey.End: keyCode = mode == KeyMapMode.GLFW ? 269 : 35; break;
                case ConsoleKey.Spacebar: keyCode = mode == KeyMapMode.GLFW ? 32 : 32; break;
                case ConsoleKey.PrintScreen: keyCode = mode == KeyMapMode.GLFW ? 283 : 44; break;

                // --------------- 功能键 F1-F12 ------------------
                case ConsoleKey.F1: keyCode = mode == KeyMapMode.GLFW ? 290 : 112; break;
                case ConsoleKey.F2: keyCode = mode == KeyMapMode.GLFW ? 291 : 113; break;
                case ConsoleKey.F3: keyCode = mode == KeyMapMode.GLFW ? 292 : 114; break;
                case ConsoleKey.F4: keyCode = mode == KeyMapMode.GLFW ? 293 : 115; break;
                case ConsoleKey.F5: keyCode = mode == KeyMapMode.GLFW ? 294 : 116; break;
                case ConsoleKey.F6: keyCode = mode == KeyMapMode.GLFW ? 295 : 117; break;
                case ConsoleKey.F7: keyCode = mode == KeyMapMode.GLFW ? 296 : 118; break;
                case ConsoleKey.F8: keyCode = mode == KeyMapMode.GLFW ? 297 : 119; break;
                case ConsoleKey.F9: keyCode = mode == KeyMapMode.GLFW ? 298 : 120; break;
                case ConsoleKey.F10: keyCode = mode == KeyMapMode.GLFW ? 299 : 121; break;
                case ConsoleKey.F11: keyCode = mode == KeyMapMode.GLFW ? 300 : 122; break;
                case ConsoleKey.F12: keyCode = mode == KeyMapMode.GLFW ? 301 : 123; break;

                // --------------- 小键盘 (NumPad) ------------------
                case ConsoleKey.NumPad0: keyCode = mode == KeyMapMode.GLFW ? 320 : 96; break;
                case ConsoleKey.NumPad1: keyCode = mode == KeyMapMode.GLFW ? 321 : 97; break;
                case ConsoleKey.NumPad2: keyCode = mode == KeyMapMode.GLFW ? 322 : 98; break;
                case ConsoleKey.NumPad3: keyCode = mode == KeyMapMode.GLFW ? 323 : 99; break;
                case ConsoleKey.NumPad4: keyCode = mode == KeyMapMode.GLFW ? 324 : 100; break;
                case ConsoleKey.NumPad5: keyCode = mode == KeyMapMode.GLFW ? 325 : 101; break;
                case ConsoleKey.NumPad6: keyCode = mode == KeyMapMode.GLFW ? 326 : 102; break;
                case ConsoleKey.NumPad7: keyCode = mode == KeyMapMode.GLFW ? 327 : 103; break;
                case ConsoleKey.NumPad8: keyCode = mode == KeyMapMode.GLFW ? 328 : 104; break;
                case ConsoleKey.NumPad9: keyCode = mode == KeyMapMode.GLFW ? 329 : 105; break;
                case ConsoleKey.Multiply: keyCode = mode == KeyMapMode.GLFW ? 332 : 106; break; // *
                case ConsoleKey.Add: keyCode = mode == KeyMapMode.GLFW ? 334 : 107; break; // +
                case ConsoleKey.Subtract: keyCode = mode == KeyMapMode.GLFW ? 333 : 109; break; // -
                case ConsoleKey.Decimal: keyCode = mode == KeyMapMode.GLFW ? 330 : 110; break; // .
                case ConsoleKey.Divide: keyCode = mode == KeyMapMode.GLFW ? 331 : 111; break; // /

                default:
                    // Fallback: 无法识别，要求用户手动输入
                    Warning($"无法通过按键捕获特殊键 [{keyInfo.Key}]。请手动输入 KeyCode 或按 Enter 键取消:");
                    string? manualInput = Console.ReadLine();
                    if (string.IsNullOrWhiteSpace(manualInput))
                    {
                        // 用户按了 Enter 或输入空白，返回失败重试
                        return (-1000, "FAILED_RETRY");
                    }
                    if (int.TryParse(manualInput, out int manualCode))
                    {
                        keyCode = manualCode;
                    }
                    else
                    {
                        Error("无效输入，按键捕获失败，请重试。");
                        return (-1000, "FAILED_RETRY");
                    }
                    break;
            }
        }

        // 3. 重新转译并返回
        string keyName = KeyMapHelper.GetKeyName(keyCode, mode);
        return (keyCode, keyName);
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
        Console.Title = "AlienCfgManager CFG 键位管理工具 - UI Enhanced";
        var mainManager = new ConfigManager();
        var compareManager = new ConfigManager();
        string? currentCfgFile = null;
        string? compareCfgFile = null;

        ConsoleHelper.ShowWelcomeScreen();

        // --- 主程序循环 ---
        while (true)
        {
            ConsoleHelper.ClearScreen();

            // --- 状态显示 ---
            string mainStatus = currentCfgFile == null ? "未载入 (Required)" : Path.GetFileName(currentCfgFile)!;
            ConsoleHelper.ShowStatus($"主配置: {mainStatus}", currentCfgFile == null ? ConsoleColor.Red : ConsoleColor.Green);

            string compareStatus = compareCfgFile == null ? "未载入 (Optional)" : Path.GetFileName(compareCfgFile)!;
            ConsoleHelper.ShowStatus($"对比配置: {compareStatus}", compareCfgFile == null ? ConsoleColor.DarkYellow : ConsoleColor.Yellow);
            ConsoleHelper.DrawSeparator(52, ConsoleColor.White);


            // --- 主菜单选项 ---
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(" >>>> [ 文件操作 ]");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine(" [1] 载入/切换主配置文件 (.cfg/.txt) [!!清除对比配置]");

            if (currentCfgFile != null)
            {
                // --- 导出/导入操作 ---
                ConsoleHelper.DrawSeparator(52, ConsoleColor.DarkGray);
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(" >>>> [ 数据处理 ]");
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine(" [2] 提取键位数据并导出 (.json)");
                Console.WriteLine(" [3] 导入键位数据覆盖并导出 (.cfg/.txt)");
                Console.WriteLine(" [4] 整理并导出已绑定功能模块");

                // --- 对比功能子菜单 ---
                ConsoleHelper.DrawSeparator(52, ConsoleColor.DarkGray);
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(" >>>> [ 配置对比 ]");
                Console.ForegroundColor = ConsoleColor.Gray;

                Console.WriteLine(" [5] 载入/切换对比配置文件 (.cfg/.txt)");

                if (compareCfgFile != null)
                {
                    Console.WriteLine(" [6] 对比活跃模块");
                }
                // --- 微调操作 ---
                ConsoleHelper.DrawSeparator(52, ConsoleColor.DarkGray);
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(" >>>> [ 实时微调 - 不建议使用 ]");
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine(" [7] 微调模块键位 (直接修改主配置) [L/R 修饰键区分]");



                // --- 历史记录 ---
                if (mainManager.GetHistory().Any())
                {
                    Console.WriteLine(" [H] 显示历史键位微调记录");
                }
            }
            ConsoleHelper.DrawSeparator(52, ConsoleColor.DarkGray);
            Console.WriteLine(" [exit] 退出程序");

            ConsoleHelper.DrawSeparator();
            Console.Write("请输入选项: ");

            var choice = Console.ReadLine()?.Trim().ToLower();
            ConsoleHelper.DrawSeparator();

            if (choice == "exit") break;

            // --- 选项 1: 载入主 CFG ---
            if (choice == "1")
            {
                ConsoleHelper.Info("请输入要载入的主配置文件名或完整路径 (.cfg 或 .txt) [输入 B 或 exit 返回]:");
                string? newCfgFile = ConsoleHelper.GetValidFileName(" > ", ".cfg", ".txt");

                if (newCfgFile == "exit" || newCfgFile == null) continue;

                // 检查对比配置是否已载入（用于控制警告信息的显示）
                bool wasCompareConfigLoaded = compareCfgFile != null;

                if (mainManager.LoadConfig(newCfgFile))
                {
                    currentCfgFile = newCfgFile;

                    // 优化逻辑: 载入主配置后清除对比配置，避免混淆
                    compareCfgFile = null;
                    compareManager.ClearConfig();

                    // 仅在对比配置确实被载入过时才显示警告
                    if (wasCompareConfigLoaded)
                    {
                        ConsoleHelper.Warning("对比配置文件已自动清除。");
                    }
                }
            }
            // --- 选项 5: 载入对比 CFG ---
            else if (choice == "5")
            {
                if (currentCfgFile == null)
                {
                    ConsoleHelper.Error("[前置条件] 请先载入主配置文件 (选项 1)。");
                }
                else
                {
                    ConsoleHelper.Info("请输入要载入的对比配置文件名或完整路径 (.cfg 或 .txt) [输入 B 或 exit 返回]:");
                    string? newCfgFile = ConsoleHelper.GetValidFileName(" > ", ".cfg", ".txt");

                    if (newCfgFile == "exit" || newCfgFile == null) continue;

                    if (compareManager.LoadConfig(newCfgFile))
                    {
                        compareCfgFile = newCfgFile;
                    }
                }
            }
            // --- 选项 H: 显示历史记录 ---
            else if (choice == "h" && currentCfgFile != null && mainManager.GetHistory().Any())
            {
                ConsoleHelper.Info("------ [ 键位微调历史记录 ] ------");
                foreach (var record in mainManager.GetHistory())
                {
                    Console.WriteLine(record.ToString());
                }
                ConsoleHelper.DrawSeparator(52, ConsoleColor.White);
            }
            // --- 必须载入主配置才能进行的操作 ---
            else if (currentCfgFile == null)
            {
                ConsoleHelper.Error("[前置条件] 请先选择 [1] 载入一个主配置文件才能进行后续操作");
            }
            // --- 选项 2: 提取并导出 JSON ---
            else if (choice == "2")
            {
                var keybinds = mainManager.ExtractKeybinds();
                if (keybinds.Count > 0)
                {
                    ConsoleHelper.Info("\n[功能键位提取预览 (前 5 条)]:");
                    ConsoleHelper.DrawSeparator();
                    foreach (var keybind in keybinds.Take(5))
                    {
                        Console.WriteLine(keybind.ToString());
                    }
                    if (keybinds.Count > 5) Console.WriteLine("...");
                    ConsoleHelper.DrawSeparator();

                    ConsoleHelper.Info("请输入导出的文件名或路径 (.json) [输入 B 或 exit 返回]:");
                    string? exportJsonFile = ConsoleHelper.GetValidFileName(" > ", ".json");
                    if (exportJsonFile == "exit" || exportJsonFile == null) continue;

                    mainManager.ExportKeybindsToJson(keybinds, exportJsonFile);
                }
                else
                {
                    ConsoleHelper.Error("[提取失败] 未提取到任何键位绑定, 操作终止");
                }
            }
            // --- 选项 3: 导入 JSON, 覆盖, 导出新 CFG ---
            else if (choice == "3")
            {
                ConsoleHelper.Info("\n[输入键值数据文件路径或文件名 (.json) [输入 B 或 exit 返回]:");
                string? importJsonFile = ConsoleHelper.GetValidFileName(" > ", ".json");
                if (importJsonFile == "exit" || importJsonFile == null) continue;

                var newKeybinds = mainManager.ImportKeybindsFromJson(importJsonFile);
                if (newKeybinds == null || newKeybinds.Count == 0)
                {
                    ConsoleHelper.Error("[导入失败] 导入数据无效,操作终止");
                }
                else
                {
                    mainManager.OverwriteConfig(newKeybinds);

                    ConsoleHelper.Info("\n请输入导出的新配置文件名 (.cfg 或 .txt) [输入 B 或 exit 返回]:");
                    string? exportCfgFile = ConsoleHelper.GetValidFileName(" > ", ".cfg", ".txt");
                    if (exportCfgFile == "exit" || exportCfgFile == null) continue;

                    mainManager.ExportModifiedConfig(exportCfgFile);
                }
            }
            // --- 选项 4: 导出活跃功能模块 ---
            else if (choice == "4")
            {
                var keybinds = mainManager.ExtractKeybinds();
                if (keybinds.Count > 0)
                {
                    mainManager.ExportActiveKeybindsToText(keybinds, "activitymodule.txt", currentCfgFile);
                }
                else
                {
                    ConsoleHelper.Error("[提取失败] 未提取到任何键位绑定, 操作终止");
                }
            }
            // --- 选项 6: 对比功能 (需要载入对比文件) ---
            else if (choice == "6")
            {
                if (compareCfgFile == null)
                {
                    ConsoleHelper.Error("[前置条件] 请先选择 [5] 载入一个对比配置文件。");
                }
                else
                {
                    string mainName = Path.GetFileName(currentCfgFile)!;
                    string compareName = Path.GetFileName(compareCfgFile)!;

                    var comparisonResult = mainManager.CompareActiveKeybinds(compareManager, mainName, compareName);

                    mainManager.ExportComparisonKeybindsToText(comparisonResult, mainName, compareName);
                }
            }
            // --- 选项 7: 微调功能 ---
            else if (choice == "7")
            {
                if (currentCfgFile != null)
                {
                    mainManager.FineTuneKeybind(currentCfgFile);
                }
                else
                {
                    ConsoleHelper.Error("[前置条件] 主配置文件未载入。");
                }
            }
            else
            {
                ConsoleHelper.Error($"[输入错误] 无效选项 '{choice}', 请重新输入");
            }

            ConsoleHelper.Pause();
        }

        Console.WriteLine("\n\n======================================================");
        ConsoleHelper.Info("程序已退出");
        Console.WriteLine("======================================================");
    }
}