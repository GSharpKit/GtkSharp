// Servo.DecideNavigationHandler.cs - args and delegate for the "decide-navigation" signal
//
// Hand-written binding for the ServoGtkWebView::decide-navigation signal
// declared in servo-gtk3-view.h:
//     gboolean (*decide_navigation) (ServoGtkWebView *web_view, const gchar *uri);
//
// Set RetVal to true to mark the navigation as handled, which blocks Servo
// from loading it. Leaving RetVal unset (or false) lets the navigation proceed.

namespace Servo {

	using System;

	public delegate void DecideNavigationHandler (object o, DecideNavigationArgs args);

	public class DecideNavigationArgs : GLib.SignalArgs {
		public string Uri {
			get {
				return (string) Args [0];
			}
		}
	}
}
