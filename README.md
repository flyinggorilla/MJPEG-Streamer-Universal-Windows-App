# MJPEG-Streamer-Universal-Windows-App
Streams USB and bulit-in camera video as MJPEG data over HTTP. 
It is not a windows service and thus does not (yet) automatically launch as a background service.

![screenshot](screenshot.png)

Note:
* App must not be minimized and run in foreground - otherwise Windows suspends the app streaming
* Tablet mode my cause the app to suspend too

Warning:
* This app uses simple HTTP which is insecure. Also there is no authentication in place at the moment. 
So use only on your local intranet and not as a service to public internet users.

Todo:
* Make it work with Windows 10 background tasks - to reduce the suspension incidents

