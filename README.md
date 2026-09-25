<img src="https://img.shields.io/badge/Language-C Sharp-blue"> <img src="https://img.shields.io/badge/Platform-Crestron 4 series-blue"> <img src="https://img.shields.io/badge/Masters- 2026-blue">  <img src="https://img.shields.io/badge/Use-Educational-green"> <img src="https://img.shields.io/badge/Copyright-Crestron-blue"> <img src="https://img.shields.io/badge/License-Restricted-orange">

# Masters 2026 C# for NAX Class Files

This program is provided for the Masters 2026 C# for NAX class. The example is designed to demonstrate programming Crestron NAX products from C#, routing audio, working with AES67 streams, controlling NAX Media Players with CRPC, using a Construct HTML5 interface, and separating the different parts of the application with a simple Message Broker.

**IMPORTANT NOTE:** This is **educational example code** written to demonstrate programming concepts to students. It is intentionally kept simple in places so the operation of the program can be followed in class. It is not intended to be dropped directly into a production program without the programmer adding the appropriate error checking, configuration management, recovery, logging, and other features required for their project.

Please take note of the extensive comments throughout the program. The comments are not there just to explain what a line of C# does. In many places they explain why something was done, limitations we discovered, or why we deliberately chose a simple solution instead of a more complicated one.

Understanding those decisions is critical for the automation programmer.

## Program Architecture

This example separates the program into several pieces instead of allowing the user interface, application, and hardware to directly control each other.

The basic flow is:

```text
Construct UI
     |
     v
Message Broker
     |
     v
Hardware Setup
     |
     v
NAX Wrapper Classes
     |
     v
Crestron NAX Devices
```

Feedback travels back through the same architecture in the opposite direction.

```text
Crestron NAX Devices
     |
     v
NAX Wrapper Classes
     |
     v
Hardware Setup
     |
     v
Message Broker
     |
     v
Construct UI
```

The goal is not to create the most complicated architecture possible. Quite the opposite. Each class should have a reasonably simple job and should not need to understand everything else in the program.

#### **NAX Hardware Wrapper Classes**

The NAX wrapper classes sit between our application and the Crestron NAX device classes. The Crestron libraries expose a large amount of functionality because they need to support everything the hardware can do. Our application only needs a small part of that functionality. The wrapper classes give the rest of our program a simpler interface for the things we actually want to do. For example, the application should be able to tell a NAX amplifier to route a source without every other class needing to understand the complete Crestron NAX API. This also gives us one place to deal with device specific behavior and any interesting things we discover while working with the Crestron libraries.

Good wrappers reduce complexity. Bad wrappers just move it somewhere else and give it a new file name.

#### **HardwareSetup**

The `HardwareSetup` class is responsible for creating the hardware wrapper classes and connecting them to the rest of the application. It receives commands from the Message Broker, translates those messages into calls to the appropriate hardware wrapper, and takes hardware feedback and sends it back through the Message Broker. This means the UI never needs a reference to `NaxAmp`, `NaxIO`, or any other hardware class.

HardwareSetup is where the application and hardware meet, but it should not become the place where all of the application logic gets dumped. translations such as source mapping happen here so the messages get back to the touchpanel properly.   

#### **NaxSourceConfiguration**

`NaxSourceConfiguration` contains the source information shared by the user interface and hardware portions of the program. A source definition can contain the friendly name displayed to the user, the actual NAX source number, an AES67 Session Name when required, and whether the source represents a Media Player. The important part is that the rest of the application does not care where this configuration came from.

For the class example we create the configuration directly in C# using `AddZone()` and `AddSource()`.

```C#
ushort zone1 = NaxSourceConfiguration.AddZone();

NaxSourceConfiguration.AddSource(
    zone1,
    "NaxAmp Analog In 1",
    5);

NaxSourceConfiguration.AddSource(
    zone1,
    "USB Input",
    17,
    "Stream02c4.42.68.74.dd.01");

NaxSourceConfiguration.AddSource(
    zone1,
    "Media Player 1",
    9,
    "",
    true);
```

A larger program would absolutely have a configuration class read the same information from JSON, XML, a database, or another configuration source and then use `AddZone()` and `AddSource()` to build the configuration. Nothing else in the program needs to change just because we changed where the configuration came from.

**NOTE:** The source configuration must be created before the hardware and UI classes are created. Both use this information during their setup.

```C#
SetupNaxSources();

myHardware = new HardwareSetup(this);
myUi = new UI(0x03, this);
```

Configuration is a dependency of the rest of the application, so configuration comes first as seen in controlsystem.cs

#### **UI**

The `UI` class handles the Construct HTML5 XPanel. It creates the XPanel, connects the generated Construct Contract, registers UI events, sends user actions into the Message Broker, and receives feedback messages that are used to update the panel, if a source button is pressed, the UI reports that the source button was pressed. It does not need to understand how that source is routed through a NAX amplifier. If the hardware reports that a source changed, the UI receives the resulting feedback message and updates the appropriate button. Keeping the UI separated from the hardware also means another interface could be added later without teaching that interface how all of the NAX hardware works.

#### **CRPC Media Player**

The Construct Media Player widget communicates with the NAX Media Player using CRPC messages.

The UI receives CRPC messages from the Construct Media Player and sends them through the Message Broker.

```C#
Broker.SendMessage(
    "UiMediaPlayerCrpcMessage",
    new Message
    {
        Serial = e.SigArgs.Sig.StringValue
    });
```

HardwareSetup receives that message and sends the CRPC data to the NAX device.

Feedback makes the same trip in reverse. HardwareSetup receives the CRPC feedback from the NAX device and publishes it through the Message Broker. The UI receives it and passes the data back to the Construct Media Player widget.

The UI does not need to know how the CRPC message reached the hardware and the hardware does not need to know anything about the Construct panel.

## How to Use it

The example is already assembled as a complete program, but the classes are deliberately separated so students can follow how information moves through the application. One of the most important parts of the example is the Message Broker.

### Message Broker

The Message Broker is a simple way for different parts of the program to communicate without requiring direct references to each other. Normally we could do something like:

```C#
myNaxAmp.RouteAudio(1, 5);
```

This requires passing the classes around to everything that needs it or create a relationship hell in the program, or worse give up and just make everything static.

Instead, the UI can publish a message:

```C#
Broker.SendMessage(
    "Zone1Source",
    new Message { Analog = 2 });
```

The UI knows that the user selected source button 2.

It does not know which NAX device controls Zone 1, what the actual NAX source number is, whether the source uses AES67, or how the hardware needs to be configured. It doesn't need to know at all what is what. Somewhere else in the application we register a method that wants to receive that message.

```C#
Broker.AddDelegate(
    "Zone1Source",
    Zone1SourceSelect);
```

When somebody sends `"Zone1Source"`, the broker calls `Zone1SourceSelect()`.

```C#
private void Zone1SourceSelect(Message m)
{
    ushort source =
        NaxSourceConfiguration.GetSourceNumber(
            1,
            m.Analog);

    myNaxamp.RouteAudio(
        1,
        source);
}
```

The string `"Zone1Source"` becomes the connection between the two parts of the program.

Neither class needs a reference to the other.

### Sending a Message

Use `Broker.SendMessage()` to publish a message.

```C#
Broker.SendMessage(
    "Zone1Source",
    new Message { Analog = 2 });
```

The first argument is the message key.

```C#
"Zone1Source"
```

The second argument is a `Message` object containing the information we want to send.

The Message class provides three simple types of information:

```C#
public class Message
{
    public bool Digital { get; set; }
    public ushort Analog { get; set; }
    public string Serial { get; set; }
}
```

This should look familiar to a Crestron programmer.

`Digital` can carry a Boolean value.

```C#
Broker.SendMessage(
    "VolumeMute",
    new Message { Digital = true });
```

`Analog` can carry a 16 bit unsigned value.

```C#
Broker.SendMessage(
    "Zone1Source",
    new Message { Analog = 3 });
```

`Serial` can carry a string.

```C#
Broker.SendMessage(
    "UiMediaPlayerCrpcMessage",
    new Message { Serial = crpc });
```

We deliberately keep the Message object simple. This is an educational example and those three data types map very naturally to concepts Crestron programmers already understand.

### Receiving a Message

Before a class can receive a particular message it needs to register a method with the broker.

```C#
Broker.AddDelegate(
    "Zone1Source",
    Zone1SourceSelect);
```

The method must accept a `Message`.

```C#
private void Zone1SourceSelect(Message m)
{
    // Do something with the message.
}
```

Now whenever another part of the program sends:

```C#
Broker.SendMessage(
    "Zone1Source",
    new Message { Analog = 2 });
```

the broker calls:

```C#
Zone1SourceSelect(Message m)
```

with the Message that was sent.

### Message Keys Are the Contract

The message key is what connects the sender and receiver.

```C#
"Zone1Source"
```

The sender and receiver must agree on the name and what information is being carried by the Message.

For example:

```text
Zone1Source
    Analog = Source button index

Zone1SourceFb
    Analog = Selected source button index

VolumeMute
    Digital = Mute button state

UiMediaPlayerCrpcMessage
    Serial = CRPC message from the UI

MediaPlayerCrpcFb
    Serial = CRPC feedback from the hardware
```

There is no magic here.

If one class sends:

```C#
"Zone1Source"
```

and another class subscribes to:

```C#
"Zone1Sources"
```

they are two different messages. The broker is not going to guess what the programmer meant. The message key and the data we agree to send with it form a small contract between the different parts of the application.

### Multiple Subscribers

More than one method can subscribe to the same message key.

```C#
Broker.AddDelegate(
    "Zone1SourceFb",
    Zone1SetSourceFb);

Broker.AddDelegate(
    "Zone1SourceFb",
    SomeOtherMethod);
```

When `"Zone1SourceFb"` is sent, both methods will receive the message. This can be useful when several parts of the application need to know that the same thing happened. The sender still does not need to know who is listening.

### Feedback Through the Broker

Commands are not the only thing that travel through the broker. Hardware feedback comes back the same way.

For example, HardwareSetup can publish:

```C#
Broker.SendMessage(
    "Zone1SourceFb",
    new Message
    {
        Analog = buttonIndex
    });
```

The UI registered for that feedback during startup:

```C#
Broker.AddDelegate(
    "Zone1SourceFb",
    Zone1SetSourceFb);
```

The UI can then update the Construct panel.

```C#
private void Zone1SetSourceFb(Message m)
{
    // Update the source button feedback.
}
```

The hardware does not know that an XPanel even exists, it only reports the new state. That separation is the entire reason we are using the broker.

### Why Use a Message Broker?

We absolutely could write this program without one and for a small program it might even require fewer lines of code. The reason we are using it is to demonstrate how we can reduce dependencies between different parts of a larger application.

Without the broker we can quickly end up with:

```text
UI knows Hardware
Hardware knows UI
UI knows Configuration
Hardware knows Configuration
One device knows another device
Everything knows everything
```

That works right up until a major change needs to happen or worse a major hardware change.

With the Message Broker, the UI can say:

```text
The user selected Zone 1 Source 2.
```

HardwareSetup can decide what that means for the hardware.

The hardware can later say:

```text
Zone 1 is now on Source 2.
```

The UI can decide how to display that feedback. Neither side needs to know how the other side accomplished its job. The Message Broker is not something that should automatically be added to every C# program. It is a tool. In this program it gives us a useful way to demonstrate separating application components without introducing a much larger framework.

### AES67 Streams

NAX AES67 streams in this example are configured using their Session Name. The source configuration stores the Session Name along with the source definition.

```C#
NaxSourceConfiguration.AddSource(
    zone1,
    "USB Input",
    17,
    "Stream02c4.42.68.74.dd.01");
```

When HardwareSetup determines that the selected source uses a stream, it can retrieve the Session Name from `NaxSourceConfiguration` and pass it to the appropriate hardware wrapper.

This keeps the AES67 configuration out of the UI. The user selects `"USB Input"`. The application then figures out what `"USB Input"` actually means to the hardware.

### Media Player Sources

A source definition can also identify a source as a Media Player.

```C#
NaxSourceConfiguration.AddSource(
    zone1,
    "Media Player 1",
    9,
    "",
    true);
```

The final `true` tells the configuration that this source logically represents a Media Player.

We store this information instead of trying to determine it only from the NAX source number because the same logical Media Player can appear differently depending on the NAX hardware being controlled.

The UI can ask:

```C#
NaxSourceConfiguration.IsMediaPlayer(
    zone,
    buttonIndex);
```

and display the Media Player controls when appropriate.

Again, the UI does not need to understand the NAX routing rules to make that decision.

## Things to Remember

This example intentionally favors code that programmers can follow over code that demonstrates every possible C# language feature, I am intentionally not getting extremely clever or objectifying code to the point that nobody can read it like some programmers out there. Do not assume fewer lines of code automatically means better code, readability matters greatly.  Do not build an interface, factory, reflection system, or another abstraction just because two small methods happen to look similar. If the abstraction makes the program harder to understand than the duplicate code, we have not improved anything.

At the same time, when several parts of the program are doing exactly the same job and can cleanly share a method, use one.

The goal is readable code with clear responsibilities.

Most importantly, do not copy code from this example without understanding what it is doing. Break it, change it, add to it, and use the debugger to follow the data through the application.



## License

Crestron example code is licensed to Crestron dealers and Crestron Service Providers (CSPs) under a limited non-exclusive, non-transferable Software Development Tools License Agreement. Crestron product operating system software is licensed to Crestron dealers, CSPs, and end-users under a separate End-User License Agreement. Both of these Agreements can be found on the Crestron website at www.crestron.com/legal/software-license-agreement. The product warranty can be found at www.crestron.com/legal/sales-terms-conditions-warranties. The specific patents that cover Crestron products are listed at www.crestron.com/legal/patents. Certain Crestron products contain open source software. For specific information, visit www.crestron.com/legal/opensource-software. Crestron, the Crestron logo, Crestron Virtual Control, VC-4, 4-Series, 4-Series Control System, Crestron Studio, Crestron Toolbox, Crestron XiO Cloud, SIMPL+, and VT-Pro e are either trademarks or registered trademarks of Crestron Electronics, Inc. in the United States and/or other countries. Microsoft Visual Studio and Active Directory is either a trademark or a registered trademark of Microsoft Corporation in the United States and/or other countries. Other trademarks, registered trademarks, and trade names may be used in this document to refer to either the entities claiming the marks and names or their products. Crestron disclaims any proprietary interest in the marks and names of others. Crestron is not responsible for errors in typography or photography. ©2026 Crestron Electronics, Inc.