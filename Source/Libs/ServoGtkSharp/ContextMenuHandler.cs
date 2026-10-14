// Servo.ContextMenuHandler.cs - args and delegate for the "context-menu" signal
//
// Hand-written binding for the ServoGtkWebView::context-menu signal
// declared in servo-gtk3-view.h:
//     gboolean (*context_menu) (ServoGtkWebView          *web_view,
//                               GMenu                    *menu,
//                               ServoGtkContextMenuFlags  flags,
//                               const gchar              *link_uri,
//                               const gchar              *image_uri);
//
// Handlers may edit Menu in place; leaving RetVal unset (or false) shows the
// edited menu. Set RetVal to true to take over, so no menu is shown. A menu
// left empty is not shown either.

namespace Servo {

	using System;
	using System.Runtime.InteropServices;

	public delegate void ContextMenuHandler (object o, ContextMenuArgs args);

	public class ContextMenuArgs : GLib.SignalArgs {
		public GLib.Menu Menu {
			get {
				return (GLib.Menu) Args [0];
			}
		}

		public ContextMenuFlags Flags {
			get {
				// Unmapped flags GTypes come back as the raw guint.
				return (ContextMenuFlags) (uint) Args [1];
			}
		}

		// The link under the pointer, if Flags has Link; otherwise null.
		public string LinkUri {
			get {
				return (string) Args [2];
			}
		}

		// The image under the pointer, if Flags has Image; otherwise null.
		public string ImageUri {
			get {
				return (string) Args [3];
			}
		}
	}

	// Mirrors ServoGtkContextMenuFlags in servo-gtk3-view.h; values must match.
	// A link around an image sets both Link and Image.
	[Flags]
	public enum ContextMenuFlags : uint {
		None         = 0,
		Link         = 1 << 0,
		Image        = 1 << 1,
		EditableText = 1 << 2,
		Selection    = 1 << 3
	}

	public static class ContextMenu {

		// Detailed action names of the entries the engine puts in a context
		// menu, for Remove() or to put one back in another place. Each is
		// enabled only when it applies (Copy only with a selection, ...).
		public const string GoBack        = "context-menu.go-back";
		public const string GoForward     = "context-menu.go-forward";
		public const string Reload        = "context-menu.reload";
		public const string CopyLink      = "context-menu.copy-link";
		public const string CopyImageLink = "context-menu.copy-image-link";
		public const string Cut           = "context-menu.cut";
		public const string Copy          = "context-menu.copy";
		public const string Paste         = "context-menu.paste";
		public const string SelectAll     = "context-menu.select-all";

		[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
		delegate bool d_servo_gtk_context_menu_remove (IntPtr menu, IntPtr action);
		static d_servo_gtk_context_menu_remove servo_gtk_context_menu_remove = FuncLoader.LoadFunction<d_servo_gtk_context_menu_remove> (FuncLoader.GetProcAddress (GLibrary.Load (Library.Servo), "servo_gtk_context_menu_remove"));

		// Removes every entry that activates action from menu, looking into
		// its sections too; a section left empty is removed with it. Returns
		// true if anything was removed.
		public static bool Remove (GLib.Menu menu, string action)
		{
			if (menu == null)
				throw new ArgumentNullException (nameof (menu));

			IntPtr native_action = GLib.Marshaller.StringToPtrGStrdup (action);
			bool ret = servo_gtk_context_menu_remove (menu.Handle, native_action);
			GLib.Marshaller.Free (native_action);
			return ret;
		}
	}
}
