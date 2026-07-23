using System;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using Gst;
using Gst.App;
using Gtk;
using DateTime = System.DateTime;

namespace Samples
{
    public class AudioRecoder : Box
    {
        Image recordImage;
        Button recordButton;

        Image playImage;
        Button playButton;

        Image finishImage;
        Button finishButton;

        Lock samplesLock = new ();
        byte[] latestSamples;
        DrawingArea drawingArea;

        Pipeline audioPlayerPipeline;
        Pipeline audioRecorderPipeline;

        string recordingFilePath;

        public AudioRecoder() : base(Orientation.Horizontal, 10)
        {
            CreateContainer();
        }

        void CreateContainer()
        {
            recordingFilePath = "/tmp/audio_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".wav";

            recordImage = new Image()
            {
                IconName = "media-record"
            };
            recordButton = new Button(recordImage);
            recordImage.Show();
            PackStart(recordButton, false, false, 1);
            recordButton.Show();

            playImage = new Image()
            {
                IconName = "media-playback-start"
            };
            playButton = new Button(playImage);
            playImage.Show();
            PackStart(playButton, false, false, 1);
            playButton.Show();

            drawingArea = new DrawingArea();
            PackStart(drawingArea, true, true, 1);
            drawingArea.Show();

            finishImage = new Image()
            {
                IconName = "media-playback-stop"
            };
            finishButton = new Button(finishImage);
            finishImage.Show();
            PackStart(finishButton, false, false, 1);
            finishButton.Show();

            CreateAudioRecorderPipeline();

            recordButton.Clicked += (_, _) =>
            {
                if (audioRecorderPipeline == null)
                {
                    return;
                }

                if (audioPlayerPipeline != null)
                {
                    audioPlayerPipeline.GetState(out var pstate, out var ppending, 1000);
                    switch (pstate)
                    {
                        case Gst.State.Playing:
                            audioPlayerPipeline.SetState(Gst.State.Paused);
                            break;
                        default:
                            audioPlayerPipeline.SetState(Gst.State.Playing);
                            break;
                    }
                }

                audioRecorderPipeline.GetState(out var state, out var pending, 1000);
                switch (state)
                {
                    case Gst.State.Playing:
                        audioRecorderPipeline.SetState(Gst.State.Paused);
                        break;
                    default:
                        audioRecorderPipeline.SetState(Gst.State.Playing);
                        break;
                }
            };

            playButton.Clicked += (_, _) =>
            {
                if (audioRecorderPipeline == null)
                {
                    return;
                }

                audioRecorderPipeline.GetState(out var rstate, out var rpending, 1000);
                switch (rstate)
                {
                    case Gst.State.Playing:
                        audioRecorderPipeline.SetState(Gst.State.Null);
                        break;
                }

                if (audioPlayerPipeline == null)
                    CreateAudioPlayerPipeline();

                if (audioPlayerPipeline != null)
                {
                    audioPlayerPipeline.GetState(out var pstate, out var ppending, 1000);
                    switch (pstate)
                    {
                        case Gst.State.Playing:
                            audioPlayerPipeline.SetState(Gst.State.Paused);
                            break;
                        default:
                            audioPlayerPipeline.SetState(Gst.State.Playing);
                            break;
                    }
                }
            };

            finishButton.Clicked += (_, _) =>
            {
                if (audioRecorderPipeline == null)
                {
                    return;
                }

                audioRecorderPipeline.GetState(out var state, out var pending, 1000);
                switch (state)
                {
                    case Gst.State.Playing:
                        audioRecorderPipeline.SetState(Gst.State.Paused);
                        break;
                }

                if (audioPlayerPipeline != null)
                    audioPlayerPipeline.SetState(Gst.State.Paused);

                drawingArea.Drawn -= OnDraw;

                audioRecorderPipeline.SetState(Gst.State.Null);
                Console.WriteLine($"Recording saved to => {recordingFilePath}");
                audioRecorderPipeline.Dispose();
                audioRecorderPipeline = null;
            };

            drawingArea.Drawn += OnDraw;
        }

        void CreateAudioPlayerPipeline()
        {
            Element filesrc = ElementFactory.Make("filesrc", "filesrc");
            filesrc.SetProperty("location", new GLib.Value(recordingFilePath));

            Element decodebin = ElementFactory.Make("decodebin", "decodebin");
            Element audioconvert = ElementFactory.Make("audioconvert", "audioconvert");
            Element audioresample = ElementFactory.Make("audioresample", "audioresample");
            Element audiosink = ElementFactory.Make("autoaudiosink", "audiosink");

            audioPlayerPipeline = new Pipeline("audio-player");
            if (filesrc == null || decodebin == null || audioconvert == null || audioresample == null || audiosink == null)
            {
                Console.WriteLine("Not all elements could be created.");
                return;
            }

            audioPlayerPipeline.Add(filesrc, decodebin, audioconvert, audioresample, audiosink);

            if (!filesrc.Link(decodebin))
            {
                Console.WriteLine("Elements could not be linked.");
                return;
            }

            // Connect decodebin dynamically when pad is available
            decodebin.PadAdded += (o, args) =>
            {
                var pad = args.NewPad;
                var sinkPad = audioconvert.GetStaticPad("sink");
                if (!pad.IsLinked && sinkPad != null)
                {
                    pad.Link(sinkPad);
                }
            };

            audioconvert.Link(audioresample);
            audioresample.Link(audiosink);

            var bus = audioPlayerPipeline.Bus;
            bus.AddWatch((_, msg) =>
            {
                switch (msg.Type)
                {
                    case Gst.MessageType.Eos:
                        Console.WriteLine($"End of stream");
                        break;
                    case Gst.MessageType.Error: // Error
                        msg.ParseError(out GLib.GException err, out string debug);
                        Console.WriteLine($"Error: {err.Message} | Debug: {debug}");
                        break;
                    case Gst.MessageType.StateChanged:
                        msg.ParseStateChanged(out State oldState, out State newState, out State pending);
                        switch (newState)
                        {
                            case Gst.State.Playing:
                                playImage.IconName = "media-playback-pause";
                                break;
                            default:
                                playImage.IconName = "media-playback-start";
                                break;
                        }
                        //Console.WriteLine($"StateChanged: {msg.Type}: {oldState} => {newState} ({pending})");
                        break;
                    case Gst.MessageType.StreamStatus:
                        msg.ParseStreamStatus(out StreamStatusType type, out Element owner);
                        Console.WriteLine($"StreamStatus: {type} | owner {owner}");
                        break;
                    case Gst.MessageType.StreamStart:
                        //msg.(out Format format, out long position);
                        Console.WriteLine($"StreamStart:");
                        break;
                    case Gst.MessageType.SegmentStart:
                        msg.ParseSegmentStart(out Format format, out long position);
                        Console.WriteLine($"SegmentStart: {format} | {position}");
                        break;
                    case Gst.MessageType.NewClock:
                        Console.WriteLine("New clock");
                        break;
                    case Gst.MessageType.Latency:
                        Console.WriteLine("Latency");
                        break;
                    case Gst.MessageType.Warning:
                        msg.ParseWarning(out var _, out string warn);
                        Console.WriteLine($"Warning: {warn}");
                        break;
                    default:
                        Console.WriteLine($"UNKNOWN: {msg.Type}");
                        break;
                }

                return true;
            });
        }

        void CreateAudioRecorderPipeline()
        {
            Element source = ElementFactory.Make("autoaudiosrc", "source");
            Element converter = ElementFactory.Make("audioconvert", "converter");
            Element resampler = ElementFactory.Make("audioresample", "resampler");
            Element tee = ElementFactory.Make("tee", "tee");

            Caps caps = Caps.FromString("audio/x-raw,format=S16LE,channels=1,rate=16000");
            Element capsfilter = ElementFactory.Make("capsfilter", "capsfilter");
            capsfilter["caps"] = caps;

            var q1 = ElementFactory.Make("queue", "q1");
            Element wavenc = ElementFactory.Make("wavenc", "encoder");

            var q2 = ElementFactory.Make("queue", "q2");
            var appsink = new AppSink("appsink");

            appsink.EmitSignals = true;
            appsink.Sync = false;
            appsink.NewSample += OnNewSample;

            Element filesink = ElementFactory.Make("filesink", "filesink");
            filesink.SetProperty("location", new GLib.Value(recordingFilePath));

            audioRecorderPipeline = new Pipeline("audio-recorder");
            if (source == null || converter == null || resampler == null || wavenc == null || audioRecorderPipeline == null)
            {
                Console.WriteLine("Not all elements could be created.");
                return;
            }

            audioRecorderPipeline.Add(source, converter, resampler, capsfilter, wavenc, tee, q1, filesink, q2, appsink);
            if (!source.Link(converter) || !converter.Link(resampler) || !resampler.Link(capsfilter) || !capsfilter.Link(tee))
            {
                Console.WriteLine("Elements could not be linked.");
                return;
            }

            var teePad1 = tee.GetRequestPad("src_%u");
            teePad1.Link(q1.GetStaticPad("sink"));
            q1.Link(wavenc);
            wavenc.Link(filesink);

            // Link Appsink branch
            var teePad2 = tee.GetRequestPad("src_%u");
            teePad2.Link(q2.GetStaticPad("sink"));
            q2.Link(appsink);

            var bus = audioRecorderPipeline.Bus;
            bus.AddWatch((_, msg) =>
            {
                switch (msg.Type)
                {
                    case Gst.MessageType.Eos:
                        Console.WriteLine($"End of stream");
                        break;
                    case Gst.MessageType.Error: // Error
                        msg.ParseError(out GLib.GException err, out string debug);
                        Console.WriteLine($"Error: {err.Message} | Debug: {debug}");
                        break;
                    case Gst.MessageType.StateChanged:
                        msg.ParseStateChanged(out State oldState, out State newState, out State pending);
                        switch (newState)
                        {
                            case Gst.State.Playing:
                                recordImage.IconName = "media-playback-pause";
                                break;
                            default:
                                recordImage.IconName = "media-record";
                                break;
                        }
                        //Console.WriteLine($"StateChanged: {msg.Type}: {oldState} => {newState} ({pending})");
                        break;
                    case Gst.MessageType.StreamStatus:
                        msg.ParseStreamStatus(out StreamStatusType type, out Element owner);
                        Console.WriteLine($"StreamStatus: {type} | owner {owner}");
                        break;
                    case Gst.MessageType.StreamStart:
                        //msg.(out Format format, out long position);
                        Console.WriteLine($"StreamStart:");
                        break;
                    case Gst.MessageType.SegmentStart:
                        msg.ParseSegmentStart(out Format format, out long position);
                        Console.WriteLine($"SegmentStart: {format} | {position}");
                        break;
                    case Gst.MessageType.NewClock:
                        Console.WriteLine("New clock");
                        break;
                    case Gst.MessageType.Latency:
                        Console.WriteLine("Latency");
                        break;
                    case Gst.MessageType.Warning:
                        msg.ParseWarning(out var _, out string warn);
                        Console.WriteLine($"Warning: {warn}");
                        break;
                    default:
                        Console.WriteLine($"UNKNOWN: {msg.Type}");
                        break;
                }

                return true;
            });
        }

        float[] ConvertS16LEToFloat(byte[] buffer)
        {
            int sampleCount = buffer.Length / 2;
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                short pcm = BitConverter.ToInt16(buffer, i * 2);
                samples[i] = pcm / 32768f;
            }

            return samples;
        }

        Queue<double> recentSamples = new ();
        int maxSize = 1024; // antal samples du vil gemme

        void AddSample(double sample)
        {
            recentSamples.Enqueue(sample);

            if (recentSamples.Count > maxSize)
            {
                recentSamples.Dequeue(); // fjern ældste
            }
        }

        private void OnDraw(object o, DrawnArgs args)
        {
            if (latestSamples is not { Length: > 0 }) return;

            Cairo.Context cr = args.Cr;

            cr.SetSourceRGB(1, 1, 1); // white
            cr.Paint(); // black background

            cr.SetSourceRGB(0, 1, 0); // green waveform

            float[] samplesCopy;
            lock (samplesLock)
            {
                samplesCopy = ConvertS16LEToFloat (latestSamples);
            }

            if (samplesCopy.Length == 0) return;

            double width = drawingArea.AllocatedWidth;
            maxSize = (int)width;

            double height = drawingArea.AllocatedHeight;
            double midY = height / 2.0;

            cr.SetSourceRGB(0, 0.7, 0); // green waveform
            cr.LineWidth = 2.0;

            double rms = Math.Sqrt(samplesCopy.Select(x => x * x).Average());

            // Converter til "pseudo dB"
            double db = 20 * Math.Log10(rms);

            // Map til 1–10 (justér -100 og 0 efter behov)
            double normalized = (db + 100) / 10;

            AddSample(normalized);

            //Console.WriteLine($"Y: {midY}, RMS: {rms:F6}, dB: {db:F2}, Normalized: {normalized:F2}");

            int i = -1;
            int s = 3;
            foreach (var sample in recentSamples)
            {
                i++;
                if (i % s != 0) continue;
                cr.MoveTo(i, midY - sample);
                cr.LineTo(i, midY + sample);
                cr.Stroke();
            }

            ((IDisposable)cr.GetTarget()).Dispose();
            ((IDisposable)cr).Dispose();
        }

        private void OnNewSample(object o, NewSampleArgs args)
        {
            if (o is AppSink appsink)
            {
                Sample sample = appsink.PullSample();
                if (sample == null)
                {
                    args.RetVal = FlowReturn.Error;
                    return;
                }

                sample.Buffer.Map(out var map, MapFlags.Read);
                if (map.Size <= 0)
                {
                    args.RetVal = FlowReturn.Error;
                    return;
                }

                int sampleCount = (int)map.Size / sizeof(float);
                byte[] samples = new byte[sampleCount];
                System.Runtime.InteropServices.Marshal.Copy(map.DataPtr, samples, 0, sampleCount);
                sample.Buffer.Unmap(map);

                lock (samplesLock)
                {
                    latestSamples = samples;
                }

                Gtk.Application.Invoke((_, _) => { drawingArea.QueueDraw(); });

                sample.Dispose();
            }

            args.RetVal = FlowReturn.Ok;
        }
    }
}