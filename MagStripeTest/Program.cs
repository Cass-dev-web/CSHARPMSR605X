// // Example Program for CSHARPMSR605X
// using MagStripeTest;
// using System.Collections;
//
// CSHARPMSR605X api = new CSHARPMSR605X();
// bool success = api.Connect(Timeout.Infinite);
// if (!success)
// {
//     Console.WriteLine("Error while connecting to MSR605X!");
//     return;
// }
// api.Reset();
// // EXAMPLES HERE
// // CSHARPMSR605X.ReadCardInformation r = await api.ReadCard();
// // Console.WriteLine($"Wrote track 1:\n{r.Track1}\ntrack2:\n{r.Track2}");
// // Thread.Sleep(500);
// // await api.WriteCard(r.Track1, r.Track2,null);
// Thread.Sleep(500);
// api.WriteCard("HELLO", "000",null);

using MagStripeTest;
CSHARPMSR605X2_0 api = new CSHARPMSR605X2_0();
api.Connect();