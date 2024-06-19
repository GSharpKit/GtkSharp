// Copyright (C) 2013  Stephan Sundermann <stephansundermann@gmail.com>
// 
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 2.1 of the License, or (at your option) any later version.
//
// This library is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public
// License along with this library; if not, write to the Free Software
// Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA
// 02110-1301  USA

namespace Gst.Base	 {
	using System;
	using System.Runtime.InteropServices;

	public partial class Adapter 
	{
		[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
		delegate IntPtr d_gst_adapter_copy(IntPtr raw, out IntPtr dest, int offset, int size);
		static d_gst_adapter_copy gst_adapter_copy = FuncLoader.LoadFunction<d_gst_adapter_copy>(FuncLoader.GetProcAddress(GLibrary.Load(Library.GstBase), "gst_adapter_copy"));

		public byte[] Copy(int offset, int size) {

			IntPtr mem = Marshal.AllocHGlobal (size);

			gst_adapter_copy(Handle, out mem, offset, size);

			byte[] bytes = new byte[size];
			Marshal.Copy (mem, bytes, 0, size);

			return bytes;
		}

		[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
		delegate IntPtr d_gst_adapter_map(IntPtr raw, out int size);
		static d_gst_adapter_map gst_adapter_map = FuncLoader.LoadFunction<d_gst_adapter_map>(FuncLoader.GetProcAddress(GLibrary.Load(Library.GstBase), "gst_adapter_map"));

		public byte[] Map() {
			int size;

			IntPtr mem = gst_adapter_map (Handle, out size);
			byte[] ret = new byte[size];
			Marshal.Copy (mem, ret , 0, size);

			return ret;
		}
	}
}
