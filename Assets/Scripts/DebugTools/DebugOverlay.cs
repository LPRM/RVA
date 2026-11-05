namespace RVA
{
    /// <summary>
    /// One switch for all the debug text boxes at the top left (MazeGravity, GameManager,
    /// WeatherService). Hidden at start; H toggles it (handled by MazeGravity's debug keys).
    /// </summary>
    public static class DebugOverlay
    {
        public static bool Visible { get; set; } = false;

        public static void Toggle()
        {
            Visible = !Visible;
        }
    }
}
