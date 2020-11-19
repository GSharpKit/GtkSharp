#!/usr/bin/bash

sn -R BuildOutput/Release/AtkSharp.dll Source/GtkSharp.snk
sudo gacutil -i BuildOutput/Release/AtkSharp.dll -package GtkSharp-3.0 -root /usr/lib -gacdir mono/gac
sudo cp BuildOutput/Release/AtkSharp.pdb /usr/lib/mono/gac/AtkSharp/3.22.24.30__313398f2149d753f/
sudo ln -sf ../gac/AtkSharp/3.22.24.30__313398f2149d753f/AtkSharp.pdb /usr/lib/mono/GtkSharp-3.0/AtkSharp.pdb
sudo cp BuildOutput/Release/AtkSharp.dll /usr/x86_64-w64-mingw32/sys-root/mingw/bin/

sn -R BuildOutput/Release/CairoSharp.dll Source/GtkSharp.snk
sudo gacutil -i BuildOutput/Release/CairoSharp.dll -package GtkSharp-3.0 -root /usr/lib -gacdir mono/gac
sudo cp BuildOutput/Release/CairoSharp.pdb /usr/lib/mono/gac/CairoSharp/3.22.24.30__313398f2149d753f/
sudo ln -sf ../gac/CairoSharp/3.22.24.30__313398f2149d753f/CairoSharp.pdb /usr/lib/mono/GtkSharp-3.0/CairoSharp.pdb
sudo cp BuildOutput/Release/CairoSharp.dll /usr/x86_64-w64-mingw32/sys-root/mingw/bin/

sn -R BuildOutput/Release/GdkSharp.dll Source/GtkSharp.snk
sudo gacutil -i BuildOutput/Release/GdkSharp.dll -package GtkSharp-3.0 -root /usr/lib -gacdir mono/gac
sudo cp BuildOutput/Release/GdkSharp.pdb /usr/lib/mono/gac/GdkSharp/3.22.24.30__313398f2149d753f/
sudo ln -sf ../gac/GdkSharp/3.22.24.30__313398f2149d753f/GdkSharp.pdb /usr/lib/mono/GtkSharp-3.0/GdkSharp.pdb
sudo cp BuildOutput/Release/GdkSharp.dll /usr/x86_64-w64-mingw32/sys-root/mingw/bin/

sn -R BuildOutput/Release/GioSharp.dll Source/GtkSharp.snk
sudo gacutil -i BuildOutput/Release/GioSharp.dll -package GtkSharp-3.0 -root /usr/lib -gacdir mono/gac
sudo cp BuildOutput/Release/GioSharp.pdb /usr/lib/mono/gac/GioSharp/3.22.24.30__313398f2149d753f/
sudo ln -sf ../gac/GioSharp/3.22.24.30__313398f2149d753f/GioSharp.pdb /usr/lib/mono/GtkSharp-3.0/GioSharp.pdb
sudo cp BuildOutput/Release/GioSharp.dll /usr/x86_64-w64-mingw32/sys-root/mingw/bin/

sn -R BuildOutput/Release/GLibSharp.dll Source/GtkSharp.snk
sudo gacutil -i BuildOutput/Release/GLibSharp.dll -package GtkSharp-3.0 -root /usr/lib -gacdir mono/gac
sudo cp BuildOutput/Release/GLibSharp.pdb /usr/lib/mono/gac/GLibSharp/3.22.24.30__313398f2149d753f/
sudo ln -sf ../gac/GLibSharp/3.22.24.30__313398f2149d753f/GLibSharp.pdb /usr/lib/mono/GtkSharp-3.0/GLibSharp.pdb
sudo cp BuildOutput/Release/GLibSharp.dll /usr/x86_64-w64-mingw32/sys-root/mingw/bin/

sn -R BuildOutput/Release/GtkSharp.dll Source/GtkSharp.snk
sudo gacutil -i BuildOutput/Release/GtkSharp.dll -package GtkSharp-3.0 -root /usr/lib -gacdir mono/gac
sudo cp BuildOutput/Release/GtkSharp.pdb /usr/lib/mono/gac/GtkSharp/3.22.24.30__313398f2149d753f/
sudo ln -sf ../gac/GtkSharp/3.22.24.30__313398f2149d753f/GtkSharp.pdb /usr/lib/mono/GtkSharp-3.0/GtkSharp.pdb
sudo cp BuildOutput/Release/GtkSharp.dll /usr/x86_64-w64-mingw32/sys-root/mingw/bin/

sn -R BuildOutput/Release/PangoSharp.dll Source/GtkSharp.snk
sudo gacutil -i BuildOutput/Release/PangoSharp.dll -package GtkSharp-3.0 -root /usr/lib -gacdir mono/gac
sudo cp BuildOutput/Release/PangoSharp.pdb /usr/lib/mono/gac/PangoSharp/3.22.24.30__313398f2149d753f/
sudo ln -sf ../gac/PangoSharp/3.22.24.30__313398f2149d753f/PangoSharp.pdb /usr/lib/mono/GtkSharp-3.0/PangoSharp.pdb
sudo cp BuildOutput/Release/PangoSharp.dll /usr/x86_64-w64-mingw32/sys-root/mingw/bin/
