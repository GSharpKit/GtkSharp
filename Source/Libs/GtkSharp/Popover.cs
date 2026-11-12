//
// Gtk.Popover.cs - Constructors that keep the popover's reference count balanced
//
// This program is free software; you can redistribute it and/or
// modify it under the terms of version 2 of the Lesser GNU General
// Public License as published by the Free Software Foundation.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public
// License along with this program; if not, write to the
// Free Software Foundation, Inc., 59 Temple Place - Suite 330,
// Boston, MA 02111-1307, USA.
//

namespace Gtk {

	using System;
	using System.Runtime.InteropServices;

	public partial class Popover {

		// gtk_popover_new (relative_to) sinks the floating popover into relative_to's popover table, so the
		// pointer it returns carries only the reference relative_to owns. Raw would claim that same reference
		// for the toggle ref, leaving the popover one reference short: disposing it then finalizes it while
		// relative_to still holds it, and its dispose unrefs it again (GLib-GObject-CRITICAL).
		// Create the popover without relative_to, so Raw sinks the floating reference as our own, and attach
		// relative_to afterwards so it takes a reference of its own.
		[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
		delegate IntPtr d_gtk_popover_new(IntPtr relative_to);
		static d_gtk_popover_new gtk_popover_new = FuncLoader.LoadFunction<d_gtk_popover_new>(FuncLoader.GetProcAddress(GLibrary.Load(Library.Gtk), "gtk_popover_new"));

		public Popover (Gtk.Widget relative_to) : base (IntPtr.Zero)
		{
			if (GetType () != typeof (Popover))
				CreateNativeObject (new string [0], new GLib.Value [0]);
			else
				Raw = gtk_popover_new (IntPtr.Zero);

			if (relative_to != null)
				RelativeTo = relative_to;
		}

		// gtk_popover_new_from_model is gtk_popover_new followed by gtk_popover_bind_model (model, NULL).
		public Popover (Gtk.Widget relative_to, GLib.MenuModel model) : this (relative_to)
		{
			if (model == null)
				throw new ArgumentNullException (nameof (model));

			BindModel (model, null);
		}
	}
}
