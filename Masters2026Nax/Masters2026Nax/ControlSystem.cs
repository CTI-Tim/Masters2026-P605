using Crestron.SimplSharp;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.AudioDistribution;
using Crestron.SimplSharpPro.UI;
using CwsDebug;
using Masters2026Nax.Configuration;
using Masters2026Nax.Hardware;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Masters2026Nax
{
    // -----------------------------------------------------------------------------
    // Why are there so many '?' and '!' operators?
    //
    // If you are learning modern C#, you will notice these sprinkled throughout
    // the code. It can look like someone lost a fight with the punctuation keys,
    // but there is actually a good reason for it.
    //
    // Beginning with C# 8.0, Microsoft added Nullable Reference Types. The compiler
    // now tries to determine if an object could possibly be null and warns you when
    // it thinks you may be about to dereference one. This is generally a good
    // thing, because NullReferenceExceptions have been ruining programmers' days
    // for decades.
    //
    // The Crestron libraries, however, were written long before Nullable Reference
    // Types existed. They also have to remain backwards compatible with many years
    // of existing code and customer projects. Breaking tens of thousands of working
    // programs just to make the compiler happier would not be a particularly
    // popular decision.
    //
    // Because of this, Visual Studio will insist that an object
    // "might be null" even though the Crestron classes guarantees that it exists.
    // When you see the '!' operator, we are simply telling the compiler,
    // "Thanks for looking out for us, but we've got this one."
    //
    // Do NOT get into the habit of sprinkling '!' and '?' around like parmesan cheese just
    // to make the warnings disappear. In your own code, stop and understand why the
    // warning exists. Use '!' and '?' only when you know something the compiler cannot.
    //
    // My preference when I am writing classes that wrap Crestron C# classes is to
    // put the '#nullable disable' compiler switch at the top of the cs file that I
    // will be leveraging a lot of Crestron class elements. But I
    // manage my nullables manually.  If you want to use that, be aware of it.
    // for example I use it in the Message.cs class because those are NOT nullable
    // -----------------------------------------------------------------------------

    /// <summary>
    ///
    /// This application uses a message broker as the primary communication mechanism
    /// between components. The goal is to keep the system as decoupled as possible
    /// while still allowing information to move freely throughout the application.
    ///
    /// One of the biggest challenges in automation programming is abstraction.
    /// We intentionally create layers of interfaces and device wrappers so that
    /// hardware can be replaced without affecting the rest of the codebase. While
    /// this makes the system more flexible, it also creates a problem: classes need
    /// to communicate with each other, and traditional approaches often require
    /// object references to be passed throughout the application.
    ///
    /// As a system grows this can quickly become difficult to maintain. Constructors
    /// become filled with dependencies, classes become tightly coupled to specific
    /// implementations, and adding new functionality often requires modifying code
    /// that otherwise has no reason to change.
    ///
    /// The message broker solves this problem by allowing components to publish and
    /// subscribe to messages instead of communicating directly. A device driver can
    /// publish status updates without knowing whether that information is being used
    /// by a touch panel, automation routine, logging service, diagnostics engine,
    /// or something that has not even been written yet.
    ///
    /// Likewise, classes/devices do not need references to specific devices. A thermostat,
    /// touch panel, occupancy sensor, scheduling engine, or any other subsystem can
    /// communicate through the same messaging infrastructure without requiring
    /// knowledge of the underlying implementation of the other components.
    ///
    /// This approach significantly reduces coupling, simplifies testing, and makes
    /// it easier to introduce new devices, services, and features without creating
    /// dependency chains throughout the application. Components become responsible
    /// only for the messages they produce and consume rather than maintaining
    /// awareness of the entire system around them.
    ///
    /// In practice this allows a device to be replaced, removed, or simulated
    /// with little to no impact on the rest of the application.
    ///
    /// </summary>
    public class ControlSystem : CrestronControlSystem
    {
        // this right here, THIS is a valid use for the ? here.  because these two class containers ARE nullable!
        UI? myXpanel;
        UI? myOtherXpanel;
        HardwareSetup? Hardware;

        /// <summary>
        ///  Default constructor. No access to really anything here. You can set the max number of threads here, instantiate code only classes, but that's about it.
        ///  You cant open ports, you cant start threads, no access to any hardware.
        ///  a try catch here for the threads definition or the subscription to the programstatusevent handler is useless.
        /// </summary>
        public ControlSystem()
            : base()
        {
            Crestron.SimplSharpPro.CrestronThread.Thread.MaxNumberOfUserThreads = 20; // Defaul 20 threads

            // We need to Shutdown some things on program stop so we need to subscribe to this
            CrestronEnvironment.ProgramStatusEventHandler += ProgranStartStopHandler;

        }

        /// <summary>
        /// Ok this is what you want to use to startup your code and system. we can start threads and access hardware here
        /// Use a try catch if you are doing something wonky or using execption based code.
        /// Dont just wrap in a try catch because the old template had one. 
        /// Know why you are using it and what you are trying to catch.
        /// 
        /// remember you need to end this fast. make sure any classes you instantiate also return quickly
        /// </summary>
        public override void InitializeSystem()
        {
            // Start the Debugger class and register it's location in the CWS URL
            // you will find the page at http://<processor>/VirtualControl/Rooms/<roomId>/cws/debugger/debugger.html
            CwsDebug.CwsDebugPage.Register("/debugger");  // /debugger adds the folder level after /cws to become /cws/debugger
            CwsDebugPage.LogInfo("Initialize started");
            
            // Load Config -- Yes this needs to be loaded first because the other classes rely on it.
            SetupNaxSources();
            CwsDebugPage.LogInfo("Source Map loaded");

            // Lets initialize the  hardware classes
            myXpanel = new UI(0x03, this);
            CwsDebugPage.LogInfo("TP Registered");

            myOtherXpanel = new UI(0x04, this);
            CwsDebugPage.LogInfo("Other TP Registered");

            Hardware = new HardwareSetup(this);
            CwsDebugPage.LogInfo("Hardware Setup Complete");
            // It can seem silly to put the above debug statements in but it gives real info when failures occour.
            // If we do not see those messages then  the class above it failed
            // there are instances in Crestron libraries that the exception will be swallowed
        }

        // For this example we are building the NAX source configuration directly in code
        // so it is easy to see exactly what is being added and how the configuration works.
        //
        // There is nothing requiring the configuration to be created this way. The
        // NaxSourceConfiguration class does not care where this information came from,
        // it only stores the zones and sources we give it.
        //
        // In a larger program you could create your own configuration class that reads
        // this information from a JSON, XML, or other configuration file and then calls
        // AddZone() and AddSource() to build the exact same configuration.
        // For the lab we are going to keep it simple and build the configuration manually.
        // Once you understand what is happening here, changing where the data comes from
        // does not require changing the rest of the program.
        private void SetupNaxSources()
        {
            // Zone 1 - NAX Amp
            ushort zone1 = NaxSourceConfiguration.AddZone();

            NaxSourceConfiguration.AddSource(zone1, "NaxAmp Analog In 1", 5);
            NaxSourceConfiguration.AddSource(zone1, "USB Input", 17, "Stream02c4.42.68.74.dd.01");
            NaxSourceConfiguration.AddSource(zone1, "Media Player 1", 9, "", new MediaPlayerId("Nax1", 1));
            NaxSourceConfiguration.AddSource(zone1, "Media Player 2", 10, "", new MediaPlayerId("Nax1", 2));
            NaxSourceConfiguration.AddSource(zone1, "Zone Off", 0);


            // Zone 2 - NAX Amp
            ushort zone2 = NaxSourceConfiguration.AddZone();

            NaxSourceConfiguration.AddSource(zone2, "NaxAmp Analog In 1", 5);
            NaxSourceConfiguration.AddSource(zone2, "USB Input", 17, "Stream02c4.42.68.74.dd.01");
            NaxSourceConfiguration.AddSource(zone2, "Media Player 1", 9, "", new MediaPlayerId("Nax1", 1));
            NaxSourceConfiguration.AddSource(zone2, "Media Player 2", 10, "", new MediaPlayerId("Nax1", 2));
            NaxSourceConfiguration.AddSource(zone2, "Zone Off", 0);


            // Zone 3 - NAX AUD-BTIO
            ushort zone3 = NaxSourceConfiguration.AddZone();

            NaxSourceConfiguration.AddSource(zone3, "NaxAmp Analog In 1", 2, "RCA1500.10.7f.f4.6a.5d");
            NaxSourceConfiguration.AddSource(zone3, "USB Input", 2, "Stream02c4.42.68.74.dd.01");
            NaxSourceConfiguration.AddSource(zone3, "Media Player 1", 2, "MediaStream1900.10.7f.f4.6a.5d", new MediaPlayerId("Nax1", 1));
            NaxSourceConfiguration.AddSource(zone3, "Media Player 2", 2, "MediaStream21000.10.7f.f4.6a.5d", new MediaPlayerId("Nax1", 2));
            NaxSourceConfiguration.AddSource(zone3, "Zone Off", 0);
        }

        // Trap for program starting or stopping or pausing. We only care about stop honestly.
        private void ProgranStartStopHandler(eProgramStatusEventType programEventType)
        {
            if (programEventType == eProgramStatusEventType.Stopping)
            {
                //myDebug.Dispose();
            }
        }

        // -----------------------------------------------------------------------------
        // Why are we wrapping the Crestron classes? And then wrapping those classes?
        // WE have classes in classes in classes!
        //
        // This is one of those Object-Oriented Programming concepts that gets taught
        // all the time, but rarely gets explained very well. When I was first learning
        // C#, my reaction was pretty simple:
        //
        //     "Great... now I have another class to look around in and manage."
        //
        // Honestly, it felt like someone took a simple problem and added another layer
        // just because programmers like adding abstraction for fun.
        //
        // In reality there is actually a good reason for it.
        //
        // The Crestron classes know how to talk to Crestron hardware. They know nothing
        // about how THIS application wants to use that hardware, and that's exactly how
        // it should be. This wrapper becomes the translator between our application and the hardware.
        // The rest of the program doesn't need to know which property changes the
        // source, which event reports volume feedback, or what magic sequence of method
        // calls wakes the amplifier up in the morning. It simply calls methods like
        // SetVolume(), SelectSource(), or Mute() and lets this class deal with the
        // hardware details.
        //
        // Another huge advantage is that hardware changes. APIs change. Sometimes
        // manufacturers even decide to "improve" things. (Programmers everywhere just
        // collectively sighed with flashbacks of Sonos breaking things)
        // If that happens, most of the changes are isolated to
        // their singular class instead of being scattered throughout the entire application.
        // Wrappers also make testing much easier. If you decide to write an emulator,
        // simulator, or simply fake the hardware for testing, you already have a clean
        // interface. The rest of the application doesn't care if it's talking to a
        // $15,000 audio device or a class making things up as it goes along.
        //
        // A good wrapper hides complexity. A bad wrapper just moves the complexity somewhere else and gives
        //  it a new name.
        //
        // The goal of all of this is to make using the hardware easier than using the
        // hardware directly, and make it easier later to rip it out and change it if needed.
        //
        // And yes I comment a lot. 
        // -----------------------------------------------------------------------------

    }
}
