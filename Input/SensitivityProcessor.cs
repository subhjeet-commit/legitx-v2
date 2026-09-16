using System;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;

namespace LegitX_V2
{
    public class SensitivitySettings
    {
        public double GeneralSensitivity { get; set; } = 1.0;
        public double SensitivityCap { get; set; } = 10.0;
        public double OffsetSensitivity { get; set; } = 0.0;

        public double PreScaleX { get; set; } = 1.0;
        public double PreScaleY { get; set; } = 1.0;
        public double PostScaleX { get; set; } = 1.0;
        public double PostScaleY { get; set; } = 1.0;

        public double Acceleration { get; set; } = 1.0;

        public bool EnableSmoothing { get; set; } = true;
        public int SmoothingWindowSize { get; set; } = 3;
        public double SmoothingWeight { get; set; } = 0.5;

        public bool EnableAntiJitter { get; set; } = true;
        public double JitterThreshold { get; set; } = 2.0;
    }

    public class SensitivityProcessor
    {
        private SensitivitySettings settings;

        private Queue<Point> movementHistory = new Queue<Point>();

        private Point lastPosition = Point.Empty;
        private DateTime lastUpdateTime = DateTime.MinValue;

        private double velocityX = 0;
        private double velocityY = 0;

        private double accelX = 0;
        private double accelY = 0;

        public SensitivityProcessor(SensitivitySettings settings)
        {
            this.settings = settings ?? new SensitivitySettings();
        }

        public Point ProcessMouseMovement(Point currentPos, Point previousPos)
        {
            if (previousPos.IsEmpty)
                return currentPos;

            int deltaX = currentPos.X - previousPos.X;
            int deltaY = currentPos.Y - previousPos.Y;

            if (deltaX == 0 && deltaY == 0)
                return currentPos;

            DateTime now = DateTime.Now;
            double timeDelta = 1.0;
            if (lastUpdateTime != DateTime.MinValue)
            {
                timeDelta = (now - lastUpdateTime).TotalSeconds;
                timeDelta = Math.Max(0.001, timeDelta);
            }
            lastUpdateTime = now;

            if (settings.EnableAntiJitter)
            {
                ApplyAntiJitter(ref deltaX, ref deltaY);
            }

            double prevVelX = velocityX;
            double prevVelY = velocityY;
            velocityX = deltaX / timeDelta;
            velocityY = deltaY / timeDelta;
            accelX = (velocityX - prevVelX) / timeDelta;
            accelY = (velocityY - prevVelY) / timeDelta;

            double[] processedDelta = ProcessDelta(deltaX, deltaY);

            if (settings.EnableSmoothing)
            {
                ApplySmoothing(ref processedDelta[0], ref processedDelta[1]);
            }

            int newX = previousPos.X + (int)Math.Round(processedDelta[0]);
            int newY = previousPos.Y + (int)Math.Round(processedDelta[1]);

            UpdateMovementHistory(new Point(deltaX, deltaY));

            return new Point(newX, newY);
        }

        private double[] ProcessDelta(int deltaX, int deltaY)
        {
            double scaledDeltaX = deltaX * settings.PreScaleX;
            double scaledDeltaY = deltaY * settings.PreScaleY;

            scaledDeltaX = (scaledDeltaX * settings.GeneralSensitivity) + settings.OffsetSensitivity;
            scaledDeltaY = (scaledDeltaY * settings.GeneralSensitivity) + settings.OffsetSensitivity;

            if (settings.Acceleration != 1.0)
            {
                double magnitude = Math.Sqrt(scaledDeltaX * scaledDeltaX + scaledDeltaY * scaledDeltaY);

                if (magnitude > 0)
                {
                    double accelerationFactor = Math.Pow(magnitude, settings.Acceleration - 1.0);

                    accelerationFactor = Math.Max(0.1, Math.Min(10.0, accelerationFactor));

                    scaledDeltaX *= accelerationFactor;
                    scaledDeltaY *= accelerationFactor;
                }
            }

            scaledDeltaX *= settings.PostScaleX;
            scaledDeltaY *= settings.PostScaleY;

            double finalMagnitude = Math.Sqrt(scaledDeltaX * scaledDeltaX + scaledDeltaY * scaledDeltaY);
            if (finalMagnitude > settings.SensitivityCap)
            {
                double scale = settings.SensitivityCap / finalMagnitude;
                scaledDeltaX *= scale;
                scaledDeltaY *= scale;
            }

            return new double[] { scaledDeltaX, scaledDeltaY };
        }

        private void ApplyAntiJitter(ref int deltaX, ref int deltaY)
        {
            double magnitude = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

            if (magnitude < settings.JitterThreshold * 1.5)
            {
                double scale = Math.Pow(magnitude / (settings.JitterThreshold * 1.5), 2);

                if (scale < 0.15)
                {
                    deltaX = 0;
                    deltaY = 0;
                }
                else
                {
                    deltaX = (int)Math.Round(deltaX * scale);
                    deltaY = (int)Math.Round(deltaY * scale);
                }
            }
        }

        private void ApplySmoothing(ref double deltaX, ref double deltaY)
        {
            if (movementHistory.Count == 0)
                return;

            Point[] snapshot = movementHistory.ToArray();
            double avgDeltaX = 0;
            double avgDeltaY = 0;
            double totalWeight = 0;

            for (int i = 0; i < snapshot.Length; i++)
            {
                Point p = snapshot[i];
                double weight = (i + 1) / (double)snapshot.Length;
                avgDeltaX += p.X * weight;
                avgDeltaY += p.Y * weight;
                totalWeight += weight;
            }

            if (totalWeight > 0)
            {
                avgDeltaX /= totalWeight;
                avgDeltaY /= totalWeight;
            }

            deltaX = (deltaX * (1 - settings.SmoothingWeight)) + (avgDeltaX * settings.SmoothingWeight);
            deltaY = (deltaY * (1 - settings.SmoothingWeight)) + (avgDeltaY * settings.SmoothingWeight);
        }

        private void UpdateMovementHistory(Point delta)
        {
            movementHistory.Enqueue(delta);

            while (movementHistory.Count > settings.SmoothingWindowSize)
            {
                movementHistory.Dequeue();
            }
        }

        public void UpdateSettings(SensitivitySettings newSettings)
        {
            if (newSettings != null)
            {
                this.settings = newSettings;
            }
        }

        public void Reset()
        {
            movementHistory.Clear();
            lastPosition = Point.Empty;
            lastUpdateTime = DateTime.MinValue;
            velocityX = 0;
            velocityY = 0;
            accelX = 0;
            accelY = 0;
        }
    }
}