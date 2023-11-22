// Example Program for CSHARPMSR605X
using MagStripeTest;

CSHARPMSR605X api = new CSHARPMSR605X();
bool success = api.Connect();
if (!success)
{
    Console.WriteLine("Error while connecting to MSR605X!");
    return;
}
api.Reset();
Thread.Sleep(1000);
byte[] returnmodel = await api.TestConnection();
Console.WriteLine(returnmodel);