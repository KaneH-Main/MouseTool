using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MouseTool;

public enum MouseButton
{
    Left,
    Right,
    Middle
}

public enum ClickKind
{
    Single,
    Double
}

/// <summary>引擎运行模式：连点 或 长按。</summary>
public enum EngineMode
{
    Click,
    Hold
}

/// <summary>
/// 连点 / 长按配置。坐标为屏幕绝对坐标；FixedPosition 为 false 时在当前光标处操作。
/// </summary>
public sealed class ClickSettings
{
    public EngineMode Mode { get; set; } = EngineMode.Click;
    public int IntervalMs { get; set; } = 100;
    public MouseButton Button { get; set; } = MouseButton.Left;
    public ClickKind Kind { get; set; } = ClickKind.Single;

    /// <summary>0 表示无限连点；&gt;0 表示点击指定次数后自动停止。（仅连点模式）</summary>
    public int RepeatCount { get; set; } = 0;

    public bool FixedPosition { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
}

/// <summary>
/// 在后台线程上执行连点或长按，使用 SendInput 模拟鼠标。
/// 连点模式用 Stopwatch 绝对时间推进 + 末段自旋提高短间隔时序精度；
/// 长按模式按下后保持，停止时释放。
/// </summary>
public sealed class Clicker
{
    private readonly object _gate = new();
    private Thread? _worker;
    private volatile bool _running;
    private ClickSettings _settings = new();

    /// <summary>连点模式每次点击后触发，参数为已点击次数。</summary>
    public event Action<long>? Clicked;

    /// <summary>运行结束（手动停止或达到次数上限）后触发。</summary>
    public event Action? Stopped;

    public bool IsRunning => _running;

    /// <summary>当前/最近一次运行的模式（仅在 IsRunning 时有意义）。</summary>
    public EngineMode CurrentMode => _settings.Mode;

    public void Start(ClickSettings settings)
    {
        lock (_gate)
        {
            if (_running) return;
            _settings = settings;
            _running = true;
            _worker = new Thread(Run)
            {
                IsBackground = true,
                Name = "MouseTool.Clicker"
            };
            _worker.Start();
        }
    }

    public void Stop()
    {
        Thread? worker;
        lock (_gate)
        {
            if (!_running) return;
            _running = false;
            worker = _worker;
            _worker = null;
        }

        // 等待工作线程退出，但不要在它自身上 Join（避免死锁）。
        if (worker is not null && worker != Thread.CurrentThread)
            worker.Join(1000);
    }

    private void Run()
    {
        var s = _settings;
        try
        {
            if (s.Mode == EngineMode.Hold)
                RunHold(s);
            else
                RunClick(s);
        }
        finally
        {
            _running = false;
            Stopped?.Invoke();
        }
    }

    private void RunClick(ClickSettings s)
    {
        long count = 0;
        var sw = Stopwatch.StartNew();
        long nextTick = 0;

        while (_running)
        {
            if (s.FixedPosition)
                NativeMethods.SetCursorPos(s.X, s.Y);

            DoClick(s.Button, s.Kind);
            count++;
            Clicked?.Invoke(count);

            if (s.RepeatCount > 0 && count >= s.RepeatCount)
                break;

            // 基于绝对时间推进，减少累计漂移。
            nextTick += s.IntervalMs;
            WaitUntil(sw, nextTick);
        }
    }

    private void RunHold(ClickSettings s)
    {
        if (s.FixedPosition)
            NativeMethods.SetCursorPos(s.X, s.Y);

        (uint down, uint up) = GetFlags(s.Button);
        SendOne(down);
        try
        {
            // 保持按下，直到外部请求停止。
            while (_running)
                Thread.Sleep(15);
        }
        finally
        {
            SendOne(up); // 务必释放，避免鼠标键卡住。
        }
    }

    private void WaitUntil(Stopwatch sw, long targetMs)
    {
        while (_running)
        {
            long remaining = targetMs - sw.ElapsedMilliseconds;
            if (remaining <= 0) return;
            // 大段时间用 Sleep 让出 CPU，最后 ~15ms 用自旋提高精度。
            if (remaining > 15)
                Thread.Sleep((int)(remaining - 15));
            else
                Thread.SpinWait(200);
        }
    }

    private static void DoClick(MouseButton button, ClickKind kind)
    {
        SendClick(button);
        if (kind == ClickKind.Double)
        {
            // 双击的两次点击间隔需小于系统 DoubleClickTime，这里给个很小的延时。
            Thread.Sleep(10);
            SendClick(button);
        }
    }

    private static void SendClick(MouseButton button)
    {
        (uint down, uint up) = GetFlags(button);
        var inputs = new[]
        {
            NewMouseInput(down),
            NewMouseInput(up)
        };
        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }

    private static void SendOne(uint flag)
    {
        var inputs = new[] { NewMouseInput(flag) };
        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }

    private static (uint down, uint up) GetFlags(MouseButton button) => button switch
    {
        MouseButton.Left => (NativeMethods.MOUSEEVENTF_LEFTDOWN, NativeMethods.MOUSEEVENTF_LEFTUP),
        MouseButton.Right => (NativeMethods.MOUSEEVENTF_RIGHTDOWN, NativeMethods.MOUSEEVENTF_RIGHTUP),
        MouseButton.Middle => (NativeMethods.MOUSEEVENTF_MIDDLEDOWN, NativeMethods.MOUSEEVENTF_MIDDLEUP),
        _ => (NativeMethods.MOUSEEVENTF_LEFTDOWN, NativeMethods.MOUSEEVENTF_LEFTUP)
    };

    private static NativeMethods.INPUT NewMouseInput(uint flags) => new()
    {
        type = NativeMethods.INPUT_MOUSE,
        U = new NativeMethods.InputUnion
        {
            mi = new NativeMethods.MOUSEINPUT
            {
                dx = 0,
                dy = 0,
                mouseData = 0,
                dwFlags = flags,
                time = 0,
                dwExtraInfo = IntPtr.Zero
            }
        }
    };
}
