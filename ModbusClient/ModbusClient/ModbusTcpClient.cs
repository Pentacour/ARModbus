﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using ModbusClient.Messages;



namespace ModbusClient
{
    public class ModbusTcpClient : IDisposable
    {
        private const int SendTimeOutMSecs = 2000;
        private const int ResponseTimeOutMSecs = 3000;

        private Socket m_Connection = null;
        private static ushort m_TransactionId = 0;
        private static Mutex m_MutexTransaction = new Mutex();
        private byte m_ModbusErrorCode = 0;
        private string m_HostName;
        private int m_Port;

        public byte ModbusErrorCode { get { return m_ModbusErrorCode; } }

        private static ushort GetNextTransactionId()
        {
            m_MutexTransaction.WaitOne();
            try
            {
                m_TransactionId = checked((ushort)(m_TransactionId+1)); 
            }
            catch(System.OverflowException)
            {
                m_TransactionId = 0;
            }

            m_MutexTransaction.ReleaseMutex();

            return m_TransactionId;
        }

        public ModbusTcpClient()
        {
        }

        public ModbusTcpClient( string hostName, int port ) 
        {
            m_HostName = hostName;
            m_Port = port;
        }

        public bool Connect()
        {
            return Connect(m_HostName, m_Port);
        }

        public bool Connect( string hostName, int port )
        {
            m_HostName = hostName;
            m_Port = port;

            IPHostEntry ipHostInfo = System.Net.Dns.GetHostEntry(m_HostName);
            IPAddress ipAddress = null;

            foreach (var addr in ipHostInfo.AddressList)
            {
                if (addr.AddressFamily == AddressFamily.InterNetwork)
                {
                    ipAddress = addr;
                    break;
                }
            }

            if (ipAddress == null)
                return false;

            IPEndPoint remoteEP = new IPEndPoint(ipAddress,m_Port);

            m_Connection = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            m_Connection.SendTimeout = SendTimeOutMSecs;
            m_Connection.ReceiveTimeout = ResponseTimeOutMSecs;

            IAsyncResult result = m_Connection.BeginConnect(remoteEP, null, null);

            result.AsyncWaitHandle.WaitOne(SendTimeOutMSecs, true);

            if (!m_Connection.Connected)
            {
                m_Connection.Close();
                return false;
            }

            return true;
        }

        public bool[] ReadCoils(byte unitIdentifier, ushort address, ushort quantity)
        {
            if (!IsConnected() && !Connect(m_HostName, m_Port))
                throw new IOException("Cannot send message.");

            ModbusTcpMessage modbusTcpMessage = 
                new ModbusTcpMessage(new ReadCoils(address, quantity), GetNextTransactionId(), unitIdentifier);

            try
            {
                SendStream(modbusTcpMessage.ToStream());
            }
            catch (IOException)
            {
                throw;
            }

            byte[] readStream = new byte[255];
            int bytesRead;

            try
            {
                bytesRead = m_Connection.Receive(readStream);

                if (bytesRead <= 0 )
                {
                    throw new IOException("No data received.");
                }

                Array.Resize(ref readStream, bytesRead);

                if (!modbusTcpMessage.DecodeResponse(readStream))
                {
                    m_ModbusErrorCode = modbusTcpMessage.ProtocolDataUnit.ModbusErrorCode;
                    throw new IOException("Error in response."); 
                }

                return modbusTcpMessage.ProtocolDataUnit.GetReadBitsData();
            }
            catch (SocketException)
            {
                throw new IOException("No data received.");
            }
        }
       
        public ushort[] ReadHoldingRegisters(byte unitIdentifier, ushort address, ushort quantity)
        {
            if( !IsConnected() && !Connect(m_HostName, m_Port))
                throw new IOException("Cannot send message.");

            ModbusTcpMessage modbusTcpMessage =
                new ModbusTcpMessage(new ReadHoldingRegisters(address, quantity), GetNextTransactionId(), unitIdentifier);

            try
            {
                SendStream(modbusTcpMessage.ToStream());
            }
            catch (IOException)
            {
                throw;
            }

            byte[] readStream = new byte[255];

            int bytesRead;
            try
            {
                bytesRead = m_Connection.Receive(readStream);

                if (bytesRead <= 0 )
                {
                    throw new IOException( "No data received." );
                }

                Array.Resize(ref readStream, bytesRead);

                if (!modbusTcpMessage.DecodeResponse(readStream))
                {
                    m_ModbusErrorCode = modbusTcpMessage.ProtocolDataUnit.ModbusErrorCode;
                    throw new IOException("Error in response."); 
                }

                return modbusTcpMessage.ProtocolDataUnit.GetReadWordsData();
            }
            catch (SocketException)
            {
                throw new IOException("No data received.");
            }
        }

        public void WriteMultipleRegisters(byte unitIdentifier, ushort address, ushort[] values)
        {
            if (!IsConnected() && !Connect(m_HostName, m_Port))
                throw new IOException("Cannot send message.");

            ModbusTcpMessage modbusTcpMessage =
                new ModbusTcpMessage(new WriteMultipleRegisters(address, values), 
                                    GetNextTransactionId(), unitIdentifier);

            try
            {
                SendStream(modbusTcpMessage.ToStream());
            }
            catch (IOException)
            {
                throw;
            }

            byte[] readStream = new byte[255];

            int bytesRead;
            try
            {
                bytesRead = m_Connection.Receive(readStream);

                if (bytesRead <= 0 )
                {
                    throw new IOException("No data received.");
                }

                Array.Resize(ref readStream, bytesRead);

                if (!modbusTcpMessage.DecodeResponse(readStream))
                {
                    m_ModbusErrorCode = modbusTcpMessage.ProtocolDataUnit.ModbusErrorCode;
                    throw new IOException("Error in response.");
                }

            }
            catch (SocketException)
            {
                throw new IOException("No data received.");
            }
        }

        public void WriteMultipleCoils(byte unitIdentifier, ushort address, bool[] values)
        {
            if (!IsConnected() && !Connect(m_HostName, m_Port))
                throw new IOException("Cannot send message.");

            ModbusTcpMessage modbusTcpMessage =
                new ModbusTcpMessage(new WriteMultipleCoils(address, values),
                                    GetNextTransactionId(), unitIdentifier);

            try
            {
                SendStream(modbusTcpMessage.ToStream());
            }
            catch(IOException)
            {
                throw;
            }

            byte[] readStream = new byte[255];

            int bytesRead;
            try
            {
                bytesRead = m_Connection.Receive(readStream);

                if (bytesRead <= 0 )
                {
                    throw new IOException("No data received.");
                }

                Array.Resize(ref readStream, bytesRead);

                if (!modbusTcpMessage.DecodeResponse(readStream))
                {
                    m_ModbusErrorCode = modbusTcpMessage.ProtocolDataUnit.ModbusErrorCode;
                    throw new IOException("Error in response.");
                }

            }
            catch (SocketException)
            {
                throw new IOException("No data received.");
            }
        }
        
        private void SendStream(byte[] stream )
        {
            bool errorSending = false;

            try 
            {
                m_Connection.Send(stream);
            }
            catch
            {
                errorSending = true;
            }

            if (errorSending)
            {
                if (!Connect(m_HostName, m_Port))
                    throw new IOException("Cannot send message.");

                try
                {
                    m_Connection.Send(stream);
                }
                catch
                {
                    throw new IOException("Cannot send message.");
                }
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing && m_Connection != null)
            {
                m_Connection.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private bool IsConnected()
        {
            return m_Connection != null && m_Connection.Connected;
        }

        /// <remarks>
        /// Only for test purpouses.
        /// </remarks>
        public static void ResetTransactionId()
        {
            m_MutexTransaction.WaitOne();
            m_TransactionId = 0;
            m_MutexTransaction.ReleaseMutex();
        }

    }
}
