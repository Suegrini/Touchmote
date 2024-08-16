﻿using Microsoft.Win32;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Timers;
using WiiTUIO.DeviceUtils;
using WiiTUIO.Properties;
using PointF = WiimoteLib.PointF;
using WiiTUIO.Output.Handlers.Touch;
using System.Diagnostics;
using WiiTUIO.Filters;

namespace WiiTUIO.Provider
{
    /// <summary>
    /// Interaction logic for CalibrationOverlay.xaml
    /// </summary>
    public partial class CalibrationOverlay : Window
    {
        private WiiKeyMapper keyMapper;
        private static CalibrationOverlay defaultInstance;

        private System.Windows.Forms.Screen primaryScreen;
        private IntPtr previousForegroundWindow = IntPtr.Zero;

        private Timer buttonTimer;

        private bool hidden = true;
        private bool timerElapsed = false;

        private int step = 0;

        private PointF btmRightPointBak;
        private PointF topLeftPointBak;

        private double topOffset;
        private double bottomOffset;
        private double leftOffset;
        private double rightOffset;

        private double topBackup;
        private double bottomBackup;
        private double leftBackup;
        private double rightBackup;
        private float tlBackup;
        private float trBackup;
        private float centerXBackup;
        private float centerYBackup;

        private double marginXBackup;
        private double marginYBackup;

        /// <summary>
        /// An event which is raised once calibration is finished.
        /// </summary>
        public event Action OnCalibrationFinished;

        public static CalibrationOverlay Current
        {
            get
            {
                if (defaultInstance == null)
                {
                    defaultInstance = new CalibrationOverlay();
                }
                return defaultInstance;
            }
        }

        public CalibrationOverlay()
        {
            InitializeComponent();

            primaryScreen = DeviceUtil.GetScreen(Settings.Default.primaryMonitor);

            Settings.Default.PropertyChanged += SettingsChanged;
            SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;

            this.CalibrationCanvas.Visibility = Visibility.Hidden;

            buttonTimer = new Timer();
            buttonTimer.Interval = 1000;
            buttonTimer.AutoReset = true;
            buttonTimer.Elapsed += buttonTimer_Elapsed;

            //Compensate for DPI settings

            Loaded += (o, e) =>
            {
                this.updateWindowToScreen(primaryScreen);

                //Prevent OverlayWindow from showing up in alt+tab menu.
                UIHelpers.HideFromAltTab(this);
            };
        }

        private void SystemEvents_DisplaySettingsChanged(object sender, EventArgs e)
        {
            this.primaryScreen = DeviceUtils.DeviceUtil.GetScreen(Settings.Default.primaryMonitor);
            Dispatcher.BeginInvoke(new Action(delegate ()
            {
                this.updateWindowToScreen(primaryScreen);
            }));
        }

        private void updateWindowToScreen(System.Windows.Forms.Screen screen)
        {
            PresentationSource source = PresentationSource.FromVisual(this);
            Matrix transformMatrix = source.CompositionTarget.TransformToDevice;

            this.Width = screen.Bounds.Width * transformMatrix.M22;
            this.Height = screen.Bounds.Height * transformMatrix.M11;
            UIHelpers.SetWindowPos((new WindowInteropHelper(this)).Handle, IntPtr.Zero, screen.Bounds.X, screen.Bounds.Y, screen.Bounds.Width, screen.Bounds.Height, UIHelpers.SetWindowPosFlags.SWP_NOACTIVATE | UIHelpers.SetWindowPosFlags.SWP_NOZORDER);
            this.CalibrationCanvas.Width = this.Width;
            this.CalibrationCanvas.Height = this.Height;
            UIHelpers.TopmostFix(this);
        }

        private void SettingsChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "primaryMonitor")
            {
                primaryScreen = DeviceUtil.GetScreen(Settings.Default.primaryMonitor);
                Dispatcher.BeginInvoke(new Action(delegate ()
                {
                    this.updateWindowToScreen(primaryScreen);
                }));
            }
        }

        public void StartCalibration(WiiKeyMapper keyMapper)
        {
            if (this.hidden)
            {
                this.hidden = false;

                this.keyMapper = keyMapper;
                this.keyMapper.SwitchToCalibration();
                this.keyMapper.OnButtonDown += keyMapper_OnButtonDown;
                this.keyMapper.OnButtonUp += keyMapper_OnButtonUp;
                buttonTimer.Elapsed += buttonTimer_Elapsed;

                previousForegroundWindow = UIHelpers.GetForegroundWindow();
                if (previousForegroundWindow == null)
                {
                    previousForegroundWindow = IntPtr.Zero;
                }

                Dispatcher.BeginInvoke(new Action(delegate ()
                {
                    this.Activate();

                    Color pointColor = CursorColor.getColor(keyMapper.WiimoteID);
                    pointColor.R = (byte)(pointColor.R * 0.8);
                    pointColor.G = (byte)(pointColor.G * 0.8);
                    pointColor.B = (byte)(pointColor.B * 0.8);
                    SolidColorBrush brush = new SolidColorBrush(pointColor);

                    this.wiimoteNo.Text = "Wiimote " + keyMapper.WiimoteID;
                    this.wiimoteNo.Foreground = brush;

                    this.TextBorder.SetValue(Canvas.LeftProperty, 0.5 * this.ActualWidth - (this.TextBorder.ActualWidth / 2));
                    this.TextBorder.SetValue(Canvas.TopProperty, 0.25 * this.ActualHeight - (this.TextBorder.ActualHeight / 2));

                    this.CalibrationCanvas.Opacity = 0.0;
                    this.CalibrationCanvas.Visibility = Visibility.Visible;

                    this.elipse.Stroke = this.lineX.Stroke = this.lineY.Stroke = brush;
                    this.elipse.Fill = new SolidColorBrush(Colors.Black);
                    this.elipse.Fill.Opacity = 0.9;

                    DoubleAnimation animation = UIHelpers.createDoubleAnimation(1.0, 200, false);
                    animation.FillBehavior = FillBehavior.HoldEnd;
                    animation.Completed += delegate (object sender, EventArgs pEvent)
                    {

                    };
                    this.CalibrationCanvas.BeginAnimation(FrameworkElement.OpacityProperty, animation, HandoffBehavior.SnapshotAndReplace);
                }), null);

                marginXBackup = Settings.Default.CalibrationMarginX;
                marginYBackup = Settings.Default.CalibrationMarginY;

                if (!Settings.Default.pointer_4IRMode)
                {
                    switch (this.keyMapper.WiimoteID)
                    {
                        case 1:
                            btmRightPointBak = Settings.Default.test_btmRightGun1;
                            topLeftPointBak = Settings.Default.test_topLeftGun1;
                            break;
                        case 2:
                            btmRightPointBak = Settings.Default.test_btmRightGun2;
                            topLeftPointBak = Settings.Default.test_topLeftGun2;
                            break;
                        case 3:
                            btmRightPointBak = Settings.Default.test_btmRightGun3;
                            topLeftPointBak = Settings.Default.test_topLeftGun3;
                            break;
                        case 4:
                            btmRightPointBak = Settings.Default.test_btmRightGun4;
                            topLeftPointBak = Settings.Default.test_topLeftGun4;
                            break;
                        default:
                            throw new Exception("Unknown Wiimote ID");
                    }
                    this.movePoint(1 - marginXBackup, 1 - marginYBackup);

                    step = 1;
                }
                else
                {
                    Settings.Default.CalibrationMarginX = 0;
                    Settings.Default.CalibrationMarginY = 0;

                    topBackup = Settings.Default.Top;
                    bottomBackup = Settings.Default.Bottom;
                    leftBackup = Settings.Default.Left;
                    rightBackup = Settings.Default.Right;
                    centerXBackup = Settings.Default.CenterX;
                    centerYBackup = Settings.Default.CenterY;
                    tlBackup = Settings.Default.TLled;
                    trBackup = Settings.Default.TRled;

                    Settings.Default.Top = 0;
                    Settings.Default.Bottom = 1;
                    Settings.Default.Left = 0;
                    Settings.Default.Right = 1;

                    topOffset = 0;
                    bottomOffset = 1;
                    leftOffset = 0;
                    rightOffset = 1;

                    this.movePoint(0.5, 0.5);

                    step = 0;
                }
            }
        }

        void OverlayWindow_KeyUp(object sender, KeyEventArgs e)
        {
            if (!this.hidden)
            {
                if (e.Key == Key.Escape)
                {
                    HideOverlay();
                }
            }
        }

        private void HideOverlay()
        {
            if (!this.hidden)
            {
                this.hidden = true;
                this.timerElapsed = false;

                this.keyMapper.OnButtonUp -= keyMapper_OnButtonUp;
                this.keyMapper.OnButtonDown -= keyMapper_OnButtonDown;
                this.keyMapper.SwitchToFallback();
                buttonTimer.Elapsed -= buttonTimer_Elapsed;

                Dispatcher.BeginInvoke(new Action(delegate ()
                {
                    if (previousForegroundWindow != IntPtr.Zero)
                    {
                        UIHelpers.SetForegroundWindow(previousForegroundWindow);
                    }
                    DoubleAnimation animation = UIHelpers.createDoubleAnimation(0.0, 200, false);
                    animation.FillBehavior = FillBehavior.HoldEnd;
                    animation.Completed += delegate (object sender, EventArgs pEvent)
                    {
                        this.CalibrationCanvas.Visibility = Visibility.Hidden;

                    };
                    this.CalibrationCanvas.BeginAnimation(FrameworkElement.OpacityProperty, animation, HandoffBehavior.SnapshotAndReplace);
                }), null);
                step = 0;
            }
        }

        private void finishedCalibration()
        {
            if (Settings.Default.pointer_4IRMode)
            {
                Settings.Default.Top = topOffset;
                Settings.Default.Bottom = bottomOffset;
                Settings.Default.Left = leftOffset;
                Settings.Default.Right = rightOffset;
                Settings.Default.CalibrationMarginX = marginXBackup;
                Settings.Default.CalibrationMarginY = marginYBackup;
            }

            Settings.Default.Save();

            this.HideOverlay();
        }

        public void CancelCalibration()
        {
            if (!Settings.Default.pointer_4IRMode)
            {
                switch (this.keyMapper.WiimoteID)
                {
                    case 1:
                        Settings.Default.test_btmRightGun1 = btmRightPointBak;
                        Settings.Default.test_btmRightGun1 = topLeftPointBak;
                        break;
                    case 2:
                        Settings.Default.test_btmRightGun2 = btmRightPointBak;
                        Settings.Default.test_btmRightGun2 = topLeftPointBak;
                        break;
                    case 3:
                        Settings.Default.test_btmRightGun3 = btmRightPointBak;
                        Settings.Default.test_topLeftGun3 = topLeftPointBak;
                        break;
                    case 4:
                        Settings.Default.test_btmRightGun4 = btmRightPointBak;
                        Settings.Default.test_topLeftGun4 = topLeftPointBak;
                        break;
                    default:
                        throw new Exception("Unknown Wiimote ID");
                }
            }
            else
            {
                Settings.Default.Top = topBackup;
                Settings.Default.Bottom = bottomBackup;
                Settings.Default.Left = leftBackup;
                Settings.Default.Right = rightBackup;
                Settings.Default.CenterX = centerXBackup;
                Settings.Default.CenterY = centerYBackup;
                Settings.Default.TLled = tlBackup;
                Settings.Default.TRled = trBackup;

                Settings.Default.CalibrationMarginX = marginXBackup;
                Settings.Default.CalibrationMarginY = marginYBackup;
            }

            Settings.Default.Save();

            this.HideOverlay();
        }

        private void keyMapper_OnButtonUp(WiiButtonEvent e)
        {
            e.Button = e.Button.Replace("OffScreen.", "");
            if (e.Button.ToLower().Equals("a") || e.Button.ToLower().Equals("b"))
            {
                this.buttonTimer.Stop();

                Dispatcher.BeginInvoke(new Action(delegate ()
                {
                    this.wiimoteNo.Text = "Wiimote " + keyMapper.WiimoteID + ":";
                    this.insText2.Text = " aim at the targets and press A or B to calibrate";

                    this.TextBorder.UpdateLayout();
                    this.TextBorder.SetValue(Canvas.LeftProperty, 0.5 * this.ActualWidth - (this.TextBorder.ActualWidth / 2));
                    this.TextBorder.SetValue(Canvas.TopProperty, 0.25 * this.ActualHeight - (this.TextBorder.ActualHeight / 2));
                }), null);

                if (this.timerElapsed)
                {
                    switch (step)
                    {
                        case 0:
                            this.movePoint(1 - marginXBackup, 1 - marginYBackup);

                            step = 1;
                            break;
                        case 1:
                            this.movePoint(marginXBackup, marginYBackup);

                            step = 2;
                            break;

                        case 2:
                            Dispatcher.BeginInvoke(new Action(delegate ()
                            {
                                this.CalibrationPoint.Visibility = Visibility.Hidden;

                                this.wiimoteNo.Text = null;
                                this.insText2.Text = "Press A confirm calibration, press B to restart calibration";

                                this.TextBorder.UpdateLayout();
                                this.TextBorder.SetValue(Canvas.LeftProperty, 0.5 * this.ActualWidth - (this.TextBorder.ActualWidth / 2));
                                this.TextBorder.SetValue(Canvas.TopProperty, 0.25 * this.ActualHeight - (this.TextBorder.ActualHeight / 2));
                            }), null);

                            step = 3;
                            break;

                        default: break;
                    }
                }

                this.timerElapsed = false;
            }
        }

        private void keyMapper_OnButtonDown(WiiButtonEvent e)
        {
            e.Button = e.Button.Replace("OffScreen.", "");
            if (step == 3)
            {
                if (e.Button.ToLower().Equals("a"))
                {
                    finishedCalibration();
                }
                else if (e.Button.ToLower().Equals("b"))
                {
                    this.movePoint(0.5, 0.5);
                    step = 1;
                }
            }
            else if (e.Button.ToLower().Equals("a") || e.Button.ToLower().Equals("b"))
            {
                if (!this.keyMapper.cursorPos.OutOfReach)
                {
                    this.buttonTimer.Start();
                    Dispatcher.BeginInvoke(new Action(delegate ()
                    {
                        this.wiimoteNo.Text = null;
                        this.insText2.Text = "Hold";

                        this.TextBorder.UpdateLayout();
                        this.TextBorder.SetValue(Canvas.LeftProperty, 0.5 * this.ActualWidth - (this.TextBorder.ActualWidth / 2));
                        this.TextBorder.SetValue(Canvas.TopProperty, 0.25 * this.ActualHeight - (this.TextBorder.ActualHeight / 2));
                    }), null);
                }
                else
                {
                    Dispatcher.BeginInvoke(new Action(delegate ()
                    {
                        this.wiimoteNo.Text = null;
                        this.insText2.Text = "Can't find sensors. Make sure you're at a proper distance and pointing at the screen";

                        this.TextBorder.UpdateLayout();
                        this.TextBorder.SetValue(Canvas.LeftProperty, 0.5 * this.ActualWidth - (this.TextBorder.ActualWidth / 2));
                        this.TextBorder.SetValue(Canvas.TopProperty, 0.25 * this.ActualHeight - (this.TextBorder.ActualHeight / 2));
                    }), null);
                }
            }
        }

        void buttonTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            this.buttonTimer.Stop();
            this.timerElapsed = true;

            Dispatcher.BeginInvoke(new Action(delegate ()
            {
                this.wiimoteNo.Text = null;
                this.insText2.Text = "Release";

                this.TextBorder.UpdateLayout();
                this.TextBorder.SetValue(Canvas.LeftProperty, 0.5 * this.ActualWidth - (this.TextBorder.ActualWidth / 2));
                this.TextBorder.SetValue(Canvas.TopProperty, 0.25 * this.ActualHeight - (this.TextBorder.ActualHeight / 2));
            }), null);

            switch (step)
            {
                case 0:
                    Settings.Default.CenterX = (float)((this.keyMapper.cursorPos.RelativeX - 2) * Math.Cos(this.keyMapper.cursorPos.Rotation) - (this.keyMapper.cursorPos.RelativeY - 2) * Math.Sin(this.keyMapper.cursorPos.Rotation) + 2);
                    Settings.Default.CenterY = (float)((this.keyMapper.cursorPos.RelativeX - 2) * Math.Sin(this.keyMapper.cursorPos.Rotation) + (this.keyMapper.cursorPos.RelativeY - 2) * Math.Cos(this.keyMapper.cursorPos.Rotation) + 2);
                    Settings.Default.TLled = (float)(0.5 - ((this.keyMapper.cursorPos.Width / this.keyMapper.cursorPos.Height) / 4));
                    Settings.Default.TRled = (float)(0.5 + ((this.keyMapper.cursorPos.Width / this.keyMapper.cursorPos.Height) / 4));
                    break;
                case 1:
                    if (!Settings.Default.pointer_4IRMode)
                    {
                        switch (this.keyMapper.WiimoteID)
                        {
                            case 1:
                                Settings.Default.test_btmRightGun1 = new PointF() { X = (float)this.keyMapper.cursorPos.RelativeX, Y = (float)this.keyMapper.cursorPos.RelativeY };
                                break;
                            case 2:
                                Settings.Default.test_btmRightGun2 = new PointF() { X = (float)this.keyMapper.cursorPos.RelativeX, Y = (float)this.keyMapper.cursorPos.RelativeY };
                                break;
                            case 3:
                                Settings.Default.test_btmRightGun3 = new PointF() { X = (float)this.keyMapper.cursorPos.RelativeX, Y = (float)this.keyMapper.cursorPos.RelativeY };
                                break;
                            case 4:
                                Settings.Default.test_btmRightGun4 = new PointF() { X = (float)this.keyMapper.cursorPos.RelativeX, Y = (float)this.keyMapper.cursorPos.RelativeY };
                                break;
                            default:
                                throw new Exception("Unknown Wiimote ID");
                        }
                    }
                    else
                    {
                        bottomOffset = this.keyMapper.cursorPos.LightbarY;
                        rightOffset = this.keyMapper.cursorPos.LightbarX;
                    }
                    break;
                case 2:
                    if (!Settings.Default.pointer_4IRMode)
                    {
                        switch (this.keyMapper.WiimoteID)
                        {
                            case 1:
                                Settings.Default.test_topLeftGun1 = new PointF() { X = (float)this.keyMapper.cursorPos.RelativeX, Y = (float)this.keyMapper.cursorPos.RelativeY };
                                break;
                            case 2:
                                Settings.Default.test_topLeftGun2 = new PointF() { X = (float)this.keyMapper.cursorPos.RelativeX, Y = (float)this.keyMapper.cursorPos.RelativeY };
                                break;
                            case 3:
                                Settings.Default.test_topLeftGun3 = new PointF() { X = (float)this.keyMapper.cursorPos.RelativeX, Y = (float)this.keyMapper.cursorPos.RelativeY };
                                break;
                            case 4:
                                Settings.Default.test_topLeftGun4 = new PointF() { X = (float)this.keyMapper.cursorPos.RelativeX, Y = (float)this.keyMapper.cursorPos.RelativeY };
                                break;
                            default:
                                throw new Exception("Unknown Wiimote ID");
                        }
                    }
                    else
                    {
                        topOffset = this.keyMapper.cursorPos.LightbarY;
                        leftOffset = this.keyMapper.cursorPos.LightbarX;
                    }
                    break;
                default: break;
            }
        }

        private Point movePoint(double fNormalX, double fNormalY)
        {
            Point tPoint = new Point(fNormalX * this.ActualWidth, fNormalY * this.ActualHeight);

            Dispatcher.BeginInvoke(new Action(delegate ()
            {
                this.CalibrationPoint.Visibility = Visibility.Visible;

                this.CalibrationPoint.SetValue(Canvas.LeftProperty, tPoint.X - (this.CalibrationPoint.ActualWidth / 2));
                this.CalibrationPoint.SetValue(Canvas.TopProperty, tPoint.Y - (this.CalibrationPoint.ActualHeight / 2));

            }), null);

            return tPoint;
        }

        public bool OverlayIsOn()
        {
            return !this.hidden;
        }
    }
}
