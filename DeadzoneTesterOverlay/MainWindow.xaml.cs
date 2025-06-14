using System;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Runtime.InteropServices;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using System.Windows.Controls;

namespace DeadzoneTesterOverlay
{
    public partial class MainWindow : Window
    {
        private const short AXIS_MAX_VALUE = 32767;
        private const short AXIS_MIN_VALUE = -32768;

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;

        private ViGEmClient client;
        private IXbox360Controller controller;
        private bool isLeftStick = false;
        private long currentTestValueX = 0;
        private long currentTestValueY = 0;
        private long lowerBoundX = 0;
        private long upperBoundX = AXIS_MAX_VALUE;
        private long lowerBoundY = 0;
        private long upperBoundY = AXIS_MAX_VALUE;
        private bool running = true;
        private bool isClickThrough = true;
        private bool isDragging;
        private Point startPoint;
        private IntPtr hwnd;
        private bool isMappingDeadzone = false;
        private readonly double[] testAngles = { 0, 45, 90, 135, 180, 225, 270, 315 };
        private int currentAngleIndex = 0;
        private double[] deadzoneMagnitudes = new double[8];
        private DateTime lastUpKeyTime = DateTime.MinValue;
        private DateTime lastDownKeyTime = DateTime.MinValue;
        private DateTime lastLeftKeyTime = DateTime.MinValue;
        private DateTime lastRightKeyTime = DateTime.MinValue;
        private DateTime lastWKeyTime = DateTime.MinValue;
        private DateTime lastSKeyTime = DateTime.MinValue;
        private DateTime lastAKeyTime = DateTime.MinValue;
        private DateTime lastDKeyTime = DateTime.MinValue;
        private readonly TimeSpan wasdDebounceTime = TimeSpan.FromMilliseconds(100);
        private readonly TimeSpan arrowDebounceTime = TimeSpan.FromMilliseconds(200);

        public MainWindow()
        {
            InitializeComponent();
            InitializeController();
            StartMainLoop();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            UpdateWindowStyle();
        }

        private void UpdateWindowStyle()
        {
            int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            if (isClickThrough)
                SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT);
            else
                SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle & ~WS_EX_TRANSPARENT);
        }

        protected override void OnMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e)
        {
            if (!isClickThrough)
            {
                base.OnMouseLeftButtonDown(e);
                isDragging = true;
                startPoint = e.GetPosition(this);
                CaptureMouse();
            }
        }

        protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
        {
            if (isDragging)
            {
                Point currentPoint = e.GetPosition(this);
                Left += currentPoint.X - startPoint.X;
                Top += currentPoint.Y - startPoint.Y;
            }
        }

        protected override void OnMouseLeftButtonUp(System.Windows.Input.MouseButtonEventArgs e)
        {
            if (isDragging)
            {
                isDragging = false;
                ReleaseMouseCapture();
            }
        }

        private void InitializeController()
        {
            try
            {
                client = new ViGEmClient();
                controller = client.CreateXbox360Controller();
                controller.Connect();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize ViGEmBus: {ex.Message}\nEnsure ViGEmBus driver is installed.", "Error");
                Close();
            }
        }

        private void StartMainLoop()
        {
            Thread loopThread = new Thread(() =>
            {
                while (running)
                {
                    try
                    {
                        UpdateInput();
                        UpdateUI();
                        UpdateController();
                        Thread.Sleep(50);
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show($"Main Loop Error: {ex.Message}", "Error");
                        });
                    }
                }

                Dispatcher.Invoke(() =>
                {
                    controller?.Disconnect();
                    client?.Dispose();
                    Close();
                });
            });
            loopThread.IsBackground = true;
            loopThread.Start();
        }

        private void UpdateInput()
        {
            DateTime now = DateTime.Now;

            if ((GetAsyncKeyState(0x11) != 0) && (GetAsyncKeyState(0x44) != 0))
            {
                isClickThrough = !isClickThrough;
                Dispatcher.Invoke(UpdateWindowStyle);
                Thread.Sleep(200);
            }

            if ((GetAsyncKeyState(0x11) != 0) && (GetAsyncKeyState(0x4D) != 0))
            {
                isMappingDeadzone = !isMappingDeadzone;
                if (isMappingDeadzone)
                {
                    currentAngleIndex = 0;
                    Array.Clear(deadzoneMagnitudes, 0, deadzoneMagnitudes.Length);
                    currentTestValueX = 0;
                    currentTestValueY = 0;
                    lowerBoundX = 0;
                    upperBoundX = AXIS_MAX_VALUE;
                    lowerBoundY = 0;
                    upperBoundY = AXIS_MAX_VALUE;
                }
                Thread.Sleep(200);
            }

            if (GetAsyncKeyState(0x1B) != 0) running = false;
            if (GetAsyncKeyState(0x70) != 0) isLeftStick = true;
            if (GetAsyncKeyState(0x71) != 0) isLeftStick = false;

            if (!isMappingDeadzone && GetAsyncKeyState(0x2E) != 0)
            {
                currentTestValueX = 0;
                currentTestValueY = 0;
                lowerBoundX = 0;
                upperBoundX = AXIS_MAX_VALUE;
                lowerBoundY = 0;
                upperBoundY = AXIS_MAX_VALUE;
                Thread.Sleep(200);
            }

            if (GetAsyncKeyState(0x41) != 0 && (now - lastAKeyTime) > wasdDebounceTime)
            {
                currentTestValueX = Math.Max(currentTestValueX - 1, AXIS_MIN_VALUE);
                lastAKeyTime = now;
            }
            if (GetAsyncKeyState(0x44) != 0 && (now - lastDKeyTime) > wasdDebounceTime)
            {
                currentTestValueX = Math.Min(currentTestValueX + 1, AXIS_MAX_VALUE);
                lastDKeyTime = now;
            }

            if (GetAsyncKeyState(0x57) != 0 && (now - lastWKeyTime) > wasdDebounceTime)
            {
                currentTestValueY = Math.Min(currentTestValueY + 1, AXIS_MAX_VALUE);
                lastWKeyTime = now;
            }
            if (GetAsyncKeyState(0x53) != 0 && (now - lastSKeyTime) > wasdDebounceTime)
            {
                currentTestValueY = Math.Max(currentTestValueY - 1, AXIS_MIN_VALUE);
                lastSKeyTime = now;
            }

            if (!isMappingDeadzone)
            {
                UpdateArrowKeys();
            }
            else
            {
                UpdateDeadzoneMapping();
            }
        }

        private void UpdateArrowKeys()
        {
            DateTime now = DateTime.Now;

            if (GetAsyncKeyState(0x25) != 0 && (now - lastLeftKeyTime) > arrowDebounceTime)
            {
                upperBoundX = currentTestValueX;
                currentTestValueX = (currentTestValueX + lowerBoundX) / 2;
                lastLeftKeyTime = now;
            }
            if (GetAsyncKeyState(0x27) != 0 && (now - lastRightKeyTime) > arrowDebounceTime)
            {
                lowerBoundX = currentTestValueX;
                currentTestValueX = (currentTestValueX + upperBoundX) / 2;
                lastRightKeyTime = now;
            }

            if (GetAsyncKeyState(0x26) != 0 && (now - lastUpKeyTime) > arrowDebounceTime) // Fixed typo: GetAsync Bursar -> GetAsyncKeyState
            {
                lowerBoundY = currentTestValueY;
                currentTestValueY = (currentTestValueY + upperBoundY) / 2;
                lastUpKeyTime = now;
            }
            if (GetAsyncKeyState(0x28) != 0 && (now - lastDownKeyTime) > arrowDebounceTime)
            {
                upperBoundY = currentTestValueY;
                currentTestValueY = (currentTestValueY + lowerBoundY) / 2;
                lastDownKeyTime = now;
            }
        }

        private void UpdateDeadzoneMapping()
        {
            if (currentAngleIndex >= testAngles.Length)
            {
                isMappingDeadzone = false;
                currentAngleIndex = 0;
                return;
            }

            double angle = testAngles[currentAngleIndex] * Math.PI / 180;
            DateTime now = DateTime.Now;

            if (GetAsyncKeyState(0x41) != 0 && (now - lastAKeyTime) > wasdDebounceTime)
            {
                currentTestValueX = Math.Max(currentTestValueX - 1, AXIS_MIN_VALUE);
                lastAKeyTime = now;
            }
            if (GetAsyncKeyState(0x44) != 0 && (now - lastDKeyTime) > wasdDebounceTime)
            {
                currentTestValueX = Math.Min(currentTestValueX + 1, AXIS_MAX_VALUE);
                lastDKeyTime = now;
            }

            if (GetAsyncKeyState(0x57) != 0 && (now - lastWKeyTime) > wasdDebounceTime)
            {
                currentTestValueY = Math.Min(currentTestValueY + 1, AXIS_MAX_VALUE);
                lastWKeyTime = now;
            }
            if (GetAsyncKeyState(0x53) != 0 && (now - lastSKeyTime) > wasdDebounceTime)
            {
                currentTestValueY = Math.Max(currentTestValueY - 1, AXIS_MIN_VALUE);
                lastSKeyTime = now;
            }

            if (GetAsyncKeyState(0x26) != 0 && (now - lastUpKeyTime) > arrowDebounceTime)
            {
                lowerBoundX = currentTestValueX;
                lowerBoundY = currentTestValueY;
                currentTestValueX = (currentTestValueX + upperBoundX) / 2;
                currentTestValueY = (currentTestValueY + upperBoundY) / 2;
                lastUpKeyTime = now;
            }
            if (GetAsyncKeyState(0x28) != 0 && (now - lastDownKeyTime) > arrowDebounceTime)
            {
                upperBoundX = currentTestValueX;
                upperBoundY = currentTestValueY;
                currentTestValueX = (currentTestValueX + lowerBoundX) / 2;
                currentTestValueY = (currentTestValueY + lowerBoundY) / 2;
                lastDownKeyTime = now;
            }

            double magnitude = Math.Sqrt(currentTestValueX * currentTestValueX + currentTestValueY * currentTestValueY);
            if (magnitude > 0)
            {
                currentTestValueX = (long)(magnitude * Math.Cos(angle));
                currentTestValueY = (long)(magnitude * Math.Sin(angle));
            }

            if (GetAsyncKeyState(0x2E) != 0)
            {
                deadzoneMagnitudes[currentAngleIndex] = magnitude / AXIS_MAX_VALUE;
                currentAngleIndex++;
                currentTestValueX = 0;
                currentTestValueY = 0;
                lowerBoundX = 0;
                upperBoundX = AXIS_MAX_VALUE;
                lowerBoundY = 0;
                upperBoundY = AXIS_MAX_VALUE;
                Thread.Sleep(200);
            }
        }

        private void UpdateUI()
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    StickLabel.Text = $"Testing: {(isLeftStick ? "Left Stick" : "Right Stick")} {(isClickThrough ? "[ClickThrough]" : "[Draggable]")}{(isMappingDeadzone ? " [Mapping]" : "")}";
                    XAxisLabel.Text = $"Output X: {currentTestValueX} ({(double)currentTestValueX / AXIS_MAX_VALUE:P1}) | Range: [{lowerBoundX}, {upperBoundX}]";
                    YAxisLabel.Text = $"Output Y: {currentTestValueY} ({(double)currentTestValueY / AXIS_MAX_VALUE:P1}) | Range: [{lowerBoundY}, {upperBoundY}]";

                    double canvasSize = 100;
                    double dotSize = 6;
                    double xPos = (currentTestValueX / (double)AXIS_MAX_VALUE) * (canvasSize - dotSize) / 2 + canvasSize / 2 - dotSize / 2;
                    double yPos = (-currentTestValueY / (double)AXIS_MAX_VALUE) * (canvasSize - dotSize) / 2 + canvasSize / 2 - dotSize / 2;
                    Canvas.SetLeft(JoystickDot, xPos);
                    Canvas.SetTop(JoystickDot, yPos);

                    PointCollection points = new PointCollection();
                    if (deadzoneMagnitudes[0] > 0)
                    {
                        for (int i = 0; i < testAngles.Length; i++)
                        {
                            if (deadzoneMagnitudes[i] > 0)
                            {
                                double angle = testAngles[i] * Math.PI / 180;
                                double magnitude = deadzoneMagnitudes[i];
                                double x = magnitude * Math.Cos(angle) * (canvasSize / 2) + canvasSize / 2;
                                double y = -magnitude * Math.Sin(angle) * (canvasSize / 2) + canvasSize / 2;
                                points.Add(new Point(x, y));
                            }
                        }
                    }
                    DeadzonePolygon.Points = points;

                    // Guard against invalid angle index
                    AngleLine.Visibility = isMappingDeadzone && currentAngleIndex < testAngles.Length ? Visibility.Visible : Visibility.Hidden;
                    if (isMappingDeadzone && currentAngleIndex < testAngles.Length)
                    {
                        double angle = testAngles[currentAngleIndex] * Math.PI / 180;
                        double x2 = Math.Cos(angle) * (canvasSize / 2) + canvasSize / 2;
                        double y2 = -Math.Sin(angle) * (canvasSize / 2) + canvasSize / 2;
                        AngleLine.X2 = x2;
                        AngleLine.Y2 = y2;
                    }

                    if (!isMappingDeadzone && deadzoneMagnitudes[0] > 0)
                    {
                        string results = "Deadzone Magnitudes:\n";
                        for (int i = 0; i < testAngles.Length; i++)
                        {
                            if (deadzoneMagnitudes[i] > 0)
                                results += $"{testAngles[i]}°: {deadzoneMagnitudes[i]:P1}\n";
                        }
                        YAxisLabel.Text += $"\n{results}";
                    }
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"UI Update Error: {ex.Message}", "Error");
                });
            }
        }

        private void UpdateController()
        {
            if (isLeftStick)
            {
                controller.SetAxisValue(Xbox360Axis.LeftThumbX, (short)currentTestValueX);
                controller.SetAxisValue(Xbox360Axis.LeftThumbY, (short)currentTestValueY);
                controller.SetAxisValue(Xbox360Axis.RightThumbX, 0);
                controller.SetAxisValue(Xbox360Axis.RightThumbY, 0);
            }
            else
            {
                controller.SetAxisValue(Xbox360Axis.RightThumbX, (short)currentTestValueX);
                controller.SetAxisValue(Xbox360Axis.RightThumbY, (short)currentTestValueY);
                controller.SetAxisValue(Xbox360Axis.LeftThumbX, 0);
                controller.SetAxisValue(Xbox360Axis.LeftThumbY, 0);
            }
            controller.SubmitReport();
        }
    }
}