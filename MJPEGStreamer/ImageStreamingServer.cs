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
using System.Web;
using System.Collections.Specialized;

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
            bool serveSingleJpegOnly = false;
            bool serveMJpegStream = false;

            try
            {

                Debug.WriteLine("Got connection");

                string request;
                using (var streamReader = new StreamReader(e.Socket.InputStream.AsStreamForRead()))
                {
                    request = await streamReader.ReadLineAsync();
                    Debug.WriteLine(request);
                }

                if (!request.StartsWith("GET ", StringComparison.OrdinalIgnoreCase))
                {
                    throw new Exception("No HTTP GET request.");
                }

                int httpPos = request.LastIndexOf(" HTTP/");
                if (httpPos < 5)
                {
                    throw new Exception("No valid HTTP Requst.");
                }

                string requestUri = request.Substring(4, httpPos - 4);
                Debug.WriteLine("relative uri: '" + requestUri + "'");
                Uri uri = new Uri("http://host" + requestUri, UriKind.Absolute);
                NameValueCollection query = HttpUtility.ParseQueryString(uri.Query);

                Debug.WriteLine("Path: {0} Query: {1}", uri.LocalPath, uri.Query);

                string framerateOption = query["framerate"];
                if (framerateOption != null)
                {
                    UInt16 fr;
                    if (UInt16.TryParse(framerateOption, out fr))
                    {
                        CalculateAndSetFrameRate(fr);
                    }

                }

                if (uri.LocalPath.Contains("image.jpg"))
                {
                    serveSingleJpegOnly = true;
                    Debug.WriteLine("Single image requested.");
                }

                if (uri.LocalPath.Equals("/") || uri.LocalPath.Equals("/stream.mjpeg"))
                {
                    serveMJpegStream = true;
                    Debug.WriteLine("MJPEG requested.");
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

                        if (serveSingleJpegOnly)
                        {
                            mjpegHttpStreamer.WriteJpegHeader();
                            Debug.WriteLine("JPEG HTTPHeader. Now sending single JPEG.");
                            try
                            {
                                    InMemoryRandomAccessStream jpegStream;
                                    jpegStream = _jpegStreamBuffer;
                                    if (jpegStream != null)
                                    {
                                        mjpegHttpStreamer.WriteJpeg(jpegStream);
                                    }
                                
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine("JPEG HTTP sending aborted." + ex.ToString());
                            }
                        }
                        else if(serveMJpegStream) {
                            mjpegHttpStreamer.WriteMJpegHeader();
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
                                        mjpegHttpStreamer.WriteMJpeg(jpegStream);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine("MJPEG HTTP Stream ended." + ex.ToString());
                            }

                        } else
                        {
                            mjpegHttpStreamer.WriteErrorHeader();
                        }



                    }
                }
            }
            catch (Exception ex2)
            {
                Debug.WriteLine("Connection closed by client: " + ex2.ToString());
            }
            e.Socket.Dispose();
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
