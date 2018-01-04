using System;
using System.Text;
using System.IO;
using Windows.Networking.Sockets;
using System.Diagnostics;
using Windows.Storage.Streams;
using System.Threading.Tasks;
using System.Threading;
using Windows.UI.Xaml.Media;
using Windows.UI;

namespace MJPEGStreamer
{

    sealed partial class MainPage
    {

        bool _isServerStarted = false;
        private StreamSocketListener _listener;
        private int _activeStreams = 0;

        async public Task StartServer()
        {
            if (_isServerStarted)
            {
                Debug.WriteLine("Webserver already running.");
                return;
            }
            _isServerStarted = true;
            Debug.WriteLine("Webserver is being started.");

            try
            {
                _listener = new StreamSocketListener();
                await _listener.BindServiceNameAsync(_httpServerPort.ToString());
                Debug.WriteLine("Bound to port: " + _httpServerPort.ToString());

                _listener.ConnectionReceived += receivedConnectionHandler; StreamingButton.Foreground = new SolidColorBrush(Colors.Green);
            }
            catch (Exception listenerException)
            {
                _listener.ConnectionReceived += receivedConnectionHandler; StreamingButton.Foreground = new SolidColorBrush(Colors.Red);
                Debug.WriteLine("Could not start listening on port: " + _httpServerPort.ToString() + " exception: " + listenerException.ToString());
            }


        }

        private async void receivedConnectionHandler(StreamSocketListener s, StreamSocketListenerConnectionReceivedEventArgs e)
        {
            Interlocked.Increment(ref _activeStreams);

            try
            {

                Debug.WriteLine("Got connection");

                string request;
                using (var streamReader = new StreamReader(e.Socket.InputStream.AsStreamForRead()))
                {
                    request = await streamReader.ReadLineAsync();
                    Debug.WriteLine(request);
                }

                /*using (IInputStream input = e.Socket.InputStream)
                {
                    var buffer = new Windows.Storage.Streams.Buffer(2);
                    await input.ReadAsync(buffer, buffer.Capacity, InputStreamOptions.Partial);


                    //System.Web.HttpUtility.
                    System.Uri uri;
                    //uri.
                }*/

                using (IOutputStream output = e.Socket.OutputStream)
                {
                    using (Stream response = output.AsStreamForWrite())
                    {
                        MjpegHttpStreamer mjpegHttpStreamer = new MjpegHttpStreamer(response);
                        mjpegHttpStreamer.WriteHeader();
                        Debug.WriteLine("MJPEG HTTPHeader sent. Now streaming JPEGs.");
                        try
                        {
                            int lastStreamHash = 0;
                            while (_isServerStarted)
                            {
                                int streamHash = _jpegStreamBuffer.GetHashCode();

                                if (streamHash == lastStreamHash)
                                {
                                    await Task.Delay(50);
                                    continue;
                                }
                                lastStreamHash = streamHash;
  
                                InMemoryRandomAccessStream jpegStream;
                                jpegStream = _jpegStreamBuffer;
                                if (jpegStream != null)
                                {
                                   mjpegHttpStreamer.Write(jpegStream);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("MJPEG HTTP Stream ended." + ex.ToString());
                        }

                    }
                }
            }
            catch (Exception ex2)
            {
                Debug.WriteLine("Connection closed by client: " + ex2.ToString());
            }
            Interlocked.Decrement(ref _activeStreams);
        }


        private async Task StopServer()
        {
            if (!_isServerStarted)
                return;

            Debug.WriteLine("************* STOPPING SERVER *****************");
            _listener.ConnectionReceived -= receivedConnectionHandler;
            await _listener.CancelIOAsync();
            _listener.Dispose();
            StreamingButton.Foreground = new SolidColorBrush(Colors.DarkGray);
            _isServerStarted = false;
        }
    }

}
