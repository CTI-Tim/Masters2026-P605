using Crestron.SimplSharp;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.AudioDistribution;
using Crestron.SimplSharpPro.UI;
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
    // but there is actually a reason for it.
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
    // put the #nullable false compiler switch at the top of the cs file that I
    // will be leveraging a lot of Crestron class elements. But I
    // manage my nullables manually.  If you want to use that, be aware of it.
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
        // Globals
        Xpanel myXpanel;
        HardwareSetup Hardware;

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
            // Lets initialize the classes
            myXpanel = new Xpanel(0x03, this);
            Hardware = new HardwareSetup(this);

        }

        // Trap for program starting or stopping or pausing. We only care about stop honestly.
        private void ProgranStartStopHandler(eProgramStatusEventType programEventType)
        {
            if (programEventType == eProgramStatusEventType.Stopping)
            {
                //myDebug.Dispose();
            }
        }

    }
}
