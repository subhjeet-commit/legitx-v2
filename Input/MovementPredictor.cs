using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace LegitX_V2
{
    public class MovementPredictor
    {
        private double predictionWeight = 0.4;

        private const int MIN_DATA_POINTS = 50;
        private const int MAX_HISTORY_SIZE = 1000;
        private const double LEARNING_RATE = 0.05;

        private List<MovementVector> movementHistory = new List<MovementVector>();

        private List<MovementPattern> learnedPatterns = new List<MovementPattern>();

        private Vector2D currentVelocity = new Vector2D(0, 0);
        private Vector2D currentAcceleration = new Vector2D(0, 0);
        private Point lastPosition = Point.Empty;
        private DateTime lastUpdateTime = DateTime.MinValue;

        private double averageSpeed = 0;
        private double speedVariance = 0;
        private double directionChangeFrequency = 0;
        private double jitterLevel = 0;

        private bool modelTrained = false;
        private int dataPointsCollected = 0;
        private MovementPattern activatedPattern = null;
        private double confidenceLevel = 0;

        private Random random = new Random();

        public class Vector2D
        {
            public double X { get; set; }
            public double Y { get; set; }

            public Vector2D(double x, double y)
            {
                X = x;
                Y = y;
            }

            public double Magnitude
            {
                get { return Math.Sqrt(X * X + Y * Y); }
            }

            public double Direction
            {
                get { return Math.Atan2(Y, X); }
            }

            public Vector2D Normalize()
            {
                double mag = Magnitude;
                if (mag > 0)
                {
                    return new Vector2D(X / mag, Y / mag);
                }
                return new Vector2D(0, 0);
            }

            public static Vector2D operator +(Vector2D v1, Vector2D v2)
            {
                return new Vector2D(v1.X + v2.X, v1.Y + v2.Y);
            }

            public static Vector2D operator -(Vector2D v1, Vector2D v2)
            {
                return new Vector2D(v1.X - v2.X, v1.Y - v2.Y);
            }

            public static Vector2D operator *(Vector2D v, double scalar)
            {
                return new Vector2D(v.X * scalar, v.Y * scalar);
            }

            public static Vector2D operator /(Vector2D v, double scalar)
            {
                if (scalar == 0)
                    return new Vector2D(0, 0);
                return new Vector2D(v.X / scalar, v.Y / scalar);
            }

            public double DotProduct(Vector2D other)
            {
                return X * other.X + Y * other.Y;
            }

            public double AngleBetween(Vector2D other)
            {
                double dot = DotProduct(other);
                double mag1 = Magnitude;
                double mag2 = other.Magnitude;

                if (mag1 == 0 || mag2 == 0)
                    return 0;

                double cos = dot / (mag1 * mag2);
                cos = Math.Max(-1, Math.Min(1, cos));

                return Math.Acos(cos);
            }
        }

        private class MovementVector
        {
            public Vector2D Position { get; set; }
            public Vector2D Velocity { get; set; }
            public Vector2D Acceleration { get; set; }
            public DateTime Timestamp { get; set; }

            public MovementVector(Vector2D position, Vector2D velocity, Vector2D acceleration, DateTime timestamp)
            {
                Position = position;
                Velocity = velocity;
                Acceleration = acceleration;
                Timestamp = timestamp;
            }
        }

        private class MovementPattern
        {
            public List<Vector2D> VelocitySequence { get; set; }
            public List<Vector2D> AccelerationSequence { get; set; }
            public double AverageSpeed { get; set; }
            public double DirectionChangeRate { get; set; }
            public int UsageCount { get; set; }
            public double ConfidenceScore { get; set; }

            public MovementPattern()
            {
                VelocitySequence = new List<Vector2D>();
                AccelerationSequence = new List<Vector2D>();
                UsageCount = 0;
                ConfidenceScore = 0;
            }

            public double CalculateSimilarity(List<Vector2D> velocities, List<Vector2D> accelerations)
            {
                if (velocities.Count == 0 || VelocitySequence.Count == 0)
                    return 0;

                double velocitySimilarity = CalculateSequenceSimilarity(velocities, VelocitySequence);
                double accelerationSimilarity = CalculateSequenceSimilarity(accelerations, AccelerationSequence);

                return (velocitySimilarity * 0.7) + (accelerationSimilarity * 0.3);
            }

            private double CalculateSequenceSimilarity(List<Vector2D> seq1, List<Vector2D> seq2)
            {
                int m = seq1.Count;
                int n = seq2.Count;

                if (m == 0 || n == 0)
                    return 0;

                if (m > 50 || n > 50)
                {
                    seq1 = ResampleSequence(seq1, Math.Min(50, m));
                    seq2 = ResampleSequence(seq2, Math.Min(50, n));
                    m = seq1.Count;
                    n = seq2.Count;
                }

                double[,] dtw = new double[m, n];

                for (int i = 0; i < m; i++)
                {
                    for (int j = 0; j < n; j++)
                    {
                        dtw[i, j] = double.MaxValue;
                    }
                }

                dtw[0, 0] = 0;

                for (int i = 0; i < m; i++)
                {
                    for (int j = 0; j < n; j++)
                    {
                        if (i > 0)
                        {
                            dtw[i, j] = Math.Min(dtw[i, j], dtw[i - 1, j]);
                        }
                        if (j > 0)
                        {
                            dtw[i, j] = Math.Min(dtw[i, j], dtw[i, j - 1]);
                        }
                        if (i > 0 && j > 0)
                        {
                            dtw[i, j] = Math.Min(dtw[i, j], dtw[i - 1, j - 1]);
                        }

                        if (i > 0 || j > 0)
                        {
                            double dist = Vector2DDistance(seq1[i], seq2[j]);
                            dtw[i, j] += dist;
                        }
                    }
                }

                double maxDistance = m + n;
                double normalizedDistance = dtw[m - 1, n - 1] / maxDistance;
                return 1.0 - Math.Min(1.0, normalizedDistance);
            }

            private List<Vector2D> ResampleSequence(List<Vector2D> sequence, int targetLength)
            {
                if (sequence.Count <= 1 || targetLength <= 1)
                    return sequence;

                List<Vector2D> result = new List<Vector2D>();

                for (int i = 0; i < targetLength; i++)
                {
                    double index = (i * (sequence.Count - 1)) / (double)(targetLength - 1);
                    int lowIndex = (int)Math.Floor(index);
                    int highIndex = (int)Math.Ceiling(index);

                    if (lowIndex == highIndex || highIndex >= sequence.Count)
                    {
                        result.Add(sequence[lowIndex]);
                    }
                    else
                    {
                        double weight = index - lowIndex;
                        Vector2D v1 = sequence[lowIndex];
                        Vector2D v2 = sequence[highIndex];

                        Vector2D interpolated = new Vector2D(
                            v1.X * (1 - weight) + v2.X * weight,
                            v1.Y * (1 - weight) + v2.Y * weight
                        );

                        result.Add(interpolated);
                    }
                }

                return result;
            }

            private double Vector2DDistance(Vector2D v1, Vector2D v2)
            {
                double dx = v1.X - v2.X;
                double dy = v1.Y - v2.Y;
                return Math.Sqrt(dx * dx + dy * dy);
            }
        }

        public Vector2D PredictMovement(Point currentPos, Point previousPos)
        {
            DateTime now = DateTime.Now;
            double timeDelta = 1.0 / 60.0;
            if (lastUpdateTime != DateTime.MinValue)
            {
                timeDelta = (now - lastUpdateTime).TotalSeconds;
                timeDelta = Math.Max(0.001, timeDelta);
            }

            UpdatePhysicsState(currentPos, previousPos, timeDelta);

            AddMovementDataPoint(currentPos, currentVelocity, currentAcceleration, now);

            UpdateBehaviorMetrics();

            if (!modelTrained && dataPointsCollected >= MIN_DATA_POINTS)
            {
                TrainModel();
                modelTrained = true;
            }
            else if (modelTrained && dataPointsCollected % 10 == 0)
            {
                UpdateModel();
            }

            Vector2D prediction = new Vector2D(0, 0);
            if (modelTrained)
            {
                prediction = GeneratePrediction();
            }

            if (timeDelta > 1.0)
            {
                this.Reset();
            }

            lastUpdateTime = now;

            return prediction;
        }

        private void UpdatePhysicsState(Point currentPos, Point previousPos, double timeDelta)
        {
            if (previousPos.IsEmpty)
            {
                lastPosition = currentPos;
                return;
            }

            Vector2D rawVelocity = new Vector2D(
                (currentPos.X - previousPos.X) / timeDelta,
                (currentPos.Y - previousPos.Y) / timeDelta
            );

            Vector2D rawAcceleration = new Vector2D(
                (rawVelocity.X - currentVelocity.X) / timeDelta,
                (rawVelocity.Y - currentVelocity.Y) / timeDelta
            );

            double smoothingFactor = 0.3;
            currentVelocity = new Vector2D(
                currentVelocity.X * (1 - smoothingFactor) + rawVelocity.X * smoothingFactor,
                currentVelocity.Y * (1 - smoothingFactor) + rawVelocity.Y * smoothingFactor
            );

            currentAcceleration = new Vector2D(
                currentAcceleration.X * (1 - smoothingFactor) + rawAcceleration.X * smoothingFactor,
                currentAcceleration.Y * (1 - smoothingFactor) + rawAcceleration.Y * smoothingFactor
            );

            lastPosition = currentPos;
        }

        private void AddMovementDataPoint(Point position, Vector2D velocity, Vector2D acceleration, DateTime timestamp)
        {
            Vector2D positionVector = new Vector2D(position.X, position.Y);
            MovementVector movementVector = new MovementVector(positionVector, velocity, acceleration, timestamp);
            movementHistory.Add(movementVector);

            if (movementHistory.Count > MAX_HISTORY_SIZE)
            {
                movementHistory.RemoveAt(0);
            }

            dataPointsCollected++;
        }

        private void UpdateBehaviorMetrics()
        {
            if (movementHistory.Count < 10)
                return;

            var recentMovements = movementHistory.Skip(Math.Max(0, movementHistory.Count - 100)).ToList();

            double totalSpeed = recentMovements.Sum(m => m.Velocity.Magnitude);
            averageSpeed = totalSpeed / recentMovements.Count;

            double sumSquaredDiff = recentMovements.Sum(m => Math.Pow(m.Velocity.Magnitude - averageSpeed, 2));
            speedVariance = sumSquaredDiff / recentMovements.Count;

            int directionChanges = 0;
            for (int i = 1; i < recentMovements.Count; i++)
            {
                Vector2D prev = recentMovements[i - 1].Velocity;
                Vector2D curr = recentMovements[i].Velocity;

                if (prev.Magnitude > 0 && curr.Magnitude > 0)
                {
                    double angleDiff = prev.AngleBetween(curr);
                    if (angleDiff > Math.PI / 4)
                    {
                        directionChanges++;
                    }
                }
            }
            directionChangeFrequency = directionChanges / (double)recentMovements.Count;

            double totalJitter = 0;
            for (int i = 1; i < recentMovements.Count; i++)
            {
                Vector2D expected = recentMovements[i - 1].Velocity + recentMovements[i - 1].Acceleration;
                Vector2D actual = recentMovements[i].Velocity;

                Vector2D diff = actual - expected;
                totalJitter += diff.Magnitude;
            }
            jitterLevel = totalJitter / (recentMovements.Count - 1);
        }

        private void TrainModel()
        {
            ExtractPatterns();

            foreach (var pattern in learnedPatterns)
            {
                pattern.ConfidenceScore = 0.5;
            }

            Console.WriteLine($"Model trained with {learnedPatterns.Count} patterns");
        }

        private void UpdateModel()
        {
            ExtractPatterns();

            UpdateConfidenceScores();

            PruneLowConfidencePatterns();
        }

        private void ExtractPatterns()
        {
            if (movementHistory.Count < 30)
                return;

            var recentMovements = movementHistory.Skip(Math.Max(0, movementHistory.Count - 200)).ToList();

            for (int windowSize = 5; windowSize <= 15; windowSize += 5)
            {
                ExtractPatternsWithWindowSize(recentMovements, windowSize);
            }
        }

        private void ExtractPatternsWithWindowSize(List<MovementVector> movements, int windowSize)
        {
            if (movements.Count < windowSize * 2)
                return;

            for (int i = 0; i <= movements.Count - windowSize; i += windowSize / 2)
            {
                List<Vector2D> velocitySequence = new List<Vector2D>();
                List<Vector2D> accelerationSequence = new List<Vector2D>();

                for (int j = 0; j < windowSize; j++)
                {
                    int index = i + j;
                    if (index < movements.Count)
                    {
                        velocitySequence.Add(movements[index].Velocity);
                        accelerationSequence.Add(movements[index].Acceleration);
                    }
                }

                if (velocitySequence.Sum(v => v.Magnitude) < 10)
                    continue;

                bool isNew = true;
                foreach (var existingPattern in learnedPatterns)
                {
                    double similarity = existingPattern.CalculateSimilarity(velocitySequence, accelerationSequence);

                    if (similarity > 0.8)
                    {
                        BlendPattern(existingPattern, velocitySequence, accelerationSequence);

                        existingPattern.UsageCount++;

                        isNew = false;
                        break;
                    }
                }

                if (isNew)
                {
                    MovementPattern newPattern = new MovementPattern
                    {
                        VelocitySequence = velocitySequence,
                        AccelerationSequence = accelerationSequence,
                        AverageSpeed = velocitySequence.Average(v => v.Magnitude),
                        DirectionChangeRate = CalculateDirectionChangeRate(velocitySequence),
                        UsageCount = 1,
                        ConfidenceScore = 0.5
                    };

                    learnedPatterns.Add(newPattern);
                }
            }
        }

        private void BlendPattern(MovementPattern pattern, List<Vector2D> velocitySequence, List<Vector2D> accelerationSequence)
        {
            if (pattern.VelocitySequence.Count != velocitySequence.Count ||
                pattern.AccelerationSequence.Count != accelerationSequence.Count)
                return;

            for (int i = 0; i < velocitySequence.Count; i++)
            {
                pattern.VelocitySequence[i] = new Vector2D(
                    pattern.VelocitySequence[i].X * (1 - LEARNING_RATE) + velocitySequence[i].X * LEARNING_RATE,
                    pattern.VelocitySequence[i].Y * (1 - LEARNING_RATE) + velocitySequence[i].Y * LEARNING_RATE
                );
            }

            for (int i = 0; i < accelerationSequence.Count; i++)
            {
                pattern.AccelerationSequence[i] = new Vector2D(
                    pattern.AccelerationSequence[i].X * (1 - LEARNING_RATE) + accelerationSequence[i].X * LEARNING_RATE,
                    pattern.AccelerationSequence[i].Y * (1 - LEARNING_RATE) + accelerationSequence[i].Y * LEARNING_RATE
                );
            }

            pattern.AverageSpeed = pattern.AverageSpeed * (1 - LEARNING_RATE) +
                                  velocitySequence.Average(v => v.Magnitude) * LEARNING_RATE;

            pattern.DirectionChangeRate = pattern.DirectionChangeRate * (1 - LEARNING_RATE) +
                                        CalculateDirectionChangeRate(velocitySequence) * LEARNING_RATE;
        }

        private double CalculateDirectionChangeRate(List<Vector2D> velocitySequence)
        {
            if (velocitySequence.Count < 2)
                return 0;

            int directionChanges = 0;
            for (int i = 1; i < velocitySequence.Count; i++)
            {
                Vector2D prev = velocitySequence[i - 1];
                Vector2D curr = velocitySequence[i];

                if (prev.Magnitude > 0 && curr.Magnitude > 0)
                {
                    double angleDiff = prev.AngleBetween(curr);
                    if (angleDiff > Math.PI / 4)
                    {
                        directionChanges++;
                    }
                }
            }

            return directionChanges / (double)(velocitySequence.Count - 1);
        }

        private void UpdateConfidenceScores()
        {
            foreach (var pattern in learnedPatterns)
            {
                double usageBonus = Math.Min(0.1, pattern.UsageCount / 100.0);

                double precisionBonus = 0;
                if (pattern.VelocitySequence.Count > 0)
                {
                    double avgMagnitude = pattern.VelocitySequence.Average(v => v.Magnitude);
                    precisionBonus = Math.Min(0.1, avgMagnitude / 100.0);
                }

                pattern.ConfidenceScore = pattern.ConfidenceScore * 0.9 + (usageBonus + precisionBonus) * 0.1;

                pattern.ConfidenceScore = Math.Max(0, Math.Min(1, pattern.ConfidenceScore));
            }
        }

        private void PruneLowConfidencePatterns()
        {
            learnedPatterns.RemoveAll(p => p.ConfidenceScore < 0.2 && p.UsageCount < 5);

            if (learnedPatterns.Count > 50)
            {
                learnedPatterns = learnedPatterns
                    .OrderByDescending(p => p.ConfidenceScore * p.UsageCount)
                    .Take(50)
                    .ToList();
            }
        }

        private Vector2D GeneratePrediction()
        {
            var recentMovements = movementHistory
                .Skip(Math.Max(0, movementHistory.Count - 15))
                .ToList();

            if (recentMovements.Count < 5)
                return new Vector2D(0, 0);

            double avgMagnitude = recentMovements.Average(m => m.Velocity.Magnitude);

            if (avgMagnitude < 2.0)
            {
                return new Vector2D(0, 0);
            }

            List<Vector2D> recentVelocities = recentMovements
                .Select(m => m.Velocity)
                .ToList();

            List<Vector2D> recentAccelerations = recentMovements
                .Select(m => m.Acceleration)
                .ToList();

            MovementPattern bestPattern = null;
            double bestSimilarity = 0;

            foreach (var pattern in learnedPatterns)
            {
                double similarity = pattern.CalculateSimilarity(recentVelocities, recentAccelerations);

                similarity *= pattern.ConfidenceScore;

                if (similarity > bestSimilarity)
                {
                    bestSimilarity = similarity;
                    bestPattern = pattern;
                }
            }

            if (bestPattern != null && bestSimilarity > 0.6)
            {
                activatedPattern = bestPattern;
                confidenceLevel = bestSimilarity;

                int matchIndex = FindBestMatchIndex(recentVelocities, bestPattern.VelocitySequence);

                int nextIndex = matchIndex + 1;
                if (nextIndex < bestPattern.VelocitySequence.Count)
                {
                    double adjustedWeight = Math.Min(predictionWeight, predictionWeight * (avgMagnitude / 10.0));

                    Vector2D prediction = bestPattern.VelocitySequence[nextIndex] * adjustedWeight;

                    Vector2D blendedPrediction = new Vector2D(
                        currentVelocity.X * (1 - adjustedWeight) + prediction.X,
                        currentVelocity.Y * (1 - adjustedWeight) + prediction.Y
                    );

                    return blendedPrediction;
                }
            }

            double dampingFactor = Math.Min(1.0, avgMagnitude / 5.0);
            return new Vector2D(currentVelocity.X * dampingFactor, currentVelocity.Y * dampingFactor);
        }

        private int FindBestMatchIndex(List<Vector2D> recentSequence, List<Vector2D> patternSequence)
        {
            if (recentSequence.Count == 0 || patternSequence.Count == 0)
                return 0;

            int window = Math.Min(5, recentSequence.Count);
            var searchSequence = recentSequence.Skip(recentSequence.Count - window).ToList();

            double bestMatchScore = double.MinValue;
            int bestIndex = 0;

            for (int i = 0; i <= patternSequence.Count - window; i++)
            {
                double matchScore = 0;

                for (int j = 0; j < window; j++)
                {
                    Vector2D recent = searchSequence[j];
                    Vector2D pattern = patternSequence[i + j];

                    double similarity = 0;
                    if (recent.Magnitude > 0 && pattern.Magnitude > 0)
                    {
                        double angleDiff = recent.AngleBetween(pattern);
                        double magnitudeRatio = Math.Min(recent.Magnitude, pattern.Magnitude) /
                                              Math.Max(recent.Magnitude, pattern.Magnitude);

                        similarity = (1 - (angleDiff / Math.PI)) * 0.7 + magnitudeRatio * 0.3;
                    }

                    matchScore += similarity;
                }

                matchScore *= (1.0 + (i / (double)patternSequence.Count) * 0.2);

                if (matchScore > bestMatchScore)
                {
                    bestMatchScore = matchScore;
                    bestIndex = i + window - 1;
                }
            }

            return bestIndex;
        }

        public void Reset()
        {
            lastPosition = Point.Empty;
            lastUpdateTime = DateTime.MinValue;
            currentVelocity = new Vector2D(0, 0);
            currentAcceleration = new Vector2D(0, 0);
            activatedPattern = null;
            confidenceLevel = 0;
        }

        public double GetConfidenceLevel()
        {
            return confidenceLevel;
        }

        public int GetPatternCount()
        {
            return learnedPatterns.Count;
        }

        public string GetModelState()
        {
            return $"Patterns: {learnedPatterns.Count}, Data Points: {dataPointsCollected}, " +
                   $"Trained: {modelTrained}, Confidence: {confidenceLevel:F2}";
        }

        public Dictionary<string, double> GetBehaviorMetrics()
        {
            return new Dictionary<string, double>
            {
                { "AverageSpeed", averageSpeed },
                { "SpeedVariance", speedVariance },
                { "DirectionChangeFrequency", directionChangeFrequency },
                { "JitterLevel", jitterLevel }
            };
        }

        public void AdaptToApplication(string applicationName, bool isFullScreen)
        {
            switch (applicationName.ToLower())
            {
                case string game when game.Contains("game") || isFullScreen:
                    predictionWeight = 0.5;
                    break;

                case string office when office.Contains("word") || office.Contains("excel") || office.Contains("powerpoint"):
                    predictionWeight = 0.2;
                    break;

                case string browser when browser.Contains("chrome") || browser.Contains("firefox") || browser.Contains("edge"):
                    predictionWeight = 0.3;
                    break;

                default:
                    predictionWeight = 0.4;
                    break;
            }
        }
    }
}