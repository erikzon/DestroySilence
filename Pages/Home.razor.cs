using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using FFmpegBlazor;

namespace DestroySilence.Pages
{
    public partial class Home : IDisposable
    {
        [Inject]
        public IJSRuntime Runtime { get; set; } = default!;

        [Inject]
        public Microsoft.Extensions.Localization.IStringLocalizer<Home> Loc { get; set; } = default!;

        private InputFile fileInputRef = default!; // DOM Reference
        private bool hasFile = false;

        // UI Variables
        private IBrowserFile? selectedFile;
        private bool isProcessing = false;
        private bool isDone = false;

        // Dynamic Parameters
        private int noiseThreshold = -20;
        private double silenceDuration = 0.3;

        // Progress Variables
        private string progressPhase = "";
        private double processingPercentage = 0;
        private double currentTargetDuration = 0;
        private int currentPhase = 0; // 1 = Detection, 2 = Rendering

        // Internals
        private FFMPEG? ffmpeg;
        private string stderr = "";

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                await Runtime.InvokeVoidAsync("audioVisualizer.registerResize","waveformCanvas");

                await Runtime.InvokeVoidAsync("initThreeBackground", "three-bg-canvas");

                if (FFmpegFactory.Runtime == null)
                {
                    FFmpegFactory.Logger += CapturarLogs;
                    await FFmpegFactory.Init(Runtime);
                }
                ffmpeg = FFmpegFactory.CreateFFmpeg(new FFmpegConfig() { Log = true });
                await ffmpeg.Load();
                StateHasChanged();
            }
        }

        public void Dispose()
        {
            Runtime.InvokeVoidAsync("audioVisualizer.unregisterResize");
        }

        private async Task OnFileSelected(InputFileChangeEventArgs e)
        {
            selectedFile = e.File;
            hasFile = true;
            StateHasChanged();

            // Order JavaScript to read the file directly from the Input and draw
            await Runtime.InvokeVoidAsync("audioVisualizer.generate", "fileInputElem", "waveformCanvas", noiseThreshold);
        }

        // This method is triggered instantly when moving the slider or changing the number
        private async Task UpdateThreshold(ChangeEventArgs e)
        {
            if (int.TryParse(e.Value?.ToString(), out int newValue))
            {
                noiseThreshold = newValue;

                // We only redraw the line on the existing canvas (super fast)
                if (hasFile)
                {
                    await Runtime.InvokeVoidAsync("audioVisualizer.draw", "waveformCanvas", noiseThreshold);
                }
            }
        }

        // Logs Interceptor (Here we do the magic of the percentage)
        private void CapturarLogs(Logs m)
        {
            if (string.IsNullOrEmpty(m.Message)) return;

            // We only need to save the entire log during phase 1 to read the silences at the end
            if (currentPhase == 1)
            {
                stderr += m.Message + "\n";
            }

            // If we still don't know the target duration, we try to catch it when FFmpeg prints it at the beginning
            if (currentTargetDuration == 0)
            {
                var durMatch = System.Text.RegularExpressions.Regex.Match(m.Message, @"Duration: (\d{2}):(\d{2}):(\d{2}\.\d{2})");
                if (durMatch.Success)
                {
                    var culture = System.Globalization.CultureInfo.InvariantCulture;
                    currentTargetDuration = int.Parse(durMatch.Groups[1].Value) * 3600 +
                                            int.Parse(durMatch.Groups[2].Value) * 60 +
                                            double.Parse(durMatch.Groups[3].Value, culture);
                }
            }

            // We look for the phrase "time=HH:MM:SS" to move the progress bar
            var timeMatch = System.Text.RegularExpressions.Regex.Match(m.Message, @"time=(\d{2}):(\d{2}):(\d{2}\.\d{2})");
            if (timeMatch.Success && currentTargetDuration > 0)
            {
                var culture = System.Globalization.CultureInfo.InvariantCulture;
                var currentSeconds = int.Parse(timeMatch.Groups[1].Value) * 3600 +
                                     int.Parse(timeMatch.Groups[2].Value) * 60 +
                                     double.Parse(timeMatch.Groups[3].Value, culture);

                // Calculate the percentage, ensuring it doesn't exceed 100%
                processingPercentage = Math.Min(100, (currentSeconds / currentTargetDuration) * 100);
                StateHasChanged(); // Force the visual update of the bar
            }
        }

        private async Task ProcessVideo()
        {
            if (selectedFile == null || ffmpeg == null || !ffmpeg.IsLoaded) return;

            Console.WriteLine("[DEBUG] Stage 1: Load file to memory.");
            isProcessing = true;
            isDone = false;
            processingPercentage = 0;
            progressPhase = Loc["LoadingFile"];
            StateHasChanged();

            var inputData = new byte[selectedFile.Size];
            using var stream = selectedFile.OpenReadStream(maxAllowedSize: 200 * 1024 * 1024); // Raised the limit to 200MB for long videos
            await stream.ReadAsync(inputData);

            ffmpeg.WriteFile("input.mp4", inputData);

            // --- PHASE 1: DETECTION ---
            Console.WriteLine("[DEBUG] Stage 2: Silence detection phase.");
            currentPhase = 1;
            currentTargetDuration = 0; // Will be filled dynamically with the log
            processingPercentage = 0;
            stderr = "";
            progressPhase = Loc["AnalyzingAudio"];
            StateHasChanged();

            // Apply input values using invariant culture to avoid issues with commas and decimal points
            string durStr = silenceDuration.ToString(System.Globalization.CultureInfo.InvariantCulture);
            await ffmpeg.Run("-i", "input.mp4", "-af", $"silencedetect=noise={noiseThreshold}dB:d={durStr}", "-f", "null", "-");

            Console.WriteLine("[DEBUG] Stage 3: Parse segments.");
            var output = stderr;

            var culture = System.Globalization.CultureInfo.InvariantCulture;
            var totalSeconds = currentTargetDuration; // Captured in CapturarLogs

            if (totalSeconds == 0) // Fallback in case the initial regex failed
            {
                var durationMatch = System.Text.RegularExpressions.Regex.Match(output, @"Duration: (\d{2}):(\d{2}):(\d{2}\.\d{2})");
                if (durationMatch.Success)
                {
                    totalSeconds = int.Parse(durationMatch.Groups[1].Value) * 3600 +
                                   int.Parse(durationMatch.Groups[2].Value) * 60 +
                                   double.Parse(durationMatch.Groups[3].Value, culture);
                }
                else
                {
                    Console.WriteLine("[DEBUG] ERROR: Failed to get total duration.");
                    isProcessing = false;
                    StateHasChanged();
                    return;
                }
            }

            var silenceStarts = System.Text.RegularExpressions.Regex.Matches(output, @"silence_start: ([\d\.]+)")
                                 .Select(m => double.Parse(m.Groups[1].Value, culture)).ToList();
            var silenceEnds = System.Text.RegularExpressions.Regex.Matches(output, @"silence_end: ([\d\.]+)")
                               .Select(m => double.Parse(m.Groups[1].Value, culture)).ToList();

            var keepSegments = new List<(double Start, double End)>();
            double currentTime = 0.0;

            for (int i = 0; i < silenceStarts.Count; i++)
            {
                var start = silenceStarts[i];
                var end = i < silenceEnds.Count ? silenceEnds[i] : totalSeconds;

                if (start > currentTime)
                {
                    keepSegments.Add((currentTime, start));
                }
                currentTime = end;
            }

            if (currentTime < totalSeconds)
            {
                keepSegments.Add((currentTime, totalSeconds));
            }

            if (keepSegments.Count <= 1)
            {
                Console.WriteLine("[DEBUG] Finish: No silence detected.");
                progressPhase = Loc["NoSilenceDetected"];
                isProcessing = false;
                StateHasChanged();
                return;
            }

            var filterComplex = "";
            var concatInputs = "";
            double expectedFinalDuration = 0;

            for (int i = 0; i < keepSegments.Count; i++)
            {
                var seg = keepSegments[i];
                var s = Math.Round(seg.Start, 3).ToString(culture);
                var e = Math.Round(seg.End, 3).ToString(culture);

                expectedFinalDuration += (seg.End - seg.Start); // Sum the fragments to know how long the new video will last

                filterComplex += $"[0:v]trim={s}:{e},setpts=PTS-STARTPTS[v{i}]; ";
                filterComplex += $"[0:a]atrim={s}:{e},asetpts=PTS-STARTPTS[a{i}]; ";
                concatInputs += $"[v{i}][a{i}]";
            }

            filterComplex += $"{concatInputs}concat=n={keepSegments.Count}:v=1:a=1[outv][outa]";
            ffmpeg.WriteFile("filter.txt", System.Text.Encoding.UTF8.GetBytes(filterComplex));

            // --- PHASE 2: RENDERING ---
            Console.WriteLine("[DEBUG] Stage 4: Rendering phase.");
            currentPhase = 2;
            currentTargetDuration = expectedFinalDuration; // The bar now reaches the trimmed duration
            processingPercentage = 0;
            progressPhase = Loc["RenderingFinalVideo", keepSegments.Count];
            StateHasChanged();

            await ffmpeg.Run("-i", "input.mp4", "-filter_complex_script", "filter.txt", "-map", "[outv]", "-map", "[outa]", "-c:v",
    "libx264", "-preset", "ultrafast", "-c:a", "aac", "-b:a", "128k", "output.mp4");

            // --- FINAL PHASE: DOWNLOAD ---
            Console.WriteLine("[DEBUG] Stage 5: Download & Cleanup.");
            progressPhase = Loc["FinalizingDownload"];
            processingPercentage = 100;
            StateHasChanged();

            var processedVideo = await ffmpeg.ReadFile("output.mp4");

            var extension = System.IO.Path.GetExtension(selectedFile.Name);
            var nameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(selectedFile.Name);
            var newFileName = $"{nameWithoutExtension}_modified{extension}";

            var urlFinal = FFmpegFactory.CreateURLFromBuffer(processedVideo, newFileName, "video/mp4");
            await Runtime.InvokeVoidAsync("descargarVideo", newFileName, urlFinal);

            ffmpeg.UnlinkFile("input.mp4");
            ffmpeg.UnlinkFile("filter.txt");
            ffmpeg.UnlinkFile("output.mp4");

            Console.WriteLine("[DEBUG] Finished completely.");
            progressPhase = "";
            isProcessing = false;
            isDone = true;
            StateHasChanged();
        }
    }
}
