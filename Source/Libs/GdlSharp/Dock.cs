namespace Gdl
{
    using System;
    using System.Runtime.InteropServices;

    public partial class Dock : Gdl.DockObject
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate IntPtr d_g_object_ref(IntPtr handle);
        static d_g_object_ref g_object_ref = FuncLoader.LoadFunction<d_g_object_ref>(FuncLoader.GetProcAddress(GLibrary.Load(Library.GObject), "g_object_ref"));

        public Dock(bool ignored) : base(IntPtr.Zero)
        {
            if (GetType() != typeof(Dock))
            {
                CreateNativeObject(Array.Empty<string>(), Array.Empty<GLib.Value>());
                return;
            }

            Raw = gdl_dock_new();
            g_object_ref(Raw);
        }
    }
}
