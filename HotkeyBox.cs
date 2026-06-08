using System.Text;

namespace MouseTool;

/// <summary>
/// 只读文本框，聚焦后按下任意组合键即记录为热键。
/// Modifiers 仅含 Ctrl/Alt/Shift/Win（不含 MOD_NOREPEAT），Vk 为虚拟键码。
/// </summary>
public sealed class HotkeyBox : TextBox
{
    public uint Modifiers { get; private set; }
    public uint Vk { get; private set; }

    /// <summary>热键被用户改动后触发。</summary>
    public event Action? HotkeyChanged;

    public HotkeyBox()
    {
        ReadOnly = true;
        ShortcutsEnabled = false;
        Cursor = Cursors.Hand;
        TextAlign = HorizontalAlignment.Center;
        Text = "未设置";
    }

    /// <summary>编程方式设置热键（用于初始化默认值）。</summary>
    public void SetHotkey(uint modifiers, uint vk)
    {
        Modifiers = modifiers;
        Vk = vk;
        Text = Format(modifiers, vk);
    }

    // ProcessCmdKey 在 OnKeyDown 之前、对所有按键（含 Tab/Alt/方向键）都会调用，
    // 聚焦时在此统一捕获，返回 true 吞掉按键，避免触发默认行为。
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (Focused)
        {
            CaptureHotkey(keyData);
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void CaptureHotkey(Keys keyData)
    {
        Keys key = keyData & Keys.KeyCode;

        // Esc 用于清除热键
        if (key == Keys.Escape)
        {
            Modifiers = 0;
            Vk = 0;
            Text = "未设置";
            HotkeyChanged?.Invoke();
            return;
        }

        // 忽略单独的修饰键
        if (key is Keys.ControlKey or Keys.ShiftKey or Keys.Menu
            or Keys.LWin or Keys.RWin or Keys.None)
            return;

        uint mod = 0;
        if ((keyData & Keys.Control) == Keys.Control) mod |= NativeMethods.MOD_CONTROL;
        if ((keyData & Keys.Alt) == Keys.Alt) mod |= NativeMethods.MOD_ALT;
        if ((keyData & Keys.Shift) == Keys.Shift) mod |= NativeMethods.MOD_SHIFT;

        Modifiers = mod;
        Vk = (uint)key;
        Text = Format(mod, Vk);
        HotkeyChanged?.Invoke();
    }

    public static string Format(uint mod, uint vk)
    {
        if (vk == 0) return "未设置";
        var sb = new StringBuilder();
        if ((mod & NativeMethods.MOD_CONTROL) != 0) sb.Append("Ctrl + ");
        if ((mod & NativeMethods.MOD_ALT) != 0) sb.Append("Alt + ");
        if ((mod & NativeMethods.MOD_SHIFT) != 0) sb.Append("Shift + ");
        if ((mod & NativeMethods.MOD_WIN) != 0) sb.Append("Win + ");
        sb.Append(KeyName(vk));
        return sb.ToString();
    }

    private static string KeyName(uint vk)
    {
        var key = (Keys)vk;
        return key switch
        {
            >= Keys.D0 and <= Keys.D9 => ((char)('0' + (key - Keys.D0))).ToString(),
            >= Keys.NumPad0 and <= Keys.NumPad9 => "小键盘" + (key - Keys.NumPad0),
            _ => key.ToString()
        };
    }
}
