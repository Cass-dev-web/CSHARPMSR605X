using HidSharp;
using System.Text;
/*
 * Name: CSHARPMSR605X
 * Author: cass-dev-web
 * Date: 22/11/2023
 * Description: MSR605X API for C# .NET applications
 * Credits: https://gitlab.com/camconn/hidmsr, https://usermanual.wiki/Pdf/MSR60520Programmers20Manual.325315846/help
 * Dependencies: HidSharp
 */
namespace MagStripeTest
{
    public class CSHARPMSR605X
    {
        // VARS
        const byte INTERFACE = 0x00;
        const int MAX_OUTPUT_BITS = 7813;
        HidDevice? MSR605X;
        HidStream? MSRStream;

        private static readonly Dictionary<string, byte> commandDictionary = new Dictionary<string, byte>()
        {
            {"READ",0x72},
            {"WRITE",0x77}, // UNSURE
            {"RED_ONLY",0x85},
            {"GREEN_AND_YELLOW",0x84},
            {"GREEN_ONLY",0x83},
            {"NONE",0x81},
            {"ALL",0x82},
        };
        
        private static readonly byte[] DATA_BLOCK_START = new byte[] // Write Function
        {
            0x1B, commandDictionary["WRITE"], 0x1B, 0x73
        };
        private static readonly byte[] DATA_BLOCK_END = new byte[] // Write Function
        {
            0x3F, 0x1C
        };
        ////////
        static bool ContainsSubSequence(byte[] mainArray, byte[] subSequence)
        {
            for (int i = 0; i <= mainArray.Length - subSequence.Length; i++)
            {
                if (IsSubSequenceMatch(mainArray, i, subSequence))
                {
                    return true;
                }
            }
            return false;
        }

        static bool IsSubSequenceMatch(byte[] mainArray, int startIndex, byte[] subSequence)
        {
            for (int i = 0; i < subSequence.Length; i++)
            {
                if (mainArray[startIndex + i] != subSequence[i])
                {
                    return false;
                }
            }
            return true;
        }
        private List<byte> ExtendCommand(List<byte> command, int length = 1024)
        {
            // Extend a command until it fills out the specified number of bytes.
            List<byte> remaining = new List<byte>(new byte[length - command.Count]);
            List<byte> result = new List<byte>(command);
            result.AddRange(remaining);
            return result;
        }
        static T[] ConcatArrays<T>(T[] array1, T[] array2)
        {
            return array1.Concat(array2).ToArray();
        }

        public enum MSRStatus
        {
            OK,
            // errors
            ERROR_WRITE_READ,
            ERROR_COMMAND_FORMAT,
            ERROR_INVALID_COMMAND,
            ERROR_INVALID_SWIPE,
            // runtime err
            INTERNAL_ERROR,
            INVALID_STATUS_BYTE
        }

        public static readonly Dictionary<byte, MSRStatus> MSRStatusByteLookupTable = new Dictionary<byte, MSRStatus>()
        {
            { 0x30, MSRStatus.OK},
            { 0x31, MSRStatus.ERROR_WRITE_READ},
            { 0x32, MSRStatus.ERROR_COMMAND_FORMAT},
            { 0x34, MSRStatus.ERROR_INVALID_COMMAND},
            { 0x39, MSRStatus.ERROR_INVALID_SWIPE},
            { 0x40, MSRStatus.INTERNAL_ERROR}
        };
        public static MSRStatus TranslateStatusByte(byte status)
        {
            return (MSRStatusByteLookupTable.ContainsKey(status) ? MSRStatusByteLookupTable[status] : MSRStatus.INVALID_STATUS_BYTE);
        }
        private byte[] CreateReportData(int interfaceNumber, List<byte> data, int length = 0)
        {
            // Create a report to send from a hex dump.
            if (length <= 0)
            {
                length = data.Count + 1;
            }
            else if (length < (data.Count + 1))
            {
                throw new ArgumentException("The length must be longer than the data!");
            }
            List<byte> report = new List<byte> { (byte)interfaceNumber };
            report.AddRange(data);
            byte[] buffer = new byte[length];
            Buffer.BlockCopy(report.ToArray(), 0, buffer, 0, report.Count);
            return buffer;
        }
        /// <summary>
        /// Connects to the MSR605X
        /// </summary>
        /// <returns>Success?</returns>
        public bool Connect(int timeout)
        {
            // Search for devices
            HidDevice[] ConnectedDevices = DeviceList.Local.GetHidDevices().ToArray();
            HidDevice? MSR605XLocal = null;
            foreach (HidDevice connectedDevice in ConnectedDevices)
            {
                if (connectedDevice.ProductID != 3 || connectedDevice.VendorID != 2049) continue;
                MSR605XLocal = connectedDevice;
            }
            this.MSR605X = MSR605XLocal;
            if (MSR605XLocal != null) openStream(timeout);
            return (MSR605XLocal != null);
        }
        private void openStream(int timeout)
        {
            if (this.MSR605X == null)
            {
                Console.WriteLine("Cannot open stream without connecting MSR605X first.");
                return;
            }
            //
            try
            {
                MSRStream = MSR605X.Open();
                MSRStream.ReadTimeout = timeout;
            }
            catch (Exception err)
            {
                Console.WriteLine($"Error opening stream: {err.Message}");
            }
        }
        // Methods //
        public void SendByteCommandNoReturn(byte[] commandBytes)
        {
            if (this.MSRStream == null)
            {
                Console.WriteLine("Cannot send byte without opening stream first.");
                return;
            }
            MSRStream.SetFeature(CreateReportData(INTERFACE, ExtendCommand(new List<byte>(commandBytes))));
        }
        public static string ConvertByteArrayToString(byte[] byteArray)
        {
            // Assuming ASCII encoding, you can also use other encodings like UTF-8, UTF-16, etc.
            string resultString = Encoding.ASCII.GetString(byteArray);
            return resultString;
        }
        public byte[] SendByteCommandWaitReturn(byte[] commandBytes)
        {
            if (this.MSRStream == null)
            {
                Console.WriteLine("Cannot send byte without opening stream first.");
                return Array.Empty<byte>();
            }
            // Send bytes
            SendByteCommandNoReturn(commandBytes);
            // Wait for bytes
            byte[] output = new byte[MAX_OUTPUT_BITS];
            while (true)
            {
                byte[] heldOutput = new byte[MAX_OUTPUT_BITS];
                try
                {
                    MSRStream.Read(heldOutput, 0, heldOutput.Length);
                }catch(Exception _) { return Array.Empty<byte>(); }
                MSRStream.Flush();
                heldOutput = truncateOutput(heldOutput);
                output = ConcatArrays(output, heldOutput);
                if (ContainsSubSequence(output, new byte[] { 0x3F, 0x1C, 0x1B })) break;
            }
            output = truncateOutput(output);
            return output;
        }

        static byte[] truncateOutput(byte[] inputArray)
        {
            // Figure out start 
            int startIndex = 0;
            for (int i = 0; i < inputArray.Length; i++)
            {
                byte indexByte = inputArray[i];
                if (indexByte != 0x00)
                {
                    startIndex = i;
                    break;
                }
            }
            // Figure out end index 
            int endIndex = 0;
            for (int i = inputArray.Length - 1; i >= 0; i--)
            {
                byte indexByte = inputArray[i];
                if (indexByte != 0x00)
                {
                    endIndex = i;
                    break;
                }
            }
            byte[] trimmedArray = new byte[endIndex - startIndex + 1];
            Array.Copy(inputArray, startIndex, trimmedArray, 0, trimmedArray.Length);
            return trimmedArray;
        }

        /// <summary>
        /// Reset device to initial test.
        /// </summary>
        public void Reset()
        {
            SendByteCommandNoReturn(new byte[] { 0xC2, 0x1B, 0x61, 0x44, 0xF8, 0x19 }); // what??? this doesn't make sense when it comes to the programmer manual :/
        }
        public enum MSR605XColorType
        {
            RED_ONLY,
            GREEN_AND_YELLOW,
            GREEN_ONLY,
            NONE,
            ALL
        }
        /// <summary>
        /// Sets the device leds.
        /// </summary>
        public void SetLEDs(MSR605XColorType colorType)
        {
            switch (colorType)
            {
                case MSR605XColorType.RED_ONLY:
                    SendByteCommandNoReturn(new byte[] { 0xC5, 0x1B, commandDictionary["RED_ONLY"] });
                    break;
                case MSR605XColorType.GREEN_AND_YELLOW:
                    SendByteCommandNoReturn(new byte[] { 0xC5, 0x1B, commandDictionary["GREEN_AND_YELLOW"] });
                    break;
                case MSR605XColorType.GREEN_ONLY:
                    SendByteCommandNoReturn(new byte[] { 0xC5, 0x1B, commandDictionary["GREEN_ONLY"] });
                    break;
                case MSR605XColorType.NONE:
                    SendByteCommandNoReturn(new byte[] { 0xC5, 0x1B, commandDictionary["NONE"] });
                    break;
                case MSR605XColorType.ALL:
                    SendByteCommandNoReturn(new byte[] { 0xC5, 0x1B, commandDictionary["ALL"] });
                    break;

            }
        }
        public enum COType
        {
            HIGH_CO,
            LOW_CO
        }
        /// <summary>
        /// Set CO.
        /// </summary>
        public void SetCo(COType type)
        {
            int controlByte = (type == COType.HIGH_CO ? 0x78 : 0x79);
            SendByteCommandNoReturn(new byte[] { 0xC2, 0x1B, (byte)controlByte });
        }
        /// <summary>
        /// Tests connection.
        /// </summary>
        public byte[] TestConnection()
        {
            return SendByteCommandWaitReturn(new byte[] { 0xC5, 0x1B, 0x65 });
        }
        public enum ErasureType
        {
            TRACK1,
            TRACK2,
            TRACK3,
            TRACK1_2,
            TRACK1_3,
            TRACK2_3,
            TRACK1_2_3,
        }
        /// <summary>
        /// Erases a card.
        /// </summary>
        public void Erase(ErasureType erasureType)
        {
            byte erasionByte = 0x00;
            switch (erasureType)
            {
                case ErasureType.TRACK1:
                    erasionByte = 0x00;
                    break;
                case ErasureType.TRACK2:
                    erasionByte = 0x02;
                    break;
                case ErasureType.TRACK3:
                    erasionByte = 0x04;
                    break;
                case ErasureType.TRACK1_2:
                    erasionByte = 0x03;
                    break;
                case ErasureType.TRACK1_3:
                    erasionByte = 0x05;
                    break;
                case ErasureType.TRACK2_3:
                    erasionByte = 0x06;
                    break;
                case ErasureType.TRACK1_2_3:
                    erasionByte = 0x07;
                    break;
            }
            SendByteCommandWaitReturn(new byte[] { 0xC5, 0x1B, 0x63, erasionByte });
        }
        /// <summary>
        /// Read card raw data.
        /// </summary>
        public byte[] ReadCardRaw()
        {
            byte[] rawData = SendByteCommandWaitReturn(new byte[] { 0xC5, 0x1B, commandDictionary["READ"] });
            if (rawData.Length == 0) return new byte[0];
            // STARTING/ENDING FIELDS: 1B 73 [CARD INFO] 3F 1C 1B [STATUS]
            // Figure out start 
            int startIndex = 0;
            for (int i = 0; i < rawData.Length; i++)
            {
                byte indexByte = rawData[i];
                if (indexByte == 0x1B && (rawData.Length >= i + 1 && rawData[i + 1] == 0x73))
                {
                    startIndex = i + 2;
                    break;
                }
            }
            // Figure out end index 
            int endIndex = 0;
            for (int i = rawData.Length - 1; i >= 0; i--)
            {
                byte indexByte = rawData[i];
                if (indexByte == 0x1B && (rawData.Length >= i - 1 && rawData[i - 1] == 0x1C) && (rawData.Length >= i - 2 && rawData[i - 2] == 0x3F))
                {
                    endIndex = i + 1;
                    break;
                }
            }
            byte[] formattedData = new byte[endIndex - startIndex + 1];
            Array.Copy(rawData, startIndex, formattedData, 0, formattedData.Length);
            return formattedData;
        }
        private static void printByteStream(byte[] data)
        {
            Console.WriteLine(BitConverter.ToString(data).Replace("-", " "));
        }
        public byte attemptFetchStatusCode(byte[] dataarray)
        {
            for (int i = 0; i < dataarray.Length; i++)
            {
                byte d = dataarray[i];
                if (dataarray.Length >= i + 3 && d == 0x3F && dataarray[i + 1] == 0x1C && dataarray[i + 2] == 0x1B) return dataarray[i + 3];
            }
            Console.WriteLine("Error attempting to fetch status code; did not reach status byte.");
            return 0x40;
        }
        public class ReadCardInformation
        {
            public bool failed;
            public MSRStatus? status;
            public string? failmessage;
            public string? Track1;
            public string? Track2;
            public string? Track3;
            public byte[]? Track1ByteArray;
            public byte[]? Track2ByteArray;
            public byte[]? Track3ByteArray;
        }
        /// <summary>
        /// Read card and return specific userfriendly class.
        /// </summary>
        public ReadCardInformation ReadCard()
        {
            byte[] returns = ReadCardRaw();
            if (returns.Length == 0)
            {
                ReadCardInformation errnoCard = new ReadCardInformation();
                errnoCard.failed = true;
                errnoCard.failmessage = "empty_return";
                return errnoCard;
            }
            returns = returns.Skip(3).ToArray();
            // Get status byte first
            bool containsStatusCode = ContainsSubSequence(returns, new byte[] { 0x3F, 0x1C, 0x1B });
            if (!containsStatusCode)
            {
                Console.WriteLine("DOES NOT CONTAIN STATUS!!");
                ReadCardInformation errnoCard = new ReadCardInformation();
                errnoCard.failed = true;
                errnoCard.failmessage = "NO STATUS CODE ; EMPTY";
                return errnoCard;
            }
            byte statusCode = attemptFetchStatusCode(returns);
            MSRStatus status = TranslateStatusByte(statusCode);
            if (status != MSRStatus.OK)
            {
                Console.WriteLine("ERROR TRYING TO READ CARD: " + status);
                ReadCardInformation errnoCard = new ReadCardInformation();
                errnoCard.failmessage = "Hardware error: " + status;
                errnoCard.failed = true;
                return errnoCard;
            }
            //
            ReadCardInformation readCardInformation = new ReadCardInformation();
            readCardInformation.status = status;
            // parse tracks: 1B01[string1]1B02[string2]1B03[string3]
            // - TRACK 1
            int endTrack1 = 0;
            int endTrack2 = 0;
            int endTrack3 = 0;
            for (int i = 0; i < returns.Length; i++)
            {
                byte item = returns[i];
                if (item == 0x1B && (returns.Length >= i + 1 && returns[i + 1] == 0x02))
                {
                    endTrack1 = i - 1;
                }
                else if (item == 0x1B && (returns.Length >= i + 1 && returns[i + 1] == 0x03))
                {
                    endTrack2 = i - 4;
                }
                else if (item == 0x3F && (returns.Length >= i + 1 && returns[i + 1] == 0x1B))
                {
                    endTrack3 = i - 1;
                }
            }
            byte[] track1ByteArray = new byte[endTrack1];
            Array.Copy(returns, 0, track1ByteArray, 0, track1ByteArray.Length);
            readCardInformation.Track1 = ConvertByteArrayToString(track1ByteArray);
            readCardInformation.Track1ByteArray = track1ByteArray;
            //
            byte[] track2ByteArray = new byte[endTrack2 - endTrack1 - 1];
            Array.Copy(returns, endTrack1 + 4, track2ByteArray, 0, track2ByteArray.Length);
            readCardInformation.Track2 = ConvertByteArrayToString(track2ByteArray);
            readCardInformation.Track2ByteArray = track2ByteArray;
            //
            // TODO: Fix third track parsing, its currently broken.
            if (endTrack3 - endTrack2 - 2 > 0)
            {
                byte[] track3ByteArray = new byte[endTrack3 - endTrack2 - 2];
                Array.Copy(returns, endTrack1, track3ByteArray, 0, track3ByteArray.Length);
                readCardInformation.Track3 = ConvertByteArrayToString(track3ByteArray);
                readCardInformation.Track3ByteArray = track3ByteArray;
            }
            return readCardInformation;
        }
        static byte[] ConvertTrackToBytes(string track1Data)
        {
            return Encoding.ASCII.GetBytes(track1Data);
        }

        /// <summary>
        /// Write card.
        /// </summary>
        /// <returns>Status byte, can be translated using translatestatusbyte method.</returns>
        public MSRStatus WriteCard(string Track1, string Track2, string? Track3)
        {
            // Translate tracks
            byte[] Track1bytes = new byte[]{0x1B,0x01}.Concat(ConvertTrackToBytes($"%{Track1}?")).ToArray();
            byte[] Track2bytes = new byte[]{0x1B,0x02}.Concat(ConvertTrackToBytes($";{Track2}?")).ToArray();
            byte[] Track3bytes = (Track3!=null?new byte[]{0x1B,0x03}.Concat(ConvertTrackToBytes($";{Track3}?")).ToArray():Array.Empty<byte>());
            //
            byte[] cardData = Track1bytes.Concat(Track2bytes).ToArray().Concat(Track3bytes).ToArray();
            byte[] dataBlock = DATA_BLOCK_START.Concat(cardData).ToArray().Concat(DATA_BLOCK_END).ToArray();
            // return
            byte[] returnBlock = SendByteCommandWaitReturn(dataBlock);
            byte statusByte = attemptFetchStatusCode(returnBlock);
            MSRStatus msr = TranslateStatusByte(statusByte);
            // MSR returns 0x40 if no return byte (null)
            return msr;
        }
    }
}
