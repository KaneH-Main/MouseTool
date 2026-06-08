using System.ComponentModel;

namespace MouseTool;

public sealed class MainForm : Form
{
    private const int HotkeyIdClick = 0xB001;
    private const int HotkeyIdHold = 0xB002;

    private readonly Clicker _clicker = new();
    private readonly AppConfig _config;

    // --- 控件 ---
    private NumericUpDown _intervalInput = null!;
    private ComboBox _buttonInput = null!;
    private ComboBox _kindInput = null!;
    private NumericUpDown _repeatInput = null!;
    private CheckBox _fixedPosCheck = null!;
    private NumericUpDown _xInput = null!;
    private NumericUpDown _yInput = null!;
    private Button _pickPosButton = null!;
    private HotkeyBox _clickHotkey = null!;
    private HotkeyBox _holdHotkey = null!;
    private Button _clickButton = null!;
    private Button _holdButton = null!;
    private Label _statusLabel = null!;
    private Label _countLabel = null!;

    public MainForm()
    {
        Text = "鼠标连点器";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(360, 430);
        Font = new Font("Microsoft YaHei UI", 9F);
        TrySetIcon();

        _config = AppConfig.Load();

        BuildUi();
        ApplyConfig();

        _clicker.Clicked += OnClicked;
        _clicker.Stopped += OnStoppedFromEngine;

        Load += (_, _) => ApplyHotkeys();
        FormClosing += OnClosing;
    }

    // 用可执行文件自身的图标（来自 ApplicationIcon）作为窗口图标。
    private void TrySetIcon()
    {
        try
        {
            string? exe = Environment.ProcessPath;
            if (exe is not null)
                Icon = Icon.ExtractAssociatedIcon(exe);
        }
        catch { /* 取不到图标时用默认 */ }
    }

    private void BuildUi()
    {
        int y = 14;
        const int labelX = 16;
        const int fieldX = 130;
        const int rowH = 34;

        AddLabel("点击间隔 (毫秒)", labelX, y + 3);
        _intervalInput = new NumericUpDown
        {
            Location = new Point(fieldX, y),
            Width = 110,
            Minimum = 1,
            Maximum = 600000,
            Value = 100
        };
        Controls.Add(_intervalInput);
        y += rowH;

        AddLabel("鼠标键", labelX, y + 3);
        _buttonInput = new ComboBox
        {
            Location = new Point(fieldX, y),
            Width = 110,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _buttonInput.Items.AddRange(new object[] { "左键", "右键", "中键" });
        _buttonInput.SelectedIndex = 0;
        Controls.Add(_buttonInput);
        y += rowH;

        AddLabel("点击方式", labelX, y + 3);
        _kindInput = new ComboBox
        {
            Location = new Point(fieldX, y),
            Width = 110,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _kindInput.Items.AddRange(new object[] { "单击", "双击" });
        _kindInput.SelectedIndex = 0;
        Controls.Add(_kindInput);
        y += rowH;

        AddLabel("次数 (0=无限)", labelX, y + 3);
        _repeatInput = new NumericUpDown
        {
            Location = new Point(fieldX, y),
            Width = 110,
            Minimum = 0,
            Maximum = 1000000,
            Value = 0
        };
        Controls.Add(_repeatInput);
        y += rowH;

        _fixedPosCheck = new CheckBox
        {
            Text = "固定坐标点击",
            Location = new Point(labelX, y + 2),
            AutoSize = true
        };
        _fixedPosCheck.CheckedChanged += (_, _) => UpdatePosEnabled();
        Controls.Add(_fixedPosCheck);
        y += 28;

        AddLabel("X", labelX, y + 3);
        _xInput = new NumericUpDown
        {
            Location = new Point(labelX + 24, y),
            Width = 70,
            Minimum = -100000,
            Maximum = 100000
        };
        Controls.Add(_xInput);

        AddLabel("Y", labelX + 104, y + 3);
        _yInput = new NumericUpDown
        {
            Location = new Point(labelX + 124, y),
            Width = 70,
            Minimum = -100000,
            Maximum = 100000
        };
        Controls.Add(_yInput);

        _pickPosButton = new Button
        {
            Text = "拾取",
            Location = new Point(labelX + 200, y - 1),
            Width = 70
        };
        _pickPosButton.Click += OnPickPosition;
        Controls.Add(_pickPosButton);
        y += rowH + 2;

        // --- 自定义热键 ---
        AddLabel("连点热键", labelX, y + 4);
        _clickHotkey = new HotkeyBox
        {
            Location = new Point(fieldX, y),
            Width = 200
        };
        _clickHotkey.SetHotkey(0, 0x75); // 默认 F6
        _clickHotkey.HotkeyChanged += ApplyHotkeys;
        Controls.Add(_clickHotkey);
        y += rowH;

        AddLabel("长按热键", labelX, y + 4);
        _holdHotkey = new HotkeyBox
        {
            Location = new Point(fieldX, y),
            Width = 200
        };
        _holdHotkey.SetHotkey(0, 0x76); // 默认 F7
        _holdHotkey.HotkeyChanged += ApplyHotkeys;
        Controls.Add(_holdHotkey);
        y += rowH;

        AddLabel("（聚焦输入框后按组合键设置，Esc 清除）", labelX, y, Color.Gray);
        y += 26;

        // --- 开始/停止按钮 ---
        _clickButton = new Button
        {
            Location = new Point(labelX, y),
            Width = 158,
            Height = 38
        };
        _clickButton.Click += (_, _) => ToggleMode(EngineMode.Click);
        Controls.Add(_clickButton);

        _holdButton = new Button
        {
            Location = new Point(labelX + 172, y),
            Width = 158,
            Height = 38
        };
        _holdButton.Click += (_, _) => ToggleMode(EngineMode.Hold);
        Controls.Add(_holdButton);
        y += 48;

        _statusLabel = new Label
        {
            Text = "状态：已停止",
            Location = new Point(labelX, y),
            AutoSize = true,
            ForeColor = Color.DimGray
        };
        Controls.Add(_statusLabel);

        _countLabel = new Label
        {
            Text = "已点击：0",
            Location = new Point(labelX + 200, y),
            AutoSize = true,
            ForeColor = Color.DimGray
        };
        Controls.Add(_countLabel);

        UpdatePosEnabled();
        RefreshUi();
    }

    private void AddLabel(string text, int x, int y, Color? color = null)
    {
        Controls.Add(new Label
        {
            Text = text,
            Location = new Point(x, y),
            AutoSize = true,
            ForeColor = color ?? SystemColors.ControlText
        });
    }

    private void UpdatePosEnabled()
    {
        bool on = _fixedPosCheck.Checked;
        _xInput.Enabled = on;
        _yInput.Enabled = on;
        _pickPosButton.Enabled = on;
    }

    // 用户点“拾取”后，下一次鼠标左键按下的位置写入 X/Y。
    private void OnPickPosition(object? sender, EventArgs e)
    {
        _pickPosButton.Text = "点击目标…";
        _pickPosButton.Enabled = false;

        var timer = new System.Windows.Forms.Timer { Interval = 60 };
        timer.Tick += (_, _) =>
        {
            if ((Control.MouseButtons & MouseButtons.Left) == MouseButtons.Left)
            {
                if (NativeMethods.GetCursorPos(out var p))
                {
                    _xInput.Value = Clamp(p.X, _xInput);
                    _yInput.Value = Clamp(p.Y, _yInput);
                }
                timer.Stop();
                timer.Dispose();
                _pickPosButton.Text = "拾取";
                _pickPosButton.Enabled = true;
            }
        };
        timer.Start();
    }

    private static decimal Clamp(int v, NumericUpDown n) =>
        Math.Min(n.Maximum, Math.Max(n.Minimum, v));

    // 将持久化配置应用到各控件。
    private void ApplyConfig()
    {
        _intervalInput.Value = Clamp(_config.IntervalMs, _intervalInput);
        _buttonInput.SelectedIndex = ClampIndex(_config.Button, _buttonInput.Items.Count);
        _kindInput.SelectedIndex = ClampIndex(_config.Kind, _kindInput.Items.Count);
        _repeatInput.Value = Clamp(_config.RepeatCount, _repeatInput);
        _fixedPosCheck.Checked = _config.FixedPosition;
        _xInput.Value = Clamp(_config.X, _xInput);
        _yInput.Value = Clamp(_config.Y, _yInput);
        _clickHotkey.SetHotkey(_config.ClickModifiers, _config.ClickVk);
        _holdHotkey.SetHotkey(_config.HoldModifiers, _config.HoldVk);

        UpdatePosEnabled();
        RefreshUi();
    }

    // 从控件读回当前配置。
    private void CaptureConfig()
    {
        _config.IntervalMs = (int)_intervalInput.Value;
        _config.Button = _buttonInput.SelectedIndex;
        _config.Kind = _kindInput.SelectedIndex;
        _config.RepeatCount = (int)_repeatInput.Value;
        _config.FixedPosition = _fixedPosCheck.Checked;
        _config.X = (int)_xInput.Value;
        _config.Y = (int)_yInput.Value;
        _config.ClickModifiers = _clickHotkey.Modifiers;
        _config.ClickVk = _clickHotkey.Vk;
        _config.HoldModifiers = _holdHotkey.Modifiers;
        _config.HoldVk = _holdHotkey.Vk;
    }

    private static int ClampIndex(int v, int count) => Math.Max(0, Math.Min(count - 1, v));

    private ClickSettings ReadSettings(EngineMode mode) => new()
    {
        Mode = mode,
        IntervalMs = (int)_intervalInput.Value,
        Button = (MouseButton)_buttonInput.SelectedIndex,
        Kind = (ClickKind)_kindInput.SelectedIndex,
        RepeatCount = (int)_repeatInput.Value,
        FixedPosition = _fixedPosCheck.Checked,
        X = (int)_xInput.Value,
        Y = (int)_yInput.Value
    };

    // 切换某模式：若同模式正在运行则停止；若另一模式运行则切换；否则启动。
    private void ToggleMode(EngineMode mode)
    {
        if (_clicker.IsRunning)
        {
            bool same = _clicker.CurrentMode == mode;
            _clicker.Stop();
            if (same)
            {
                RefreshUi();
                return;
            }
        }
        _clicker.Start(ReadSettings(mode));
        RefreshUi();
    }

    private void RefreshUi()
    {
        bool running = _clicker.IsRunning;
        EngineMode mode = _clicker.CurrentMode;

        string clickKey = _clickHotkey.Text;
        string holdKey = _holdHotkey.Text;

        _clickButton.Text = (running && mode == EngineMode.Click ? "停止连点" : "开始连点") + $" ({clickKey})";
        _holdButton.Text = (running && mode == EngineMode.Hold ? "停止长按" : "开始长按") + $" ({holdKey})";

        if (!running)
        {
            _statusLabel.Text = "状态：已停止";
            _statusLabel.ForeColor = Color.DimGray;
            _countLabel.Text = "已点击：0";
        }
        else if (mode == EngineMode.Click)
        {
            _statusLabel.Text = "状态：连点中";
            _statusLabel.ForeColor = Color.SeaGreen;
        }
        else
        {
            _statusLabel.Text = "状态：长按中";
            _statusLabel.ForeColor = Color.DarkOrange;
        }
    }

    private void OnClicked(long count)
    {
        if (IsDisposed) return;
        try
        {
            BeginInvoke(() => _countLabel.Text = $"已点击：{count}");
        }
        catch (InvalidOperationException) { /* 句柄已销毁 */ }
    }

    private void OnStoppedFromEngine()
    {
        if (IsDisposed) return;
        try
        {
            BeginInvoke(RefreshUi);
        }
        catch (InvalidOperationException) { /* 句柄已销毁 */ }
    }

    // ---------------- 全局热键 ----------------

    // 重新注册两个热键；任一失败时在状态栏提示。
    private void ApplyHotkeys()
    {
        if (!IsHandleCreated) return;

        NativeMethods.UnregisterHotKey(Handle, HotkeyIdClick);
        NativeMethods.UnregisterHotKey(Handle, HotkeyIdHold);

        var failed = new List<string>();

        if (_clickHotkey.Vk != 0 && !NativeMethods.RegisterHotKey(
                Handle, HotkeyIdClick,
                _clickHotkey.Modifiers | NativeMethods.MOD_NOREPEAT, _clickHotkey.Vk))
            failed.Add("连点");

        if (_holdHotkey.Vk != 0 && !NativeMethods.RegisterHotKey(
                Handle, HotkeyIdHold,
                _holdHotkey.Modifiers | NativeMethods.MOD_NOREPEAT, _holdHotkey.Vk))
            failed.Add("长按");

        RefreshUi();

        if (failed.Count > 0)
        {
            _statusLabel.Text = $"热键冲突：{string.Join("、", failed)} 注册失败（已被占用，请换一个）";
            _statusLabel.ForeColor = Color.IndianRed;
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WM_HOTKEY)
        {
            int id = m.WParam.ToInt32();
            if (id == HotkeyIdClick) { ToggleMode(EngineMode.Click); return; }
            if (id == HotkeyIdHold) { ToggleMode(EngineMode.Hold); return; }
        }
        base.WndProc(ref m);
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        _clicker.Stop();
        NativeMethods.UnregisterHotKey(Handle, HotkeyIdClick);
        NativeMethods.UnregisterHotKey(Handle, HotkeyIdHold);

        CaptureConfig();
        _config.Save();
    }
}
