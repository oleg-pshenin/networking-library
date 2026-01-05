namespace Networking.Utils
{
    public static class DynamicAverageCalculator
    {
        public static double UpdateAverage(double currentAverage, double newValue, int samplesNumber)
        {
            var sampleWeight = 1.0 / samplesNumber;
            return currentAverage * (samplesNumber - 1) * sampleWeight + newValue * sampleWeight;
        }
    }
}