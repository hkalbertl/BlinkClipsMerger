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

        [Option('m', "ffmpeg", HelpText = "Specify the path to \"ffmpeg\" executable if FFmpeg is not configurated in system PATH variable.")]
        public string FFMpegExecutablePath { get; set; }

        [Option('p', "ffprobe", HelpText = "Specify the path to \"ffprobe\" executable if FFmpeg is not configurated in system PATH variable.")]
        public string FFProbeExecutablePath { get; set; }

        [Option('t', "filename-template", HelpText = "The template string for generating output file name. Default value is \"{0}_{1:yyyy-MM-dd}.mp4\" where {0} is the camera name and {1} is the capture time (standard .NET date/time format can be applied). If -g option is used without -t, the default template \"{0}_{1:yyyy-MM}.mp4\" will be used.")]
        public string FileNameTemplate { get; set; }

        [Option('r', "video-frame-rate", Default = 25, HelpText = "The output video frame rate.")]
        public float FrameRate { get; set; }

        [Option('d', "title-duration", Default = 2, HelpText = "The duration (in seconds) of title image.")]
        public int TitleDuration { get; set; }

        [Option('c', "video-codec", Default = "libx265", HelpText = "The video codec for merged output files.")]
        public string VideoCodec { get; set; }

        [Option('s', "video-codec-preset", HelpText = "The video codec preset to be used for output files. It will affect the output quality and not all video codec supported.")]
        public string VideoCodecPreset { get; set; }

        [Option('i', "ignore-duration", Default = 1, HelpText = "Ignore clips that having duration (in seconds) lesser than specified value.")]
        public int IgnoreDuration { get; set; }

        [Option('f', "camera-filter", HelpText = "The camera name regex filter. Such as -f \"^Gar\" will match Garden and Garage clips.")]
        public string CameraFilter { get; set; }

        [Option("start-date", HelpText = "Filter out the clips captured before this start date. The date should be in yyyy-MM-dd format.")]
        public string StartDate { get; set; }

        [Option("end-date", HelpText = "Filter out the clips captured after this end date. The date should be in yyyy-MM-dd format.")]
        public string EndDate { get; set; }

        [Option("thread-culture", HelpText = "The custom thread culture to be used for date/time formatting. Such as zh-TW for Taiwan.")]
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
