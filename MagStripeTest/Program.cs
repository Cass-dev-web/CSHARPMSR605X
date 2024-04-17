using MagStripeTest;

CSHARPMSR605X_2 MSR = new CSHARPMSR605X_2();
// Connect
if(!MSR.Connect())
    throw new Exception(MSR.ErrorMessage);
if (!MSR.TestConnection())
    throw new Exception(MSR.ErrorMessage);
// Reset
MSR.Reset();
MSR.SetCo(true);
// List<byte> data = new List<byte>()
// {
//     0x00, 0xdd, 0x1b, 0x6e, 0x1b, 0x73,
//     0x1b, 0x01, 
//         0x09, 0x45, 0x9a, 0x2c, 0x3c, 0x5a, 0x03, 0xa3, 0x1f, 0x32,
//     0x1b, 0x02,
//         0x01, 0x02, 0x03, 0x04, 
//     0x1b, 0x03, 
//         0x00, 0x3f,
//     0x1c 
// };
List<byte> data = new List<byte>()
{
    0x00, 0xdd, 0x1B, 0x77, 0x1B, 0x73, 0x1B, 0x01, 0x30, 0x31, 0x1B,
    0x02, 0x32, 0x33, 0x1B, 0x03, 0x34, 0x35, 0x3F, 0x1C
};
CSHARPMSR605X_2.PrintBytes(true,data.ToArray());
List<byte> remaining = new List<byte>(new byte[100 - data.Count]);
data = new List<byte>(data);
data.AddRange(remaining);
MSR.MSRStream.SetFeature(
    data.ToArray()
);
CSHARPMSR605X_2.MSRData? card = MSR.ReadCardISO();
if(card==null)
    Console.WriteLine(MSR.ErrorStatus);
else
    Console.WriteLine(card?.toHeavyString());
while (true)
{
    byte[] o = MSR.MSRStream.Read();
    if(o.Length==0) continue;
    CSHARPMSR605X_2.PrintBytes(false, o);
    CSHARPMSR605X_2.PrintBytes(true, o);
    MSR.MSRStream.Flush();
}
// // // Read the card