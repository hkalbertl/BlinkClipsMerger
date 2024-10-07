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

        public string FrameRate { get; set; }

        /// <summary>
        /// Parse the ffprobe frame rate to decimal.
        /// Such as "30000/1001" to 29.97
        /// </summary>
        public double CalculatedFrameRate
        {
            get
            {
                if (!string.IsNullOrEmpty(this.FrameRate))
                {
                    int slash = this.FrameRate.IndexOf('/');
                    if (slash > 0 
                        && int.TryParse(this.FrameRate[..slash], out int top) 
                        && int.TryParse(this.FrameRate[(slash + 1)..], out int bottom))
                    {
                        return 1d * top / bottom;
                    }
                }
                return double.NaN;
            }
        }

        public override string ToString()
        {
            return $"au={(this.HasAudio ? 'y' : 'n')},di={this.ClipWidth}x{this.ClipHeight},fr={this.CalculatedFrameRate:0.00}fps,ln={this.Duration:0.00}s";
        }
    }
}
