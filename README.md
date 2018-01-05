# MJPEG-Streamer-Universal-Windows-App
Streams USB and bulit-in camera video as MJPEG data over HTTP. JPG retrieval of single images is implemented too.

Developed as Windows 10 Universal App in C#. 

![screenshot](screenshot.png)

### Note:
* App is now also able to run in background, at the cost of needing to use ExtendedExecutionForeground credentials.
* It is not a windows service and thus does not (yet) automatically launch as a background service at startup.

### Warning:
* This app uses simple HTTP which is insecure. Also there is no authentication in place at the moment. 
So use only on your local intranet and not as a service to public internet users.

## Usage:

### remote configuration 
Remote configuration of a running MJPEG Streamer app. impacts all streams.
```http://<remotehost>:8000/configure?quality=30&framerate=5```

* quality: 0..100, 0 is max compression, 100 is largest JPEG
* framerate: 1..30 per second

### MJPEG stream
The MPEG stream over HTTP. It is streamed in the same format as the Linux mjpg-streamer to be compatible with common webcam stream receivers.
```http://<remotehost>:8000/stream.mjpeg```

### JPEG image
Retrieves a single JPEG image. This should work in any browser.
```http://<remotehost>:8000/image.jpg```




