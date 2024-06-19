// This is free and unencumbered software released into the public domain.
// Happy coding!!! - GtkSharp Team

using System;
using Gst;
using Gtk;

namespace Samples
{
    [Section(ContentType = typeof(VideoSection), Category = Category.Widgets)]
    class VideoSection : Box
    {
        private Label label;
        private Button button;

        private Element playbin;

        private bool playing;

        public VideoSection() : base(Orientation.Vertical, 10)
        {
            CreateContainer();
        }

        void CreateContainer()
        {
            //playbin = Parse.Launch ("playbin uri=http://download.blender.org/durian/trailer/sintel_trailer-1080p.mp4");
            playbin = ElementFactory.Make ("playbin", "bin");
            playbin["uri"] = "http://download.blender.org/durian/trailer/sintel_trailer-1080p.mp4";
            //playbin["uri"] = "file:///tmp/sintel_trailer-1080p.mp4";

            var gtksink = ElementFactory.Make("gtksink");
            playbin["video-sink"] = gtksink;

            playbin.SetState(Gst.State.Ready);

            label = new Label("Start");
            button = new Button(label);

            PackStart(button, false, false, 1);
            button.Show();

            var video = (Widget)gtksink["widget"];
            PackStart(video, true, true, 0);
            video.Show();

            button.Clicked += (sender, args) =>
            {
                if (playing)
                {
                    playbin.SetState(Gst.State.Paused);
                    playing = false;
                    label.Text = "Start";
                }
                else
                {
                    playbin.SetState(Gst.State.Playing);
                    playing = true;
                    label.Text = "Pause";
                }
            };
        }
    }
}
