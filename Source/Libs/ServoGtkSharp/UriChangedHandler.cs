// Servo.UriChangedHandler.cs - args and delegate for the "uri-changed" signal
//
// Hand-written binding for the ServoGtkWebView::uri-changed signal
// declared in servo-gtk3-view.h:
//     void (*uri_changed) (ServoGtkWebView *web_view, const gchar *uri);

namespace Servo {

	using System;

	public delegate void UriChangedHandler (object o, UriChangedArgs args);

	public class UriChangedArgs : GLib.SignalArgs {
		public string Uri {
			get {
				return (string) Args [0];
			}
		}
	}
}
