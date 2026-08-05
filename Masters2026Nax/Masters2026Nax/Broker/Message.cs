#nullable disable // I know by design that these below are not nullable, so shut up the compiler here
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MessageBroker
{
    // -----------------------------------------------------------------------------
    // Why is this class so simple?
    //
    // You may see examples online that use private constructors, factory methods,
    // validation, helper functions, and enough design patterns to make a software
    // architect smile. There is certainly a place for those techniques.
    //
    // This absolutely isn't one of them.
    //
    // The Message Broker is simply moving data from one part of the application to
    // another. The message key defines the contract between the sender and the
    // receiver. If you subscribe to "NAX.Zone.Volume", you should already know
    // that the Analog value contains the volume. If you don't know what a message
    // contains, no amount of factory methods is going to save you.
    //
    // Could we add helper methods to prevent someone from creating an "invalid"
    // message? Sure. We could also wrap every integer in its own class, but at some
    // point we stop solving real problems and start writing code to protect the
    // programmer from... the programmer. Or worse to impress someone on 
    // stack overflow or reddit. Those people do not matter. Their opinions
    // do not matter.   
    //
    // Good software is not measured by how many classes or helper methods it has.
    // It is measured by how easy it is to understand and maintain. Keeping this
    // class simple makes it easier to read, easier to debug, and easier to learn.
    //
    // Remember: Every line of code you write is another line you have to maintain.
    // Don't add complexity unless it buys you something worthwhile. 
    // -----------------------------------------------------------------------------

    public class Message
    {
        public bool Digital { get; set; }
        public ushort Analog { get; set; }
        public string Serial { get; set; }
    }
}

