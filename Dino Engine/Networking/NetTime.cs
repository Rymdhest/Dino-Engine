namespace Dino_Engine.Networking
{
    /// <summary>
    /// The beat a simulation runs to. Server and clients step in the same fixed ticks, whatever the frame rate, so that a hero
    /// simulated on two machines moves the same way on both.
    /// </summary>
    public static class NetTime
    {
        public const int TicksPerSecond = 60;
        public const float TickSeconds = 1f / TicksPerSecond;
        public const double TickSecondsPrecise = 1.0 / TicksPerSecond;
    }
}
