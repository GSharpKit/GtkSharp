using System;
using System.Linq;
using System.Threading;
using System.Timers;
using Gst;
using Gst.App;
using Gtk;
using DateTime = System.DateTime;

namespace Samples
{
	public class AudioPlayer : Box
	{
		private Image image;
		private Button button;

		private Scale seekBar;

		System.Timers.Timer audioPlayerTimer;
		private Pipeline audioPlayerPipeline;
		private bool playing;
		private string recordingFilePath;

		public AudioPlayer(string filePath) : base(Orientation.Horizontal, 10)
		{
			recordingFilePath = filePath;

			CreateContainer();
		}

		void CreateContainer()
		{
			image = new Image()
			{
				IconName = "media-playback-start"
			};
			button = new Button(image);
			image.Show();
			PackStart(button, false, false, 1);
			button.Show();

			seekBar = new Scale(Orientation.Horizontal, 0, 10000, 1);
			PackStart(seekBar, true, true, 1);
			seekBar.Show();

			seekBar.ValueChanged += OnSeekBarValueChanged;

			button.Clicked += (sender, args) =>
			{
				if (playing)
				{
					audioPlayerPipeline.SetState(Gst.State.Paused);
					playing = false;
					image.IconName = "media-playback-start";
				}
				else
				{
					if (audioPlayerPipeline == null)
					{
						CreateAudioPlayerPipeline();
					}

					audioPlayerPipeline?.SetState(Gst.State.Playing);
					playing = true;
					image.IconName = "media-playback-pause";
				}

				GC.Collect();
				GC.WaitForPendingFinalizers();
			};
		}

		void CreateAudioPlayerPipeline()
		{
			//Console.WriteLine ("Creating pipeline");
			if (audioPlayerPipeline != null)
			{
				//Console.WriteLine ("Stopping old loop");
				audioPlayerPipeline.SetState(Gst.State.Null);
				audioPlayerPipeline = null;
			}

			audioPlayerPipeline = new Pipeline("audio-player");

			var source = ElementFactory.Make("filesrc", "file-source");
			var decoder = ElementFactory.Make("wavparse", "wav-decoder");
			var converter = ElementFactory.Make("audioconvert", "audio-converter");
			var sink = ElementFactory.Make("autoaudiosink", "audio-output");

			if (source == null || decoder == null || converter == null || sink == null)
			{
				Console.WriteLine("One element could not be created. Exiting.");
				return;
			}

			source.SetProperty("location", new GLib.Value(recordingFilePath));

			audioPlayerPipeline.Add(source, decoder, converter, sink);

			source.Link(decoder);
			decoder.Link(converter);
			converter.Link(sink);

			//var bus = audioPlayerPipeline.Bus;
			audioPlayerPipeline.Bus.AddWatch((Bus bus, Message msg) =>
			{
				switch (msg.Type)
				{
					case Gst.MessageType.Eos:
						//ResetToStart ();
						//AudioPlayerState = AudioPlayerStateEnum.Paused;
						break;
					case Gst.MessageType.Error: // Error
						msg.ParseError(out GLib.GException err, out string debug);
						// Console.WriteLine($"Error: {err.Message}");
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
						audioPlayerPipeline.QueryDuration(Format.Time, out long duration);
						Console.WriteLine ($"StreamStart: {duration}");
						seekBar.SetRange (0, duration);
						audioPlayerTimer.Enabled = true;
						break;
					case Gst.MessageType.NewClock:
						// Console.WriteLine ("New clock");
						break;
					default:
						Console.WriteLine ($"{msg.Type}");
						break;
				}

				return true;
			});

			Gtk.Window win = (Gtk.Window)this.Toplevel;
			int step = 1000000;
			win.KeyReleaseEvent += (o, args) =>
			{
				if (args.Event.Key == Gdk.Key.Left)
				{
					Console.WriteLine("Left click");
					seekBar.Value -= step;
					if (audioPlayerPipeline.CurrentState is Gst.State.Playing or Gst.State.Paused)
					{
						audioPlayerPipeline.SeekSimple (Format.Time, SeekFlags.Flush, (long)seekBar.Value);
					}
					return;
				}
				if (args.Event.Key == Gdk.Key.Right)
				{
					if (seekBar.Value + step > seekBar.Adjustment.Upper)
					{
						seekBar.Value = seekBar.Adjustment.Upper;
					}
					else
					{
						seekBar.Value += step;
					}
					if (audioPlayerPipeline.CurrentState is Gst.State.Playing or Gst.State.Paused)
					{
						audioPlayerPipeline.SeekSimple (Format.Time, SeekFlags.Flush, (long)seekBar.Value);
					}
					return;
				}
			};

			audioPlayerTimer = new (100);
			audioPlayerTimer.Elapsed += OnAudioPlayerTimerElapsed;
			seekBar.Sensitive = true;
		}

		private void OnAudioPlayerTimerElapsed(object sender, ElapsedEventArgs e)
		{
			Gtk.Application.Invoke(delegate
			{
				try
				{
					if (audioPlayerPipeline.CurrentState == Gst.State.Playing)
					{
						audioPlayerPipeline.QueryPosition(Format.Time, out long position);
						seekBar.Value = position;
					}
				}
				catch (Exception)
				{
					return;
				}
			});
		}

		void OnSeekBarValueChanged (object sender, EventArgs e)
		{
			try
			{
				if (audioPlayerPipeline.CurrentState is Gst.State.Playing or Gst.State.Paused)
				{
					audioPlayerPipeline.SeekSimple (Format.Time, SeekFlags.Flush, (long)seekBar.Value);
				}
			}
			catch (Exception)
			{
			}
		}
	}
}

