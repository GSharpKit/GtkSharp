using Gtk;

namespace Samples
{
	public class VideoBox : DrawingArea
	{
		public VideoBox(DrawingArea w) : base(w.Handle)
		{
		}
	}
}
