﻿// Example Program for CSHARPMSR605X
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
// EXAMPLES HERE