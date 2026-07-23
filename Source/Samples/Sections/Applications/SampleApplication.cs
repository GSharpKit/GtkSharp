using Gtk;
using GLib;
using Application = Gtk.Application;

namespace Samples
{
    [Section(ContentType = typeof(SampleApplication), Category = Category.Applications)]
    class SampleApplicationSection : ListSection
    {
        public SampleApplicationSection()
        {
            AddItem("Press button to start application:", new SampleApplicationDemo("Press me"));
        }
    }

    class SampleApplicationDemo : Button
    {
        public SampleApplicationDemo(string text) : base(text)
        {
        }

        protected override void OnPressed()
        {
            base.OnPressed();

            var app = new SampleApplication();
            app.Activate();
        }
    }

    class SampleApplication : Application
    {
        SampleApplicationWindow window;

        public SampleApplication() : base("org.gtk.samples.SampleApplication", GLib.ApplicationFlags.None)
        {
            window = new SampleApplicationWindow(this);

            AddWindow(window);
            window.Activate();

            Run();
        }
    }

    class SampleApplicationWindow : ApplicationWindow
    {
        public SampleApplicationWindow(Application app) : base(app)
        {
        }

        protected override void OnActivate()
        {
            var win = new Window("Sample Application");
            win.SetDefaultSize(400, 300);
            win.ShowAll();
        }
    }
}