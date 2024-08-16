using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Forms;
using WiimoteLib;
using WiiTUIO.Filters;
using WiiTUIO.Properties;
using Point = WiimoteLib.Point;

namespace WiiTUIO.Provider
{
    public class ScreenPositionCalculator
    {
        private int wiimoteId = 0;

        private int minXPos;
        private int maxXPos;
        private int maxWidth;

        private uint[] see = new uint[4];
        private float buff = 0.05f;

        private PointF median;

        private PointF[] finalPos = new PointF[4]
        {
            new PointF { X = 0.39f, Y = 0.26f },
            new PointF { X = 0.61f, Y = 0.26f },
            new PointF { X = 0.39f, Y = 0.74f },
            new PointF { X = 0.61f, Y = 0.74f }
        };
        private PointF[] irPositionNew = new PointF[4];

        private double mappedX;
        private double mappedY;

        private float xDistTop;
        private float xDistBottom;
        private float yDistLeft;
        private float yDistRight;

        float angleTop;
        float angleBottom;
        float angleLeft;
        float angleRight;

        double angle;
        float height;
        float width;

        private float[] angleOffset = new float[4];

        private int minYPos;
        private int maxYPos;
        private int maxHeight;
        private int SBPositionOffset;
        private double CalcMarginOffsetY;

        private double midMarginX;
        private double midMarginY;
        private double marginBoundsX;
        private double marginBoundsY;

        private PointF topLeftPt = new PointF();
        private PointF bottomRightPt = new PointF();
        private PointF trueTopLeftPt = new PointF();
        private PointF trueBottomRightPt = new PointF();
        private double boundsX;
        private double boundsY;
        // Use 0.0 to mean use full mapped range
        private double targetAspectRatio = 0.0;

        private double smoothedX, smoothedZ;
        private int orientation;

        private int leftPoint = -1;

        private bool start = false;

        private Warper pWarper;

        private CursorPos lastPos;

        private Screen primaryScreen;

        private RadiusBuffer smoothingBuffer;
        private CoordFilter coordFilter;

        private int lastIrPoint1 = -1;
        private int lastIrPoint2 = -1;

        public CalibrationSettings settings;

        public ScreenPositionCalculator(int id, CalibrationSettings settings)
        {
            this.wiimoteId = id;
            this.settings = settings;
            this.pWarper = new Warper(this.settings);
            this.primaryScreen = DeviceUtils.DeviceUtil.GetScreen(Settings.Default.primaryMonitor);
            this.recalculateScreenBounds(this.primaryScreen);

            Settings.Default.PropertyChanged += SettingsChanged;
            SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;
            this.settings.PropertyChanged += SettingsChanged;

            lastPos = new CursorPos(0, 0, 0, 0, 0);

            coordFilter = new CoordFilter();
            this.smoothingBuffer = new RadiusBuffer(Settings.Default.pointer_positionSmoothing);

            //topLeftPt = new PointF() { X = 0.0f, Y = 0.0f };
            //centerPt = new PointF() { X = 0.5f, Y = 0.5f };
        }

        private void SettingsChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "primaryMonitor")
            {
                this.primaryScreen = DeviceUtils.DeviceUtil.GetScreen(Settings.Default.primaryMonitor);
                Console.WriteLine("Setting primary monitor for screen position calculator to " + this.primaryScreen.Bounds);
                this.recalculateScreenBounds(this.primaryScreen);
            }
            else
            {
                if (!Settings.Default.pointer_4IRMode)
                {
                    Console.WriteLine("Settings changed to " + e.PropertyName);
                    switch (e.PropertyName)
                    {
                        case "test_topLeftGun1":
                            trueTopLeftPt = topLeftPt = Settings.Default.test_topLeftGun1;
                            recalculateLightgunCoordBounds();
                            break;
                        case "test_topLeftGun2":
                            trueTopLeftPt = topLeftPt = Settings.Default.test_topLeftGun2;
                            recalculateLightgunCoordBounds();
                            break;
                        case "test_topLeftGun3":
                            trueTopLeftPt = topLeftPt = Settings.Default.test_topLeftGun3;
                            recalculateLightgunCoordBounds();
                            break;
                        case "test_topLeftGun4":
                            trueTopLeftPt = topLeftPt = Settings.Default.test_topLeftGun4;
                            recalculateLightgunCoordBounds();
                            break;
                        case "test_btmRightGun1":
                            trueBottomRightPt = bottomRightPt = Settings.Default.test_btmRightGun1;
                            recalculateLightgunCoordBounds();
                            break;
                        case "test_btmRightGun2":
                            trueBottomRightPt = bottomRightPt = Settings.Default.test_btmRightGun2;
                            recalculateLightgunCoordBounds();
                            break;
                        case "test_btmRightGun3":
                            trueBottomRightPt = bottomRightPt = Settings.Default.test_btmRightGun3;
                            recalculateLightgunCoordBounds();
                            break;
                        case "test_btmRightGun4":
                            trueBottomRightPt = bottomRightPt = Settings.Default.test_btmRightGun4;
                            recalculateLightgunCoordBounds();
                            break;
                        default: break;
                    }
                }
                else
                {
                    if (e.PropertyName == "Left" || e.PropertyName == "Right" || e.PropertyName == "Top" || e.PropertyName == "Bottom")
                    {
                        trueTopLeftPt.X = topLeftPt.X = this.settings.Left;
                        trueTopLeftPt.Y = topLeftPt.Y = this.settings.Top;
                        trueBottomRightPt.X = bottomRightPt.X = this.settings.Right;
                        trueBottomRightPt.Y = bottomRightPt.Y = this.settings.Bottom;
                        recalculateLightgunCoordBounds();
                    }
                }
            }
        }

        private void SystemEvents_DisplaySettingsChanged(object sender, EventArgs e)
        {
            this.primaryScreen = DeviceUtils.DeviceUtil.GetScreen(Settings.Default.primaryMonitor);
            recalculateScreenBounds(this.primaryScreen);
        }

        private void recalculateScreenBounds(Screen screen)
        {
            Console.WriteLine("Setting primary monitor for screen position calculator to " + this.primaryScreen.Bounds);
            minXPos = -(int)(screen.Bounds.Width * Settings.Default.pointer_marginsLeftRight);
            maxXPos = screen.Bounds.Width + (int)(screen.Bounds.Width * Settings.Default.pointer_marginsLeftRight);
            maxWidth = maxXPos - minXPos;
            minYPos = -(int)(screen.Bounds.Height * Settings.Default.pointer_marginsTopBottom);
            maxYPos = screen.Bounds.Height + (int)(screen.Bounds.Height * Settings.Default.pointer_marginsTopBottom);
            maxHeight = maxYPos - minYPos;
            SBPositionOffset = (int)(screen.Bounds.Height * Settings.Default.pointer_sensorBarPosCompensation);
            //CalcMarginOffsetY = 2.8571428571428568 * (0.3 - (Settings.Default.pointer_sensorBarPosCompensation * 0.5));
            CalcMarginOffsetY = Settings.Default.pointer_sensorBarPosCompensation;

            midMarginX = Settings.Default.pointer_marginsLeftRight * 0.5;
            midMarginY = Settings.Default.pointer_marginsTopBottom * 0.5;
            marginBoundsX = 1 / (1 - Settings.Default.pointer_marginsLeftRight);
            marginBoundsY = 1 / (1 - Settings.Default.pointer_marginsTopBottom);

            //topLeftPt = new PointF() { X = 0.76f, Y = 0.02f };
            //centerPt = new PointF() { X = 0.48f, Y = 0.25f };
            //topLeftPt = new PointF() { X = 0.22f, Y = 0.02f };
            //centerPt = new PointF() { X = 0.50f, Y = 0.40f };

            // OLD WORKING
            //topLeftPt = new PointF() { X = 0.22f, Y = 0.02f };
            //centerPt = new PointF() { X = 0.46f, Y = 0.17f };

            // NEWER WORKING
            //topLeftPt = new PointF() { X = 0.18f, Y = 0.01f };
            //centerPt = new PointF() { X = 0.50f, Y = 0.19f };
            //topLeftPt = new PointF() { X = 0.166f, Y = 0.004f };
            //centerPt = new PointF() { X = 0.44f, Y = 0.19f };

            //topLeftPt = new PointF() { X = 0.15f, Y = 0.002f };
            //centerPt = new PointF() { X = 0.43f, Y = 0.205f };

            if (!Settings.Default.pointer_4IRMode)
            {
                switch (this.wiimoteId)
                {
                    case 1:
                        trueTopLeftPt = topLeftPt = Settings.Default.test_topLeftGun1;
                        trueBottomRightPt = bottomRightPt = Settings.Default.test_btmRightGun1;
                        break;
                    case 2:
                        trueTopLeftPt = topLeftPt = Settings.Default.test_topLeftGun2;
                        trueBottomRightPt = bottomRightPt = Settings.Default.test_btmRightGun2;
                        break;
                    case 3:
                        trueTopLeftPt = topLeftPt = Settings.Default.test_topLeftGun3;
                        trueBottomRightPt = bottomRightPt = Settings.Default.test_btmRightGun3;
                        break;
                    case 4:
                        trueTopLeftPt = topLeftPt = Settings.Default.test_topLeftGun4;
                        trueBottomRightPt = bottomRightPt = Settings.Default.test_btmRightGun4;
                        break;
                    default:
                        break;
                }
            }
            else
            {
                trueTopLeftPt.X = topLeftPt.X = this.settings.Left;
                trueTopLeftPt.Y = topLeftPt.Y = this.settings.Top;
                trueBottomRightPt.X = bottomRightPt.X = this.settings.Right;
                trueBottomRightPt.Y = bottomRightPt.Y = this.settings.Bottom;
            }

            if (targetAspectRatio == 0.0)
            {
                //topLeftPt = new PointF() { X = (float)0.22010275999999998,
                //    Y = (float)Settings.Default.test_topLeftGunY
                //};

                recalculateLightgunCoordBounds();
            }
            else
            {
                RecalculateLightgunAspect(targetAspectRatio);
            }

            // topLeftPt = new PointF() { X = 0.21f, Y = 0.01f };
            // centerPt = new PointF() { X = 0.46f, Y = 0.17f };

            //topLeftPt = new PointF() { X = 0.17928672f, Y = 0.00781759f };
            //centerPt = new PointF() { X = 0.4391793f, Y = 0.14462541f };

            //lightbarXSlope = ((topLeftPt.X - centerPt.X) * 2.0) / (0.8 - 0.2);
            //lightbarYSlope = ((centerPt.Y - topLeftPt.Y) * 2.0) / (0.8 - 0.2);
        }

        private void recalculateLightgunCoordBounds()
        {
            boundsX = (1 - Settings.Default.CalibrationMarginX * 2) / (bottomRightPt.X - topLeftPt.X);
            boundsY = (1 - Settings.Default.CalibrationMarginY * 2) / (bottomRightPt.Y - topLeftPt.Y);
        }

        public CursorPos CalculateCursorPos(WiimoteState wiimoteState)
        {
            int x = 0;
            int y = 0;
            double marginX, marginY = 0.0;
            double lightbarX = 0.0;
            double lightbarY = 0.0;
            int offsetY = 0;
            double marginOffsetY = 0.0;
            PointF resultPos = new PointF();

            IRState irState = wiimoteState.IRState;

            if (!Settings.Default.pointer_4IRMode)
            {
                int irPoint1 = 0;
                int irPoint2 = 0;
                bool foundMidpoint = false;
                // First check if previously found points are still detected.
                // Prefer those points first
                if (lastIrPoint1 != -1 && lastIrPoint2 != -1)
                {
                    if (irState.IRSensors[lastIrPoint1].Found &&
                        irState.IRSensors[lastIrPoint2].Found)
                    {
                        foundMidpoint = true;
                        irPoint1 = lastIrPoint1;
                        irPoint2 = lastIrPoint2;
                    }
                }

                // If no midpoint found from previous points, check all available
                // IR points for a possible midpoint
                for (int i = 0; !foundMidpoint && i < irState.IRSensors.Count(); i++)
                {
                    if (irState.IRSensors[i].Found)
                    {
                        for (int j = i + 1; j < irState.IRSensors.Count() && !foundMidpoint; j++)
                        {
                            if (irState.IRSensors[j].Found)
                            {
                                foundMidpoint = true;

                                irPoint1 = i;
                                irPoint2 = j;
                            }
                        }
                    }
                }

                if (foundMidpoint)
                {
                    int i = irPoint1;
                    int j = irPoint2;
                    median.X = (irState.IRSensors[i].Position.X + irState.IRSensors[j].Position.X) / 2.0f;
                    median.Y = (irState.IRSensors[i].Position.Y + irState.IRSensors[j].Position.Y) / 2.0f;

                    if (Settings.Default.pointer_considerRotation)
                    {
                        smoothedX = smoothedX * 0.9f + wiimoteState.AccelState.RawValues.X * 0.1f;
                        smoothedZ = smoothedZ * 0.9f + wiimoteState.AccelState.RawValues.Z * 0.1f;

                        int l = leftPoint, r;
                        if (leftPoint == -1)
                        {
                            double absx = Math.Abs(smoothedX - 128), absz = Math.Abs(smoothedZ - 128);

                            if (orientation == 0 || orientation == 2) absx -= 5;
                            if (orientation == 1 || orientation == 3) absz -= 5;

                            if (absz >= absx)
                            {
                                if (absz > 5)
                                    orientation = (smoothedZ > 128) ? 0 : 2;
                            }
                            else
                            {
                                if (absx > 5)
                                    orientation = (smoothedX > 128) ? 3 : 1;
                            }

                            switch (orientation)
                            {
                                case 0: l = (irState.IRSensors[i].RawPosition.X < irState.IRSensors[j].RawPosition.X) ? i : j; break;
                                case 1: l = (irState.IRSensors[i].RawPosition.Y > irState.IRSensors[j].RawPosition.Y) ? i : j; break;
                                case 2: l = (irState.IRSensors[i].RawPosition.X > irState.IRSensors[j].RawPosition.X) ? i : j; break;
                                case 3: l = (irState.IRSensors[i].RawPosition.Y < irState.IRSensors[j].RawPosition.Y) ? i : j; break;
                            }
                        }
                        leftPoint = l;
                        r = l == i ? j : i;

                        double dx = irState.IRSensors[r].RawPosition.X - irState.IRSensors[l].RawPosition.X;
                        double dy = irState.IRSensors[r].RawPosition.Y - irState.IRSensors[l].RawPosition.Y;

                        double d = Math.Sqrt(dx * dx + dy * dy);

                        dx /= d;
                        dy /= d;

                        angle = Math.Atan2(dy, dx);
                    }

                    lastIrPoint1 = irPoint1;
                    lastIrPoint2 = irPoint2;
                }
                else if (!foundMidpoint)
                {
                    CursorPos err = lastPos;
                    err.OutOfReach = true;
                    err.OffScreen = true;
                    leftPoint = -1;
                    lastIrPoint1 = -1;
                    lastIrPoint2 = -1;

                    return err;
                }

                if (Properties.Settings.Default.pointer_sensorBarPos == "top")
                {
                    offsetY = -SBPositionOffset;
                    marginOffsetY = CalcMarginOffsetY;
                }
                else if (Properties.Settings.Default.pointer_sensorBarPos == "bottom")
                {
                    offsetY = SBPositionOffset;
                    marginOffsetY = -CalcMarginOffsetY;
                }

                if (Settings.Default.pointer_considerRotation)
                {
                    median.X = median.X - 0.5F;
                    median.Y = median.Y - 0.5F;

                    median = this.rotatePoint(median, angle);

                    median.X = median.X + 0.5F;
                    median.Y = median.Y + 0.5F;
                }

                resultPos = median;
            }
            else
            {
                // Wait for all postions to be recognised before starting
                if (irState.IRSensors[0].Found == true || irState.IRSensors[1].Found == true || irState.IRSensors[2].Found == true || irState.IRSensors[3].Found == true)
                {
                    start = true;
                }
                else if (start)
                {
                    // all positions not yet seen
                    CursorPos err = lastPos;
                    err.OutOfReach = true;
                    err.OffScreen = true;

                    return err;
                }
                for (int i = 0; i < 4; i++)
                {
                    // if LED not seen...
                    if (irState.IRSensors[i].Found != true)
                    {
                        // if unseen make sure all quadrants have a value if missing apply value with buffer and set to unseen (this step is important for 1 LED usage)
                        if (!(((irPositionNew[0].Y < median.Y) && (irPositionNew[0].X < median.X)) || ((irPositionNew[1].Y < median.Y) && (irPositionNew[1].X < median.X)) || ((irPositionNew[2].Y < median.Y) && (irPositionNew[2].X < median.X)) || ((irPositionNew[3].Y < median.Y) && (irPositionNew[3].X < median.X))))
                        {
                            irPositionNew[i].X = median.X + (median.X - finalPos[3].X) - buff;
                            irPositionNew[i].Y = median.Y + (median.Y - finalPos[3].Y) - buff;
                            see[0] = 0;
                        }
                        if (!(((irPositionNew[0].Y < median.Y) && (irPositionNew[0].X > median.X)) || ((irPositionNew[1].Y < median.Y) && (irPositionNew[1].X > median.X)) || ((irPositionNew[2].Y < median.Y) && (irPositionNew[2].X > median.X)) || ((irPositionNew[3].Y < median.Y) && (irPositionNew[3].X > median.X))))
                        {
                            irPositionNew[i].X = median.X + (median.X - finalPos[2].X) + buff;
                            irPositionNew[i].Y = median.Y + (median.Y - finalPos[2].Y) - buff;
                            see[1] = 0;
                        }
                        if (!(((irPositionNew[0].Y > median.Y) && (irPositionNew[0].X < median.X)) || ((irPositionNew[1].Y > median.Y) && (irPositionNew[1].X < median.X)) || ((irPositionNew[2].Y > median.Y) && (irPositionNew[2].X < median.X)) || ((irPositionNew[3].Y > median.Y) && (irPositionNew[3].X < median.X))))
                        {
                            irPositionNew[i].X = median.X + (median.X - finalPos[1].X) - buff;
                            irPositionNew[i].Y = median.Y + (median.Y - finalPos[1].Y) + buff;
                            see[2] = 0;
                        }
                        if (!(((irPositionNew[0].Y > median.Y) && (irPositionNew[0].X > median.X)) || ((irPositionNew[1].Y > median.Y) && (irPositionNew[1].X > median.X)) || ((irPositionNew[2].Y > median.Y) && (irPositionNew[2].X > median.X)) || ((irPositionNew[3].Y > median.Y) && (irPositionNew[3].X > median.X))))
                        {
                            irPositionNew[i].X = median.X + (median.X - finalPos[0].X) + buff;
                            irPositionNew[i].Y = median.Y + (median.Y - finalPos[0].Y) + buff;
                            see[3] = 0;
                        }

                        // if all quadrants have a value apply value with buffer and set to see/unseen            
                        if (irPositionNew[i].Y < median.Y)
                        {
                            if (irPositionNew[i].X < median.X)
                            {
                                irPositionNew[i].X = median.X + (median.X - finalPos[3].X) - buff;
                                irPositionNew[i].Y = median.Y + (median.Y - finalPos[3].Y) - buff;
                                see[0] = 0;
                            }
                            if (irPositionNew[i].X > median.X)
                            {
                                irPositionNew[i].X = median.X + (median.X - finalPos[2].X) + buff;
                                irPositionNew[i].Y = median.Y + (median.Y - finalPos[2].Y) - buff;
                                see[1] = 0;
                            }
                        }
                        if (irPositionNew[i].Y > median.Y)
                        {
                            if (irPositionNew[i].X < median.X)
                            {
                                irPositionNew[i].X = median.X + (median.X - finalPos[1].X) - buff;
                                irPositionNew[i].Y = median.Y + (median.Y - finalPos[1].Y) + buff;
                                see[2] = 0;
                            }
                            if (irPositionNew[i].X > median.X)
                            {
                                irPositionNew[i].X = median.X + (median.X - finalPos[0].X) + buff;
                                irPositionNew[i].Y = median.Y + (median.Y - finalPos[0].Y) + buff;
                                see[3] = 0;
                            }
                        }
                    }
                    else
                    {
                        // If LEDS have been seen place in correct quadrant, apply buffer an set to seen.
                        if (irState.IRSensors[i].Position.Y < median.Y)
                        {
                            if (irState.IRSensors[i].Position.X < median.X)
                            {
                                irPositionNew[i].X = irState.IRSensors[i].Position.X - buff;
                                irPositionNew[i].Y = irState.IRSensors[i].Position.Y - buff;
                                see[0] <<= 1;
                                see[0] |= 1;
                            }
                            else if (irState.IRSensors[i].Position.X > median.X)
                            {
                                irPositionNew[i].X = irState.IRSensors[i].Position.X + buff;
                                irPositionNew[i].Y = irState.IRSensors[i].Position.Y - buff;
                                see[1] <<= 1;
                                see[1] |= 1;
                            }
                        }
                        else if (irState.IRSensors[i].Position.Y > median.Y)
                        {
                            if (irState.IRSensors[i].Position.X < median.X)
                            {
                                irPositionNew[i].X = irState.IRSensors[i].Position.X - buff;
                                irPositionNew[i].Y = irState.IRSensors[i].Position.Y + buff;
                                see[2] <<= 1;
                                see[2] |= 1;
                            }
                            else if (irState.IRSensors[i].Position.X > median.X)
                            {
                                irPositionNew[i].X = irState.IRSensors[i].Position.X + buff;
                                irPositionNew[i].Y = irState.IRSensors[i].Position.Y + buff;
                                see[3] <<= 1;
                                see[3] |= 1;
                            }
                        }
                    }

                    // Arrange all values in to quadrants and remove buffer.
                    // If LEDS have been seen use there value
                    // If LEDS haven't been seen work out values form live positions

                    if (irPositionNew[i].Y < median.Y)
                    {
                        if (irPositionNew[i].X < median.X)
                        {
                            if ((see[0] & 0x02) != 0)
                            {
                                finalPos[0].X = irPositionNew[i].X + buff;
                                finalPos[0].Y = irPositionNew[i].Y + buff;
                            }
                            else if (irPositionNew[i].Y < 0)
                            {
                                float f = angleBottom + angleOffset[2];
                                finalPos[0].X = finalPos[2].X + yDistLeft * MathF.Cos(f);
                                finalPos[0].Y = finalPos[2].Y + yDistLeft * -MathF.Sin(f);
                            }
                            else if (irPositionNew[i].X < 0)
                            {
                                float f = angleRight - angleOffset[1];
                                finalPos[0].X = finalPos[1].X + xDistTop * -MathF.Cos(f);
                                finalPos[0].Y = finalPos[1].Y + xDistTop * MathF.Sin(f);
                            }
                        }
                        else if (irPositionNew[i].X > median.X)
                        {
                            if ((see[1] & 0x02) != 0)
                            {
                                finalPos[1].X = irPositionNew[i].X - buff;
                                finalPos[1].Y = irPositionNew[i].Y + buff;
                            }
                            else if (irPositionNew[i].Y < 0)
                            {
                                float f = angleBottom - (angleOffset[3] - MathF.PI);
                                finalPos[1].X = finalPos[3].X + yDistRight * MathF.Cos(f);
                                finalPos[1].Y = finalPos[3].Y + yDistRight * -MathF.Sin(f);
                            }
                            else if (irPositionNew[i].X > 1)
                            {
                                float f = angleLeft + (angleOffset[0] - MathF.PI);
                                finalPos[1].X = finalPos[0].X + xDistTop * MathF.Cos(f);
                                finalPos[1].Y = finalPos[0].Y + xDistTop * -MathF.Sin(f);
                            }
                        }
                    }
                    else if (irPositionNew[i].Y > median.Y)
                    {
                        if (irPositionNew[i].X < median.X)
                        {
                            if ((see[2] & 0x02) != 0)
                            {
                                finalPos[2].X = irPositionNew[i].X + buff;
                                finalPos[2].Y = irPositionNew[i].Y - buff;
                            }
                            else if (irPositionNew[i].Y > 1)
                            {
                                float f = angleTop - angleOffset[0];
                                finalPos[2].X = finalPos[0].X + yDistLeft * MathF.Cos(f);
                                finalPos[2].Y = finalPos[0].Y + yDistLeft * -MathF.Sin(f);
                            }
                            else if (irPositionNew[i].X < 0)
                            {
                                float f = angleRight + angleOffset[3];
                                finalPos[2].X = finalPos[3].X + xDistBottom * MathF.Cos(f);
                                finalPos[2].Y = finalPos[3].Y + xDistBottom * -MathF.Sin(f);
                            }
                        }
                        else if (irPositionNew[i].X > median.X)
                        {
                            if ((see[3] & 0x02) != 0)
                            {
                                finalPos[3].X = irPositionNew[i].X - buff;
                                finalPos[3].Y = irPositionNew[i].Y - buff;
                            }
                            else if (irPositionNew[i].Y > 1)
                            {
                                float f = angleTop + (angleOffset[1] - MathF.PI);
                                finalPos[3].X = finalPos[1].X + yDistRight * MathF.Cos(f);
                                finalPos[3].Y = finalPos[1].Y + yDistRight * -MathF.Sin(f);
                            }
                            else if (irPositionNew[i].X > 1)
                            {
                                float f = angleLeft - (angleOffset[2] - MathF.PI);
                                finalPos[3].X = finalPos[2].X + xDistBottom * -MathF.Cos(f);
                                finalPos[3].Y = finalPos[2].Y + xDistBottom * MathF.Sin(f);
                            }
                        }
                    }
                }

                pWarper.setSource(finalPos[0].X, finalPos[0].Y, finalPos[1].X, finalPos[1].Y, finalPos[2].X, finalPos[2].Y, finalPos[3].X, finalPos[3].Y);
                float[] fWarped = pWarper.warp();
                resultPos.X = fWarped[0];
                resultPos.Y = fWarped[1];

                if (irState.IRSensors[0].Found == true && irState.IRSensors[1].Found == true && irState.IRSensors[2].Found == true && irState.IRSensors[3].Found == true)
                {
                    median.Y = (irPositionNew[0].Y + irPositionNew[1].Y + irPositionNew[2].Y + irPositionNew[3].Y + 0.002f) / 4;
                    median.X = (irPositionNew[0].X + irPositionNew[1].X + irPositionNew[2].X + irPositionNew[3].X + 0.002f) / 4;
                }
                else
                {
                    median.Y = (finalPos[0].Y + finalPos[1].Y + finalPos[2].Y + finalPos[3].Y + 0.002f) / 4;
                    median.X = (finalPos[0].X + finalPos[1].X + finalPos[2].X + finalPos[3].X + 0.002f) / 4;
                }
                // If 4 LEDS can be seen and loop has run through 5 times update offsets and height      

                if (((1 << 5) & see[0] & see[1] & see[2] & see[3]) != 0)
                {
                    angleOffset[0] = angleTop - (angleLeft - MathF.PI);
                    angleOffset[1] = -(angleTop - angleRight);
                    angleOffset[2] = -(angleBottom - angleLeft);
                    angleOffset[3] = angleBottom - (angleRight - MathF.PI);
                    height = (yDistLeft + yDistRight) / 2.0f;
                    width = (xDistTop + xDistBottom) / 2.0f;
                }

                // If 2 LEDS can be seen and loop has run through 5 times update angle and distances

                if (((1 << 5) & see[0] & see[2]) != 0)
                {
                    angleLeft = MathF.Atan2(finalPos[2].Y - finalPos[0].Y, finalPos[0].X - finalPos[2].X);
                    yDistLeft = MathF.Hypot((finalPos[0].Y - finalPos[2].Y), (finalPos[0].X - finalPos[2].X));
                }

                if (((1 << 5) & see[3] & see[1]) != 0)
                {
                    angleRight = MathF.Atan2(finalPos[3].Y - finalPos[1].Y, finalPos[1].X - finalPos[3].X);
                    yDistRight = MathF.Hypot((finalPos[3].Y - finalPos[1].Y), (finalPos[3].X - finalPos[1].X));
                }

                if (((1 << 5) & see[0] & see[1]) != 0)
                {
                    angleTop = MathF.Atan2(finalPos[0].Y - finalPos[1].Y, finalPos[1].X - finalPos[0].X);
                    xDistTop = MathF.Hypot((finalPos[0].Y - finalPos[1].Y), (finalPos[0].X - finalPos[1].X));
                }

                if (((1 << 5) & see[3] & see[2]) != 0)
                {
                    angleBottom = MathF.Atan2(finalPos[2].Y - finalPos[3].Y, finalPos[3].X - finalPos[2].X);
                    xDistBottom = MathF.Hypot((finalPos[2].Y - finalPos[3].Y), (finalPos[2].X - finalPos[3].X));
                }

                // Add tilt correction
                angle = (MathF.Atan2(finalPos[0].Y - finalPos[1].Y, finalPos[1].X - finalPos[0].X) + MathF.Atan2(finalPos[2].Y - finalPos[3].Y, finalPos[3].X - finalPos[2].X)) / 2;

                if (see.Count(seen => seen == 0) >= 2 || Double.IsNaN(resultPos.X) || Double.IsNaN(resultPos.Y))
                {
                    CursorPos err = lastPos;
                    err.OutOfReach = true;
                    err.OffScreen = true;

                    return err;
                }
            }

            /*System.Windows.Point filteredPoint = coordFilter.AddGetFilteredCoord(new System.Windows.Point(relativePosition.X, relativePosition.Y), 1.0, 1.0);
            
            relativePosition.X = (float)filteredPoint.X;
            relativePosition.Y = (float)filteredPoint.Y;

            Vector smoothedPoint = smoothingBuffer.AddAndGet(new Vector(relativePosition.X, relativePosition.Y));
            */


            //x = Convert.ToInt32((float)maxWidth * smoothedPoint.X + minXPos);
            //y = Convert.ToInt32((float)maxHeight * smoothedPoint.Y + minYPos) + offsetY;
            x = Convert.ToInt32((float)maxWidth * (1 - median.X) + minXPos);
            y = Convert.ToInt32((float)maxHeight * median.Y + minYPos) + offsetY;
            //x = Convert.ToInt32((float)3902 * relativePosition.X + (-1170)); // input: [0.3, 0.65]
            //y = Convert.ToInt32((float)2191 * relativePosition.Y + (-657)) + offsetY; // Input: [0.3, 0.65]

            //// input: [0.3, 0.65]
            //marginX = Math.Min(1.0, Math.Max(0.0, 2.8571428571428568 * relativePosition.X - 0.857142857142857));
            //// input: [0.3, 0.65]
            //marginY = Math.Min(1.0, Math.Max(0.0, 2.8571428571428568 * relativePosition.Y + (marginOffsetY - 0.857142857142857)));
            marginX = Math.Min(1.0, Math.Max(0.0, (1 - median.X - midMarginX) * marginBoundsX));
            marginY = Math.Min(1.0, Math.Max(0.0, (median.Y - (marginOffsetY + midMarginX)) * marginBoundsY));

            //System.Diagnostics.Trace.WriteLine($"{marginY} | {relativePosition.Y}");
            lightbarX = (resultPos.X - topLeftPt.X) * boundsX + Settings.Default.CalibrationMarginX;
            lightbarY = (resultPos.Y - topLeftPt.Y) * boundsY + Settings.Default.CalibrationMarginY;

            //System.Diagnostics.Trace.WriteLine($"X {lightbarX} | {relativePosition.X}");
            //System.Diagnostics.Trace.WriteLine($"Y {lightbarY} | {relativePosition.Y}");

            if (x <= 0)
            {
                x = 0;
            }
            else if (x >= primaryScreen.Bounds.Width)
            {
                x = primaryScreen.Bounds.Width - 1;
            }
            if (y <= 0)
            {
                y = 0;
            }
            else if (y >= primaryScreen.Bounds.Height)
            {
                y = primaryScreen.Bounds.Height - 1;
            }

            //Console.WriteLine("{0} {1} {2}", relativePosition.X, marginX, x / (double)primaryScreen.Bounds.Width);

            //CursorPos result = new CursorPos(x, y, smoothedPoint.X, smoothedPoint.Y, smoothedRotation);
            CursorPos result = new CursorPos(x, y, median.X, median.Y, angle,
                marginX, marginY, lightbarX, lightbarY, width, height);

            if (lightbarX < 0.0 || lightbarX > 1.0 || lightbarY < 0.0 || lightbarY > 1.0)
            {
                result.OffScreen = true;
                result.LightbarX = Math.Min(1.0,
                Math.Max(0.0, lightbarX));
                result.LightbarY = Math.Min(1.0,
                Math.Max(0.0, lightbarY));
            }

            lastPos = result;
            return result;
        }

        private PointF rotatePoint(PointF point, double angle)
        {
            double sin = Math.Sin(angle);
            double cos = Math.Cos(angle);

            double xnew = point.X * cos - point.Y * sin;
            double ynew = point.X * sin + point.Y * cos;

            PointF result;

            xnew = Math.Min(0.5, Math.Max(-0.5, xnew));
            ynew = Math.Min(0.5, Math.Max(-0.5, ynew));

            result.X = (float)xnew;
            result.Y = (float)ynew;

            return result;
        }

        public void RecalculateFullLightgun()
        {
            targetAspectRatio = 0.0;

            topLeftPt = trueTopLeftPt;
            //topLeftPt = new PointF() { X = (float)0.22010275999999998,
            //    Y = (float)Settings.Default.test_topLeftGunY
            //};
            bottomRightPt = trueBottomRightPt;

            recalculateLightgunCoordBounds();
        }

        public void RecalculateLightgunAspect(double targetAspect)
        {
            this.targetAspectRatio = targetAspect;

            int outputWidth = (int)(targetAspect * primaryScreen.Bounds.Height);
            double scaleFactor = outputWidth / (double)primaryScreen.Bounds.Width;
            Console.WriteLine("scale: " + scaleFactor);
            double target_topLeftX = ((trueBottomRightPt.X + trueTopLeftPt.X) / 2) - ((trueBottomRightPt.X - trueTopLeftPt.X) * scaleFactor / 2);
            double target_bottomRightY = trueBottomRightPt.X - (target_topLeftX - trueTopLeftPt.X);

            //Trace.WriteLine($"{outputWidth} {target_topLeftX} {scaleFactor}");

            topLeftPt = new PointF()
            {
                X = (float)target_topLeftX,
                Y = trueTopLeftPt.Y
            };
            //topLeftPt = new PointF() { X = (float)0.22010275999999998,
            //    Y = (float)Settings.Default.test_topLeftGunY
            //};
            bottomRightPt = new PointF()
            {
                X = (float)target_bottomRightY,
                Y = trueBottomRightPt.Y
            };

            recalculateLightgunCoordBounds();
        }
    }

    public static class MathF
    {
        public const float PI = (float)Math.PI;
        public static float Atan2(float y, float x) => (float)Math.Atan2(y, x);
        public static float Cos(float d) => (float)Math.Cos(d);
        public static float Round(float a) => (float)Math.Round(a);
        public static float Sin(float a) => (float)Math.Sin(a);
        public static float Hypot(float p, float b) => (float)Math.Sqrt(Math.Pow(p, 2) + Math.Pow(b, 2));
        public static float Sqrt(float d) => (float)Math.Sqrt(d);
        public static float Max(float val1, float val2) => (float)Math.Max(val1, val2);
        public static float Min(float val1, float val2) => (float)Math.Min(val1, val2);
    }
}
