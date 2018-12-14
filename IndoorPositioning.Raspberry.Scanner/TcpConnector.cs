﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using Windows.Networking.Connectivity;

namespace IndoorPositioning.Raspberry.Scanner
{
    public delegate void DataReceivedEventHandler(string data);

    public sealed class TcpConnector// : IDisposable
    {
        /* Port of the server to be connected */
        private const int PORT = 8088;
        private const int READ_BUFFER_SIZE = 8192;

        /* TcpClient for the client */
        private TcpClient tcpClient;

        /* Connection details of the client */
        public string LocalIpAddress { get; private set; }
        public string ServerIpAddress { get; private set; }
        public int Port { get { return PORT; } }
        public bool IsReceiving { get; private set; } = false;

        /* Gives the connection status of the client */
        public bool IsConnected
        {
            get
            {
                return tcpClient == null || tcpClient.Client == null
                    ? false
                    : tcpClient.Connected;
            }
        }

        public event DataReceivedEventHandler DataReceived;
        protected void OnDataReceived(string data)
        {
            DataReceived?.Invoke(data);
        }

        public TcpConnector()
        {
            tcpClient = new TcpClient();
        }

        /* Searches the server address and does not return until to connect a server.  */
        public void Connect()
        {
            /* If it is not connected then wait for the connection */
            while (true)
            {
                var connection = NetworkInformation.GetInternetConnectionProfile();

                /* Is it connected? */
                if (connection != null && connection.NetworkAdapter != null)
                {
                    var hostname = NetworkInformation.GetHostNames()
                        .SingleOrDefault(c => c.IPInformation != null &&
                            c.IPInformation.NetworkAdapter != null &&
                            c.IPInformation.NetworkAdapter.NetworkAdapterId == connection.NetworkAdapter.NetworkAdapterId);

                    /* Set ipaddress */
                    if (hostname != null) { this.LocalIpAddress = hostname.DisplayName; }

                    /* We found the connection */
                    break;
                }
            }

            var firstPart = LocalIpAddress.Substring(0, LocalIpAddress.LastIndexOf('.'));

            while (true)
            {
                bool isConnected = false;
                /* Start searching for the server IP to connect */
                for (int i = 2; i < 255; i++)
                {
                    try
                    {
                        var serverAddress = string.Format("{0}.{1}", firstPart, i);
                        /* Try connect to first IP address in the subnet */
                        tcpClient.Client.Connect(serverAddress, Port);

                        /* If connects successfully, break the loop */
                        this.ServerIpAddress = serverAddress;
                        isConnected = true;
                        break;
                    }
                    catch { }
                }
                if (isConnected) { break; }
            }
        }

        /* Sends the given data */
        public void Send(string data)
        {
            if (tcpClient == null)
                throw new Exception("tcpClient cannot be null!");

            try
            {
                byte[] bytes = Encoding.ASCII.GetBytes(data);
                tcpClient.Client.Send(bytes, bytes.Length, SocketFlags.None);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /* Receives, collects and returns the data */
        public string Receive(int timeoutInSeconds)
        {
            long timeout = timeoutInSeconds * TimeSpan.TicksPerMillisecond;

            StringBuilder sb = new StringBuilder();
            byte[] data = ReceiveInternal(timeout);
            if (data != null)
            {
                do
                {
                    sb.Append(Encoding.ASCII.GetString(data));
                    data = ReceiveInternal(100);
                } while (data != null);
            }

            Debug.WriteLine(sb.ToString());

            return sb.ToString();
        }

        /* Tries to receive data from the socket in given duration.
         * If the timeout occured then returns null! */
        private byte[] ReceiveInternal(long timeoutInMilliseconds)
        {
            byte[] bytes;

            /* Calculation of the loop count according to the given timeout duration */
            long loopCount = (timeoutInMilliseconds) / 1000;

            while (true)
            {
                /* Poll the socket to read. */
                if (tcpClient.Client.Poll(100000, SelectMode.SelectRead)) break;
                if (tcpClient.Client.Poll(1, SelectMode.SelectError))
                    throw new Exception("Polling Error occured!");

                loopCount--;
                if (loopCount <= 0) return null;
            }

            bytes = new byte[READ_BUFFER_SIZE];
            int read = tcpClient.Client.Receive(bytes, bytes.Length, SocketFlags.None);

            if (read > 0)
            {
                byte[] bytesRead = new byte[read];
                Array.Copy(bytes, bytesRead, bytesRead.Length);
                return bytesRead;
            }

            return null;
        }

        /* Closes the Client's connection */
        private bool isClosed = false; // To detect redundant calls
        public void Close()
        {
            if (isClosed) return;

            try { tcpClient.Client.Shutdown(SocketShutdown.Both); }
            catch { }
            try { tcpClient.Dispose(); }
            catch { }

            isClosed = true;
        }

        //#region IDisposable Support

        //private bool disposedValue = false; // To detect redundant calls

        //protected void Dispose(bool disposing)
        //{
        //    if (!disposedValue)
        //    {
        //        if (disposing)
        //        {
        //            Close();
        //        }

        //        disposedValue = true;
        //    }
        //}

        //// This code added to correctly implement the disposable pattern.
        //void IDisposable.Dispose()
        //{
        //    // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //    Dispose(true);
        //}

        //#endregion IDisposable Support
    }
}
