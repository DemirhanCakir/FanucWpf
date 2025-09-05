using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Buffers.Binary;
using System.IO;

namespace FanucWpf
{
    public class FanucInterface
    {
        private TcpClient _client;
        private NetworkStream _stream;
        private CancellationTokenSource _cts;
        private byte sequenceNumber = 0;
        private SemaphoreSlim _streamLock = new SemaphoreSlim(1, 1); // For thread synchronization
        private bool _robotInitialized = false;

        public event Action<string> LogMessage;
        public event Action<string> MessageReceived;
        public event Action<bool> ConnectionStateChanged;

        public bool IsConnected => _client != null && _client.Connected;

        public async Task ConnectAsync(string ipAddress, int port)
        {
            if (IsConnected)
            {
                LogMessage?.Invoke("Already connected.");
                return;
            }

            _client = new TcpClient { NoDelay = true };
            _cts = new CancellationTokenSource();

            try
            {
                LogMessage?.Invoke($"Connecting to {ipAddress}:{port}...");
                // Connection timeout of 3 seconds
                var connectCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                try
                {
                    await _client.ConnectAsync(ipAddress, port);
                }
                finally
                {
                    connectCts.Dispose();
                }

                _stream = _client.GetStream();
                LogMessage?.Invoke("Connection successful. Starting to listen for messages.");
                ConnectionStateChanged?.Invoke(true);

                // Initialize robot connection after establishing TCP connection
                _robotInitialized = await InitializeRobotConnection();
                if (!_robotInitialized)
                {
                    LogMessage?.Invoke("Robot initialization failed. Some commands may not work correctly.");
                }
                else
                {
                    LogMessage?.Invoke("Robot initialization successful. Ready to send commands.");
                }

                // Start listening for messages in the background
                _ = Task.Run(() => ReceiveLoopAsync(_cts.Token));
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Connection failed: {ex.Message}");
                await DisconnectAsync();
            }
        }

        public async Task DisconnectAsync()
        {
            if (!IsConnected && _client == null) return;

            LogMessage?.Invoke("Disconnecting...");
            _cts?.Cancel();

            if (_stream != null) _stream.Dispose();
            _client?.Close();

            _stream = null;
            _client = null;
            _cts = null;
            _robotInitialized = false;

            ConnectionStateChanged?.Invoke(false);
            LogMessage?.Invoke("Disconnected.");
        }

        public async Task SendMessageAsync(string message)
        {
            if (!IsConnected || _stream == null)
            {
                LogMessage?.Invoke("Cannot send message. Not connected.");
                return;
            }

            try
            {
                await _streamLock.WaitAsync();
                try
                {
                    byte[] payload = Encoding.UTF8.GetBytes(message);
                    // 4-byte length prefix (Big Endian) + payload
                    byte[] packet = new byte[4 + payload.Length];
                    BinaryPrimitives.WriteInt32BigEndian(packet.AsSpan(0, 4), payload.Length);
                    payload.CopyTo(packet, 4);

                    await _stream.WriteAsync(packet, 0, packet.Length);
                    LogMessage?.Invoke($"Sent: {message}");
                }
                finally
                {
                    _streamLock.Release();
                }
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Error sending message: {ex.Message}");
                await DisconnectAsync();
            }
        }

        private async Task ReceiveLoopAsync(CancellationToken token)
        {
            var lengthBuffer = new byte[4];
            try
            {
                while (!token.IsCancellationRequested && _stream != null)
                {
                    // Check if we can acquire the lock without blocking
                    if (await _streamLock.WaitAsync(0))
                    {
                        try
                        {
                            // Only try to read if there's data available
                            if (_client.Available >= 4)
                            {
                                // Step 1: Read the 4-byte length prefix
                                int bytesRead = await _stream.ReadAsync(lengthBuffer, 0, 4, token);
                                if (bytesRead < 4) continue; // Connection closed or partial data

                                int messageLength = BinaryPrimitives.ReadInt32BigEndian(lengthBuffer);

                                if (messageLength <= 0 || messageLength > 65536) // Safety check
                                {
                                    LogMessage?.Invoke($"Invalid message length received: {messageLength}. Skipping message.");
                                    continue;
                                }

                                // Step 2: Read the message itself
                                var messageBuffer = new byte[messageLength];
                                var totalBytesRead = 0;
                                while (totalBytesRead < messageLength)
                                {
                                    bytesRead = await _stream.ReadAsync(messageBuffer, totalBytesRead, messageLength - totalBytesRead, token);
                                    if (bytesRead == 0) break; // Connection closed
                                    totalBytesRead += bytesRead;
                                }

                                if (totalBytesRead == messageLength)
                                {
                                    string receivedMessage = Encoding.UTF8.GetString(messageBuffer);
                                    MessageReceived?.Invoke(receivedMessage);
                                    LogMessage?.Invoke($"Received: {receivedMessage}");
                                }
                            }
                        }
                        finally
                        {
                            _streamLock.Release();
                        }
                    }

                    // Add a small delay to avoid tight loop
                    await Task.Delay(50, token);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected condition, clean exit
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"An error occurred in receive loop: {ex.Message}");
            }
            finally
            {
                // Clean up connection when exiting the loop
                if (IsConnected)
                {
                    await DisconnectAsync();
                }
            }
        }

        #region Robot Communication Methods

        public async Task<bool> InitializeRobotConnection()
        {
            try
            {
                await _streamLock.WaitAsync();
                try
                {
                    LogMessage?.Invoke("Initializing robot connection...");

                    // Send initialization packet
                    byte[] initPacket = CreateInitPacket();
                    await SendRawPacket(initPacket);

                    byte[] response = await ReceiveRawPacket();
                    if (response == null || response.Length < 56)
                    {
                        LogMessage?.Invoke("Invalid response from robot");
                        return false;
                    }

                    // Second initialization packet
                    byte[] initPacket2 = CreateSecondInitPacket();
                    await SendRawPacket(initPacket2);

                    response = await ReceiveRawPacket();
                    if (response == null || response.Length < 56)
                    {
                        LogMessage?.Invoke("Invalid response from robot for second init");
                        return false;
                    }

                    LogMessage?.Invoke("Robot initialization successful");
                    return true;
                }
                finally
                {
                    _streamLock.Release();
                }
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Initialization error: {ex.Message}");
                return false;
            }
        }

        private byte[] CreateInitPacket()
        {
            byte[] packet = new byte[56];

            // Packet type (INIT = 0x00)
            packet[0] = 0x00;
            packet[1] = 0x00;

            // Sequence number
            packet[2] = sequenceNumber++;

            // Message type (SHORT = 0xC0)
            packet[31] = 0xC0;

            // Set some required values based on protocol
            packet[8] = 0x00;
            packet[9] = 0x01;
            packet[16] = 0x00;
            packet[17] = 0x01;

            return packet;
        }

        private byte[] CreateSecondInitPacket()
        {
            byte[] packet = new byte[56];

            // Packet type (UNKNOWN_8 = 0x08)
            packet[0] = 0x08;
            packet[1] = 0x00;

            // Sequence number
            packet[2] = sequenceNumber++;
            packet[30] = packet[2]; // Duplicate sequence number in another field

            // Message type (SHORT = 0xC0)
            packet[31] = 0xC0;

            // Set some required values based on protocol
            packet[8] = 0x00;
            packet[9] = 0x01;
            packet[16] = 0x00;
            packet[17] = 0x01;

            // Mailbox destination (0x00000e10)
            packet[36] = 0x10;
            packet[37] = 0x0e;
            packet[38] = 0x00;
            packet[39] = 0x00;

            // Packet number and total packet number
            packet[40] = 0x01; // Packet number
            packet[41] = 0x01; // Total packet number

            // Service request code and segment selector for init
            packet[42] = 0x4F; // INIT service request code
            packet[43] = 0x01; // INIT segment selector

            return packet;
        }

        private async Task ExecuteCommand(string command)
        {
            try
            {
                await _streamLock.WaitAsync();
                try
                {
                    if (!IsConnected || _client == null)
                    {
                        LogMessage?.Invoke("Not connected to robot");
                        return;
                    }

                    if (!_robotInitialized)
                    {
                        LogMessage?.Invoke("Robot not initialized. Trying to initialize now...");
                        _robotInitialized = await InitializeRobotConnection();
                        if (!_robotInitialized)
                        {
                            LogMessage?.Invoke("Failed to initialize robot connection. Cannot execute command.");
                            return;
                        }
                    }

                    LogMessage?.Invoke($"Sending command: {command}");
                    byte[] commandBytes = Encoding.ASCII.GetBytes(command);
                    byte[] packet = CreateCommandPacket(commandBytes);

                    await SendRawPacket(packet);
                    byte[] response = await ReceiveRawPacket();

                    if (response != null && response.Length >= 56)
                    {
                        LogMessage?.Invoke($"Command {command} sent successfully");
                    }
                    else
                    {
                        LogMessage?.Invoke($"Failed to send command {command}");
                    }
                }
                finally
                {
                    _streamLock.Release();
                }
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Error executing command: {ex.Message}");
            }
        }

        private byte[] CreateCommandPacket(byte[] commandBytes)
        {
            byte[] packet;
            int headerSize = 56;

            // Determine if we need a short or extended packet
            if (commandBytes.Length <= 6)
            {
                // Short packet - command fits in the header
                packet = new byte[headerSize];

                // Copy command into the payload area
                for (int i = 0; i < commandBytes.Length; i++)
                {
                    packet[48 + i] = commandBytes[i];
                }
            }
            else
            {
                // Extended packet - need extra space for command
                packet = new byte[headerSize + commandBytes.Length];

                // Copy header
                // Set extra length field
                packet[4] = (byte)(commandBytes.Length & 0xFF);
                packet[5] = (byte)((commandBytes.Length >> 8) & 0xFF);

                // Copy command bytes after the header
                Buffer.BlockCopy(commandBytes, 0, packet, headerSize, commandBytes.Length);
            }

            // Packet type (REQUEST = 0x02)
            packet[0] = 0x02;
            packet[1] = 0x00;

            // Sequence number
            packet[2] = sequenceNumber++;
            packet[30] = packet[2]; // Duplicate sequence number in another field

            // Message type (SHORT or LONG)
            packet[31] = (commandBytes.Length <= 6) ? (byte)0xC0 : (byte)0x80;

            // Set some required values based on protocol
            packet[8] = 0x00;
            packet[9] = 0x01;
            packet[16] = 0x00;
            packet[17] = 0x01;

            // Mailbox destination (0x00000e10)
            packet[36] = 0x10;
            packet[37] = 0x0e;
            packet[38] = 0x00;
            packet[39] = 0x00;

            // Packet number and total packet number
            packet[40] = 0x01; // Packet number
            packet[41] = 0x01; // Total packet number

            // Service request code and segment selector
            packet[42] = 0x07; // WRITE_SYS_MEM service request code
            packet[43] = 0x38; // BYTE_G segment selector

            // Index and count
            packet[44] = 0x00; // Index low byte
            packet[45] = 0x00; // Index high byte
            packet[46] = (byte)(commandBytes.Length & 0xFF); // Count low byte
            packet[47] = (byte)((commandBytes.Length >> 8) & 0xFF); // Count high byte

            return packet;
        }

        public async Task SetRegisterValue(int registerNumber, float value)
        {
            try
            {
                await _streamLock.WaitAsync();
                try
                {
                    LogMessage?.Invoke($"Setting R[{registerNumber}] = {value}");

                    if (!_robotInitialized)
                    {
                        LogMessage?.Invoke("Robot not initialized. Initializing now...");
                        _robotInitialized = await InitializeRobotConnection();
                        if (!_robotInitialized)
                        {
                            LogMessage?.Invoke("Failed to initialize robot connection. Cannot set register value.");
                            return;
                        }
                    }

                    // Create SETASG command for register
                    string command = $"SETASG 1 2 R[{registerNumber}] 0";
                    await ExecuteCommand(command);

                    // Now write the register value
                    byte[] valueBytes = BitConverter.GetBytes(value);
                    await WriteSNPX(1, valueBytes);

                    LogMessage?.Invoke($"Successfully set R[{registerNumber}] to {value}");
                }
                finally
                {
                    _streamLock.Release();
                }
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Error setting register value: {ex.Message}");
            }
        }

        public async Task<float> GetRegisterValue(int registerNumber)
        {
            try
            {
                await _streamLock.WaitAsync();
                try
                {
                    LogMessage?.Invoke($"Reading R[{registerNumber}]...");

                    if (!_robotInitialized)
                    {
                        LogMessage?.Invoke("Robot not initialized. Initializing now...");
                        _robotInitialized = await InitializeRobotConnection();
                        if (!_robotInitialized)
                        {
                            LogMessage?.Invoke("Failed to initialize robot connection. Cannot get register value.");
                            return float.NaN;
                        }
                    }

                    // Create SETASG command for register
                    string command = $"SETASG 1 2 R[{registerNumber}] 0";
                    await ExecuteCommand(command);

                    // Read the register value
                    byte[] valueBytes = await ReadSNPX(1, 4);
                    if (valueBytes != null && valueBytes.Length >= 4)
                    {
                        float value = BitConverter.ToSingle(valueBytes, 0);
                        LogMessage?.Invoke($"R[{registerNumber}] = {value}");
                        return value;
                    }
                    else
                    {
                        LogMessage?.Invoke("Failed to read register value");
                        return float.NaN;
                    }
                }
                finally
                {
                    _streamLock.Release();
                }
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Error getting register value: {ex.Message}");
                return float.NaN;
            }
        }

        public async Task GetPositionRegister(int prNumber)
        {
            try
            {
                await _streamLock.WaitAsync();
                try
                {
                    LogMessage?.Invoke($"Reading PR[{prNumber}]...");

                    if (!_robotInitialized)
                    {
                        LogMessage?.Invoke("Robot not initialized. Initializing now...");
                        _robotInitialized = await InitializeRobotConnection();
                        if (!_robotInitialized)
                        {
                            LogMessage?.Invoke("Failed to initialize robot connection. Cannot get position register.");
                            return;
                        }
                    }

                    // Create SETASG command for position register
                    string command = $"SETASG 1 50 PR[{prNumber}] 0.0";
                    await ExecuteCommand(command);

                    // Read the position register value (100 bytes for full position data)
                    byte[] valueBytes = await ReadSNPX(1, 100);
                    if (valueBytes != null && valueBytes.Length >= 100)
                    {
                        // Extract X, Y, Z, W, P, R values
                        float x = BitConverter.ToSingle(valueBytes, 0);
                        float y = BitConverter.ToSingle(valueBytes, 4);
                        float z = BitConverter.ToSingle(valueBytes, 8);
                        float w = BitConverter.ToSingle(valueBytes, 12);
                        float p = BitConverter.ToSingle(valueBytes, 16);
                        float r = BitConverter.ToSingle(valueBytes, 20);

                        string posInfo = $"X:{x:F2} Y:{y:F2} Z:{z:F2} W:{w:F2} P:{p:F2} R:{r:F2}";

                        LogMessage?.Invoke($"PR[{prNumber}] = {posInfo}");
                    }
                    else
                    {
                        LogMessage?.Invoke("Failed to read position register");
                    }
                }
                finally
                {
                    _streamLock.Release();
                }
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Error getting position register: {ex.Message}");
            }
        }

        private async Task WriteSNPX(int index, byte[] values)
        {
            try
            {
                if (values.Length % 2 != 0)
                {
                    // Pad with one extra byte if odd length
                    byte[] paddedValues = new byte[values.Length + 1];
                    Buffer.BlockCopy(values, 0, paddedValues, 0, values.Length);
                    values = paddedValues;
                }

                index--; // Convert to 0-based index for the protocol

                byte[] packet;
                int headerSize = 56;

                // Determine if we need a short or extended packet
                if (values.Length <= 6)
                {
                    // Short packet - data fits in the header
                    packet = new byte[headerSize];

                    // Copy values into the payload area
                    for (int i = 0; i < values.Length; i++)
                    {
                        packet[48 + i] = values[i];
                    }
                }
                else
                {
                    // Extended packet - need extra space
                    packet = new byte[headerSize + values.Length];

                    // Set extra length field
                    packet[4] = (byte)(values.Length & 0xFF);
                    packet[5] = (byte)((values.Length >> 8) & 0xFF);

                    // Copy values after the header
                    Buffer.BlockCopy(values, 0, packet, headerSize, values.Length);
                }

                // Packet type (REQUEST = 0x02)
                packet[0] = 0x02;
                packet[1] = 0x00;

                // Sequence number
                packet[2] = sequenceNumber++;
                packet[30] = packet[2]; // Duplicate sequence number in another field

                // Message type (SHORT or LONG)
                packet[31] = (values.Length <= 6) ? (byte)0xC0 : (byte)0x80;

                // Set some required values based on protocol
                packet[8] = 0x00;
                packet[9] = 0x01;
                packet[16] = 0x00;
                packet[17] = 0x01;

                // Mailbox destination (0x00000e10)
                packet[36] = 0x10;
                packet[37] = 0x0e;
                packet[38] = 0x00;
                packet[39] = 0x00;

                // Packet number and total packet number
                packet[40] = 0x01; // Packet number
                packet[41] = 0x01; // Total packet number

                // Service request code and segment selector
                packet[42] = 0x07; // WRITE_SYS_MEM service request code
                packet[43] = 0x08; // WORD_R segment selector

                // Index and count
                packet[44] = (byte)(index & 0xFF); // Index low byte
                packet[45] = (byte)((index >> 8) & 0xFF); // Index high byte
                packet[46] = (byte)((values.Length / 2) & 0xFF); // Count low byte (in words)
                packet[47] = (byte)(((values.Length / 2) >> 8) & 0xFF); // Count high byte

                await SendRawPacket(packet);
                byte[] response = await ReceiveRawPacket();

                if (response == null || response.Length < 56 || response[31] == 0xD1) // 0xD1 = SHORT_FAILED
                {
                    throw new Exception("Failed to write SNPX data");
                }
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Error writing SNPX: {ex.Message}");
                throw;
            }
        }

        private async Task<byte[]> ReadSNPX(int index, ushort count)
        {
            try
            {
                index--; // Convert to 0-based index for the protocol

                byte[] packet = new byte[56]; // Header size for short packet

                // Packet type (REQUEST = 0x02)
                packet[0] = 0x02;
                packet[1] = 0x00;

                // Sequence number
                packet[2] = sequenceNumber++;
                packet[30] = packet[2]; // Duplicate sequence number in another field

                // Message type (SHORT = 0xC0)
                packet[31] = 0xC0;

                // Set some required values based on protocol
                packet[8] = 0x00;
                packet[9] = 0x01;
                packet[16] = 0x00;
                packet[17] = 0x01;

                // Mailbox destination (0x00000e10)
                packet[36] = 0x10;
                packet[37] = 0x0e;
                packet[38] = 0x00;
                packet[39] = 0x00;

                // Packet number and total packet number
                packet[40] = 0x01; // Packet number
                packet[41] = 0x01; // Total packet number

                // Service request code and segment selector
                packet[42] = 0x04; // READ_SYS_MEM service request code
                packet[43] = 0x08; // WORD_R segment selector

                // Index and count (in words - 2 bytes each)
                packet[44] = (byte)(index & 0xFF); // Index low byte
                packet[45] = (byte)((index >> 8) & 0xFF); // Index high byte
                packet[46] = (byte)((count / 2) & 0xFF); // Count low byte (in words)
                packet[47] = (byte)(((count / 2) >> 8) & 0xFF); // Count high byte

                await SendRawPacket(packet);
                byte[] response = await ReceiveRawPacket();

                if (response == null || response.Length < 56)
                {
                    throw new Exception("Invalid response from robot");
                }

                if (response[31] == 0xD1) // SHORT_FAILED
                {
                    throw new Exception("Failed to read SNPX data");
                }

                // Extract the payload
                byte[] result;

                if (response.Length <= 56)
                {
                    // Short response - data is in the header
                    result = new byte[Math.Min((int)count, 6)];
                    Buffer.BlockCopy(response, 44, result, 0, result.Length);
                }
                else
                {
                    // Extended response - data is after the header
                    int dataSize = Math.Min(response.Length - 56, count);
                    result = new byte[dataSize];
                    Buffer.BlockCopy(response, 56, result, 0, dataSize);
                }

                return result;
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Error reading SNPX: {ex.Message}");
                throw;
            }
        }

        private async Task SendRawPacket(byte[] packet)
        {
            if (_client != null && _client.Connected)
            {
                await _stream.WriteAsync(packet, 0, packet.Length);
            }
            else
            {
                throw new Exception("Not connected to robot");
            }
        }

        private async Task<byte[]> ReceiveRawPacket()
        {
            if (_client == null || !_client.Connected)
            {
                throw new Exception("Not connected to robot");
            }

            try
            {
                byte[] headerBuffer = new byte[56];
                int bytesRead = await _stream.ReadAsync(headerBuffer, 0, headerBuffer.Length);

                if (bytesRead < 56)
                {
                    LogMessage?.Invoke($"Incomplete header received: {bytesRead} bytes");
                    return null;
                }

                // Check if there's extra data (extended packet)
                int extraLength = BitConverter.ToUInt16(new byte[] { headerBuffer[4], headerBuffer[5] }, 0);

                if (extraLength > 0)
                {
                    byte[] fullPacket = new byte[56 + extraLength];
                    Buffer.BlockCopy(headerBuffer, 0, fullPacket, 0, 56);

                    int extraBytesRead = await _stream.ReadAsync(fullPacket, 56, extraLength);
                    if (extraBytesRead < extraLength)
                    {
                        LogMessage?.Invoke($"Incomplete extended data: got {extraBytesRead} of {extraLength} bytes");
                        return null;
                    }

                    return fullPacket;
                }

                return headerBuffer;
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Error receiving data: {ex.Message}");
                return null;
            }
        }

        #endregion
    }
}