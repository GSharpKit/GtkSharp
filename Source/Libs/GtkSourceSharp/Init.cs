using System.Runtime.InteropServices;

namespace GtkSource
{
    using System;
    public static class Functions
    {
        [UnmanagedFunctionPointer (CallingConvention.Cdecl)]
        delegate IntPtr d_gtk_source_init();
        static d_gtk_source_init gtk_source_init = FuncLoader.LoadFunction<d_gtk_source_init>(FuncLoader.GetProcAddress(GLibrary.Load(Library.GtkSource), "gtk_source_init"));

        [UnmanagedFunctionPointer (CallingConvention.Cdecl)]
        delegate IntPtr d_gtk_source_finalize();
        static d_gtk_source_finalize gtk_source_finalize = FuncLoader.LoadFunction<d_gtk_source_finalize>(FuncLoader.GetProcAddress(GLibrary.Load(Library.GtkSource), "gtk_source_finalize"));

        public static void Init()
        {
            gtk_source_init();
        }

        public static void Finalize()
        {
            gtk_source_finalize();
        }
    }
}