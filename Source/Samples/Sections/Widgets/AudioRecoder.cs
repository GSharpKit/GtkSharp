using System;
using System.IO;
using System.Linq;
using System.Threading;
using Gst;
using Gst.App;
using Gtk;
using DateTime = System.DateTime;

namespace Samples
{
    public class AudioRecoder : Box
    {
        private Image image;
        private Button button;

        private Image cimage;
        private Button cbutton;

        Lock samplesLock = new Lock();
        private float[] latestSamples;
        DrawingArea drawingArea;

        private Pipeline audioRecorderPipeline;
        private bool playing;
        private string recordingFilePath;

        public AudioRecoder() : base(Orientation.Horizontal, 10)
        {
            CreateContainer();
        }

        void CreateContainer()
        {
	        image = new Image()
	        {
		        IconName = "media-record"
	        };
            button = new Button(image);
            image.Show();
            PackStart(button, false, false, 1);
            button.Show();

            drawingArea = new DrawingArea();
            PackStart(drawingArea, true, true, 1);
            drawingArea.Show();

            cimage = new Image()
            {
	            IconName = "media-playback-stop"
            };
            cbutton = new Button(cimage);
            cimage.Show();
            PackStart(cbutton, false, false, 1);
            cbutton.Show();

            button.Clicked += (sender, args) =>
            {
                if (playing)
				{
					audioRecorderPipeline.SetState(Gst.State.Paused);
					playing = false;
					image.IconName = "media-record";
				}
				else
				{
					if (audioRecorderPipeline == null)
					{
						CreateAudioRecorderPipeline();
					}
					audioRecorderPipeline?.SetState(Gst.State.Playing);
					playing = true;
					image.IconName = "media-playback-pause";
				}
            };

            cbutton.Clicked += (sender, args) =>
            {
	            if (audioRecorderPipeline == null)
	            {
		            return;
	            }

	            if (playing)
	            {
		            audioRecorderPipeline.SetState(Gst.State.Paused);
		            playing = false;
	            }

	            drawingArea.Drawn -= OnDraw;

	            audioRecorderPipeline.SetState(Gst.State.Null);
	            Console.WriteLine($"Recording saved to => {recordingFilePath}");
	            audioRecorderPipeline.Dispose();
	            audioRecorderPipeline = null;

	            GC.Collect();
	            GC.WaitForPendingFinalizers();
            };

            drawingArea.Drawn += OnDraw;
        }

        void CreateAudioRecorderPipeline()
		{
			recordingFilePath = "/tmp/audio_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".wav";
			Console.WriteLine($"Created pipeline => {recordingFilePath}");

			Element source = ElementFactory.Make("autoaudiosrc", "source");
			Element converter = ElementFactory.Make("audioconvert", "converter");
			Element resampler = ElementFactory.Make("audioresample", "resampler");
			Element tee   = ElementFactory.Make("tee", "tee");

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
						msg.ParseStateChanged (out State oldState,out State newState,out State pending);
						Console.WriteLine ($"StateChanged: {msg.Type}: {oldState} => {newState} ({pending})");
						break;
					case Gst.MessageType.StreamStatus:
						msg.ParseStreamStatus (out StreamStatusType type, out Element owner);
						Console.WriteLine ($"StreamStatus: {type} | owner {owner}");
						break;
					case Gst.MessageType.StreamStart:
						//msg.(out Format format, out long position);
						Console.WriteLine ($"StreamStart:");
						break;
					case Gst.MessageType.SegmentStart:
						msg.ParseSegmentStart(out Format format, out long position);
						Console.WriteLine ($"SegmentStart: {format} | {position}");
						break;
					case Gst.MessageType.NewClock:
						Console.WriteLine ("New clock");
						break;
					case Gst.MessageType.Latency:
						Console.WriteLine ("Latency");
						break;
					case Gst.MessageType.Warning:
						msg.ParseWarning(out IntPtr gerror, out string warn);
						Console.WriteLine ($"Warning: {warn}");
						break;
					default:
						Console.WriteLine ($"UNKNOWN: {msg.Type}");
						break;
				}
				return true;
			});
		}

		private double idx = 0;
		float[] samplesBuffer = new float[1024];
		private void OnDraw(object o, DrawnArgs args)
		{
			if (latestSamples == null || latestSamples.Length <= 0) return;

			Cairo.Context cr = args.Cr;

			cr.SetSourceRGB(1, 1, 1); // white
			cr.Paint(); // black background

			cr.SetSourceRGB(0, 1, 0); // green waveform

			float[] samplesCopy;
			lock (samplesLock)
			{
				samplesCopy = (float[])latestSamples.Clone();
			}

			if (samplesCopy.Length == 0) return;

			double width  = drawingArea.AllocatedWidth;
			double height = drawingArea.AllocatedHeight;
			double midY   = height / 2.0;
			double scaleY = midY * 0.9;

			idx++;
			if (idx >= width)
				idx = 0;

			samplesBuffer[(int)idx] = samplesCopy.Max();

			cr.SetSourceRGB(0, 0.7, 0); // green waveform
			cr.LineWidth = 1.0;

			/*for (int i = 1; i < width; i++)
			{
				int index = (int)(i * samplesCopy.Length / width);
				if (index >= samplesCopy.Length)
					index = samplesCopy.Length - 1;

				double y1 = midY - samplesCopy[index] * scaleY;
				double y2 = midY + samplesCopy[index] * scaleY;

				cr.MoveTo (i, y1);
				cr.LineTo(i, y2);

				cr.Stroke();
			}*/

			/*for (var b = 0; b < samplesBuffer.Length; b++)
			{
				Console.WriteLine(samplesBuffer[b]);
				double val = (double)samplesBuffer[b];
				cr.MoveTo(b+1, midY - val);
				cr.LineTo(b+1, midY + val);

				cr.Stroke();
			}*/

			double val = (double)samplesBuffer[(int)idx];
			if (val > 0)
			{
				Console.WriteLine($"{val} - {(double)1.5}");
				val *= 1^39;
			}
			else
				val = 1.5;

			cr.MoveTo(idx, midY - val);
			cr.LineTo(idx, midY + val);

			cr.Stroke();

			((IDisposable) cr.GetTarget()).Dispose ();
			((IDisposable) cr).Dispose ();
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
				float[] samples = new float[sampleCount];
				System.Runtime.InteropServices.Marshal.Copy(map.DataPtr, samples, 0, sampleCount);
				sample.Buffer.Unmap(map);

				lock (samplesLock)
				{
					latestSamples = samples;
				}

				Gtk.Application.Invoke((s, e) =>
				{
					drawingArea.QueueDraw();
				});

				sample.Dispose();
			}
			args.RetVal = FlowReturn.Ok;
		}
    }
}

