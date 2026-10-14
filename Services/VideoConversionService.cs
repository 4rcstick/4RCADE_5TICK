using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ArcadeStick.Models;

namespace ArcadeStick.Services
{
    // Probes a freshly scraped video with the bundled custom ffmpeg build and places it into its final
    // destination, re-encoding first if it isn't MediaElement-safe (H.264 + yuv420p video, AAC audio).
    // Probing uses ffmpeg alone (no ffprobe): running "ffmpeg -i file" with no output target prints the
    // stream details to stderr and exits non-zero by design, so the exit code is ignored for probes.
    public class VideoConversionService
    {
        // Video is encoded with x264 in CRF mode (see ConvertAsync), so only audio needs a bitrate.
        private const int AudioBitrateKbps = 128;

        private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan ConvertTimeout = TimeSpan.FromMinutes(10);

        private static readonly Regex VideoCodecRegex = new Regex(@"Video:\s*([A-Za-z0-9_]+)", RegexOptions.Compiled);
        private static readonly Regex AudioCodecRegex = new Regex(@"Audio:\s*([A-Za-z0-9_]+)", RegexOptions.Compiled);
        // Word boundary keeps yuv420p10le and yuvj420p from matching as plain yuv420p.
        private static readonly Regex Yuv420pRegex = new Regex(@"\byuv420p\b", RegexOptions.Compiled);

        private readonly ConfigurationSettings _settings;

        public VideoConversionService(ConfigurationSettings settings)
        {
            _settings = settings;
        }

        public bool IsFfmpegAvailable => File.Exists(_settings.GetFfmpegPath());

        // True when the file passes the same compatibility check the scraper uses (H.264 4:2:0 video plus AAC
        // or no audio), false when it fails it, and null when that can't be determined (ffmpeg missing, the
        // file gone, the probe timed out or threw). Callers should treat null as "unknown", not as "bad".
        // Only a cancellation requested through ct is rethrown; a probe timeout comes back as null.
        public async Task<bool?> IsPlayableAsync(string filePath, CancellationToken ct = default)
        {
            try
            {
                if (!IsFfmpegAvailable || !File.Exists(filePath)) return null;

                var probe = await ProbeAsync(filePath, ct);
                return probe.VideoFound && probe.VideoOk && probe.AudioOk;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"VideoConversion: probe failed for {filePath}: {ex.Message}");
                return null;
            }
        }

        // Takes a downloaded source file (typically in a temp location), makes sure it's compatible, and
        // leaves the final result at destinationPath. Returns true if a usable file now exists there.
        // On any failure (ffmpeg missing, probe/convert error, timeout) nothing is placed at
        // destinationPath and false is returned. The source file is always removed afterward.
        public async Task<bool> PlaceCompatibleVideoAsync(string sourcePath, string destinationPath, CancellationToken ct = default)
        {
            string tempOutputPath = destinationPath + ".converting";

            try
            {
                if (!IsFfmpegAvailable)
                {
                    Debug.WriteLine($"VideoConversion: ffmpeg not found at {_settings.GetFfmpegPath()}");
                    return false;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

                var probe = await ProbeAsync(sourcePath, ct);

                if (probe.VideoFound && probe.VideoOk && probe.AudioOk)
                {
                    File.Move(sourcePath, destinationPath, overwrite: true);
                    return true;
                }

                if (!await ConvertAsync(sourcePath, tempOutputPath, probe.VideoOk, probe.AudioOk, ct))
                {
                    return false;
                }

                File.Move(tempOutputPath, destinationPath, overwrite: true);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"VideoConversion: failed for {sourcePath}: {ex.Message}");
                return false;
            }
            finally
            {
                TryDelete(sourcePath);
                TryDelete(tempOutputPath);
            }
        }

        // Converts a video that's already in the library, in place. The result lands next to the source as
        // {name}.mp4, and the original is moved into archiveFolder only AFTER the conversion succeeds, so a
        // failure leaves the library untouched. Returns the final path, or null on failure (or if a
        // different {name}.mp4 already exists and would be overwritten by converting an .avi).
        public async Task<string?> ConvertInPlaceAsync(string sourcePath, string archiveFolder, CancellationToken ct = default)
        {
            string finalPath = Path.ChangeExtension(sourcePath, ".mp4");
            string tempOutputPath = finalPath + ".converting";

            try
            {
                if (!IsFfmpegAvailable || !File.Exists(sourcePath)) return null;

                bool samePath = string.Equals(sourcePath, finalPath, StringComparison.OrdinalIgnoreCase);
                if (!samePath && File.Exists(finalPath)) return null;

                var probe = await ProbeAsync(sourcePath, ct);
                if (!probe.VideoFound) return null;

                if (!await ConvertAsync(sourcePath, tempOutputPath, probe.VideoOk, probe.AudioOk, ct))
                {
                    return null;
                }

                Directory.CreateDirectory(archiveFolder);
                string archivePath = GetUniqueArchivePath(archiveFolder, Path.GetFileName(sourcePath));
                File.Move(sourcePath, archivePath);

                try
                {
                    File.Move(tempOutputPath, finalPath, overwrite: true);
                }
                catch
                {
                    // Couldn't place the converted file - put the original back where it was.
                    File.Move(archivePath, sourcePath);
                    throw;
                }

                return finalPath;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"VideoConversion: in-place convert failed for {sourcePath}: {ex.Message}");
                return null;
            }
            finally
            {
                TryDelete(tempOutputPath);
            }
        }

        // Avoids overwriting an earlier archived copy: "name.mp4", then "name (2).mp4", "name (3).mp4", ...
        private static string GetUniqueArchivePath(string archiveFolder, string fileName)
        {
            string path = Path.Combine(archiveFolder, fileName);
            if (!File.Exists(path)) return path;

            string name = Path.GetFileNameWithoutExtension(fileName);
            string ext = Path.GetExtension(fileName);
            int counter = 2;
            do
            {
                path = Path.Combine(archiveFolder, $"{name} ({counter}){ext}");
                counter++;
            }
            while (File.Exists(path));

            return path;
        }

        // Probes the file and reports the video and audio checks separately, so the converter can copy
        // whichever stream is already fine and re-encode only the one that isn't. Video passes when the
        // first real video stream is H.264 in 4:2:0 (yuv420p, or full-range yuvj420p). Audio passes when
        // the first audio stream is AAC, or when the file has no audio stream at all. Anything
        // unparseable counts as failing so it goes through the converter.
        private async Task<(bool VideoFound, bool VideoOk, bool AudioOk)> ProbeAsync(string filePath, CancellationToken ct)
        {
            var result = await RunFfmpegAsync(new[] { "-hide_banner", "-i", filePath }, ProbeTimeout, ct);

            bool videoOk = false;
            bool videoFound = false;
            bool audioOk = true;
            bool audioSeen = false;

            foreach (string rawLine in result.StdErr.Split('\n'))
            {
                string line = rawLine.Trim();
                if (!line.StartsWith("Stream #", StringComparison.Ordinal)) continue;

                if (!videoFound && line.Contains("Video:") && !line.Contains("attached pic"))
                {
                    videoFound = true;
                    var codecMatch = VideoCodecRegex.Match(line);
                    videoOk = codecMatch.Success
                        && codecMatch.Groups[1].Value.Equals("h264", StringComparison.OrdinalIgnoreCase)
                        && (Yuv420pRegex.IsMatch(line) || line.Contains("yuvj420p"));
                }
                else if (!audioSeen && line.Contains("Audio:"))
                {
                    // First audio stream decides; later ones are dropped by the converter's mapping anyway.
                    audioSeen = true;
                    var codecMatch = AudioCodecRegex.Match(line);
                    audioOk = codecMatch.Success && codecMatch.Groups[1].Value.Equals("aac", StringComparison.OrdinalIgnoreCase);
                }
            }

            return (videoFound, videoOk, audioOk);
        }

        // Produces an H.264 yuv420p + AAC MP4 in an MP4 container. Only the failing stream is re-encoded:
        // a video stream that already passes is copied as-is (videoOk), and likewise audio (audioOk).
        // Maps only the first video and first audio stream (audio optional) and drops subtitle/data streams.
        private async Task<bool> ConvertAsync(string inputPath, string outputPath, bool videoOk, bool audioOk, CancellationToken ct)
        {
            var argList = new System.Collections.Generic.List<string>
            {
                "-y", "-hide_banner", "-loglevel", "error",
                "-i", inputPath,
                "-map", "0:v:0", "-map", "0:a:0?",
                "-sn", "-dn"
            };

            if (videoOk) argList.AddRange(new[] { "-c:v", "copy" });
            // x264 (GPL build) in CRF mode: constant quality, so bitrate follows the content. veryslow plus
            // the animation tune measured smaller and cleaner than the old medium preset on flat-color arcade
            // footage. CRF 22 is the baseline to tweak from (lower = better/bigger). The filter keeps the
            // dimensions even and forces 8-bit 4:2:0, which is what MediaElement needs.
            else argList.AddRange(new[] { "-c:v", "libx264", "-preset", "veryslow", "-tune", "animation", "-crf", "22", "-profile:v", "high", "-vf", "scale=trunc(iw/2)*2:trunc(ih/2)*2,format=yuv420p" });

            if (audioOk) argList.AddRange(new[] { "-c:a", "copy" });
            else argList.AddRange(new[] { "-c:a", "aac", "-b:a", $"{AudioBitrateKbps}k" });

            argList.AddRange(new[] { "-movflags", "+faststart", "-f", "mp4", outputPath });
            var args = argList.ToArray();

            var result = await RunFfmpegAsync(args, ConvertTimeout, ct);

            if (result.ExitCode != 0 || !File.Exists(outputPath) || new FileInfo(outputPath).Length == 0)
            {
                Debug.WriteLine($"VideoConversion: convert failed (exit {result.ExitCode}): {result.StdErr}");
                return false;
            }

            return true;
        }

        private async Task<(int ExitCode, string StdErr)> RunFfmpegAsync(string[] arguments, TimeSpan timeout, CancellationToken ct)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _settings.GetFfmpegPath(),
                WorkingDirectory = Path.GetDirectoryName(_settings.GetFfmpegPath())!,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };
            foreach (string arg in arguments)
            {
                startInfo.ArgumentList.Add(arg);
            }

            using var process = new Process { StartInfo = startInfo };
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(timeout);

            process.Start();
            Task<string> stdErrTask = process.StandardError.ReadToEndAsync();

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                throw;
            }

            string stdErr = await stdErrTask;
            return (process.ExitCode, stdErr);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
            }
        }
    }
}