#!/usr/bin/bash

sn -R BuildOutput/Release/AtkSharp.dll Source/GtkSharp.snk
sudo gacutil -i BuildOutput/Release/AtkSharp.dll -package GtkSharp-3.0 -root /usr/lib -gacdir mono/gac

sn -R BuildOutput/Release/CairoSharp.dll Source/GtkSharp.snk
sudo gacutil -i BuildOutput/Release/CairoSharp.dll -package GtkSharp-3.0 -root /usr/lib -gacdir mono/gac

sn -R BuildOutput/Release/GdkSharp.dll Source/GtkSharp.snk
sudo gacutil -i BuildOutput/Release/GdkSharp.dll -package GtkSharp-3.0 -root /usr/lib -gacdir mono/gac

sn -R BuildOutput/Release/GioSharp.dll Source/GtkSharp.snk
sudo gacutil -i BuildOutput/Release/GioSharp.dll -package GtkSharp-3.0 -root /usr/lib -gacdir mono/gac

sn -R BuildOutput/Release/GLibSharp.dll Source/GtkSharp.snk
sudo gacutil -i BuildOutput/Release/GLibSharp.dll -package GtkSharp-3.0 -root /usr/lib -gacdir mono/gac

sn -R BuildOutput/Release/GtkSharp.dll Source/GtkSharp.snk
sudo gacutil -i BuildOutput/Release/GtkSharp.dll -package GtkSharp-3.0 -root /usr/lib -gacdir mono/gac

sn -R BuildOutput/Release/PangoSharp.dll Source/GtkSharp.snk
sudo gacutil -i BuildOutput/Release/PangoSharp.dll -package GtkSharp-3.0 -root /usr/lib -gacdir mono/gac

