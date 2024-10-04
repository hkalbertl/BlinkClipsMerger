using CommandLine;

namespace BlinkClipsMerger
{
    /// <summary>
    /// The parsed options from command line arguments.
    /// </summary>
    internal class Options
    {
        [Option('q', "quite", HelpText = "Hide unnecessary messages to console.")]
        public bool Quiet { get; set; }

        [Option('y', "overwrite", HelpText = "Overwrite existing output files.")]
        public bool Overwrite { get; set; }

        [Option('g', "group-by-month", HelpText = "Merge clips based on the captured month instead of date.")]
        public bool GroupByMonth { get; set; }

        [Option('m', "ffmpeg", HelpText = "Path to the ffmpeg executable, leave blank to use command ffmpeg.")]
        public string FFMpegExecutablePath { get; set; }

        [Option('p', "probe", HelpText = "Path to the ffprobe executable, leave blank to use command ffprobe.")]
        public string FFProbeExecutablePath { get; set; }

        [Option('t', "filename-template", Default = "{0}_{1:yyyy-MM-dd}.mp4", HelpText = "The template string for generating output file name. Where {0} is the camera name and {1} is the date. Standard .NET date/time format can be applied.")]
        public string FileNameTemplate { get; set; }

        [Option('f', "camera-filter", HelpText = "The camera name regex filter.")]
        public string CameraFilter { get; set; }

        [Option('r', "video-frame-rate", Default = 25, HelpText = "The video framerate.")]
        public float FrameRate { get; set; }

        [Option('d', "title-duration", Default = 2, HelpText = "The duration (in seconds) of title image.")]
        public int TitleDuration { get; set; }

        [Option('c', "video-codec", Default = "libx265", HelpText = "The video codec for merged output files.")]
        public string VideoCodec { get; set; }

        [Option('s', "video-codec-preset", HelpText = "The video codec preset that will affect the output quality.")]
        public string VideoCodecPreset { get; set; }

        [Option('i', "ignore-duration", Default = 1, HelpText = "Ignore clips that having duration (in seconds) lesser than specified value.")]
        public int IgnoreDuration { get; set; }

        [Option("thread-culture", HelpText = "The custom thread culture to be used for date/time formatting.")]
        public string ThreadCulture { get; set; }

        [Option("font-family", Default = "sans-serif", HelpText = "The font family used in titles.")]
        public string FontFamily { get; set; }

        [Option("font-size", Default = 144, HelpText = "The font size of titles.")]
        public float FontSize { get; set; }

        [Option("date-format", Default = "yyyy-MM-dd", HelpText = "The date format of title that using standard .NET date/time formatting string.")]
        public string DateFormat { get; set; }

        [Option("time-format", Default = "HH:mm:ss", HelpText = "The time format of title that using standard .NET date/time formatting string.")]
        public string TimeFormat { get; set; }

        [Option("title-foreground", Default = "#ffffff", HelpText = "Foreground color of title.")]
        public string TitleForeground { get; set; }

        [Option("title-background", Default = "#000000", HelpText = "Background color of title.")]
        public string TitleBackground { get; set; }

        [Value(0, Required = true, MetaName = "<input directory>", HelpText = "Path to the Blink clips source directory")]
        public string InputDirectory { get; set; }

        [Value(1, Required = true, MetaName = "<output directory>", HelpText = "Path to the video output directory")]
        public string OutputDirectory { get; set; }
    }
}
