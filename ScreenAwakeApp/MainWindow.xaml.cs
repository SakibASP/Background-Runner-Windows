using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ScreenAwake;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool _running;
    private bool _moving;
    private DateTime _endTime;
    private uint _lastPulseTick;
    private uint _lastUserTick;
    private int _idleThreshold = 5;

    private static readonly Brush AccentBrush = (Brush)Application.Current.Resources["Accent"];
    private static readonly Brush DangerBrush = (Brush)Application.Current.Resources["Danger"];
    private static readonly Brush MutedBrush = (Brush)Application.Current.Resources["TextMuted"];
    private static readonly Brush SuccessBrush = (Brush)Application.Current.Resources["Success"];
    private static readonly Brush BlueBrush = Hex("#38BDF8");

    public MainWindow()
    {
        InitializeComponent();
        _timer.Tick += Timer_Tick;
    }

    // ---- Window chrome ----------------------------------------------------
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void MinBtn_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();

    // ---- Numeric input helpers -------------------------------------------
    private void Numeric_PreviewTextInput(object sender, TextCompositionEventArgs e)
        => e.Handled = !Regex.IsMatch(e.Text, "^[0-9]+$");

    private static int Read(TextBox box, int fallback, int min)
        => int.TryParse(box.Text, out var v) && v >= min ? v : fallback;

    private void MinutesUp_Click(object s, RoutedEventArgs e) => Bump(MinutesBox, +1, 1, 1440);
    private void MinutesDown_Click(object s, RoutedEventArgs e) => Bump(MinutesBox, -1, 1, 1440);
    private void IdleUp_Click(object s, RoutedEventArgs e) => Bump(IdleBox, +1, 1, 600);
    private void IdleDown_Click(object s, RoutedEventArgs e) => Bump(IdleBox, -1, 1, 600);

    private static void Bump(TextBox box, int delta, int min, int max)
    {
        int v = int.TryParse(box.Text, out var cur) ? cur : min;
        v = Math.Clamp(v + delta, min, max);
        box.Text = v.ToString();
    }

    // ---- Start / stop -----------------------------------------------------
    private void ActionBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_running) Stop("Stopped", "Ready when you are");
        else Start();
    }

    private void Start()
    {
        int minutes = Read(MinutesBox, 0, 1);
        if (minutes <= 0)
        {
            SubStatus.Text = "Enter minutes greater than 0";
            return;
        }
        _idleThreshold = Read(IdleBox, 5, 1);

        MinutesBox.Text = minutes.ToString();
        IdleBox.Text = _idleThreshold.ToString();

        _running = true;
        _moving = false;
        _lastPulseTick = 0;
        _lastUserTick = Native.GetTickCount();
        _endTime = DateTime.Now.AddMinutes(minutes);

        Native.KeepAwake(true);

        ActionBtn.Content = "Stop";
        ActionBtn.Background = DangerBrush;
        SetInputsEnabled(false);

        Timer_Tick(this, EventArgs.Empty);   // paint immediately
        _timer.Start();
    }

    private void Stop(string chip, string sub)
    {
        _running = false;
        _timer.Stop();
        Native.KeepAwake(false);

        ActionBtn.Content = "Start";
        ActionBtn.Background = AccentBrush;
        SetInputsEnabled(true);

        Countdown.Text = "00:00";
        SetChip(chip, MutedBrush);
        SubStatus.Text = sub;
    }

    private void SetInputsEnabled(bool on)
    {
        MinutesBox.IsEnabled = on;
        IdleBox.IsEnabled = on;
    }

    // ---- The heartbeat ----------------------------------------------------
    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (!_running) return;

        uint now = Native.GetTickCount();
        uint lastInput = Native.LastInputTick();

        // Input that arrived after our own pulse must be the real user.
        if (unchecked(lastInput - _lastPulseTick) > 200 && lastInput != _lastPulseTick)
            _lastUserTick = lastInput;

        double userIdle = unchecked(now - _lastUserTick) / 1000.0;
        _moving = userIdle >= _idleThreshold;

        if (_moving)
        {
            Native.WakePulse();                 // genuine input -> stays "Available"
            _lastPulseTick = Native.GetTickCount();
        }

        var remaining = _endTime - DateTime.Now;
        if (remaining <= TimeSpan.Zero)
        {
            Stop("Done", "Time's up — screen was kept awake");
            SetChip("Done", SuccessBrush);
            return;
        }

        Countdown.Text = $"{(int)remaining.TotalMinutes:00}:{remaining.Seconds:00}";

        if (_moving)
        {
            SetChip("Active", SuccessBrush);
            SubStatus.Text = "Keeping you Available";
        }
        else
        {
            SetChip("You're active", BlueBrush);
            SubStatus.Text = "Paused while you work";
        }
    }

    private void SetChip(string text, Brush color)
    {
        StateText.Text = text;
        StateText.Foreground = color;
        StateDot.Fill = color;
    }

    protected override void OnClosed(EventArgs e)
    {
        Native.KeepAwake(false);
        base.OnClosed(e);
    }

    private static SolidColorBrush Hex(string hex)
        => new((Color)ColorConverter.ConvertFromString(hex));
}
