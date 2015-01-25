# mappics
A C# WPF application creating an always on top overlay window, which shows the embedded gps position of an image that has been opened in irfan viewer.

Has been designed to work with IrfanView image viewer http://www.irfanview.com/

Uses code from http://hintdesk.com/c-get-all-files-being-accessed-by-a-process-in-64-bits/ to get all open file handles of an IrfanView process.

Uses exif-utils from https://code.google.com/p/exif-utils/ to read out the GPS data of images.

Uses Static Maps API from gmaps to get map pictures https://developers.google.com/maps/documentation/staticmaps/.

Created with Microsoft Visual Studio 2012 Ultimate.

Usage after Compilation:
1. Make sure the *.exe and the ExifUtils.dll are in the same directory

2. Double click the *.exe

3. Open up an image in IrfanView, that contains GPS information

4. Close via click on cross on the upper right

The controls will disappear if you used them once.

Further usage:
- move the overlay via the move icon on the upper left
- adjust size of overlay via the + and - symbols in the upper middle
- adjust zoom level in the map via the magnifier icons in the middle

It has only been tested on Windows 8.1 with IrfanView 4.36 and JPEG images from only one camera.
