// This is free and unencumbered software released into the public domain.
// Happy coding!!! - GtkSharp Team

using System;
using System.IO;
using System.Linq;
using Gst;
using Gtk;
using DateTime = System.DateTime;

namespace Samples
{
	[Section(ContentType = typeof(AudioSection), Category = Category.Widgets)]
	class AudioSection : Box
	{
		private AudioRecoder recoder;

		public AudioSection() : base(Orientation.Vertical, 10)
		{
			CreateContainer();
		}

		void CreateContainer()
		{
			recoder = new AudioRecoder();
			PackStart(recoder, false, false, 1);
			recoder.Show();

			var dir = new DirectoryInfo ("/tmp");
			var files = dir.GetFiles ("*.wav").OrderBy(p => p.CreationTime).ToArray();
			if (files.Length > 0)
			{
				foreach (var file in files)
				{
					var recorder = new AudioPlayer(file.FullName);
					PackStart(recorder, false, false, 1);
					recorder.Show();
				}
			}

			/*button.Clicked += (sender, args) =>
			{
				if (playing)
				{
					audioRecorderPipeline.SetState(Gst.State.Paused);
					playing = false;
					label.Text = "Start";
				}
				else
				{
					if (audioRecorderPipeline == null)
					{
						CreateAudioRecorderPipeline();
					}
					audioRecorderPipeline?.SetState(Gst.State.Playing);
					playing = true;
					label.Text = "Pause";
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
					label.Text = "Start";
				}


				audioRecorderPipeline.SetState(Gst.State.Null);
				Console.WriteLine($"Recording saved to => {recordingFilePath}");
				audioRecorderPipeline.Dispose();
				audioRecorderPipeline = null;

				GC.Collect();
				GC.WaitForPendingFinalizers();
			};*/
		}
	}
}