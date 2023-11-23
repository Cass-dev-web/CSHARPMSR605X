// Example Program for CSHARPMSR605X
using MagStripeTest;
using System.Collections;

CSHARPMSR605X api = new CSHARPMSR605X();
bool success = api.Connect();
if (!success)
{
    Console.WriteLine("Error while connecting to MSR605X!");
    return;
}
api.Reset();
Thread.Sleep(1000);
while (true)
{
    byte[] returnmodelraw = api.ReadCardRaw();
    Console.WriteLine(CSHARPMSR605X.ConvertByteArrayToString(returnmodelraw));
    Console.WriteLine(BitConverter.ToString(returnmodelraw).Replace("-", " "));
    CSHARPMSR605X.ReadCardInformation returnmodel = await api.ReadCard();
    Console.WriteLine($"Track 1: {returnmodel.Track1} [{BitConverter.ToString(returnmodel.Track1ByteArray).Replace("-", " ")}]\nTrack 2: {returnmodel.Track2} [{BitConverter.ToString(returnmodel.Track2ByteArray).Replace("-", " ")}]");
}