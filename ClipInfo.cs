namespace BlinkClipsMerger
{
    /// <summary>
    /// Storing details on a clip.
    /// </summary>
    /// <param name="fullPath">The full path to clip file.</param>
    /// <param name="captureTime">The capture time based on file name.</param>
    internal class ClipInfo(string fullPath, DateTime captureTime)
    {
        public string FullPath { get; set; } = fullPath;

        public DateTime CaptureTime { get; set; } = captureTime;

        public bool HasAudio { get; set; }

        public double Duration { get; set; }

        public int ClipWidth { get; set; }

        public int ClipHeight { get; set; }

        public override string ToString()
        {
            return $"au={(this.HasAudio ? 'y' : 'n')},di={this.ClipWidth}x{this.ClipHeight},ln={this.Duration:0.00}s";
        }
    }
}
